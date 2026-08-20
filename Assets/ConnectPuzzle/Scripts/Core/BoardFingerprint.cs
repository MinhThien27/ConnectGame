using System.Collections.Generic;

namespace ConnectPuzzle.Core
{
    /// <summary>
    /// Vân tay của một bàn đã sinh: một số 64 bit gói toàn bộ thứ mà bộ sinh màn
    /// quyết định.
    ///
    /// Dùng để trả lời một câu hỏi mà KHÔNG suy luận nào thay thế được: cùng một seed
    /// chạy trên PC và trên điện thoại có ra đúng cùng một bàn không? Cả "Thử thách
    /// hằng ngày" lẫn "Đấu seed bạn bè" đều đứng trên giả định đó, mà giả định đó
    /// chưa từng được đo trên máy thật.
    ///
    /// Bản thân hàm băm chỉ dùng SỐ NGUYÊN. Nếu nó dùng số thực thì chính nó lại trở
    /// thành một nguồn sai lệch, và ta không phân biệt được "bàn khác nhau" với "hàm
    /// băm khác nhau".
    /// </summary>
    public static class BoardFingerprint
    {
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        private static ulong Mix(ulong hash, int value)
        {
            unchecked
            {
                uint v = (uint)value;
                for (int i = 0; i < 4; i++)
                {
                    hash ^= (byte)(v >> (i * 8));
                    hash *= FnvPrime;
                }
                return hash;
            }
        }

        /// <summary>Vân tay của một LevelData đã dựng xong.</summary>
        public static ulong Of(LevelData level)
        {
            ulong h = FnvOffset;

            h = Mix(h, level.TotalCells);
            h = Mix(h, level.VisibleCells);
            h = Mix(h, level.Par);
            h = Mix(h, level.MaxMoves);
            h = Mix(h, level.TwoStarMoves);
            h = Mix(h, level.MedalChains);
            h = Mix(h, level.MinChain);
            h = Mix(h, level.MaxChain == int.MaxValue ? -1 : level.MaxChain);
            h = Mix(h, level.Gravity ? 1 : 0);
            h = Mix(h, level.GoalMode ? 1 : 0);
            h = Mix(h, level.GoalTotal);

            if (level.Template != null)
            {
                h = Mix(h, level.Template.Length);
                foreach (int c in level.Template) h = Mix(h, c);
            }

            if (level.Columns != null)
            {
                h = Mix(h, level.Columns.Length);
                foreach (int[] column in level.Columns)
                {
                    h = Mix(h, column == null ? -1 : column.Length);
                    if (column == null) continue;
                    foreach (int c in column) h = Mix(h, c);
                }
            }

            h = MixMarks(h, level.Marks);
            if (level.MarkColumns != null)
            {
                h = Mix(h, level.MarkColumns.Length);
                foreach (CellMark[] column in level.MarkColumns) h = MixMarks(h, column);
            }

            if (level.Paths != null)
            {
                h = Mix(h, level.Paths.Count);
                foreach (List<int> p in level.Paths)
                {
                    h = Mix(h, p.Count);
                    foreach (int c in p) h = Mix(h, c);
                }
            }

            if (level.Solution != null)
            {
                h = Mix(h, level.Solution.Count);
                foreach (List<SlotRef> move in level.Solution)
                {
                    h = Mix(h, move.Count);
                    foreach (SlotRef r in move) { h = Mix(h, r.Column); h = Mix(h, r.Slot); }
                }
            }

            if (level.StoneCells != null)
            {
                h = Mix(h, level.StoneCells.Length);
                foreach (int c in level.StoneCells) h = Mix(h, c);
            }
            return h;
        }

        private static ulong MixMarks(ulong h, CellMark[] marks)
        {
            if (marks == null) return Mix(h, -1);
            h = Mix(h, marks.Length);
            foreach (CellMark m in marks)
            {
                if (m == null) { h = Mix(h, -1); continue; }
                h = Mix(h, (int)m.Kind);
                h = Mix(h, m.Hp);
                h = Mix(h, m.Fuse);
                h = Mix(h, m.Goal ? 1 : 0);
                h = Mix(h, m.LinkPartner);
                h = Mix(h, m.LinkId);
            }
            return h;
        }

        /// <summary>
        /// Bộ mẫu dùng để so hai nền tảng. Cố định và có thứ tự — hai bên phải chạy
        /// ĐÚNG cùng danh sách này thì mới so được từng dòng với nhau.
        /// </summary>
        public static List<KeyValuePair<string, ulong>> Sample()
        {
            var result = new List<KeyValuePair<string, ulong>>();

            // 1. cả 90 màn chiến dịch
            for (int i = 0; i < LevelCatalog.Levels.Length; i++)
                result.Add(new KeyValuePair<string, ulong>(
                    "level." + (i + 1), Of(LevelBuilder.Build(LevelCatalog.Levels[i]))));

            // 2. thử thách hằng ngày, đủ 7 kiểu bàn trong 28 ngày liên tiếp
            for (int d = 0; d < 28; d++)
            {
                int key = DailyChallenge.DayKey(new System.DateTime(2026, 3, 1).AddDays(d));
                result.Add(new KeyValuePair<string, ulong>(
                    "daily." + key, Of(DailyChallenge.BuildFor(key))));
            }

            // 3. seed ngẫu nhiên rải trên nhiều cấu hình — đây mới là thứ "đấu seed"
            //    sẽ dùng, và cũng là chỗ dễ lệch nhất vì Fuse là số thực
            int[] seeds = { 1, 7, 12345, 99991, 424242, 1000003, 7654321, 2147483 };
            foreach (int seed in seeds)
                for (int preset = 0; preset < 4; preset++)
                {
                    LevelConfig cfg = DuelConfig(seed, preset);
                    result.Add(new KeyValuePair<string, ulong>(
                        "duel." + seed + "." + preset, Of(LevelBuilder.Build(cfg))));
                }

            return result;
        }

        /// <summary>
        /// Cấu hình "đấu seed". Gọi thẳng hàm SẼ CHẠY THẬT chứ không chép lại: chép
        /// lại thì vân tay đo một bộ luật, còn người chơi gặp một bộ luật khác, và
        /// phép đo giống nhau giữa các máy trở thành vô nghĩa.
        /// </summary>
        public static LevelConfig DuelConfig(int seed, int preset) =>
            DuelChallenge.ConfigFor(seed, preset);

        /// <summary>Vân tay gộp của cả bộ mẫu — một chuỗi để so bằng mắt.</summary>
        public static ulong Aggregate(List<KeyValuePair<string, ulong>> samples)
        {
            ulong h = FnvOffset;
            foreach (var s in samples)
            {
                for (int i = 0; i < 8; i++) { h ^= (byte)(s.Value >> (i * 8)); h *= FnvPrime; }
            }
            return h;
        }
    }
}
