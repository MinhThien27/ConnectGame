using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using ConnectPuzzle.Core;
using UnityEngine;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Đấu cùng Wi-Fi bằng UDP broadcast. Không dùng Netcode, không dùng server.
    ///
    /// Vì sao UDP thô: cả tính năng chỉ cần gửi ~12 byte một lần mỗi người. Netcode
    /// mang theo NetworkManager, prefab, vòng đời riêng — búa tạ đóng đinh ghim.
    ///
    /// Vì sao có Poll() thay vì coroutine nhận tin: coroutine KHÔNG chạy ở edit mode,
    /// nên toàn bộ đường nhận tin sẽ thành thứ không kiểm được cho tới khi có người
    /// bấm Play. Tách ra thành một hàm gọi tay thì bài kiểm chạy được hai đầu trong
    /// cùng một tiến trình, và Update() chỉ là một dòng gọi nó.
    /// </summary>
    public sealed class DuelLanLink : MonoBehaviour
    {
        /// <summary>
        /// Cổng CHUNG cho mọi máy chơi game này. Phải cố định vì không có server để
        /// hỏi; đổi số này là hai bản app không thấy nhau.
        /// </summary>
        public const int Port = 48711;

        public enum Role { Off, Host, Guest }

        public Role CurrentRole { get; private set; }
        public string LocalName = "Máy này";

        /// <summary>Lời mời nhận được (khách). Trả về seed/preset để dựng bàn.</summary>
        public event Action<int, int, string> OnInvite;

        /// <summary>Kết quả đối thủ nhận được.</summary>
        public event Action<DuelResult, string> OnOpponentResult;

        /// <summary>Lỗi mạng nói được cho người chơi, không phải stack trace.</summary>
        public event Action<string> OnProblem;

        private UdpClient socket;
        private int senderId;

        /// <summary>
        /// MỌI địa chỉ phát cần thử, không chỉ 255.255.255.255.
        ///
        /// 255.255.255.255 là "broadcast giới hạn" — router không chuyển tiếp nó, và
        /// KHÔNG ÍT thiết bị Android/Wi-Fi bỏ nó ở đường nhận. Broadcast có hướng của
        /// từng card mạng (ví dụ 192.168.1.255) thì đi đáng tin cậy hơn nhiều. Không có
        /// cách nào biết trước cái nào thông, nên gửi hết.
        /// </summary>
        private readonly List<IPEndPoint> targets = new List<IPEndPoint>();

        /// <summary>Bàn đang mở, để chủ trả lời gói TÌM mà không cần bảng UI còn mở.</summary>
        private int openSeed = -1, openPreset = -1;

        // ---- số liệu để CHẨN ĐOÁN: không có chúng thì "hai máy không thấy nhau" là một
        // câu không có đầu mối nào, trên máy không cắm được debugger.
        public int SentCount { get; private set; }
        public int SeekReplies { get; private set; }
        public string LastError { get; private set; } = "";

        /// <summary>Địa chỉ IPv4 của máy này trên từng card mạng đang bật.</summary>
        public readonly List<string> LocalAddresses = new List<string>();

        /// <summary>Danh sách địa chỉ phát, dạng đọc được — hiện thẳng ra bảng UI.</summary>
        public string TargetList
        {
            get
            {
                var parts = new List<string>();
                foreach (IPEndPoint t in this.targets) parts.Add(t.Address.ToString());
                return string.Join(", ", parts.ToArray());
            }
        }

        /// <summary>Mã máy của phiên này — bài kiểm cần đọc để dựng hai đầu.</summary>
        public int SenderId => this.senderId;

        /// <summary>Gói đã nhận, để kiểm thử soi được mà không cần bắt sự kiện.</summary>
        public readonly List<DuelWire.Packet> Received = new List<DuelWire.Packet>();

        /// <summary>Số gói bị TỪ CHỐI kèm lý do — hiện ra thay vì im lặng bỏ qua.</summary>
        public readonly Dictionary<DuelWire.ParseResult, int> Rejected =
            new Dictionary<DuelWire.ParseResult, int>();

        public bool Start(Role role)
        {
            Stop();
            try
            {
                this.socket = new UdpClient();

                // ReuseAddress: hai tiến trình trên CÙNG máy phải cùng nghe được cổng
                // này. Cần cho bài kiểm hai đầu, và cũng cần thật khi hai người dùng
                // hai app trên một máy (giả lập, hoặc chia đôi màn hình).
                this.socket.Client.SetSocketOption(SocketOptionLevel.Socket,
                    SocketOptionName.ReuseAddress, true);
                this.socket.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
                this.socket.EnableBroadcast = true;

                // Mã máy mới mỗi phiên. Dùng số ngẫu nhiên chứ không dùng thứ cố định
                // (tên máy, địa chỉ IP): hai người có thể trùng tên, và địa chỉ thì trùng
                // hẳn khi hai app chạy trên cùng một máy.
                this.senderId = UnityEngine.Random.Range(1, 65536);
                CollectTargets();
                this.CurrentRole = role;
                this.SentCount = 0;
                this.SeekReplies = 0;
                this.LastError = "";
                return true;
            }
            catch (SocketException e)
            {
                Stop();
                Raise("Không mở được kết nối Wi-Fi (" + e.SocketErrorCode +
                      "). Kiểm tra xem hai máy có cùng một mạng không.");
                return false;
            }
        }

        /// <summary>
        /// Dựng danh sách địa chỉ phát từ các card mạng đang bật.
        ///
        /// Broadcast có hướng tính bằng địa chỉ OR phần bù của mặt nạ mạng: 192.168.1.7
        /// với mặt nạ 255.255.255.0 ra 192.168.1.255. Bỏ loopback và card đang tắt —
        /// gửi vào đó chỉ tốn thời gian và sinh lỗi rác.
        /// </summary>
        private void CollectTargets()
        {
            this.targets.Clear();
            this.LocalAddresses.Clear();

            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    foreach (UnicastIPAddressInformation info in
                             nic.GetIPProperties().UnicastAddresses)
                    {
                        if (info.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        this.LocalAddresses.Add(info.Address.ToString());

                        IPAddress mask = info.IPv4Mask;
                        if (mask == null) continue;

                        byte[] a = info.Address.GetAddressBytes();
                        byte[] m = mask.GetAddressBytes();
                        if (a.Length != 4 || m.Length != 4) continue;

                        var b = new byte[4];
                        for (int i = 0; i < 4; i++) b[i] = (byte)(a[i] | (byte)~m[i]);
                        Add(new IPAddress(b));
                    }
                }
            }
            catch (Exception e)
            {
                // Liệt kê card mạng có thể văng trên một số nền tảng. Không được để nó
                // giết cả tính năng — 255.255.255.255 bên dưới vẫn là một đường đi.
                this.LastError = "Không đọc được danh sách card mạng: " + e.GetType().Name;
            }

            // Luôn giữ broadcast giới hạn làm đường cuối: có mạng mà mặt nạ đọc ra sai
            // hoặc không đọc được, và ở đó nó là thứ duy nhất còn chạy.
            Add(IPAddress.Broadcast);
        }

        private void Add(IPAddress address)
        {
            foreach (IPEndPoint existing in this.targets)
                if (existing.Address.Equals(address)) return;
            this.targets.Add(new IPEndPoint(address, Port));
        }

        public void Stop()
        {
            if (this.socket != null)
            {
                try { this.socket.Close(); } catch (Exception) { }
                this.socket = null;
            }
            this.CurrentRole = Role.Off;

            // Quên bàn đang mở, không thì lần sau bấm "Tìm phòng" mà máy này vẫn đi trả
            // lời gói TÌM bằng bàn của ván trước.
            this.openSeed = -1;
            this.openPreset = -1;
        }

        private void OnDestroy() => Stop();

        public bool Announce(int seed, int preset)
        {
            this.openSeed = seed;
            this.openPreset = preset;
            byte[] data = DuelWire.EncodeInvite(seed, preset, this.LocalName, this.senderId);
            return Send(data);
        }

        /// <summary>
        /// Khách hỏi cả mạng "có ai mở phòng không".
        ///
        /// Không thay cho việc chủ phát lời mời đều đặn, mà THÊM một chiều nữa: chiều nào
        /// thông cũng bắt được cặp.
        /// </summary>
        public bool Seek()
        {
            byte[] data = DuelWire.EncodeSeek(this.LocalName, this.senderId);
            return Send(data);
        }

        public bool SendResult(DuelResult result)
        {
            byte[] data = DuelWire.EncodeFinished(result, this.LocalName, this.senderId);
            return Send(data);
        }

        /// <summary>
        /// Gửi tới MỌI địa chỉ phát. Thành công nếu ít nhất một đường đi được.
        ///
        /// Không dừng ở lỗi đầu tiên: card mạng ảo (VPN, giả lập Android, Hyper-V) rất
        /// hay từ chối broadcast, mà đúng cái card Wi-Fi thật thì lại đi được. Dừng sớm
        /// là để một card rác chặn cả tính năng.
        /// </summary>
        private bool Send(byte[] data)
        {
            if (this.socket == null) { Raise("Chưa bật kết nối Wi-Fi."); return false; }

            int ok = 0;
            SocketError lastCode = SocketError.Success;
            foreach (IPEndPoint target in this.targets)
            {
                try
                {
                    this.socket.Send(data, data.Length, target);
                    ok++;
                }
                catch (SocketException e) { lastCode = e.SocketErrorCode; }
            }

            if (ok == 0)
            {
                this.LastError = "Gửi không được (" + lastCode + ")";
                Raise("Gửi không được (" + lastCode + ").");
                return false;
            }
            this.SentCount++;
            return true;
        }

        /// <summary>
        /// Gửi riêng cho một máy. Dùng để trả lời gói TÌM.
        ///
        /// Unicast đi được ở gần như mọi mạng mà broadcast bị chặn, nên đây là đường
        /// đáng tin nhất trong cả lớp này — chỉ cần gói TÌM đến được là xong.
        /// </summary>
        private bool SendTo(byte[] data, IPEndPoint to)
        {
            if (this.socket == null || to == null) return false;
            try
            {
                this.socket.Send(data, data.Length, to);
                this.SentCount++;
                return true;
            }
            catch (SocketException e)
            {
                this.LastError = "Trả lời riêng không được (" + e.SocketErrorCode + ")";
                return false;
            }
        }

        private void Update() { Poll(); }

        /// <summary>
        /// Vét hết gói đang chờ. Gọi được từ Update hoặc gọi tay trong bài kiểm.
        ///
        /// Bỏ qua gói của CHÍNH MÌNH bằng cách so nội dung, không so địa chỉ: broadcast
        /// quay lại chính máy gửi, mà hai app trên cùng một máy thì cùng địa chỉ — lọc
        /// theo địa chỉ sẽ chặn luôn đối thủ thật.
        /// </summary>
        public int Poll()
        {
            if (this.socket == null) return 0;
            int handled = 0;
            IPEndPoint sender = null;

            while (true)
            {
                byte[] data;
                try
                {
                    if (this.socket.Available <= 0) break;
                    data = this.socket.Receive(ref sender);
                }
                catch (SocketException) { break; }
                catch (ObjectDisposedException) { break; }

                DuelWire.ParseResult outcome = DuelWire.Parse(data, data.Length, out DuelWire.Packet p);
                if (outcome != DuelWire.ParseResult.Ok)
                {
                    int count;
                    this.Rejected.TryGetValue(outcome, out count);
                    this.Rejected[outcome] = count + 1;
                    continue;
                }

                // Gói của CHÍNH MÌNH: broadcast luôn quay về máy gửi. ĐÃ ĐO: bỏ dòng này
                // thì chủ phòng tự nhận lời mời của mình và cả vòng đấu sai từ đầu.
                //
                // Lọc theo mã máy chứ không theo địa chỉ. Lý do là SUY LUẬN, chưa đo
                // được: hai app trên cùng một máy sẽ có cùng địa chỉ, nên lọc theo địa
                // chỉ có nguy cơ chặn luôn đối thủ thật. Tôi thử dựng phép kiểm ngược cho
                // chỗ này và nó KHÔNG bắt được — trong rig, gói tới từ địa chỉ LAN của
                // máy chứ không phải 127.0.0.1, nên bộ lọc loopback chẳng chặn gì. Cần
                // hai app trên một điện thoại thật mới đo được.
                if (p.SenderId == this.senderId) continue;

                this.Received.Add(p);
                handled++;

                if (p.Kind == DuelWire.Kind.Invite)
                {
                    if (p.RulesVersion != DuelCode.Version)
                    {
                        Raise("Máy kia đang ở luật bản " + p.RulesVersion + ", máy này bản " +
                              DuelCode.Version + " — hai bên sẽ ra bàn khác nhau. Cần cập nhật.");
                        continue;
                    }
                    if (this.OnInvite != null) this.OnInvite(p.Seed, p.Preset, p.Name);
                }
                else if (p.Kind == DuelWire.Kind.Finished)
                {
                    if (this.OnOpponentResult != null) this.OnOpponentResult(p.Result, p.Name);
                }
                else if (p.Kind == DuelWire.Kind.Seek)
                {
                    // Chỉ chủ phòng ĐANG có bàn mới trả lời.
                    //
                    // Trả lời HAI đường: unicast về đúng địa chỉ vừa gửi, và broadcast
                    // ngay lập tức. Unicast là đường đáng tin nhất — gói TÌM đến được thì
                    // đường về gần như chắc chắn thông. Broadcast kèm theo vì có mạng làm
                    // ngược lại, và vì đó là đường DUY NHẤT kiểm được bằng hai đầu trong
                    // một tiến trình: đã đo, hai socket chung cổng trên cùng một máy thì
                    // unicast chỉ vào được MỘT socket, còn broadcast vào cả hai.
                    //
                    // Trả lời nhiều lần cho một lần tìm là chuyện bình thường: gói TÌM
                    // được phát tới từng địa chỉ trong danh sách nên chủ nghe thấy vài
                    // bản. Bên nhận đã bỏ qua lời mời sau khi vào bàn, nên vô hại.
                    if (this.CurrentRole == Role.Host && this.openSeed >= 0)
                    {
                        byte[] reply = DuelWire.EncodeInvite(this.openSeed, this.openPreset,
                                                             this.LocalName, this.senderId);
                        bool direct = SendTo(reply, sender);
                        bool wide = Send(reply);
                        if (direct || wide) this.SeekReplies++;
                    }
                }
            }
            return handled;
        }

        private void Raise(string message)
        {
            if (this.OnProblem != null) this.OnProblem(message);
            else Debug.LogWarning("[DuelLan] " + message);
        }
    }
}
