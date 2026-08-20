using System;
using System.Text;

namespace ConnectPuzzle.Core
{
    /// <summary>
    /// Mã đấu: gói (seed, preset, phiên bản luật) thành 8 ký tự kiểu "K7M2-QX9F".
    ///
    /// Mã này người ta sẽ ĐỌC CHO NHAU QUA ĐIỆN THOẠI và gõ tay, nên hai thứ quan
    /// trọng ngang với việc nó ngắn:
    ///
    ///  1. Bảng chữ Crockford Base32 — bỏ I, L, O, U. Nhờ vậy 0/O và 1/I/L không còn
    ///     là hai ký tự khác nhau, và lúc giải mã ta quy chúng về một mối thay vì báo
    ///     lỗi cho người gõ đúng ý nhưng sai hình.
    ///  2. Có checksum — gõ sai một ký tự phải bị TỪ CHỐI. Không có checksum thì mã
    ///     sai vẫn giải ra một seed hợp lệ khác, hai người ngồi chơi hai bàn khác nhau
    ///     mà cả hai đều tưởng đang đấu chung. Sai im lặng, kiểu tệ nhất.
    /// </summary>
    public static class DuelCode
    {
        /// <summary>
        /// Phiên bản LUẬT SINH MÀN. Phải tăng mỗi khi bộ sinh màn đổi cách chạy.
        ///
        /// Không có nó thì sau một lần cập nhật, cùng một mã ra hai bàn khác nhau trên
        /// hai máy chạy hai bản app — và không bên nào biết. Vân tay bàn
        /// (BoardFingerprint) chính là cách phát hiện lúc nào phải tăng số này.
        /// </summary>
        public const int Version = 1;

        public const int PresetCount = 4;
        public const int SeedBits = 24;
        public const int MaxSeed = 1 << SeedBits;          // 16.777.216 bàn

        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        public static string Encode(int seed, int preset) => Encode(seed, preset, Version);

        public static string Encode(int seed, int preset, int version)
        {
            if (seed < 0 || seed >= MaxSeed) throw new ArgumentOutOfRangeException(nameof(seed));
            if (preset < 0 || preset >= 16) throw new ArgumentOutOfRangeException(nameof(preset));
            if (version < 0 || version >= 16) throw new ArgumentOutOfRangeException(nameof(version));

            uint payload = ((uint)seed << 8) | ((uint)preset << 4) | (uint)version;

            // ulong chứ KHÔNG phải uint: payload đã chiếm trọn 32 bit, dịch thêm 8 bit
            // cho checksum là 40 bit. Để uint thì 8 bit cao của seed rơi mất lặng lẽ và
            // mã giải ra một seed khác — đúng kiểu sai im lặng mà cả cơ chế này phải tránh.
            ulong full = ((ulong)payload << 8) | Checksum(payload);      // 40 bit

            var sb = new StringBuilder(9);
            for (int i = 7; i >= 0; i--)
            {
                int index = (int)((full >> (i * 5)) & 0x1F);
                sb.Append(Alphabet[index]);
                if (i == 4) sb.Append('-');                      // chia đôi cho dễ đọc
            }
            return sb.ToString();
        }

        public enum DecodeResult { Ok, BadLength, BadChar, BadChecksum }

        public static DecodeResult TryDecode(string code, out int seed, out int preset, out int version)
        {
            seed = 0; preset = 0; version = 0;
            if (code == null) return DecodeResult.BadLength;

            ulong full = 0;
            int digits = 0;
            foreach (char raw in code)
            {
                char c = char.ToUpperInvariant(raw);
                if (c == '-' || c == ' ' || c == '_' || c == '.') continue;   // bỏ dấu trang trí

                // Quy các ký tự DỄ NHÌN NHẦM về một mối. Người gõ chữ O thay số 0 là
                // đang gõ đúng ý mình; từ chối họ là đổ lỗi cho người dùng vì lỗi phông chữ.
                if (c == 'O') c = '0';
                else if (c == 'I' || c == 'L') c = '1';
                else if (c == 'U') c = 'V';

                int index = Alphabet.IndexOf(c);
                if (index < 0) return DecodeResult.BadChar;

                if (digits >= 8) return DecodeResult.BadLength;
                full = (full << 5) | (uint)index;
                digits++;
            }
            if (digits != 8) return DecodeResult.BadLength;

            uint payload = (uint)(full >> 8);
            if ((byte)(full & 0xFF) != Checksum(payload)) return DecodeResult.BadChecksum;

            seed = (int)(payload >> 8);
            preset = (int)((payload >> 4) & 0xF);
            version = (int)(payload & 0xF);
            return DecodeResult.Ok;
        }

        /// <summary>
        /// CRC-8 (đa thức 0x07) trên 4 byte payload.
        ///
        /// Lý do chọn CRC — đã ĐO chứ không chép theo lời đồn. Ban đầu tôi viết ở đây
        /// rằng "tổng cộng dồn không bắt được lỗi đảo chỗ"; đem đo thì sai: tổng cũng
        /// bắt 100% ca đảo chỗ ở kích thước này (ký tự base32 dài 5 bit nên không khớp
        /// biên byte, đảo chỗ vẫn làm tổng byte đổi).
        ///
        /// Số thật trên cùng bộ mẫu:
        ///     sai 1 ký tự     CRC 100%     tổng 100%
        ///     đảo chỗ 2       CRC 100%     tổng 100%
        ///     hỏng 3 ký tự    CRC 99.62%   tổng 99.51%   (trần lý thuyết 8 bit ~99.61%)
        ///
        /// Nên CRC hơn không đáng kể. Giữ CRC vì nó BẢO ĐẢM bắt mọi lỗi cụm ≤ 8 bit —
        /// mà sai một ký tự chỉ đổi 5 bit nằm liền nhau, nên 100% ở dòng đầu là định
        /// lý chứ không phải may mắn. Với tổng thì 100% đó chỉ là quan sát.
        /// </summary>
        private static byte Checksum(uint payload)
        {
            byte crc = 0;
            for (int i = 3; i >= 0; i--)
            {
                crc ^= (byte)(payload >> (i * 8));
                for (int bit = 0; bit < 8; bit++)
                    crc = (byte)((crc & 0x80) != 0 ? (crc << 1) ^ 0x07 : crc << 1);
            }
            return crc;
        }

        /// <summary>
        /// Moi mã ra khỏi một đoạn văn bản dán vào. Trả null nếu không có mã hợp lệ.
        ///
        /// Người ta KHÔNG copy đúng 8 ký tự — họ copy cả tin nhắn:
        ///     "Đấu Connect Puzzle với tôi! Mã: K7M2-QX9F (Băng)"
        /// Bắt buộc cả chuỗi phải là mã thì nút Dán hầu như luôn báo lỗi, và người
        /// chơi phải tự cắt chuỗi bằng tay trên điện thoại — việc khó chịu nhất có thể.
        ///
        /// Quét mọi cửa sổ 8 ký tự hợp lệ và lấy cửa sổ ĐẦU TIÊN qua được checksum.
        /// Nhờ checksum nên chuyện quét trúng một đoạn rác mà vẫn hợp lệ là hiếm
        /// (~1/256), và nếu có thì nó cũng là một mã có thật, không phải bàn hỏng.
        /// </summary>
        public static string ExtractFrom(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            // giữ lại các ký tự CÓ THỂ là một phần của mã, nhớ nguyên bản để dựng lại
            var sb = new StringBuilder(text.Length);
            foreach (char raw in text)
            {
                char c = char.ToUpperInvariant(raw);
                if (c == 'O') c = '0';
                else if (c == 'I' || c == 'L') c = '1';
                else if (c == 'U') c = 'V';
                if (Alphabet.IndexOf(c) >= 0) sb.Append(c);
                else if (c == '-' || c == '_' || c == '.') { /* dấu trang trí TRONG mã: bỏ đi */ }
                else sb.Append(' ');                    // mọi thứ khác là ngắt cụm
            }

            foreach (string chunk in sb.ToString().Split(' '))
            {
                if (chunk.Length < 8) continue;
                for (int start = 0; start + 8 <= chunk.Length; start++)
                {
                    string candidate = chunk.Substring(start, 8);
                    if (TryDecode(candidate, out int s, out int p, out int v) == DecodeResult.Ok)
                        return Encode(s, p, v);          // trả về dạng chuẩn có gạch
                }
            }
            return null;
        }
    }
}
