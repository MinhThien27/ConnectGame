using System.Collections.Generic;
using System.Text;

namespace ConnectPuzzle.Core
{
    /// <summary>
    /// Dựng đoạn kết quả thử thách hằng ngày để dán vào chỗ khác (Zalo, Messenger...).
    ///
    /// Vì sao có: bàn của một ngày GIỐNG NHAU trên mọi máy — đó là cả điểm của chế độ này
    /// — nhưng chơi xong thì không mang được gì ra ngoài, nên không ai so với ai. Một đoạn
    /// chữ dán được là kênh lan truyền rẻ nhất mà một game chạy offline có thể có, và dữ
    /// liệu thì đã nằm sẵn cả.
    ///
    /// KHÔNG TIẾT LỘ BÀN. Đây là ràng buộc chính, không phải chi tiết: mọi người chơi cùng
    /// một bàn trong ngày, nên một lưới ô vuông tô theo MÀU hay theo VỊ TRÍ là bản đồ đáp
    /// án — người nhận mở ra là mất luôn câu đố. Ô vuông ở đây tô theo CHẤT LƯỢNG chuỗi
    /// (dài so với trần của màn), thứ nói lên cách người ta chơi mà không nói gì về bàn.
    ///
    /// Không phụ thuộc UnityEngine — dựng chuỗi là việc thuần, kiểm được ngoài Editor.
    /// </summary>
    public static class DailyShare
    {
        /// <summary>Số ô vuông mỗi dòng. 10 để một ván dài vẫn gọn vài dòng.</summary>
        private const int PerRow = 10;

        // Bốn bậc chất lượng. Dùng ô vuông màu vì chúng nằm trong bộ emoji cơ bản, hiện
        // được ở gần như mọi app chat — khác với ký hiệu lạ, thứ hay ra ô trống.
        private const string Full = "🟩";      // kịch trần
        private const string Long = "🟦";      // gần trần
        private const string Mid  = "🟨";      // giữa
        private const string Min  = "⬜";      // tối thiểu

        /// <summary>
        /// Bậc của một chuỗi dài `length`.
        ///
        /// Khi màn CÓ trần chuỗi thì đo theo trần, vì đó mới là thước đo thật: chuỗi 5 ô
        /// ở màn trần 5 là hoàn hảo, còn ở màn trần 8 thì chỉ là trung bình. Màn không có
        /// trần (chế độ vô tận dùng chung hàm này về sau) thì rơi về mốc tuyệt đối.
        /// </summary>
        public static string Tier(int length, int minChain, int maxChain)
        {
            if (maxChain == int.MaxValue || maxChain <= 0)
                return length >= 7 ? Full : length >= 5 ? Long : length >= 4 ? Mid : Min;

            if (length >= maxChain) return Full;
            if (length >= maxChain - 1) return Long;
            if (length <= minChain) return Min;
            return Mid;
        }

        /// <summary>Lưới ô vuông, mỗi ô một nước ăn chuỗi, cắt dòng mỗi 10 ô.</summary>
        public static string Grid(IList<int> chainLog, int minChain, int maxChain)
        {
            if (chainLog == null || chainLog.Count == 0) return "";

            var sb = new StringBuilder();
            for (int i = 0; i < chainLog.Count; i++)
            {
                if (i > 0 && i % PerRow == 0) sb.Append('\n');
                sb.Append(Tier(chainLog[i], minChain, maxChain));
            }
            return sb.ToString();
        }

        /// <summary>Ngày dạng dd/MM từ khoá yyyyMMdd. Không dùng DateTime.ToString để
        /// khỏi phụ thuộc văn hoá vùng của máy — cùng một ngày phải ra cùng một chuỗi.</summary>
        public static string DayLabel(int dayKey)
        {
            int day = dayKey % 100;
            int month = dayKey / 100 % 100;
            return (day < 10 ? "0" : "") + day + "/" + (month < 10 ? "0" : "") + month;
        }

        /// <summary>
        /// Đoạn kết quả hoàn chỉnh.
        ///
        /// `streak` là chuỗi ngày SAU khi đã ghi ván này, nên nó đã tính cả hôm nay.
        /// </summary>
        public static string Build(int dayKey, bool won, int stars, int movesUsed, int par,
                                  int score, int streak, IList<int> chainLog,
                                  int minChain, int maxChain)
        {
            var sb = new StringBuilder();

            sb.Append("Connect Puzzle · Thử thách ").Append(DayLabel(dayKey)).Append('\n');

            if (won)
            {
                for (int i = 0; i < 3; i++) sb.Append(i < stars ? '★' : '☆');
                sb.Append(' ').Append(movesUsed).Append(" lượt");
                // Chỉ nói "tối ưu" khi mình CHƯA đạt: đã bằng tối ưu rồi thì số sao đã nói
                // hết, thêm một mốc nữa chỉ làm dòng dài ra.
                if (movesUsed > par) sb.Append(" (tối ưu ").Append(par).Append(')');
            }
            else
            {
                sb.Append("✗ chưa giải được · ").Append(movesUsed).Append(" lượt");
            }
            sb.Append(" · ").Append(score).Append(" điểm");

            string grid = Grid(chainLog, minChain, maxChain);
            if (grid.Length > 0)
            {
                sb.Append('\n').Append(grid);

                // Chú giải BẮT BUỘC phải có. Lưới của Wordle tự giải thích được (xanh = đúng
                // chỗ) nên nó không cần chú giải; lưới ở đây thì không — người nhận thấy một
                // dãy ô vuông mà không biết nghĩa thì nó chỉ là trang trí. Mà người nhận
                // chính là người CHƯA chơi, tức là đối tượng duy nhất mà việc chia sẻ nhắm tới.
                sb.Append('\n').Append(Full).Append(" chuỗi dài nhất · ")
                  .Append(Min).Append(" ngắn nhất · mỗi ô một lượt");
            }

            if (streak > 1) sb.Append("\nchuỗi ").Append(streak).Append(" ngày");

            return sb.ToString();
        }
    }
}
