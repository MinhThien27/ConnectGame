using System;
using System.Collections.Generic;

namespace ConnectPuzzle.Core
{
    /// <summary>Một màn đã dựng xong: bàn khởi đầu + lời giải tham chiếu + các mốc.</summary>
    public sealed class LevelData
    {
        public LevelConfig Config;
        public BoardGeometry Geometry;
        public bool Gravity;

        /// <summary>Màn tĩnh: màu ban đầu của từng ô lưới. Clone ra để chơi.</summary>
        public int[] Template;

        /// <summary>Màn tĩnh: lời giải tham chiếu (dùng cho nút gợi ý).</summary>
        public List<List<int>> Paths;

        /// <summary>Màn gravity: Columns[x][slot] = màu, slot 0 ở đáy.</summary>
        public int[][] Columns;

        /// <summary>Màn gravity: lời giải tham chiếu (dùng cho nút gợi ý).</summary>
        public List<List<SlotRef>> Solution;

        public int TotalCells;
        public int VisibleCells;

        /// <summary>Số lượt của lời giải tham chiếu — mốc CHẮC CHẮN đạt được.</summary>
        public int Par;
        public int MaxMoves;

        /// <summary>
        /// Số chuỗi KỊCH TRẦN cần đạt để lấy huy hiệu kỹ thuật. 0 = màn này không có
        /// huy hiệu (không có trần chuỗi, hoặc quá ít đường dài để đo được gì).
        /// </summary>
        public int MedalChains;
        public int TwoStarMoves;
        public int Undos;
        public int Shuffles;

        /// <summary>Số ô tối thiểu / tối đa của một chuỗi hợp lệ.</summary>
        public int MinChain = 2;
        public int MaxChain = int.MaxValue;

        /// <summary>Màn tĩnh: dấu ô đặc biệt theo chỉ số lưới. Null = màn không có.</summary>
        public CellMark[] Marks;

        /// <summary>Màn gravity: dấu theo [cột][bậc], đi THEO Ô nên rơi cùng ô.</summary>
        public CellMark[][] MarkColumns;

        /// <summary>Màn tĩnh: các ô là đá (đã bị loại khỏi hình học lúc phân hoạch).</summary>
        public int[] StoneCells;

        public int GoalTotal;

        /// <summary>Thắng khi dọn hết ô đích thay vì dọn sạch bàn.</summary>
        public bool GoalMode => this.GoalTotal > 0;

        /// <summary>Chế độ vô tận: ô rớt xuống mãi, không lượt, không mục tiêu.</summary>
        public bool Endless;
    }

    public static class LevelBuilder
    {
        public static LevelData Build(LevelConfig cfg)
        {
            Validate(cfg);
            return cfg.Gravity ? BuildGravity(cfg) : BuildStatic(cfg);
        }

        /// <summary>
        /// Luật chơi và tham số sinh màn phải khớp nhau, nếu không lời giải tham chiếu
        /// chứa nước không đánh được và màn thành bất khả thi. Sai cấu hình thì hỏng
        /// ngay ở đây, chứ đừng để người chơi phát hiện.
        /// </summary>
        private static void Validate(LevelConfig cfg)
        {
            if (cfg.MinChain > cfg.MinPathLength)
                throw new Exception("Màn '" + cfg.Name + "': MinChain (" + cfg.MinChain +
                    ") lớn hơn MinPathLength (" + cfg.MinPathLength + ") — generator sẽ sinh ra nhóm ngắn hơn luật cho phép.");

            if (cfg.MaxChain > 0 && cfg.MaxChain < cfg.MaxPathLength)
                throw new Exception("Màn '" + cfg.Name + "': MaxChain (" + cfg.MaxChain +
                    ") nhỏ hơn MaxPathLength (" + cfg.MaxPathLength + ") — generator sẽ sinh ra nhóm dài hơn luật cho phép.");

            if (cfg.MinPathLength > cfg.MaxPathLength)
                throw new Exception("Màn '" + cfg.Name + "': MinPathLength > MaxPathLength.");
        }

        public static LevelData Build(int levelIndex)
        {
            return Build(LevelCatalog.Levels[levelIndex]);
        }

        /// <summary>
        /// Chốt par và các mốc lượt.
        ///
        /// par KHÔNG lấy thẳng số nước của generator. Generator chia bàn thành nhóm
        /// 3-5 ô, nhưng bàn thật thường cho phép quét con rắn dài hơn — đo được par 7
        /// mà lối chơi tham lam chỉ cần 4 lượt. Lấy nguyên số của generator thì ngân
        /// sách lượt rộng gấp đôi mức cần và mốc 3 sao thành cho không.
        ///
        /// Nên par = lời giải TỐT NHẤT biết được. Vẫn đảm bảo giải được: cả hai lời
        /// giải đều là nhân chứng thật, ta chỉ chọn cái ngắn hơn.
        /// </summary>
        private static void ApplyPar(LevelData level, int referenceMoves, LevelConfig cfg)
        {
            // PuzzleSession cần MaxMoves hợp lệ để chạy; đặt tạm rồi chốt lại bên dưới
            level.Par = referenceMoves;
            level.MaxMoves = level.TotalCells;
            level.TwoStarMoves = level.TotalCells;

            int best = referenceMoves;

            // Bot tham lam đi tìm cách DỌN SẠCH, nên ở màn mục tiêu nó trả lời sai câu
            // hỏi: ở đó thắng chỉ cần chạm hết đích, và số nước dọn sạch thường lớn hơn
            // nhiều. Dùng nó ở đây sẽ đẩy par lên cao và mốc 3 sao thành cho không.
            if (!level.GoalMode)
            {
                GreedySolver.Result greedy = GreedySolver.Solve(level, level.TotalCells);
                if (greedy.Cleared && greedy.Moves > 0 && greedy.Moves < best) best = greedy.Moves;
            }

            level.Par = best;
            level.MaxMoves = best + cfg.Slack;
            level.TwoStarMoves = best + Math.Max(1, (int)Math.Ceiling(cfg.Slack / 2.0));

            ApplyMedal(level, cfg);
        }

        /// <summary>
        /// Huy hiệu KỸ THUẬT: số chuỗi dài ĐÚNG BẰNG TRẦN cần đạt trong một ván.
        ///
        /// Sao đo sự TIẾT KIỆM (ít lượt), huy hiệu đo sự KHÉO (chẻ bàn đúng chỗ để
        /// chuỗi nào cũng kịch trần). Hai thứ không trùng nhau: dọn sạch bằng toàn
        /// chuỗi ngắn vẫn có thể vừa số lượt, mà không có lấy một chuỗi đầy.
        ///
        /// Ngưỡng lấy 60%% số đường kịch trần CÓ THẬT trong lời giải tham chiếu, nên
        /// nó luôn ĐẠT ĐƯỢC — đặt một con số cố định kiểu "4 chuỗi đầy" thì màn nào
        /// bàn nhỏ sẽ có một huy hiệu vĩnh viễn không ai lấy nổi.
        ///
        /// Màn không có trần chuỗi (hai màn đầu) và màn quá ít đường dài thì KHÔNG có
        /// huy hiệu: yêu cầu 1 chuỗi đầy là cho không, chẳng đo được gì.
        /// </summary>
        private static bool PathHasGoal(LevelData level, System.Collections.Generic.List<int> path)
        {
            if (level.Marks == null) return false;
            foreach (int c in path)
                if (c >= 0 && c < level.Marks.Length && level.Marks[c] != null && level.Marks[c].Goal)
                    return true;
            return false;
        }

        private static void ApplyMedal(LevelData level, LevelConfig cfg)
        {
            level.MedalChains = 0;
            int cap = level.MaxChain;
            if (cap == int.MaxValue || cap <= 0) return;

            int full = 0;
            if (level.Gravity)
            {
                // Gravity + mục tiêu: ở đó ván kết thúc ngay khi chạm hết đích, nên
                // không biết trước nước nào sẽ được đi. Không phát huy hiệu còn hơn
                // phát một cái đòi nhiều hơn số nước người chơi thực sự đi.
                if (level.GoalMode || level.Solution == null) return;
                foreach (var move in level.Solution) if (move.Count == cap) full++;
            }
            else if (level.Paths != null)
            {
                // Màn mục tiêu chỉ CHƠI những đường có chứa đích — đếm cả đường không
                // bao giờ được đi thì yêu cầu vượt quá số nước có thật, và huy hiệu
                // thành thứ không ai lấy nổi. Đây đúng là lỗi bài kiểm vừa bắt được.
                foreach (var p in level.Paths)
                {
                    if (p.Count != cap) continue;
                    if (level.GoalMode && !PathHasGoal(level, p)) continue;
                    full++;
                }
            }

            if (full < 3) return;                       // ít quá thì không đo được kỹ thuật
            level.MedalChains = (int)Math.Ceiling(full * 0.6);
        }

        private static LevelData BuildGravity(LevelConfig cfg)
        {
            for (int attempt = 0; attempt < 300; attempt++)
            {
                var rng = new DeterministicRng(cfg.Seed + attempt * 7919);
                GravityPlan plan = GravityLevelGenerator.Simulate(
                    cfg, rng, null, cfg.MinPathLength, cfg.MaxPathLength,
                    cfg.MinChain, cfg.ResolvedMaxChain);
                if (plan == null) continue;

                Decoration decoration = LevelDecorator.DecorateGravity(plan, cfg, rng);
                if (decoration == null) continue;

                var level = new LevelData
                {
                    Config = cfg,
                    Geometry = BoardGeometry.Rectangle(cfg.Columns, cfg.Rows),
                    Gravity = true,
                    Columns = plan.Columns,
                    Solution = plan.Solution,
                    MarkColumns = decoration.MarkColumns,
                    GoalTotal = decoration.GoalTotal,
                    TotalCells = plan.TotalCells,
                    VisibleCells = cfg.Columns * cfg.Rows,
                    Undos = cfg.Undos,
                    Shuffles = cfg.ResolvedShuffles,
                    MinChain = cfg.MinChain,
                    MaxChain = cfg.ResolvedMaxChain
                };
                ApplyPar(level, decoration.Par, cfg);
                return level;
            }
            throw new Exception("Không sinh được màn gravity: " + cfg.Name);
        }

        private static LevelData BuildStatic(LevelConfig cfg)
        {
            BoardGeometry full = BoardGeometry.FromConfig(cfg);

            for (int attempt = 0; attempt < 400; attempt++)
            {
                var rng = new DeterministicRng(cfg.Seed + attempt * 7919);

                // 200 lần đầu dùng độ dài khai báo; sau đó nới lỏng để chắc chắn xong
                int minLen = attempt < 200 ? cfg.MinPathLength : 2;
                int maxLen = attempt < 200 ? cfg.MaxPathLength : 3;

                // Đá bị lấy khỏi hình học TRƯỚC khi phân hoạch: nó không phải ô nối
                // được, để lẫn vào thì đường đi sẽ xuyên qua đá.
                List<int> stones = LevelDecorator.PickStones(full, cfg, rng);
                BoardGeometry geo = full;
                if (stones.Count > 0)
                {
                    var active = (bool[])full.Active.Clone();
                    foreach (int i in stones) active[i] = false;
                    geo = BoardGeometry.FromMask(full.Columns, full.Rows, active);
                    if (geo.Cells.Length == 0) continue;
                }

                List<List<int>> paths = StaticLevelGenerator.TryPartition(
                    geo, minLen, maxLen, cfg.MinChain, cfg.ResolvedMaxChain, rng);
                if (paths == null) continue;

                int[] colorOfPath = StaticLevelGenerator.AssignColors(geo, paths, cfg.Colors, cfg.Fuse, rng);

                Decoration decoration = LevelDecorator.DecorateStatic(full, paths, stones, cfg, rng);
                if (decoration == null) continue;

                var template = new int[full.CellCount];
                for (int i = 0; i < template.Length; i++) template[i] = PuzzleSession.Wall;
                for (int p = 0; p < paths.Count; p++)
                    foreach (int i in paths[p]) template[i] = colorOfPath[p];
                foreach (int i in stones) template[i] = PuzzleSession.Stone;

                // Gỡ ô vỡ-theo-liên-kết khỏi lời giải tham chiếu — PHẢI làm sau khi tô
                // màu xong, gỡ trước thì ô không được tô và biến thành tường.
                foreach (var victim in decoration.LinkVictims)
                    paths[victim.Key].Remove(victim.Value);

                var level = new LevelData
                {
                    Config = cfg,
                    Geometry = full,
                    Gravity = false,
                    Template = template,
                    Paths = paths,
                    Marks = decoration.Marks,
                    StoneCells = stones.ToArray(),
                    GoalTotal = decoration.GoalTotal,
                    TotalCells = geo.Cells.Length + stones.Count,
                    VisibleCells = geo.Cells.Length + stones.Count,
                    Undos = cfg.Undos,
                    Shuffles = cfg.ResolvedShuffles,
                    MinChain = cfg.MinChain,
                    MaxChain = cfg.ResolvedMaxChain
                };
                ApplyPar(level, decoration.Par, cfg);
                return level;
            }
            throw new Exception("Không sinh được màn: " + cfg.Name);
        }
    }
}
