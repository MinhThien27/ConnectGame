using System.Collections.Generic;

namespace ConnectPuzzle.Core
{
    /// <summary>
    /// Một chặng leo tháp: 5 màn liên tiếp, MỘT ngân sách lượt dùng chung.
    ///
    /// Vì sao chế độ này đáng có: trong 100 màn hiện tại, về đích còn dư lượt KHÔNG để làm
    /// gì cả. Sao có đo hiệu quả, nhưng sao không mang sang được màn sau, nên nước đi tối
    /// ưu và nước đi vừa đủ có giá như nhau. Ở đây lượt dư là VỐN: tiết kiệm ở màn 1 là
    /// thứ cứu bạn ở màn 5. Không thêm một cơ chế nào lên bàn mà đổi được cách nghĩ về
    /// từng nước — đó là lý do nó rẻ mà không nhạt.
    ///
    /// Lấy 5 màn CUỐI của thế giới, không phải 5 màn đầu: đó là những màn dùng cơ chế của
    /// thế giới ở mức mạnh nhất, nên chặng leo tháp thành bài kiểm tra tay nghề đúng cái
    /// thế giới đó dạy, chứ không phải một lượt đi lại phần nhập môn.
    ///
    /// KHÔNG có lưu tiếp tục và KHÔNG cho chơi lại một màn giữa chặng. Cho chơi lại là cho
    /// thử vô hạn ở cùng một ngân sách, và cả chặng mất hết ý nghĩa; còn lưu tiếp tục thì
    /// mở ra chuyện đóng app để tránh một chặng đang hỏng. Một chặng là một lượt, hỏng thì
    /// bắt đầu lại từ đầu.
    ///
    /// Không phụ thuộc UnityEngine.
    /// </summary>
    public sealed class GauntletRun
    {
        /// <summary>Số màn tối đa một chặng.</summary>
        public const int MaxLength = 5;

        /// <summary>Số màn tối thiểu — dưới mức này thì không còn là một "chặng".</summary>
        public const int MinLength = 3;

        /// <summary>
        /// Trần tổng par của một chặng.
        ///
        /// Có vì độ dài màn lệch nhau rất xa: 5 màn cuối thế giới 1 tổng par 43, còn 5 màn
        /// cuối thế giới 3 (gravity, bàn 8x9 cộng hàng chờ) tổng par 150 — hơn ba lần. Mà
        /// chặng KHÔNG cho lưu tiếp tục, nên 150 nước là một phiên rất dài mà hỏng ở nước
        /// 149 là mất tất. Chạm trần thì chặng lấy ít màn hơn, không phải lấy màn dễ hơn.
        /// </summary>
        public const int ParCap = 90;

        /// <summary>
        /// Phần lượt dư được giữ lại, so với khi chơi RỜI từng màn.
        ///
        /// Đây là chỗ đã sửa một lỗi cân bằng đo được. Bản đầu cộng một số CỐ ĐỊNH (+6) cho
        /// mọi chặng, và nó bất công theo tỉ lệ: +6 trên tổng par 43 là biên 14%, còn +6
        /// trên 150 là 4% — cùng một chế độ mà độ khít chênh gần bốn lần, chỉ vì thế giới
        /// đó có bàn to hơn.
        ///
        /// Lấy theo PHẦN của lượt dư bình thường thì độ khít bằng nhau ở mọi thế giới, và
        /// ngân sách chặng luôn nằm hẳn giữa "đúng par" và "chơi rời" — không cần kẹp tay.
        /// </summary>
        public const double SlackKept = 0.45;

        /// <summary>Lượt dư tối thiểu, để chặng ngắn không thành khít đến vô lý.</summary>
        public const int MinBonus = 3;

        public int World { get; }

        /// <summary>Chỉ số màn trong LevelCatalog, theo thứ tự chơi.</summary>
        public int[] LevelIndices { get; }

        /// <summary>Màn đã dựng sẵn, cùng thứ tự với LevelIndices.</summary>
        public LevelData[] Levels { get; }

        /// <summary>Đang ở màn thứ mấy của chặng, đếm từ 0.</summary>
        public int Position { get; private set; }

        /// <summary>Lượt còn lại của cả chặng.</summary>
        public int Budget { get; private set; }

        public int StartBudget { get; }
        public int Score { get; private set; }

        /// <summary>Chặng đã hỏng: hết lượt mà chưa xong màn đang chơi.</summary>
        public bool Failed { get; private set; }

        public bool Cleared => this.Position >= this.LevelIndices.Length;
        public bool Over => this.Cleared || this.Failed;

        /// <summary>Số màn đã qua.</summary>
        public int Done => this.Position;

        public LevelData CurrentLevel =>
            this.Position < this.Levels.Length ? this.Levels[this.Position] : null;

        public int CurrentLevelIndex =>
            this.Position < this.LevelIndices.Length ? this.LevelIndices[this.Position] : -1;

        private GauntletRun(int world, int[] indices, LevelData[] levels, int budget)
        {
            this.World = world;
            this.LevelIndices = indices;
            this.Levels = levels;
            this.Budget = budget;
            this.StartBudget = budget;
        }

        /// <summary>Chỉ số mọi màn của một thế giới, theo thứ tự trong bảng màn.</summary>
        public static int[] LevelsOf(int world)
        {
            var all = new List<int>();
            for (int i = 0; i < LevelCatalog.Levels.Length; i++)
                if (LevelCatalog.Levels[i].World == world) all.Add(i);
            return all.ToArray();
        }

        /// <summary>
        /// Dựng một chặng. Trả null nếu thế giới không có màn nào.
        ///
        /// Dựng cả 5 màn NGAY tại đây, không dựng dần từng màn, vì ngân sách là tổng par
        /// của cả 5 — chưa dựng thì chưa biết par, mà chưa biết ngân sách thì không mở được
        /// màn đầu. Dựng sẵn rồi giữ lại luôn để không phải dựng lần thứ hai lúc chơi tới.
        /// </summary>
        public static GauntletRun Start(int world)
        {
            int[] all = LevelsOf(world);
            if (all.Length < MinLength) return null;

            // Đi LÙI từ màn cuối: lấy thêm màn khi nào tổng par còn dưới trần. Dừng sớm
            // thì chặng ngắn hơn nhưng vẫn là những màn khó nhất của thế giới.
            var picked = new List<int>();
            var built = new List<LevelData>();
            int totalPar = 0, separate = 0;

            for (int k = all.Length - 1; k >= 0 && picked.Count < MaxLength; k--)
            {
                LevelData level = LevelBuilder.Build(LevelCatalog.Levels[all[k]]);
                if (picked.Count >= MinLength && totalPar + level.Par > ParCap) break;

                picked.Insert(0, all[k]);
                built.Insert(0, level);
                totalPar += level.Par;
                separate += level.MaxMoves;
            }

            if (picked.Count < MinLength) return null;

            // Thế giới mà chơi rời cũng KHÔNG có lượt dư (thế giới Chính xác: MaxMoves ==
            // Par) thì không mở chặng được. Ở đó ngân sách chặng bằng hoặc hơn tổng lượt
            // chơi rời, tức là leo tháp lại DỄ hơn chơi từng màn — ngược hẳn ý của chế độ.
            // Chặn bằng dữ liệu chứ không chặn tên thế giới: thêm một thế giới khít nữa về
            // sau thì nó tự nằm ngoài, không phải ai đó nhớ ra mà đi sửa.
            int normalSlack = separate - totalPar;
            if (normalSlack <= 0) return null;

            int bonus = (int)System.Math.Round(normalSlack * SlackKept);
            if (bonus < MinBonus) bonus = MinBonus;
            if (bonus >= normalSlack) bonus = normalSlack - 1;   // luôn khít hơn chơi rời

            return new GauntletRun(world, picked.ToArray(), built.ToArray(), totalPar + bonus);
        }

        /// <summary>Chặng có mở được ở thế giới này không. Rẻ hơn Start vì không dựng màn.</summary>
        public static bool AvailableFor(int world)
        {
            int[] all = LevelsOf(world);
            if (all.Length < MinLength) return false;

            // Đọc từ bảng cân bằng, không dựng màn: Exact nghĩa là MaxMoves == Par, tức
            // lượt dư bằng 0. Đúng cái điều kiện mà Start kiểm bằng số thật.
            for (int k = all.Length - 1, taken = 0; k >= 0 && taken < MaxLength; k--, taken++)
                if (!LevelCatalog.Levels[all[k]].Exact) return true;
            return false;
        }

        /// <summary>
        /// Tổng lượt mà 5 màn này cho nếu chơi RỜI từng màn. Dùng để nói cho người chơi
        /// biết chặng khít hơn bao nhiêu — con số đó là toàn bộ lời giải thích vì sao khó.
        /// </summary>
        public int SeparateBudget()
        {
            int n = 0;
            foreach (LevelData level in this.Levels) n += level.MaxMoves;
            return n;
        }

        /// <summary>
        /// Ngân sách cho màn đang chơi = TOÀN BỘ lượt còn lại của chặng.
        ///
        /// Cố ý không chia phần cho từng màn: chia phần là dựng lại đúng cái hàng rào mà
        /// chế độ này bỏ đi. Người chơi được phép dốc cả ngân sách vào một màn, và tự chịu.
        /// </summary>
        public int BudgetForCurrentLevel() => this.Budget;

        /// <summary>Qua màn: trừ lượt đã dùng, cộng điểm, đi tiếp.</summary>
        public void Complete(int movesUsed, int score)
        {
            if (this.Over) return;
            this.Budget -= movesUsed;
            if (this.Budget < 0) this.Budget = 0;
            this.Score += score;
            this.Position++;
        }

        /// <summary>Hỏng chặng. Điểm của màn đang dở VẪN cộng — nó đã ăn được thật.</summary>
        public void Fail(int score)
        {
            if (this.Over) return;
            this.Score += score;
            this.Failed = true;
        }
    }
}
