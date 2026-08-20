using System.Collections.Generic;

namespace ConnectPuzzle.Core
{
    /// <summary>
    /// Sinh màn TĨNH bằng cách dựng ngược từ lời giải.
    ///
    /// Ý tưởng: phân hoạch TOÀN BỘ ô thành các "đường" (các ô phân biệt, kề nhau
    /// 8 hướng) độ dài >= 2, rồi tô mỗi đường một màu. Vì mỗi đường tự nó đã là
    /// một chuỗi hợp lệ, người chơi luôn xoá được nó trong đúng 1 lượt
    /// => par = số đường là mốc CHẮC CHẮN đạt được, không phải phỏng đoán.
    /// Đây là lý do không màn nào bị bất khả thi.
    /// </summary>
    public static class StaticLevelGenerator
    {
        /// <summary>
        /// Thử phân hoạch bàn thành các đường độ dài >= 2. Trả null nếu còn sót
        /// ô lẻ không nối được — gọi lại với seed khác.
        ///
        /// Heuristic then chốt: luôn xuất phát (và ưu tiên đi tới) ô CÒN ÍT LÁNG
        /// GIỀNG TRỐNG NHẤT. Ô ở góc/khe hẹp bị ăn trước nên gần như không sót.
        /// </summary>
        public static List<List<int>> TryPartition(BoardGeometry geo, int minLen, int maxLen, DeterministicRng rng)
        {
            return TryPartition(geo, minLen, maxLen, minLen, int.MaxValue, rng);
        }

        /// <summary>
        /// minChain / maxChain là luật chơi: mọi đường sinh ra phải nằm trong khoảng đó,
        /// nếu không lời giải tham chiếu có nước không đánh được.
        /// </summary>
        public static List<List<int>> TryPartition(BoardGeometry geo, int minLen, int maxLen,
                                                   int minChain, int maxChain, DeterministicRng rng)
        {
            var free = new OrderedIntSet(geo.Cells);
            var paths = new List<List<int>>();

            while (free.Count > 0)
            {
                // ô xuất phát bị bó buộc nhất
                int start = -1, best = int.MaxValue;
                var items = free.Items;
                for (int k = 0; k < items.Count; k++)
                {
                    int i = items[k];
                    int d = FreeDegree(geo, free, i);
                    if (d < best) { best = d; start = i; }
                    else if (d == best && rng.NextDouble() < 0.25) start = i;
                }
                free.Remove(start);

                // mọc đường
                var path = new List<int> { start };
                int target = rng.NextRange(minLen, maxLen);
                var candidates = new List<int>(8);

                while (path.Count < target)
                {
                    int last = path[path.Count - 1];
                    candidates.Clear();
                    foreach (int j in geo.Neighbors[last])
                        if (free.Contains(j)) candidates.Add(j);
                    if (candidates.Count == 0) break;

                    SortByFreeDegree(geo, free, candidates);
                    int pick = rng.NextDouble() < 0.7
                        ? candidates[0]
                        : candidates[rng.NextInt(candidates.Count)];

                    free.Remove(pick);
                    path.Add(pick);
                }

                if (path.Count < minChain)
                {
                    // Đường quá ngắn để đánh được. Chỉ nối vào ĐẦU hoặc CUỐI một đường
                    // có sẵn — nối vào giữa thì tập ô mới có thể không còn xoá được
                    // trong 1 lượt, tức là phá mất chính điều đang cố bảo đảm.
                    // Và đường ghép xong không được vượt trần chuỗi.
                    bool attached = false;
                    for (int p = 0; p < paths.Count && !attached; p++)
                    {
                        var other = paths[p];
                        if (other.Count + path.Count > maxChain) continue;

                        if (IsNeighbor(geo, path[0], other[other.Count - 1]))
                        {
                            for (int k = 0; k < path.Count; k++) other.Add(path[k]);
                            attached = true;
                        }
                        else if (IsNeighbor(geo, path[path.Count - 1], other[0]))
                        {
                            for (int k = path.Count - 1; k >= 0; k--) other.Insert(0, path[k]);
                            attached = true;
                        }
                    }
                    if (!attached) return null;
                }
                else
                {
                    paths.Add(path);
                }
            }

            return paths;
        }

        /// <summary>
        /// Tô màu các đường. Mặc định cấm trùng màu với đường kề, nên mỗi nhóm màu
        /// là đúng một đường và bàn đọc rất rõ. Với xác suất `fuse` thì BỎ lệnh cấm
        /// đó => hai đường kề dính thành một cục lớn cùng màu, người chơi phải tự
        /// tìm cách chẻ. par không đổi vì vẫn xoá được từng đường riêng lẻ.
        /// </summary>
        public static int[] AssignColors(BoardGeometry geo, List<List<int>> paths, int colorCount, double fuse, DeterministicRng rng)
        {
            var owner = new int[geo.CellCount];
            for (int i = 0; i < owner.Length; i++) owner[i] = -1;
            for (int p = 0; p < paths.Count; p++)
                foreach (int i in paths[p]) owner[i] = p;

            // đường nào kề đường nào — giữ thứ tự để việc cấm màu là tất định
            var adjacent = new OrderedIntSet[paths.Count];
            for (int p = 0; p < paths.Count; p++) adjacent[p] = new OrderedIntSet();
            for (int p = 0; p < paths.Count; p++)
                foreach (int i in paths[p])
                    foreach (int j in geo.Neighbors[i])
                    {
                        int o = owner[j];
                        if (o >= 0 && o != p) adjacent[p].Add(o);
                    }

            var colorOfPath = new int[paths.Count];
            for (int p = 0; p < paths.Count; p++) colorOfPath[p] = -1;

            var banned = new HashSet<int>();
            var pool = new List<int>();

            for (int p = 0; p < paths.Count; p++)
            {
                banned.Clear();
                foreach (int o in adjacent[p].Items)
                    if (colorOfPath[o] >= 0 && rng.NextDouble() >= fuse) banned.Add(colorOfPath[o]);

                pool.Clear();
                for (int c = 0; c < colorCount; c++) if (!banned.Contains(c)) pool.Add(c);
                if (pool.Count == 0) for (int c = 0; c < colorCount; c++) pool.Add(c);

                colorOfPath[p] = pool[rng.NextInt(pool.Count)];
            }

            return colorOfPath;
        }

        private static int FreeDegree(BoardGeometry geo, OrderedIntSet free, int cell)
        {
            int n = 0;
            foreach (int j in geo.Neighbors[cell]) if (free.Contains(j)) n++;
            return n;
        }

        /// <summary>
        /// Sắp xếp ỔN ĐỊNH theo số láng giềng trống.
        /// List.Sort của .NET là introsort — KHÔNG ổn định, các phần tử bằng điểm
        /// có thể bị đảo, và ở đây thứ tự đó quyết định bàn sinh ra. JS
        /// Array.prototype.sort ổn định, nên bản port phải ổn định theo.
        /// </summary>
        private static void SortByFreeDegree(BoardGeometry geo, OrderedIntSet free, List<int> cells)
        {
            for (int i = 1; i < cells.Count; i++)
            {
                int value = cells[i];
                int key = FreeDegree(geo, free, value);
                int j = i - 1;
                while (j >= 0 && FreeDegree(geo, free, cells[j]) > key)
                {
                    cells[j + 1] = cells[j];
                    j--;
                }
                cells[j + 1] = value;
            }
        }

        private static bool IsNeighbor(BoardGeometry geo, int a, int b)
        {
            foreach (int j in geo.Neighbors[a]) if (j == b) return true;
            return false;
        }
    }
}
