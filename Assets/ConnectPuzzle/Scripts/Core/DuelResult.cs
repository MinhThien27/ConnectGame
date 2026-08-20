using System;
using System.Text;

namespace ConnectPuzzle.Core
{
    /// <summary>
    /// Thành tích của một người trên một bàn đấu cụ thể.
    ///
    /// Mang theo DẤU NHẬN DẠNG BÀN, không chỉ mang con số. Nếu chỉ mang số thì hai
    /// người có thể đem kết quả của hai bàn khác nhau ra so mà không ai biết — cùng
    /// một loại hỏng im lặng mà checksum của mã đấu đã chặn.
    ///
    /// Seed/Preset/Version chỉ có nghĩa với kết quả do CHÍNH máy này sinh; kết quả
    /// giải mã từ máy khác để chúng bằng -1 và chỉ có BoardTag.
    /// </summary>
    public struct DuelResult
    {
        public int Seed;
        public int Preset;
        public int Version;

        public int MovesUsed;
        public int Score;

        /// <summary>Số ô còn lại lúc ván kết thúc. 0 nghĩa là dọn sạch.</summary>
        public int CellsLeft;

        public bool Won;

        /// <summary>
        /// Dấu nhận dạng bàn, 16 bit băm từ (seed, preset, phiên bản).
        ///
        /// Mã kết quả chở DẤU này chứ không chở cả seed. Lý do là số học: seed 24 bit
        /// cộng đủ các trường thành tích là 58 bit, thêm 8 bit checksum thành 66 — vượt
        /// ulong 64 bit. Tôi đã thử và nó tràn im lặng, 3017/4000 mã bung ra sai.
        ///
        /// Dấu 16 bit làm đúng việc cần làm: trả lời "hai người có chơi cùng một bàn
        /// không". Nó không dựng lại được bàn, mà cũng không cần — người nhận đã có bàn
        /// trong tay rồi. Xác suất hai bàn khác nhau trùng dấu là 1/65536.
        /// </summary>
        public int BoardTag;

        public static int TagOf(int seed, int preset, int version)
        {
            unchecked
            {
                uint h = (uint)(seed & 0xFFFFFF);
                h = h * 2654435761u + (uint)((preset & 0xF) << 4 | (version & 0xF));
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (int)(h & 0xFFFF);
            }
        }

        public bool SameBoardAs(DuelResult other) => this.BoardTag == other.BoardTag;

        /// <summary>Mã bàn — chỉ đọc được khi kết quả này do CHÍNH máy này sinh ra.</summary>
        public string BoardCode =>
            this.Seed >= 0 ? DuelCode.Encode(this.Seed, this.Preset, this.Version)
                           : "dấu " + this.BoardTag.ToString("X4");
    }

    public enum DuelOutcome { Win, Lose, Draw, DifferentBoard }

    /// <summary>
    /// Phân định thắng thua giữa hai kết quả.
    ///
    /// KHÔNG có yếu tố thời gian, và không thêm luật mới nào: thứ tự ưu tiên đúng bằng
    /// thứ tự giá trị mà game vẫn dùng để chấm một ván đơn — dọn sạch được là quan
    /// trọng nhất, rồi tới tiết kiệm lượt (đúng thứ sao đang đo), rồi tới điểm.
    ///
    /// Vì sao KHÔNG lấy "ai xong trước": luật đó đo tốc độ tay, mà cả game này không
    /// thưởng cho nhanh. Tệ hơn, nó có thể phong vương cho người chơi dở hơn — người
    /// xong ở nước 14 thắng người đang ở nước 10 và sắp xong ở nước 11.
    /// </summary>
    public static class DuelVerdict
    {
        public static DuelOutcome Compare(DuelResult mine, DuelResult theirs, out string reason)
        {
            if (!mine.SameBoardAs(theirs))
            {
                reason = "Hai kết quả thuộc hai bàn khác nhau (" + mine.BoardCode +
                         " và " + theirs.BoardCode + ") nên không so được.";
                return DuelOutcome.DifferentBoard;
            }

            // 1. Dọn sạch bàn ăn tất. Đây là điều kiện THẮNG của game, không phải một
            //    tiêu chí phụ mới bày ra cho chế độ đấu.
            if (mine.Won != theirs.Won)
            {
                reason = mine.Won
                    ? "Bạn dọn sạch bàn, đối thủ thì không."
                    : "Đối thủ dọn sạch bàn, bạn thì không.";
                return mine.Won ? DuelOutcome.Win : DuelOutcome.Lose;
            }

            if (mine.Won)
            {
                // 2. Cả hai thắng: ít lượt hơn thì hơn — đúng thứ mà sao đang đo.
                if (mine.MovesUsed != theirs.MovesUsed)
                {
                    reason = "Bạn đi " + mine.MovesUsed + " lượt, đối thủ " + theirs.MovesUsed + ".";
                    return mine.MovesUsed < theirs.MovesUsed ? DuelOutcome.Win : DuelOutcome.Lose;
                }
            }
            else
            {
                // 3. Cả hai thua: ai còn ít ô hơn thì đi xa hơn.
                if (mine.CellsLeft != theirs.CellsLeft)
                {
                    reason = "Cả hai đều bí. Bạn còn " + mine.CellsLeft +
                             " ô, đối thủ còn " + theirs.CellsLeft + ".";
                    return mine.CellsLeft < theirs.CellsLeft ? DuelOutcome.Win : DuelOutcome.Lose;
                }
            }

            // 4. Cuối cùng mới tới điểm.
            if (mine.Score != theirs.Score)
            {
                reason = "Bằng nhau về lượt, phân định bằng điểm: " + mine.Score +
                         " so với " + theirs.Score + ".";
                return mine.Score > theirs.Score ? DuelOutcome.Win : DuelOutcome.Lose;
            }

            reason = "Giống nhau hoàn toàn — hoà.";
            return DuelOutcome.Draw;
        }

        /// <summary>Kết quả của một session đang chơi, để gửi đi hoặc để so.</summary>
        public static DuelResult From(PuzzleSession session, int seed, int preset)
        {
            return new DuelResult
            {
                Seed = seed,
                Preset = preset,
                Version = DuelCode.Version,
                MovesUsed = session.MovesUsed,
                Score = session.Score,
                CellsLeft = session.TotalLeft(),
                Won = session.IsWon(),
                BoardTag = DuelResult.TagOf(seed, preset, DuelCode.Version)
            };
        }
    }

    /// <summary>
    /// Mã kết quả: gói một DuelResult thành 10 ký tự dán được, kiểu "ABCD-EFG-HJK".
    ///
    /// Dài hơn mã đấu (10 so với 8) vì nó chở thêm thành tích. Không sao: mã này chỉ để
    /// DÁN, không ai gõ tay nó — khác với mã đấu, thứ người ta còn đọc cho nhau qua
    /// điện thoại.
    /// </summary>
    public static class DuelResultCode
    {
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        public const int MaxMoves = 128;        // 7 bit
        public const int MaxScore = 4096;       // 12 bit
        public const int MaxCells = 64;         // 6 bit

        // 16 (dấu bàn) + 7 + 12 + 6 + 1 = 42 bit payload, + 8 checksum = 50 bit.
        // 50 chia hết cho 5 nên ra ĐÚNG 10 ký tự base32, và nằm gọn trong ulong.
        //
        // Bản đầu tôi chở cả seed 24 bit: 58 + 8 = 66 bit, vượt ulong. Nó tràn IM LẶNG
        // (không ngoại lệ, không cảnh báo) và 3017/4000 mã bung ra sai. Bài kiểm vòng
        // tròn bắt được; nếu chỉ thử vài mã bằng tay thì đã lọt.
        private const int PayloadBits = 16 + 7 + 12 + 6 + 1;    // 42
        private const int TotalBits = PayloadBits + 8;          // 50 -> 10 ký tự
        private const int Chars = TotalBits / 5;                // 10

        public static string Encode(DuelResult r)
        {
            // Kẹp thay vì ném: một ván có điểm bất thường cao thì thà mã hoá hơi lệch
            // còn hơn văng ngay lúc người chơi vừa thắng.
            int moves = Clamp(r.MovesUsed, MaxMoves);
            int score = Clamp(r.Score, MaxScore);
            int cells = Clamp(r.CellsLeft, MaxCells);
            int tag = r.BoardTag != 0 || r.Seed < 0
                ? r.BoardTag & 0xFFFF
                : DuelResult.TagOf(r.Seed, r.Preset, r.Version);

            ulong payload = 0;
            payload = (payload << 16) | (uint)tag;
            payload = (payload << 7) | (uint)moves;
            payload = (payload << 12) | (uint)score;
            payload = (payload << 6) | (uint)cells;
            payload = (payload << 1) | (r.Won ? 1u : 0u);

            ulong full = (payload << 8) | Checksum(payload);

            var sb = new StringBuilder(Chars + 2);
            for (int i = Chars - 1; i >= 0; i--)
            {
                sb.Append(Alphabet[(int)((full >> (i * 5)) & 0x1F)]);
                if (i == 6 || i == 3) sb.Append((char)45);       // 4-3-3 cho dễ đọc
            }
            return sb.ToString();
        }

        private static int Clamp(int value, int exclusiveMax)
        {
            if (value < 0) return 0;
            return value >= exclusiveMax ? exclusiveMax - 1 : value;
        }

        public static DuelCode.DecodeResult TryDecode(string code, out DuelResult result)
        {
            result = default(DuelResult);
            if (code == null) return DuelCode.DecodeResult.BadLength;

            ulong full = 0;
            int digits = 0;
            foreach (char raw in code)
            {
                char c = Normalise(raw);
                if (c == '\0') continue;
                int index = Alphabet.IndexOf(c);
                if (index < 0) return DuelCode.DecodeResult.BadChar;
                if (digits >= Chars) return DuelCode.DecodeResult.BadLength;
                full = (full << 5) | (uint)index;
                digits++;
            }
            if (digits != Chars) return DuelCode.DecodeResult.BadLength;

            ulong payload = full >> 8;
            if ((byte)(full & 0xFF) != Checksum(payload)) return DuelCode.DecodeResult.BadChecksum;

            result.Won = (payload & 0x1) != 0;            payload >>= 1;
            result.CellsLeft = (int)(payload & 0x3F);     payload >>= 6;
            result.Score = (int)(payload & 0xFFF);        payload >>= 12;
            result.MovesUsed = (int)(payload & 0x7F);     payload >>= 7;
            result.BoardTag = (int)(payload & 0xFFFF);

            // Kết quả đến từ máy khác: KHÔNG biết seed/preset/phiên bản, chỉ biết dấu bàn.
            // Đánh -1 rõ ràng thay vì để 0 — 0 là một seed có thật, và một seed sai lặng
            // lẽ còn tệ hơn một giá trị nói thẳng "tôi không biết".
            result.Seed = -1;
            result.Preset = -1;
            result.Version = -1;
            return DuelCode.DecodeResult.Ok;
        }

        /// <summary>
        /// Quy ký tự dễ nhìn nhầm về một mối; trả '\0' cho dấu trang trí cần bỏ qua và
        /// giữ nguyên mọi thứ khác để bên gọi tự xử.
        /// </summary>
        private static char Normalise(char raw)
        {
            char c = char.ToUpperInvariant(raw);
            if (c == '-' || c == ' ' || c == '_' || c == '.') return '\0';
            if (c == 'O') return '0';
            if (c == 'I' || c == 'L') return '1';
            if (c == 'U') return 'V';
            return c;
        }

        /// <summary>Moi mã kết quả ra khỏi cả câu, như DuelCode.ExtractFrom.</summary>
        public static string ExtractFrom(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            var sb = new StringBuilder(text.Length);
            foreach (char raw in text)
            {
                char c = Normalise(raw);
                if (c == '\0') continue;                       // dấu trong mã: bỏ, KHÔNG cắt cụm
                if (Alphabet.IndexOf(c) >= 0) sb.Append(c);
                else sb.Append(' ');
            }

            foreach (string chunk in sb.ToString().Split(' '))
            {
                if (chunk.Length < Chars) continue;
                for (int start = 0; start + Chars <= chunk.Length; start++)
                {
                    string candidate = chunk.Substring(start, Chars);
                    if (TryDecode(candidate, out DuelResult r) == DuelCode.DecodeResult.Ok)
                        return Encode(r);
                }
            }
            return null;
        }

        private static byte Checksum(ulong payload)
        {
            byte crc = 0;
            for (int i = 7; i >= 0; i--)
            {
                crc ^= (byte)(payload >> (i * 8));
                for (int bit = 0; bit < 8; bit++)
                    crc = (byte)((crc & 0x80) != 0 ? (crc << 1) ^ 0x07 : crc << 1);
            }
            return crc;
        }
    }
}
