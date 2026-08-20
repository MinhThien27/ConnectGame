using System.Collections.Generic;

namespace ConnectPuzzle.Core
{
    /// <summary>Một ô trong lời giải gravity: cột nào, slot thứ mấy tính từ đáy.</summary>
    public struct SlotRef
    {
        public int Column;
        public int Slot;

        public SlotRef(int column, int slot) { this.Column = column; this.Slot = slot; }
    }

    public sealed class GravityPlan
    {
        /// <summary>Columns[x][slot] = màu, slot 0 ở đáy. Độ dài = chiều cao cột.</summary>
        public int[][] Columns;

        /// <summary>Dãy nước đi tạo thành lời giải. Số nước = par.</summary>
        public List<List<SlotRef>> Solution;

        public int TotalCells;
    }

    /// <summary>
    /// Sinh màn GRAVITY bằng mô phỏng ván chơi xuôi + tô màu lười.
    ///
    /// Mô hình: mỗi CỘT là một chồng slot (Rows slot nhìn thấy + QueueRows slot ẩn
    /// phía trên). Gravity chỉ xoá phần tử trong chồng — thứ tự nội bộ của cột
    /// KHÔNG BAO GIỜ đổi, và ô không bao giờ nhảy sang cột khác. Nên vị trí hiện
    /// tại của slot thứ k (tính từ đáy, trong các slot còn lại) chính là hàng k,
    /// và mô phỏng là tất định.
    ///
    /// Mẹo đảm bảo giải được: KHÔNG tô màu trước. Máy tự chơi trên bàn chưa có màu,
    /// mỗi lượt chọn một chuỗi rồi MỚI gán màu cho đúng các ô vừa ăn. Vì mọi ô chưa
    /// ăn đều chưa có màu nên chuỗi nào cũng gán được => dãy nước đi ghi lại luôn là
    /// một lời giải hợp lệ, và par = số nước đi.
    /// </summary>
    public static class GravityLevelGenerator
    {
        /// <summary>
        /// heights = số slot của từng cột. Truyền null khi sinh màn mới
        /// (= Rows + QueueRows cho mọi cột); truyền chiều cao hiện tại khi XÁO LẠI.
        /// </summary>
        public static GravityPlan Simulate(LevelConfig cfg, DeterministicRng rng, int[] heights,
                                           int minLen, int maxLen)
        {
            return Simulate(cfg, rng, heights, minLen, maxLen, minLen, int.MaxValue);
        }

        /// <summary>
        /// minChain / maxChain là luật chơi: mọi nước trong lời giải phải nằm trong
        /// khoảng đó, và không được để lại phần dư ít hơn minChain ô.
        /// </summary>
        public static GravityPlan Simulate(LevelConfig cfg, DeterministicRng rng, int[] heights,
                                           int minLen, int maxLen, int minChain, int maxChain)
        {
            int columns = cfg.Columns;
            int rows = cfg.Rows;

            var hs = new int[columns];
            if (heights == null) for (int x = 0; x < columns; x++) hs[x] = rows + cfg.QueueRows;
            else for (int x = 0; x < columns; x++) hs[x] = heights[x];

            // remaining[x] = danh sách slot gốc còn lại, từ đáy lên
            var remaining = new List<int>[columns];
            var colorOf = new int[columns][];
            int total = 0;
            for (int x = 0; x < columns; x++)
            {
                remaining[x] = new List<int>(hs[x]);
                colorOf[x] = new int[hs[x]];
                for (int k = 0; k < hs[x]; k++) { remaining[x].Add(k); colorOf[x][k] = -1; }
                total += hs[x];
            }

            var solution = new List<List<SlotRef>>();
            if (total == 0) return new GravityPlan { Columns = colorOf, Solution = solution, TotalCells = 0 };

            // Dải cột mà mỗi màu đã chiếm, để giữ màu không trải quá rộng
            var colorMinColumn = new int[cfg.Colors];
            var colorMaxColumn = new int[cfg.Colors];
            for (int c = 0; c < cfg.Colors; c++) { colorMinColumn[c] = -1; colorMaxColumn[c] = -1; }
            int columnSpan = cfg.ColorColumnSpan;

            int left = total;
            int guard = 0;
            var used = new HashSet<long>();
            var candidates = new List<SlotRef>(8);
            var occupiedOrder = new List<int>(columns);
            var banned = new HashSet<int>();
            var pool = new List<int>();

            while (left > 0)
            {
                if (guard++ > 20000) return null;
                // còn ít hơn một chuỗi hợp lệ => phần dư không bao giờ ăn được
                if (left < minChain) return null;

                List<SlotRef> path = null;

                for (int attempt = 0; attempt < 40 && path == null; attempt++)
                {
                    // Ưu tiên cột còn NHIỀU ô nhất để các cột cạn đều nhau. Giữ độ cao
                    // xấp xỉ bằng nhau thì luôn còn ô kề nhau, tránh phân mảnh cuối ván
                    // (mấy cột lẻ loi cách nhau 2 cột thì không ô nào kề ô nào nữa).
                    occupiedOrder.Clear();
                    for (int x = 0; x < columns; x++) if (remaining[x].Count > 0) occupiedOrder.Add(x);
                    if (occupiedOrder.Count == 0) return null;
                    SortByRemainingDescending(remaining, occupiedOrder);

                    int startColumn = rng.NextDouble() < 0.75
                        ? occupiedOrder[0]
                        : occupiedOrder[rng.NextInt(occupiedOrder.Count)];

                    int visibleCount = Min(remaining[startColumn].Count, rows);
                    int startIndex = rng.NextInt(visibleCount);

                    // Không được để lại phần dư nhỏ hơn một chuỗi hợp lệ: hoặc ăn hết,
                    // hoặc chừa lại ít nhất minChain ô.
                    int target = rng.NextRange(minLen, maxLen);
                    if (left - target > 0 && left - target < minChain) target = left - minChain;
                    if (target < minChain) target = minChain;
                    if (target > left) target = left;
                    if (target > maxChain) target = maxChain;
                    if (left - target > 0 && left - target < minChain) continue;

                    used.Clear();
                    used.Add(Key(startColumn, startIndex));
                    var candidate = new List<SlotRef> { new SlotRef(startColumn, startIndex) };

                    while (candidate.Count < target)
                    {
                        SlotRef last = candidate[candidate.Count - 1];
                        CollectNeighbors(remaining, rows, columns, last.Column, last.Slot, used, candidates);
                        if (candidates.Count == 0) break;

                        SortByColumnRemainingDescending(remaining, candidates);
                        SlotRef pick = rng.NextDouble() < 0.6
                            ? candidates[0]
                            : candidates[rng.NextInt(candidates.Count)];

                        used.Add(Key(pick.Column, pick.Slot));
                        candidate.Add(pick);
                    }

                    // Không được để sót phần dư nhỏ hơn một chuỗi hợp lệ. Nới dài chuỗi
                    // nếu còn chỗ, không thì rút ngắn — miễn vẫn đủ minChain.
                    int guardFix = 0;
                    while (left - candidate.Count > 0 && left - candidate.Count < minChain && guardFix++ < 8)
                    {
                        SlotRef tail = candidate[candidate.Count - 1];
                        CollectNeighbors(remaining, rows, columns, tail.Column, tail.Slot, used, candidates);
                        if (candidates.Count > 0 && candidate.Count < maxChain)
                        {
                            used.Add(Key(candidates[0].Column, candidates[0].Slot));
                            candidate.Add(candidates[0]);
                        }
                        else if (candidate.Count - 1 >= minChain)
                        {
                            SlotRef drop = candidate[candidate.Count - 1];
                            used.Remove(Key(drop.Column, drop.Slot));
                            candidate.RemoveAt(candidate.Count - 1);
                        }
                        else break;
                    }
                    if (left - candidate.Count > 0 && left - candidate.Count < minChain) continue;

                    if (candidate.Count >= minChain && candidate.Count <= maxChain) path = candidate;
                }

                if (path == null) return null;

                // slot gốc của từng ô trong chuỗi
                var slots = new List<SlotRef>(path.Count);
                foreach (SlotRef p in path) slots.Add(new SlotRef(p.Column, remaining[p.Column][p.Slot]));

                // Tô màu: chỉ ràng buộc theo BỐ CỤC BAN ĐẦU (slot < rows là những ô
                // người chơi thấy lúc mở màn) để `fuse` vẫn điều khiển được độ "dính"
                // của bàn khởi đầu. Các ô trong hàng chờ thì màu tự do.
                banned.Clear();
                foreach (SlotRef s in slots)
                {
                    if (s.Slot >= rows) continue;
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dk = -1; dk <= 1; dk++)
                        {
                            if (dx == 0 && dk == 0) continue;
                            int nx = s.Column + dx, nk = s.Slot + dk;
                            if (nx < 0 || nx >= columns || nk < 0 || nk >= rows || nk >= hs[nx]) continue;
                            int c = colorOf[nx][nk];
                            if (c >= 0 && rng.NextDouble() >= cfg.Fuse) banned.Add(c);
                        }
                }
                // Dải cột của chuỗi này
                int pathMinColumn = int.MaxValue, pathMaxColumn = int.MinValue;
                foreach (SlotRef s in slots)
                {
                    if (s.Column < pathMinColumn) pathMinColumn = s.Column;
                    if (s.Column > pathMaxColumn) pathMaxColumn = s.Column;
                }

                // Ưu tiên màu mà thêm chuỗi này vào vẫn không làm dải cột của nó vượt
                // giới hạn. Nới dần khi không còn lựa chọn, để không bao giờ bế tắc.
                pool.Clear();
                for (int c = 0; c < cfg.Colors; c++)
                {
                    if (banned.Contains(c)) continue;
                    if (!FitsColumnSpan(colorMinColumn[c], colorMaxColumn[c], pathMinColumn, pathMaxColumn, columnSpan))
                        continue;
                    pool.Add(c);
                }
                if (pool.Count == 0)
                    for (int c = 0; c < cfg.Colors; c++)
                        if (FitsColumnSpan(colorMinColumn[c], colorMaxColumn[c], pathMinColumn, pathMaxColumn, columnSpan))
                            pool.Add(c);
                if (pool.Count == 0)
                    for (int c = 0; c < cfg.Colors; c++) if (!banned.Contains(c)) pool.Add(c);
                if (pool.Count == 0)
                    for (int c = 0; c < cfg.Colors; c++) pool.Add(c);

                int color = pool[rng.NextInt(pool.Count)];
                foreach (SlotRef s in slots) colorOf[s.Column][s.Slot] = color;

                colorMinColumn[color] = colorMinColumn[color] < 0
                    ? pathMinColumn : Min(colorMinColumn[color], pathMinColumn);
                colorMaxColumn[color] = colorMaxColumn[color] < 0
                    ? pathMaxColumn : Max(colorMaxColumn[color], pathMaxColumn);

                // ăn: xoá khỏi remaining, xoá chỉ số lớn trước để không lệch
                RemoveFromColumns(remaining, path);
                left -= slots.Count;
                solution.Add(slots);
            }

            return new GravityPlan { Columns = colorOf, Solution = solution, TotalCells = total };
        }

        /// <summary>
        /// Ô nhìn thấy = chỉ số &lt; rows trong chồng còn lại. Hai ô kề nhau khi lệch
        /// tối đa 1 cột và 1 bậc — đúng luật 8 hướng trên bàn đang thấy.
        /// </summary>
        private static void CollectNeighbors(List<int>[] remaining, int rows, int columns,
                                            int column, int slot, HashSet<long> used, List<SlotRef> output)
        {
            output.Clear();
            for (int dx = -1; dx <= 1; dx++)
                for (int di = -1; di <= 1; di++)
                {
                    if (dx == 0 && di == 0) continue;
                    int nx = column + dx, ni = slot + di;
                    if (nx < 0 || nx >= columns || ni < 0) continue;
                    if (ni >= Min(remaining[nx].Count, rows)) continue;
                    if (used.Contains(Key(nx, ni))) continue;
                    output.Add(new SlotRef(nx, ni));
                }
        }

        private static void RemoveFromColumns(List<int>[] remaining, List<SlotRef> path)
        {
            var byColumn = new Dictionary<int, List<int>>();
            foreach (SlotRef p in path)
            {
                if (!byColumn.TryGetValue(p.Column, out var list))
                {
                    list = new List<int>();
                    byColumn[p.Column] = list;
                }
                list.Add(p.Slot);
            }
            foreach (var pair in byColumn)
            {
                pair.Value.Sort();
                for (int i = pair.Value.Count - 1; i >= 0; i--)
                    remaining[pair.Key].RemoveAt(pair.Value[i]);
            }
        }

        // Sắp xếp ổn định (xem ghi chú ở StaticLevelGenerator.SortByFreeDegree).
        private static void SortByRemainingDescending(List<int>[] remaining, List<int> columns)
        {
            for (int i = 1; i < columns.Count; i++)
            {
                int value = columns[i];
                int key = remaining[value].Count;
                int j = i - 1;
                while (j >= 0 && remaining[columns[j]].Count < key)
                {
                    columns[j + 1] = columns[j];
                    j--;
                }
                columns[j + 1] = value;
            }
        }

        private static void SortByColumnRemainingDescending(List<int>[] remaining, List<SlotRef> cells)
        {
            for (int i = 1; i < cells.Count; i++)
            {
                SlotRef value = cells[i];
                int key = remaining[value.Column].Count;
                int j = i - 1;
                while (j >= 0 && remaining[cells[j].Column].Count < key)
                {
                    cells[j + 1] = cells[j];
                    j--;
                }
                cells[j + 1] = value;
            }
        }

        /// <summary>Thêm dải [pathMin, pathMax] vào màu này thì dải cột của nó có còn trong hạn?</summary>
        private static bool FitsColumnSpan(int colorMin, int colorMax, int pathMin, int pathMax, int span)
        {
            if (span <= 0) return true;                       // không giới hạn
            if (colorMin < 0) return pathMax - pathMin + 1 <= span;   // màu chưa dùng
            int lo = Min(colorMin, pathMin);
            int hi = Max(colorMax, pathMax);
            return hi - lo + 1 <= span;
        }

        private static long Key(int column, int slot) => ((long)column << 32) | (uint)slot;
        private static int Min(int a, int b) => a < b ? a : b;
        private static int Max(int a, int b) => a > b ? a : b;
    }
}
