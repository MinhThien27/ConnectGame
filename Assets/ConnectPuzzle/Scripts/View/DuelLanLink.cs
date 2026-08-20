using System;
using System.Collections.Generic;
using System.Net;
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
        private IPEndPoint broadcast;
        private int senderId;

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
                this.broadcast = new IPEndPoint(IPAddress.Broadcast, Port);
                this.CurrentRole = role;
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

        public void Stop()
        {
            if (this.socket != null)
            {
                try { this.socket.Close(); } catch (Exception) { }
                this.socket = null;
            }
            this.CurrentRole = Role.Off;
        }

        private void OnDestroy() => Stop();

        public bool Announce(int seed, int preset)
        {
            byte[] data = DuelWire.EncodeInvite(seed, preset, this.LocalName, this.senderId);
            return Send(data);
        }

        public bool SendResult(DuelResult result)
        {
            byte[] data = DuelWire.EncodeFinished(result, this.LocalName, this.senderId);
            return Send(data);
        }

        private bool Send(byte[] data)
        {
            if (this.socket == null) { Raise("Chưa bật kết nối Wi-Fi."); return false; }
            try
            {
                this.socket.Send(data, data.Length, this.broadcast);
                return true;
            }
            catch (SocketException e)
            {
                Raise("Gửi không được (" + e.SocketErrorCode + ").");
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

            while (true)
            {
                byte[] data;
                try
                {
                    if (this.socket.Available <= 0) break;
                    IPEndPoint from = null;
                    data = this.socket.Receive(ref from);
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
