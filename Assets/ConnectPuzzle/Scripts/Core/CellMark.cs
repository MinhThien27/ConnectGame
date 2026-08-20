using System;
using System.Collections.Generic;

namespace ConnectPuzzle.Core
{
    public enum CellKind
    {
        Plain = 0,

        /// <summary>Ghép được với mọi màu. Mỗi chuỗi chỉ được chứa một ô.</summary>
        Wild = 1,

        /// <summary>Không nối được; chỉ vỡ khi có chuỗi bị ăn KỀ nó.</summary>
        Stone = 2,

        /// <summary>Đếm ngược theo lượt; về 0 mà chưa được ăn là thua.</summary>
        Bomb = 3,

        /// <summary>
        /// Ô CÓ MÀU nhưng đóng băng — không chọn được tới khi tan hết. Mỗi lượt có
        /// chuỗi bị ăn KỀ nó thì tan 1 lớp; tan hết thì thành ô thường, ăn được ngay.
        /// Khác Đá đúng một điểm: Đá biến mất khi hết máu, Băng thì HIỆN NGUYÊN HÌNH
        /// thành ô ăn được — Đá là gỡ vật cản, Băng là mở khoá đường đi.
        /// </summary>
        Ice = 4,

        /// <summary>
        /// Một nửa của CẶP LIÊN KẾT: ăn ô này thì ô bạn của nó ở NƠI KHÁC tự vỡ theo,
        /// dù không hề kề nhau.
        ///
        /// Đây là cơ chế đầu tiên phá giả định "muốn ăn thì phải kề" — mọi thứ trước đó
        /// đều là biến thể của "chặn ô lại". Nó biến bài toán từ tìm chuỗi tại chỗ thành
        /// tính xem ăn ở đây thì mất gì ở kia.
        /// </summary>
        Link = 5
    }

    /// <summary>
    /// Dấu gắn lên một ô. Là CLASS chứ không phải struct: ô bình thường mang null nên
    /// bàn không có ô đặc biệt thì không tốn gì, và máu đá / số ngòi sửa được tại chỗ.
    /// </summary>
    public sealed class CellMark
    {
        public CellKind Kind;

        /// <summary>Đá: số lần còn phải bị va mới vỡ.</summary>
        public int Hp;

        /// <summary>Ngòi nổ: nổ khi số nước đã đi chạm tới giá trị này.</summary>
        public int Fuse;

        /// <summary>Ô đích — chỉ cần dọn hết các ô này là thắng.</summary>
        public bool Goal;

        /// <summary>
        /// Cặp liên kết: chỉ số ô BẠN, và số hiệu cặp để tầng hiển thị tô cùng một màu
        /// cho hai đầu — không có số hiệu thì bàn nhiều cặp nhìn không biết ô nào ăn
        /// theo ô nào. -1 = không thuộc cặp nào.
        /// </summary>
        public int LinkPartner = -1;
        public int LinkId = -1;

        public CellMark Clone()
        {
            return new CellMark
            {
                Kind = this.Kind, Hp = this.Hp, Fuse = this.Fuse, Goal = this.Goal,
                LinkPartner = this.LinkPartner, LinkId = this.LinkId
            };
        }

        public static CellMark[] CloneAll(CellMark[] source)
        {
            if (source == null) return null;
            var copy = new CellMark[source.Length];
            for (int i = 0; i < source.Length; i++) copy[i] = source[i]?.Clone();
            return copy;
        }
    }

    /// <summary>Kết quả gắn ô đặc biệt lên một màn đã có lời giải tham chiếu.</summary>
    public sealed class Decoration
    {
        public CellMark[] Marks;             // màn tĩnh: theo chỉ số lưới
        public CellMark[][] MarkColumns;     // màn gravity: theo [cột][bậc]
        public int Par;
        public int GoalTotal;

        /// <summary>
        /// (chỉ số đường, ô) — các ô bị VỠ THEO liên kết, phải gỡ khỏi lời giải tham
        /// chiếu vì tới lượt đường đó chúng đã không còn trên bàn.
        ///
        /// Trả ra ngoài thay vì tự gỡ, vì phải gỡ SAU khi tô màu: gỡ trước thì ô không
        /// được tô, template để nguyên giá trị Wall và ô biến mất khỏi bàn.
        /// </summary>
        public List<KeyValuePair<int, int>> LinkVictims = new List<KeyValuePair<int, int>>();
    }

    /// <summary>
    /// Gắn ô đặc biệt lên màn.
    ///
    /// Nguyên tắc giống hệt phần sinh màn: KHÔNG rắc ô đặc biệt rồi cầu cho màn vẫn
    /// giải được. Mọi thứ gắn SAU khi đã có lời giải tham chiếu, và gắn theo cách mà
    /// chính lời giải đó vẫn chạy được — nên "giải được" vẫn là tính chất đúng theo
    /// cấu trúc chứ không phải thứ phải đi kiểm bằng may rủi.
    /// </summary>
    public static class LevelDecorator
    {
        /// <summary>Lấy n phần tử khác nhau, theo rng tất định.</summary>
        private static List<T> PickSome<T>(IList<T> source, int n, DeterministicRng rng)
        {
            var pool = new List<T>(source);
            var picked = new List<T>();
            while (picked.Count < n && pool.Count > 0)
            {
                int at = rng.NextInt(pool.Count);
                picked.Add(pool[at]);
                pool.RemoveAt(at);
            }
            return picked;
        }

        /// <summary>
        /// Chọn ô đá TRƯỚC khi phân hoạch — đá không phải ô nối được nên phải bị lấy
        /// khỏi hình học, để lẫn vào thì đường đi sẽ xuyên qua đá.
        ///
        /// Tránh ô rìa: đá ở rìa ít láng giềng nên hay không đủ đường kề để phá, và
        /// nhìn cũng không ra "chướng ngại giữa đường".
        /// </summary>
        public static List<int> PickStones(BoardGeometry geo, LevelConfig cfg, DeterministicRng rng)
        {
            var none = new List<int>();
            if (cfg.Stones <= 0) return none;

            var inner = new List<int>();
            foreach (int i in geo.Cells)
                if (geo.Neighbors[i].Length >= 5) inner.Add(i);

            return PickSome(inner.Count >= cfg.Stones ? inner : new List<int>(geo.Cells), cfg.Stones, rng);
        }

        /// <summary>
        /// Gắn ô đặc biệt cho màn TĨNH. Trả null nếu bố cục vừa sinh không đỡ nổi yêu
        /// cầu — người gọi sinh lại với seed khác.
        /// </summary>
        public static Decoration DecorateStatic(BoardGeometry geo, List<List<int>> paths,
                                                List<int> stones, LevelConfig cfg, DeterministicRng rng)
        {
            var marks = new CellMark[geo.CellCount];
            var linkVictims = new List<KeyValuePair<int, int>>();
            var pathOf = new int[geo.CellCount];
            for (int i = 0; i < pathOf.Length; i++) pathOf[i] = -1;
            for (int p = 0; p < paths.Count; p++)
                foreach (int i in paths[p]) pathOf[i] = p;

            // --- ĐÁ: mỗi hòn phải kề đủ số đường PHÂN BIỆT bằng máu của nó.
            //     Đó chính là điều kiện làm cho "ăn hết các đường" kéo theo "vỡ hết đá".
            int baseHp = cfg.StoneHp > 0 ? cfg.StoneHp : 1;
            foreach (int s in stones)
            {
                var near = new HashSet<int>();
                foreach (int j in geo.Neighbors[s])
                    if (pathOf[j] >= 0) near.Add(pathOf[j]);
                if (near.Count == 0) return null;                 // đá bị vây kín
                marks[s] = new CellMark { Kind = CellKind.Stone, Hp = Math.Min(baseHp, near.Count) };
            }

            var pathIndexes = new List<int>();
            for (int p = 0; p < paths.Count; p++) pathIndexes.Add(p);

            // `Kind` chỉ chứa được MỘT giá trị, nên đa sắc/ngòi không được chọn trúng ô
            // đã gán Băng — chọn trúng thì Băng bị GHI ĐÈ mất. Với ngòi còn nguy hiểm
            // hơn: ô đóng băng không chọn được mà vẫn đếm ngược, có thể nổ trước khi
            // người chơi kịp mở khoá nó ra.
            Func<List<int>, int> pickCellAvoidingIce = path =>
            {
                var eligible = new List<int>();
                foreach (int c in path)
                {
                    CellMark existing = marks[c];
                    // Né mọi ô đã mang LOẠI riêng (băng, liên kết): ghi đè là mất loại cũ,
                    // và với liên kết còn tệ hơn — đầu kia trỏ về một ô không còn liên kết,
                    // thành một mũi tên chỉ vào hư không.
                    if (existing == null ||
                        (existing.Kind != CellKind.Ice && existing.Kind != CellKind.Link))
                        eligible.Add(c);
                }
                if (eligible.Count == 0) eligible = path;    // hiếm: cả đường đều đã có loại
                return eligible[rng.NextInt(eligible.Count)];
            };

            // --- MỤC TIÊU phải gán TRƯỚC băng.
            //     Ở màn mục tiêu, lời giải tham chiếu KHÔNG chơi đường 0,1,2,… mà chỉ
            //     chơi dãy con các đường CÓ ĐÍCH — nên băng phải biết đường nào nằm
            //     trong dãy con đó mới đặt được nguồn tan cho đúng.
            int par = paths.Count;
            var goalCells = new List<int>();
            var goalPaths = new List<int>();                    // chỉ số đường có đích, tăng dần
            if (cfg.Goals > 0)
            {
                if (paths.Count < cfg.Goals) return null;
                foreach (int p in PickSome(pathIndexes, cfg.Goals, rng))
                {
                    int cell = paths[p][rng.NextInt(paths[p].Count)];
                    CellMark m = marks[cell] ?? (marks[cell] = new CellMark());
                    m.Goal = true;
                    goalCells.Add(cell);
                    goalPaths.Add(p);
                }
                goalPaths.Sort();
                par = cfg.Goals;
            }

            // --- BĂNG: ô CÓ MÀU nhưng đóng băng tới khi tan. Nguồn tan chỉ tính các
            //     đường được ăn TRƯỚC đường chứa nó, theo đúng thứ tự mà lời giải tham
            //     chiếu thật sự chạy:
            //       · màn thường  — mọi đường, theo chỉ số tăng dần;
            //       · màn mục tiêu — CHỈ các đường có đích, cũng theo chỉ số tăng dần.
            //     Ở màn mục tiêu mà vẫn tính cả đường không có đích thì nguồn tan có thể
            //     rơi vào một đường không bao giờ được chơi, và ô băng đóng vĩnh viễn.
            int baseIceHp = cfg.IceHp > 0 ? cfg.IceHp : 1;
            if (cfg.Ices > 0)
            {
                // tập đường "được chơi", và ô băng phải nằm trên một đường trong tập đó
                var playedPaths = cfg.Goals > 0 ? goalPaths : pathIndexes;
                var playedSet = new HashSet<int>(playedPaths);

                var candidates = new List<int>();
                foreach (int p in playedPaths)
                {
                    if (p == playedPaths[0]) continue;          // đường đầu không có gì trước nó
                    foreach (int cell in paths[p]) candidates.Add(cell);
                }
                if (candidates.Count < cfg.Ices) return null;

                int placed = 0;
                foreach (int cell in PickSome(candidates, candidates.Count, rng))
                {
                    if (placed >= cfg.Ices) break;
                    if (marks[cell] != null) continue;          // đã có dấu khác, bỏ qua

                    var earlier = new HashSet<int>();
                    foreach (int j in geo.Neighbors[cell])
                    {
                        int pj = pathOf[j];
                        if (pj >= 0 && pj < pathOf[cell] && playedSet.Contains(pj)) earlier.Add(pj);
                    }
                    if (earlier.Count == 0) continue;           // không có nguồn tan nào được chơi trước

                    marks[cell] = new CellMark { Kind = CellKind.Ice, Hp = Math.Min(baseIceHp, earlier.Count) };
                    placed++;
                }
                if (placed < cfg.Ices) return null;             // không đủ chỗ hợp lệ, thử seed khác
            }

            // --- CẶP LIÊN KẾT: ăn một đầu thì đầu kia tự vỡ, dù ở xa.
            //
            // Ba ràng buộc dưới đây là thứ giữ cho lời giải tham chiếu vẫn chạy được.
            // Bỏ bất kỳ cái nào là màn có thể thành bất khả thi:
            //   (1) đầu B phải là ô ĐẦU hoặc CUỐI đường của nó — vỡ ô giữa thì đường bị
            //       CHẺ ĐÔI thành hai đoạn rời, mỗi đoạn có thể ngắn hơn MinChain;
            //   (2) đường của B phải dài >= MinChain + 1 — bỏ một ô vẫn còn đủ luật;
            //   (3) đường của A phải có chỉ số NHỎ HƠN đường của B — B vỡ trước khi tới
            //       lượt đường của nó, nên khi chơi tới đó nó đã không còn.
            if (cfg.Links > 0)
            {
                var usedPaths = new HashSet<int>();
                int made = 0;

                foreach (int pa in PickSome(pathIndexes, pathIndexes.Count, rng))
                {
                    if (made >= cfg.Links) break;
                    if (usedPaths.Contains(pa)) continue;

                    // tìm một đường SAU nó, đủ dài, mà hai đầu còn trống dấu
                    int pb = -1, endCell = -1;
                    foreach (int candidate in pathIndexes)
                    {
                        if (candidate <= pa || usedPaths.Contains(candidate)) continue;
                        List<int> path = paths[candidate];
                        if (path.Count < cfg.MinChain + 1) continue;      // (2)

                        int first = path[0], last = path[path.Count - 1]; // (1)
                        if (marks[first] == null) { pb = candidate; endCell = first; break; }
                        if (marks[last] == null) { pb = candidate; endCell = last; break; }
                    }
                    if (pb < 0) continue;

                    int aCell = -1;
                    foreach (int c in paths[pa]) if (marks[c] == null) { aCell = c; break; }
                    if (aCell < 0) continue;

                    marks[aCell] = new CellMark
                    { Kind = CellKind.Link, LinkPartner = endCell, LinkId = made };
                    marks[endCell] = new CellMark
                    { Kind = CellKind.Link, LinkPartner = aCell, LinkId = made };

                    // Ô này sẽ vỡ theo TRƯỚC khi tới lượt đường của nó, nên nó không còn
                    // được nằm trong lời giải tham chiếu nữa.
                    linkVictims.Add(new KeyValuePair<int, int>(pb, endCell));

                    usedPaths.Add(pa);
                    usedPaths.Add(pb);
                    made++;
                }
                if (made < cfg.Links) return null;      // không đủ chỗ hợp lệ, thử seed khác
            }

            // --- ĐA SẮC: tối đa 1 ô mỗi đường, để ăn nguyên đường vẫn hợp lệ
            if (cfg.Wilds > 0)
            {
                if (paths.Count < cfg.Wilds) return null;
                foreach (int p in PickSome(pathIndexes, cfg.Wilds, rng))
                {
                    int cell = pickCellAvoidingIce(paths[p]);
                    CellMark m = marks[cell] ?? (marks[cell] = new CellMark());
                    m.Kind = CellKind.Wild;
                }
            }

            // --- NGÒI NỔ: đặt theo THỨ TỰ ăn của lời giải tham chiếu
            if (cfg.Bombs > 0)
            {
                // Ở màn mục tiêu, ngòi phải nằm trên chính ô đích — nếu không người
                // chơi có thể thắng mà chưa từng cần đụng tới ô mang ngòi.
                List<int> hosts;
                if (cfg.Goals > 0) hosts = goalCells;
                else
                {
                    hosts = new List<int>();
                    foreach (int p in PickSome(pathIndexes, cfg.Bombs, rng))
                        hosts.Add(pickCellAvoidingIce(paths[p]));
                }
                if (hosts.Count < cfg.Bombs) return null;

                foreach (int cell in PickSome(hosts, cfg.Bombs, rng))
                {
                    CellMark m = marks[cell] ?? (marks[cell] = new CellMark());
                    m.Kind = CellKind.Bomb;
                    m.Fuse = pathOf[cell] + 1 + cfg.BombSlack;     // nước ăn được ô này + dư
                }
            }

            return new Decoration
            {
                Marks = marks, Par = par, GoalTotal = goalCells.Count, LinkVictims = linkVictims
            };
        }

        /// <summary>
        /// Gắn ô đặc biệt cho màn GRAVITY. Ở đây lời giải là một DÃY nước nên mọi thứ
        /// tính theo chỉ số nước — chính xác hơn màn tĩnh.
        ///
        /// Không dùng đá: đá rơi được thì phải chèn vào cột SAU khi đã mô phỏng, mà
        /// chèn xong thì thứ tự rơi đổi và lời giải ghi sẵn hỏng theo.
        /// </summary>
        public static Decoration DecorateGravity(GravityPlan plan, LevelConfig cfg, DeterministicRng rng)
        {
            int columns = plan.Columns.Length;
            var marks = new CellMark[columns][];
            var clearedAt = new int[columns][];
            for (int x = 0; x < columns; x++)
            {
                marks[x] = new CellMark[plan.Columns[x].Length];
                clearedAt[x] = new int[plan.Columns[x].Length];
                for (int k = 0; k < clearedAt[x].Length; k++) clearedAt[x][k] = -1;
            }

            for (int t = 0; t < plan.Solution.Count; t++)
                foreach (SlotRef s in plan.Solution[t]) clearedAt[s.Column][s.Slot] = t;

            var all = new List<SlotRef>();
            var timeOf = new List<int>();
            for (int x = 0; x < columns; x++)
                for (int k = 0; k < clearedAt[x].Length; k++)
                    if (clearedAt[x][k] >= 0)
                    {
                        all.Add(new SlotRef { Column = x, Slot = k });
                        timeOf.Add(clearedAt[x][k]);
                    }
            if (all.Count == 0) return null;

            Func<SlotRef, CellMark> markAt = s =>
                marks[s.Column][s.Slot] ?? (marks[s.Column][s.Slot] = new CellMark());

            // --- ĐA SẮC: tối đa 1 ô mỗi NƯỚC
            if (cfg.Wilds > 0)
            {
                var oneEach = new List<SlotRef>();
                var seenMove = new HashSet<int>();
                for (int i = 0; i < all.Count; i++)
                    if (seenMove.Add(timeOf[i])) oneEach.Add(all[i]);

                List<SlotRef> picked = PickSome(oneEach, cfg.Wilds, rng);
                if (picked.Count < cfg.Wilds) return null;
                foreach (SlotRef s in picked) markAt(s).Kind = CellKind.Wild;
            }

            // --- MỤC TIÊU: par = nước cuối cùng còn phải đi để chạm hết đích
            int par = plan.Solution.Count;
            var goalSlots = new List<SlotRef>();
            if (cfg.Goals > 0)
            {
                // rải đích ra nhiều mốc thời gian khác nhau cho có nhịp
                int step = Math.Max(1, all.Count / cfg.Goals);
                int last = -1;
                for (int g = 0; g < cfg.Goals; g++)
                {
                    int from = g * step;
                    int to = Math.Min(all.Count, from + step);
                    if (from >= to) return null;
                    int at = from + rng.NextInt(to - from);
                    markAt(all[at]).Goal = true;
                    goalSlots.Add(all[at]);
                    if (timeOf[at] > last) last = timeOf[at];
                }
                par = last + 1;
            }

            // --- NGÒI NỔ
            if (cfg.Bombs > 0)
            {
                List<SlotRef> hosts;
                if (cfg.Goals > 0) hosts = goalSlots;
                else
                {
                    hosts = new List<SlotRef>();
                    for (int i = 0; i < all.Count; i++) if (timeOf[i] >= 1) hosts.Add(all[i]);
                }
                if (hosts.Count < cfg.Bombs) return null;

                foreach (SlotRef s in PickSome(hosts, cfg.Bombs, rng))
                {
                    int t = clearedAt[s.Column][s.Slot];
                    CellMark m = markAt(s);
                    m.Kind = CellKind.Bomb;
                    m.Fuse = t + 1 + cfg.BombSlack;
                }
            }

            return new Decoration { MarkColumns = marks, Par = par, GoalTotal = goalSlots.Count };
        }
    }
}
