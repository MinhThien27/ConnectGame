using System;
using System.Text;

namespace ConnectPuzzle.Core
{
    /// <summary>
    /// Định dạng gói tin cho đấu cùng Wi-Fi. Không phụ thuộc UnityEngine, không phụ
    /// thuộc socket — chỉ là byte vào byte ra, nên kiểm được headless trọn vẹn.
    ///
    /// Chỉ có hai loại tin:
    ///   MỜI   — chủ phòng công bố bàn (seed/preset/phiên bản) để bên kia dựng ĐÚNG bàn đó
    ///   XONG  — một bên gửi thành tích của mình
    ///   TÌM   — khách hỏi "có ai mở phòng không", chủ nghe thấy thì mời riêng nó
    ///
    /// Vì sao cần TÌM khi đã có MỜI phát đều đặn: broadcast có thể đi được MỘT CHIỀU.
    /// Có router chặn broadcast từ máy A mà không chặn từ máy B, và có Android bỏ gói
    /// broadcast đến trong khi vẫn gửi đi bình thường. Hai chiều thì chỉ cần một chiều
    /// thông là bắt được cặp, và câu trả lời cho TÌM đi bằng UNICAST — thứ gần như không
    /// bao giờ bị chặn.
    ///
    /// Mỗi gói có magic + phiên bản giao thức + CRC. Nghe hơi thừa cho một mạng LAN,
    /// nhưng lý do rất thật: cổng UDP broadcast là cổng CHUNG. Máy in, TV, app khác đều
    /// có thể phát gói lên đó. Không có magic và CRC thì một gói của thiết bị khác lọt
    /// vào có thể phân tích ra thành một "kết quả đối thủ" hợp lệ nhưng bịa — đúng kiểu
    /// hỏng im lặng mà cả tính năng đấu này phải tránh.
    /// </summary>
    public static class DuelWire
    {
        private const byte Magic0 = 0x43;         // 'C'
        private const byte Magic1 = 0x44;         // 'D'
        public const byte ProtocolVersion = 1;

        public const int MaxNameBytes = 16;
        public const int MaxPacket = 40;

        /// <summary>
        /// TIẾN ĐỘ được thêm mà KHÔNG tăng ProtocolVersion, và đó là chủ ý.
        ///
        /// Tăng phiên bản thì bản cũ từ chối MỌI gói (Parse trả BadProtocol ngay ở byte
        /// thứ ba), tức hai bản không nói chuyện được nữa — mất cả mời lẫn kết quả, chỉ để
        /// thêm một loại tin phụ. Giữ nguyên phiên bản thì bản cũ chỉ từ chối đúng gói nó
        /// không hiểu (BadKind, có đếm trong Rejected) còn mời và kết quả vẫn đi bình
        /// thường: người dùng bản cũ mất phần xem tiến độ đối thủ, nhưng vẫn đấu được.
        /// </summary>
        public enum Kind : byte { Invite = 1, Finished = 2, Seek = 3, Progress = 4 }

        public enum ParseResult { Ok, TooShort, BadMagic, BadProtocol, BadKind, BadCrc, BadName }

        public struct Packet
        {
            public Kind Kind;
            public string Name;

            /// <summary>
            /// Mã của máy GỬI, sinh ngẫu nhiên mỗi phiên.
            ///
            /// Cần vì broadcast quay lại chính máy gửi: chủ phòng phát lời mời rồi tự
            /// nhận lại lời mời của mình. Lọc theo ĐỊA CHỈ thì không xong — hai app trên
            /// cùng một máy có cùng địa chỉ, lọc kiểu đó là chặn luôn đối thủ thật.
            /// </summary>
            public int SenderId;

            // MỜI: đủ để bên nhận dựng lại bàn
            public int Seed;
            public int Preset;
            public int RulesVersion;

            // XONG: thành tích
            public DuelResult Result;

            /// <summary>
            /// TIẾN ĐỘ: trạng thái GIỮA ván của đối thủ.
            ///
            /// Cùng kiểu DuelResult với Result, và đó không phải cẩu thả: DuelVerdict.From
            /// đọc từ một PuzzleSession đang chạy và cho ra đúng năm con số này, nên "tiến
            /// độ" chỉ là cùng bản ghi đó lấy ở giữa ván thay vì lúc kết. Khác nhau ở đúng
            /// một chỗ: Won hầu như luôn false, và Kind mới là thứ nói bản ghi này là gì.
            /// </summary>
            public DuelResult Progress;

            /// <summary>
            /// TIẾN ĐỘ: TỔNG SỐ ô băng bên gửi đã đánh sang mình từ đầu ván.
            ///
            /// Là số TỔNG CỘNG, không phải "vừa đánh thêm mấy ô", và đó là chỗ quan trọng
            /// nhất của cả cơ chế đòn tấn công. UDP không có ack: gửi sự kiện "cộng 2 ô"
            /// mà mất gói là mất đòn im lặng, mà gói tới hai lần là ăn đòn hai lần. Gửi
            /// tổng thì bên nhận chỉ cần so với số nó ĐÃ áp dụng — gói sau tự chữa cho gói
            /// mất, gói trùng thành vô hại, gói tới muộn (tổng nhỏ hơn) bị bỏ qua.
            ///
            /// Một byte nên trần là 255 ô băng cho cả ván; bàn đấu 7x7 chỉ có 49 ô.
            /// </summary>
            public int SentAttacks;
        }

        public static byte[] EncodeInvite(int seed, int preset, string name, int senderId)
        {
            // 8 byte đầu + 1 byte độ dài tên + tên. Cộng tay thì tôi vừa quên đúng byte
            // độ dài và nó văng IndexOutOfRange — nên viết rõ từng thành phần ra.
            var body = new byte[10 + 1 + MaxNameBytes];
            int n = 0;
            body[n++] = Magic0;
            body[n++] = Magic1;
            body[n++] = ProtocolVersion;
            body[n++] = (byte)Kind.Invite;
            body[n++] = (byte)(senderId & 0xFF);
            body[n++] = (byte)((senderId >> 8) & 0xFF);
            body[n++] = (byte)(seed & 0xFF);
            body[n++] = (byte)((seed >> 8) & 0xFF);
            body[n++] = (byte)((seed >> 16) & 0xFF);
            body[n++] = (byte)(((preset & 0xF) << 4) | (DuelCode.Version & 0xF));
            n = WriteName(body, n, name);
            return Finish(body, n);
        }

        /// <summary>Gói TÌM: chỉ có tên và mã máy, không mang bàn nào.</summary>
        public static byte[] EncodeSeek(string name, int senderId)
        {
            var body = new byte[6 + 1 + MaxNameBytes];
            int n = 0;
            body[n++] = Magic0;
            body[n++] = Magic1;
            body[n++] = ProtocolVersion;
            body[n++] = (byte)Kind.Seek;
            body[n++] = (byte)(senderId & 0xFF);
            body[n++] = (byte)((senderId >> 8) & 0xFF);
            n = WriteName(body, n, name);
            return Finish(body, n);
        }

        public static byte[] EncodeFinished(DuelResult r, string name, int senderId)
            => EncodeStats(Kind.Finished, r, 0, name, senderId);

        /// <summary>
        /// Gói TIẾN ĐỘ: gửi sau mỗi nước đi để bên kia thấy mình đang ở đâu.
        ///
        /// Cùng khuôn byte với gói XONG. Dùng chung một khuôn chứ không tiết kiệm đi byte
        /// `Won`: tiết kiệm được đúng 1 byte trên một gói ~15 byte, mà đổi lại là hai
        /// đường phân tích phải giữ cho khớp nhau — chỗ đó mới là chỗ sinh lỗi.
        /// </summary>
        /// <param name="sentAttacks">Tổng số ô băng đã đánh sang đối thủ từ đầu ván.</param>
        public static byte[] EncodeProgress(DuelResult r, int sentAttacks, string name, int senderId)
            => EncodeStats(Kind.Progress, r, sentAttacks, name, senderId);

        /// <summary>
        /// Khuôn chung của gói XONG và gói TIẾN ĐỘ.
        ///
        /// Byte tổng đòn CHỈ có ở gói TIẾN ĐỘ. Thêm nó vào cả gói XONG thì bản cũ đọc gói
        /// XONG của bản mới sẽ trả BadName (độ dài không khớp) — mất luôn khả năng phân
        /// định thắng thua giữa hai bản, cái giá đắt hơn hẳn việc giữ hai khuôn lệch nhau
        /// một byte. Gói TIẾN ĐỘ thì bản cũ vốn đã từ chối ở BadKind trước khi đọc tới
        /// trường nào, nên thêm bao nhiêu byte cũng không ảnh hưởng.
        /// </summary>
        private static byte[] EncodeStats(Kind kind, DuelResult r, int sentAttacks,
                                          string name, int senderId)
        {
            var body = new byte[14 + 1 + MaxNameBytes];
            int n = 0;
            body[n++] = Magic0;
            body[n++] = Magic1;
            body[n++] = ProtocolVersion;
            body[n++] = (byte)kind;
            body[n++] = (byte)(senderId & 0xFF);
            body[n++] = (byte)((senderId >> 8) & 0xFF);
            body[n++] = (byte)(r.BoardTag & 0xFF);
            body[n++] = (byte)((r.BoardTag >> 8) & 0xFF);
            body[n++] = (byte)Clamp(r.MovesUsed, 255);
            body[n++] = (byte)(Clamp(r.Score, 65535) & 0xFF);
            body[n++] = (byte)((Clamp(r.Score, 65535) >> 8) & 0xFF);
            body[n++] = (byte)Clamp(r.CellsLeft, 255);
            body[n++] = (byte)(r.Won ? 1 : 0);
            if (kind == Kind.Progress) body[n++] = (byte)Clamp(sentAttacks, 255);
            n = WriteName(body, n, name);
            return Finish(body, n);
        }

        private static int Clamp(int v, int max) => v < 0 ? 0 : (v > max ? max : v);

        private static int WriteName(byte[] body, int n, string name)
        {
            byte[] raw = string.IsNullOrEmpty(name)
                ? new byte[0]
                : Encoding.UTF8.GetBytes(name);

            // Cắt theo BYTE, và cắt ở ranh giới ký tự: cắt giữa một ký tự nhiều byte
            // (mọi chữ có dấu tiếng Việt) tạo ra chuỗi UTF-8 hỏng, và bên nhận sẽ hiện
            // ra ô vuông thay vì tên người.
            //
            // CHỈ lùi khi thật sự phải cắt. Bản đầu tôi lùi vô điều kiện, nên "Chủ" (5
            // byte, thừa sức vừa) bị lùi khỏi ký tự "ủ" và thành "Ch". Bài kiểm "tên bị
            // cắt vẫn là tiền tố đọc được" KHÔNG bắt được — "Ch" đúng là tiền tố; chỉ
            // phép so khớp chính xác ở bài kiểm loopback mới lộ ra.
            int take = raw.Length;
            if (take > MaxNameBytes)
            {
                take = MaxNameBytes;
                while (take > 0 && (raw[take - 1] & 0xC0) == 0x80) take--;  // lùi khỏi byte tiếp nối
                if (take > 0 && (raw[take - 1] & 0x80) != 0) take--;        // lùi khỏi byte mở đầu lẻ
            }

            body[n++] = (byte)take;
            for (int i = 0; i < take; i++) body[n++] = raw[i];
            return n;
        }

        private static byte[] Finish(byte[] body, int length)
        {
            var packet = new byte[length + 1];
            Array.Copy(body, packet, length);
            packet[length] = Crc8(packet, 0, length);
            return packet;
        }

        public static ParseResult Parse(byte[] data, int length, out Packet packet)
        {
            packet = default(Packet);
            if (data == null || length < 6 || length > MaxPacket) return ParseResult.TooShort;
            if (data[0] != Magic0 || data[1] != Magic1) return ParseResult.BadMagic;
            if (data[2] != ProtocolVersion) return ParseResult.BadProtocol;

            if (Crc8(data, 0, length - 1) != data[length - 1]) return ParseResult.BadCrc;

            var kind = (Kind)data[3];
            int n = 4;
            int senderId = data[n] | (data[n + 1] << 8); n += 2;

            if (kind == Kind.Invite)
            {
                if (length < 12) return ParseResult.TooShort;
                packet.Seed = data[n] | (data[n + 1] << 8) | (data[n + 2] << 16); n += 3;
                packet.Preset = (data[n] >> 4) & 0xF;
                packet.RulesVersion = data[n] & 0xF;
                n++;
            }
            else if (kind == Kind.Finished || kind == Kind.Progress)
            {
                // Gói TIẾN ĐỘ dài hơn gói XONG đúng một byte (tổng đòn), nên hai mốc khác nhau.
                if (length < (kind == Kind.Progress ? 17 : 16)) return ParseResult.TooShort;
                var r = new DuelResult { Seed = -1, Preset = -1, Version = -1 };
                r.BoardTag = data[n] | (data[n + 1] << 8); n += 2;
                r.MovesUsed = data[n++];
                r.Score = data[n] | (data[n + 1] << 8); n += 2;
                r.CellsLeft = data[n++];
                r.Won = data[n++] != 0;

                // Cùng khối byte, khác chỗ đến: bên nhận không phải nhìn Kind lần nữa để
                // biết bản ghi này là kết quả cuối hay chỉ là một mốc giữa ván.
                if (kind == Kind.Finished) packet.Result = r;
                else
                {
                    packet.SentAttacks = data[n++];
                    packet.Progress = r;
                }
            }
            else if (kind != Kind.Seek)
            {
                // TÌM không mang thêm trường nào, nên không có gì để đọc ở đây.
                return ParseResult.BadKind;
            }

            int nameLen = data[n++];
            if (nameLen > MaxNameBytes || n + nameLen != length - 1) return ParseResult.BadName;
            packet.Kind = kind;
            packet.SenderId = senderId;
            packet.Name = nameLen == 0 ? "" : Encoding.UTF8.GetString(data, n, nameLen);
            return ParseResult.Ok;
        }

        private static byte Crc8(byte[] data, int offset, int count)
        {
            byte crc = 0;
            for (int i = 0; i < count; i++)
            {
                crc ^= data[offset + i];
                for (int bit = 0; bit < 8; bit++)
                    crc = (byte)((crc & 0x80) != 0 ? (crc << 1) ^ 0x07 : crc << 1);
            }
            return crc;
        }
    }
}
