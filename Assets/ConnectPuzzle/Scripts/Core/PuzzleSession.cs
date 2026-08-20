using System;
using System.Collections.Generic;

namespace ConnectPuzzle.Core
{
    /// <summary>Một ô trong chồng cột (chế độ gravity). Slot là danh tính để khớp lời giải.</summary>
    public struct SlotCell
    {
        public int Color;
        public int Slot;

        /// <summary>
        /// Dấu ô đặc biệt, đi THEO Ô chứ không theo vị trí lưới. Ô rơi xuống thì ngòi
        /// nổ và vòng đích phải rơi cùng nó; gắn theo ô lưới thì chúng đứng yên tại chỗ.
        /// </summary>
        public CellMark Mark;

        public SlotCell(int color, int slot) { this.Color = color; this.Slot = slot; this.Mark = null; }

        public SlotCell(int color, int slot, CellMark mark)
        {
            this.Color = color; this.Slot = slot; this.Mark = mark;
        }
    }

    /// <summary>Một ô tụt xuống sau khi ăn: từ bậc nào về bậc nào trong cột.</summary>
    public struct FallStep
    {
        public int Column;
        public int FromSlotIndex;
        public int ToSlotIndex;
    }

    public enum SelectionChange { None, Added, Removed }

    /// <summary>
    /// Một ô trượt từ chỗ cũ sang chỗ mới khi xáo lại dồn ô.
    ///
    /// Dùng cột+hàng thay vì chỉ số lưới vì hàng CÓ THỂ ÂM: ở màn gravity, ô nằm trong
    /// hàng chờ có bậc lớn hơn số hàng nhìn thấy, tức là nó ở phía trên bàn. Chỉ số
    /// lưới không biểu diễn được chỗ đó.
    /// </summary>
    public struct ShuffleMove
    {
        public int FromColumn, FromRow;
        public int ToColumn, ToRow;

        /// <summary>
        /// Màu ô mang lúc bắt đầu trượt và màu nó sẽ thành khi tới đích.
        /// Xáo lại vừa dồn ô vừa tô lại màu, nên không có hai giá trị này thì ô phải
        /// đổi màu tức thời lúc xuất phát — mắt khó theo được ô nào đi đâu.
        /// </summary>
        public int FromColor, ToColor;
    }

    /// <summary>Kết quả một nước đi, đủ dữ liệu cho tầng hiển thị dựng hoạt ảnh.</summary>
    public sealed class MoveResult
    {
        public int[] ClearedCells;
        public int Color;
        public int Gained;
        public int ScoreBefore;
        public List<FallStep> Falls = new List<FallStep>();

        /// <summary>Đá bị nứt (mất máu nhưng chưa vỡ) và đá vỡ hẳn trong nước này.</summary>
        public List<int> CrackedStones = new List<int>();
        public List<int> BrokenStones = new List<int>();

        /// <summary>
        /// Băng bị NỨT (mất một lớp, vẫn còn đóng) và băng TAN HẲN trong nước này.
        ///
        /// Phải tách hai danh sách: tan hẳn thì dấu bị xoá, nên sau khi Commit trả về,
        /// tầng hiển thị không còn cách nào phân biệt "vừa nứt" với "vừa tan" nếu chỉ
        /// nhận một danh sách chung — mà hai thứ đó cần hai hoạt ảnh khác hẳn nhau.
        /// </summary>
        public List<int> CrackedIce = new List<int>();
        public List<int> ThawedIce = new List<int>();

        /// <summary>Ô bị vỡ THEO vì ô bạn liên kết của nó vừa bị ăn.</summary>
        public List<int> LinkedBroken = new List<int>();

        public int GoalsCleared;
    }

    /// <summary>
    /// Núm của chế độ vô tận. Áp lực đến từ SỐ MÀU tăng dần theo điểm: càng nhiều màu,
    /// ô càng khó gặp bạn cùng màu, và bế tắc dần trở thành chuyện có thật.
    /// </summary>
    public static class EndlessRules
    {
        public const int Columns = 7;
        public const int Rows = 8;
        public const int ComboMinChain = 4;
        public const int ComboCap = 8;
        public const int Shuffles = 3;

        /// <summary>Tỉ lệ một ô rơi xuống là ô đa sắc (phần nghìn, cho khỏi dùng số thực).</summary>
        public const int WildPerMille = 35;

        public static int ColorsFor(int score) => score >= 2500 ? 6 : (score >= 800 ? 5 : 4);
    }

    public sealed class ShufflePlan
    {
        /// <summary>Số lượt lời giải mới cần. Nếu lớn hơn số lượt còn lại thì xáo vô nghĩa.</summary>
        public int RequiredMoves;

        // màn tĩnh
        public List<List<int>> Paths;
        public int[] ColorOfPath;

        /// <summary>Màn tĩnh: các ô sẽ ĐƯỢC DỒN VỀ. Có thể khác hoàn toàn chỗ cũ.</summary>
        public int[] TargetCells;

        // màn gravity
        public GravityPlan Gravity;

        /// <summary>Màn gravity: chiều cao mới của từng cột sau khi dồn.</summary>
        public int[] TargetHeights;

        /// <summary>
        /// Dấu ô đặc biệt SAU khi xáo, đã đi theo ô về chỗ mới và đã được kiểm là còn
        /// chơi được với bộ đường mới.
        ///
        /// Bắt buộc phải có: xáo lại DỜI CHỖ các ô, nên dấu gắn theo chỉ số lưới mà
        /// không mang theo thì ô đích/ngòi/băng đứng lại toạ độ cũ trong khi ô đã đi —
        /// vòng đích hiện trên một ô chưa từng là đích, còn ô đích thật thì mất dấu.
        /// </summary>
        public CellMark[] Marks;                 // màn tĩnh: theo chỉ số lưới
        public CellMark[][] MarkColumns;         // màn gravity: theo [cột][bậc]
    }

    /// <summary>
    /// Toàn bộ trạng thái một ván: bàn, chọn chuỗi, ăn ô, gravity, hoàn tác, xáo lại.
    /// Không phụ thuộc UnityEngine — chạy được cả trong test console.
    /// </summary>
    public sealed class PuzzleSession
    {
        public const int Wall = -2;
        public const int Empty = -1;

        /// <summary>
        /// Ô đá. Là ô SỐNG (vẫn phải dọn) nhưng KHÔNG có màu, nên nó cố tình nằm ngoài
        /// mọi phép đếm theo màu. Mọi chỗ hỏi "ô này còn sống không" phải dùng IsAlive
        /// chứ không phải `>= 0`.
        /// </summary>
        public const int Stone = -3;

        public const int MaxColors = 6;

        public static bool IsColor(int v) => v >= 0;
        public static bool IsAlive(int v) => v >= 0 || v == Stone;

        public LevelData Level { get; private set; }

        /// <summary>Lưới đang nhìn thấy: Wall, Empty, Stone, hoặc chỉ số màu.</summary>
        public int[] Board { get; private set; }

        /// <summary>
        /// Dấu ô đặc biệt theo chỉ số lưới. Màn tĩnh: đây là bản gốc, sửa tại chỗ.
        /// Màn gravity: dựng lại từ Stacks mỗi lần đồng bộ.
        /// </summary>
        public CellMark[] Marks { get; private set; }

        /// <summary>Số ô đích còn lại (0 nếu màn không dùng mục tiêu).</summary>
        public int GoalsLeft { get; private set; }

        public CellMark MarkAt(int cell) => this.Marks == null ? null : this.Marks[cell];
        public bool IsWild(int cell)
        {
            CellMark m = MarkAt(cell);
            return m != null && m.Kind == CellKind.Wild;
        }

        /// <summary>
        /// Ô đang đóng băng — CÓ màu nhưng chưa chọn được. Mọi chỗ hỏi "ô này dùng
        /// được chưa" phải qua đây, không chỉ nhìn Board[cell] >= 0.
        /// </summary>
        public bool IsFrozen(int cell)
        {
            CellMark m = MarkAt(cell);
            return m != null && m.Kind == CellKind.Ice && m.Hp > 0;
        }


        /// <summary>Chỉ dùng khi gravity: Stacks[x] = các ô CÒN LẠI của cột, từ đáy lên.</summary>
        public List<SlotCell>[] Stacks { get; private set; }

        public int MovesUsed { get; private set; }
        public int Score { get; private set; }
        public int UndosLeft { get; private set; }
        public int ShufflesLeft { get; private set; }

        /// <summary>
        /// Đã thử xáo mà không có cách nào đủ lượt. Giữ cờ để không mời người chơi
        /// xáo lại lần nữa trong cùng một trạng thái vô vọng.
        /// </summary>
        public bool ShuffleImpossible { get; private set; }

        public List<int> Selection { get; } = new List<int>();
        public int SelectionColor { get; private set; } = -1;

        private readonly List<Snapshot> history = new List<Snapshot>();
        private int shuffleCounter;

        public int MovesLeft => this.Level.MaxMoves - this.MovesUsed;
        public bool CanUndo => this.history.Count > 0 && this.UndosLeft > 0;
        public bool CanShuffle => this.ShufflesLeft > 0 && !this.ShuffleImpossible
                                  && this.MovesUsed < this.Level.MaxMoves && TotalLeft() >= 2;

        /// <summary>Điểm một chuỗi — giữ đúng công thức của source gốc.</summary>
        public static int ChainScore(int length) => length * (length - 1);

        public PuzzleSession(LevelData level)
        {
            Load(level);
        }

        public void Load(LevelData level)
        {
            this.Level = level;
            this.baseMaxMoves = level.MaxMoves;
            Restart();
        }

        /// <summary>
        /// Ngân sách lượt GỐC của màn. Vật phẩm "+1 lượt" sửa Level.MaxMoves, mà LevelData
        /// sống lâu hơn một ván — không nhớ số gốc thì bấm Chơi lại là giữ luôn lượt đã
        /// mua, và cộng dồn thêm mỗi lần mua lại.
        /// </summary>
        private int baseMaxMoves;

        public void Restart()
        {
            LevelData level = this.Level;
            if (this.baseMaxMoves > 0) level.MaxMoves = this.baseMaxMoves;
            this.LastUndoneItem = ItemKind.None;
            this.FullChains = 0;
            this.MovesUsed = 0;
            this.Score = 0;
            this.UndosLeft = level.Undos;
            this.ShufflesLeft = level.Shuffles;
            this.ShuffleImpossible = false;
            this.history.Clear();
            this.shuffleCounter = 0;
            this.Selection.Clear();
            this.SelectionColor = -1;

            if (level.Endless)
            {
                this.Stacks = new List<SlotCell>[level.Geometry.Columns];
                for (int x = 0; x < level.Geometry.Columns; x++) this.Stacks[x] = new List<SlotCell>();
                this.Board = new int[level.Geometry.CellCount];
                this.Marks = new CellMark[level.Geometry.CellCount];
                RefillEndless(null);
            }
            else if (level.Gravity)
            {
                this.Stacks = new List<SlotCell>[level.Geometry.Columns];
                for (int x = 0; x < level.Geometry.Columns; x++)
                {
                    int[] column = level.Columns[x];
                    var stack = new List<SlotCell>(column.Length);
                    for (int k = 0; k < column.Length; k++)
                    {
                        CellMark mark = level.MarkColumns == null ? null : level.MarkColumns[x][k];
                        stack.Add(new SlotCell(column[k], k, mark?.Clone()));
                    }
                    this.Stacks[x] = stack;
                }
                this.Board = new int[level.Geometry.CellCount];
                this.Marks = new CellMark[level.Geometry.CellCount];
                SyncBoardFromStacks();
            }
            else
            {
                this.Stacks = null;
                this.Board = (int[])level.Template.Clone();
                this.Marks = CellMark.CloneAll(level.Marks) ?? new CellMark[this.Board.Length];
            }

            this.GoalsLeft = level.GoalMode ? CountGoalsOnBoard() : 0;
        }

        private int CountGoalsOnBoard()
        {
            int n = 0;
            if (this.Level.Gravity)
            {
                for (int x = 0; x < this.Stacks.Length; x++)
                    foreach (SlotCell cell in this.Stacks[x])
                        if (cell.Mark != null && cell.Mark.Goal) n++;
            }
            else
            {
                for (int i = 0; i < this.Board.Length; i++)
                    if (IsAlive(this.Board[i]) && this.Marks[i] != null && this.Marks[i].Goal) n++;
            }
            return n;
        }

        // ------------------------------------------------------------------
        // Đếm
        // ------------------------------------------------------------------

        public int VisibleAliveCount()
        {
            int n = 0;
            for (int i = 0; i < this.Board.Length; i++) if (IsAlive(this.Board[i])) n++;
            return n;
        }

        /// <summary>Còn bao nhiêu ô đa sắc trên bàn — chúng cứu được mọi ô lẻ.</summary>
        public int WildsLeft()
        {
            int n = 0;
            if (this.Level.Gravity)
            {
                for (int x = 0; x < this.Stacks.Length; x++)
                    foreach (SlotCell cell in this.Stacks[x])
                        if (cell.Mark != null && cell.Mark.Kind == CellKind.Wild) n++;
            }
            else
            {
                for (int i = 0; i < this.Board.Length; i++)
                    if (IsAlive(this.Board[i]) && IsWild(i)) n++;
            }
            return n;
        }

        /// <summary>Tổng ô còn lại, TÍNH CẢ hàng chờ chưa nhìn thấy.</summary>
        public int TotalLeft()
        {
            if (!this.Level.Gravity) return VisibleAliveCount();
            int n = 0;
            for (int x = 0; x < this.Stacks.Length; x++) n += this.Stacks[x].Count;
            return n;
        }

        public int QueueLeft()
        {
            return this.Level.Gravity ? TotalLeft() - VisibleAliveCount() : 0;
        }

        public bool IsCleared() => TotalLeft() == 0;

        /// <summary>
        /// Đã thắng chưa. Màn mục tiêu KHÔNG cần dọn sạch — chạm hết ô đích là xong,
        /// phần bàn còn lại kệ nó.
        /// </summary>
        public bool IsWon()
        {
            if (this.Level.Endless) return false;                 // vô tận không có "thắng"
            return this.Level.GoalMode ? this.GoalsLeft <= 0 : TotalLeft() == 0;
        }

        /// <summary>Các ô mang ngòi nổ đã cháy hết mà chưa được ăn.</summary>
        public List<int> BlownBombs()
        {
            var list = new List<int>();
            for (int i = 0; i < this.Board.Length; i++)
            {
                CellMark m = MarkAt(i);
                if (IsAlive(this.Board[i]) && m != null && m.Kind == CellKind.Bomb &&
                    m.Fuse - this.MovesUsed <= 0) list.Add(i);
            }
            return list;
        }

        /// <summary>Số chuỗi KỊCH TRẦN đã ăn trong ván này.</summary>
        public int FullChains { get; private set; }

        /// <summary>
        /// Huy hiệu kỹ thuật của ván. Chỉ tính khi ĐÃ THẮNG — gom đủ chuỗi đầy rồi
        /// thua thì không có gì để trao.
        /// </summary>
        public bool MedalEarned =>
            this.Level.MedalChains > 0 && this.FullChains >= this.Level.MedalChains && IsWon();

        public int StarsEarned()
        {
            if (this.MovesUsed <= this.Level.Par) return 3;
            if (this.MovesUsed <= this.Level.TwoStarMoves) return 2;
            return 1;
        }

        /// <summary>
        /// Còn nước đi hợp lệ? = còn dựng được một chuỗi dài ít nhất MinChain ô.
        ///
        /// Không còn là "hai ô cùng màu kề nhau": khi MinChain = 3 thì một cặp lẻ
        /// KHÔNG phải nước đi, và bàn chỉ còn toàn cặp lẻ là bàn chết.
        /// </summary>
        public bool HasMove()
        {
            if (this.Level.MinChain <= 1) return VisibleAliveCount() > 0;

            var used = new bool[this.Board.Length];
            for (int i = 0; i < this.Board.Length; i++)
            {
                // Ô đóng băng chưa dùng được nên KHÔNG tính là nước đi. Bỏ sót chỗ này
                // thì bàn chỉ còn toàn băng vẫn bị coi là "còn nước đi", và người chơi
                // ngồi nhìn một bàn không bấm được mà game không báo gì.
                if (!IsColor(this.Board[i]) || IsFrozen(i)) continue;
                used[i] = true;
                bool found = HasChainFrom(i, 1, IsWild(i) ? -1 : this.Board[i], IsWild(i) ? 1 : 0, used);
                used[i] = false;
                if (found) return true;
            }
            return false;
        }

        /// <summary>
        /// DFS nông: chỉ cần biết có tồn tại chuỗi đủ dài hay không.
        /// `colour` = -1 nghĩa là chuỗi CHƯA quyết định màu (mới chỉ toàn ô đa sắc).
        /// `wilds` đếm số ô đa sắc đã dùng — luật cho tối đa 1.
        /// </summary>
        private bool HasChainFrom(int cell, int length, int colour, int wilds, bool[] used)
        {
            if (length >= this.Level.MinChain) return true;
            foreach (int j in this.Level.Geometry.Neighbors[cell])
            {
                if (used[j] || !IsColor(this.Board[j]) || IsFrozen(j)) continue;

                bool wild = IsWild(j);
                if (wild && wilds >= 1) continue;
                if (!wild && colour >= 0 && this.Board[j] != colour) continue;

                used[j] = true;
                bool found = HasChainFrom(j, length + 1,
                    wild ? colour : (colour < 0 ? this.Board[j] : colour),
                    wilds + (wild ? 1 : 0), used);
                used[j] = false;
                if (found) return true;
            }
            return false;
        }

        /// <summary>Số ô còn lại của từng màu, tính cả hàng chờ.</summary>
        /// <summary>
        /// Số ô còn lại của từng màu, tính cả hàng chờ.
        /// Ô đa sắc KHÔNG tính vào màu nào: nó ghép được với tất cả, nên cộng nó vào một
        /// màu cụ thể sẽ làm suy luận "màu này chỉ còn 1 ô" sai lệch.
        /// </summary>
        public int[] CountByColor()
        {
            var n = new int[MaxColors];
            if (this.Level.Gravity)
            {
                for (int x = 0; x < this.Stacks.Length; x++)
                    foreach (SlotCell cell in this.Stacks[x])
                        if (cell.Mark == null || cell.Mark.Kind != CellKind.Wild) n[cell.Color]++;
            }
            else
            {
                for (int i = 0; i < this.Board.Length; i++)
                    if (IsColor(this.Board[i]) && !IsWild(i)) n[this.Board[i]]++;
            }
            return n;
        }

        public List<int> AliveVisibleCells()
        {
            var list = new List<int>();
            for (int i = 0; i < this.Board.Length; i++) if (IsAlive(this.Board[i])) list.Add(i);
            return list;
        }

        /// <summary>Các ô đích còn lại; màn thường thì rơi về toàn bộ ô trên bàn.</summary>
        public List<int> GoalOrAliveCells()
        {
            if (!this.Level.GoalMode) return AliveVisibleCells();
            var list = new List<int>();
            for (int i = 0; i < this.Board.Length; i++)
            {
                CellMark m = MarkAt(i);
                if (IsAlive(this.Board[i]) && m != null && m.Goal) list.Add(i);
            }
            return list;
        }

        /// <summary>Các nhóm liên thông cùng màu trên bàn đang thấy.</summary>
        public List<List<int>> ColorComponents()
        {
            int[][] neighbors = this.Level.Geometry.Neighbors;
            var seen = new bool[this.Board.Length];
            var result = new List<List<int>>();
            var stack = new List<int>();

            for (int i = 0; i < this.Board.Length; i++)
            {
                int c = this.Board[i];
                if (c < 0 || seen[i]) continue;

                var component = new List<int>();
                stack.Clear();
                stack.Add(i);
                seen[i] = true;

                while (stack.Count > 0)
                {
                    int k = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    component.Add(k);
                    foreach (int j in neighbors[k])
                        if (!seen[j] && this.Board[j] == c) { seen[j] = true; stack.Add(j); }
                }
                result.Add(component);
            }
            return result;
        }

        // ------------------------------------------------------------------
        // Chọn chuỗi — cùng luật với source gốc: cùng màu, kề 8 hướng, dài >= 2,
        // lùi về ô kế cuối thì bỏ chọn ô cuối.
        // ------------------------------------------------------------------

        public SelectionChange TryExtendSelection(int cell)
        {
            if (cell < 0 || cell >= this.Board.Length) return SelectionChange.None;
            if (this.Board[cell] == Stone) return SelectionChange.None;   // đá không nối được
            if (IsFrozen(cell)) return SelectionChange.None;              // băng chưa tan
            if (!IsColor(this.Board[cell])) return SelectionChange.None;

            if (this.Selection.Count == 0)
            {
                // Bắt đầu bằng ô đa sắc thì màu chuỗi CHƯA quyết định: ô có màu đầu tiên
                // ghép vào sẽ quyết. Lấy luôn màu ẩn dưới ô đa sắc là khoá người chơi vào
                // một màu mà họ không hề nhìn thấy.
                this.SelectionColor = IsWild(cell) ? -1 : this.Board[cell];
                this.Selection.Add(cell);
                return SelectionChange.Added;
            }

            int position = this.Selection.IndexOf(cell);
            if (position >= 0)
            {
                if (this.Selection.Count >= 2 && position == this.Selection.Count - 2)
                {
                    this.Selection.RemoveAt(this.Selection.Count - 1);
                    if (AllSelectedAreWild()) this.SelectionColor = -1;
                    return SelectionChange.Removed;
                }
                return SelectionChange.None;
            }

            if (IsWild(cell))
            {
                if (WildsInSelection() >= 1) return SelectionChange.None;  // tối đa 1 ô mỗi chuỗi
            }
            else if (this.SelectionColor >= 0 && this.Board[cell] != this.SelectionColor)
            {
                return SelectionChange.None;
            }
            if (this.Selection.Count >= this.Level.MaxChain) return SelectionChange.None;   // chạm trần

            int last = this.Selection[this.Selection.Count - 1];
            bool adjacent = false;
            foreach (int j in this.Level.Geometry.Neighbors[last]) if (j == cell) { adjacent = true; break; }
            if (!adjacent) return SelectionChange.None;

            if (!IsWild(cell) && this.SelectionColor < 0) this.SelectionColor = this.Board[cell];
            this.Selection.Add(cell);
            return SelectionChange.Added;
        }

        private int WildsInSelection()
        {
            int n = 0;
            foreach (int i in this.Selection) if (IsWild(i)) n++;
            return n;
        }

        private bool AllSelectedAreWild()
        {
            foreach (int i in this.Selection) if (!IsWild(i)) return false;
            return true;
        }

        public void ClearSelection()
        {
            this.Selection.Clear();
            this.SelectionColor = -1;
        }

        /// <summary>
        /// Chốt nước đi: ăn chuỗi đang chọn, cộng điểm, và (nếu gravity) cho ô rơi.
        /// Trả null nếu chuỗi chưa hợp lệ. Trạng thái sau khi trả về đã là trạng thái
        /// CUỐI của nước đi — tầng hiển thị dùng dữ liệu trả về để dựng hoạt ảnh, chứ
        /// model không nằm ở trạng thái nửa vời trong lúc animation chạy.
        /// </summary>
        public MoveResult Commit()
        {
            if (this.Selection.Count < this.Level.MinChain) return null;

            var result = new MoveResult
            {
                ClearedCells = this.Selection.ToArray(),
                Color = this.SelectionColor,
                Gained = ChainScore(this.Selection.Count),
                ScoreBefore = this.Score
            };

            // Vô tận: chuỗi dài liên tiếp nhân điểm. Đây là thứ duy nhất biến "ăn cho
            // hết" thành "ăn cho khéo" khi đã bỏ hết giới hạn lượt.
            if (this.Level.Endless)
            {
                if (this.Selection.Count >= EndlessRules.ComboMinChain) this.Combo++;
                else this.Combo = 0;
                result.Gained = (int)Math.Round(result.Gained * EndlessMultiplier);
            }

            this.history.Add(Capture());
            this.ShuffleImpossible = false;                  // bàn đã đổi, đánh giá lại
            this.Score += result.Gained;
            this.MovesUsed++;

            // Chuỗi kịch trần — đơn vị đo của huy hiệu kỹ thuật.
            if (this.Level.MaxChain != int.MaxValue && this.Selection.Count == this.Level.MaxChain)
                this.FullChains++;

            // ô đích vừa dọn được
            if (this.Level.GoalMode)
                foreach (int i in result.ClearedCells)
                {
                    CellMark m = MarkAt(i);
                    if (m != null && m.Goal) { this.GoalsLeft--; result.GoalsCleared++; }
                }

            if (this.Level.Gravity) ApplyGravity(result.ClearedCells, result.Falls);
            else
            {
                // Ô bạn liên kết phải vỡ TRƯỚC khi xoá chuỗi: đọc dấu của ô vừa ăn để
                // biết nó trỏ đi đâu, xoá chuỗi rồi thì dấu không còn để mà đọc.
                BreakLinkedPartners(result.ClearedCells, result);

                foreach (int i in result.ClearedCells) { this.Board[i] = Empty; this.Marks[i] = null; }
                DamageStones(result.ClearedCells, result);
            }

            ClearSelection();
            return result;
        }

        /// <summary>
        /// Đá kề chuỗi vừa ăn mất 1 máu. MỖI NƯỚC TỐI ĐA 1, dù chuỗi kề nó bằng mấy ô —
        /// đây đúng là điều kiện mà lúc sinh màn đã bảo đảm ("mỗi hòn kề ít nhất Hp
        /// đường phân biệt"). Đổi luật ở đây là mất bảo đảm đó.
        /// </summary>
        /// <summary>
        /// Ô bạn của mỗi ô liên kết vừa bị ăn cũng vỡ theo, dù ở đâu trên bàn.
        ///
        /// Không đệ quy: ô bạn vỡ theo KHÔNG kích hoạt liên kết của chính nó. Cho đệ
        /// quy thì một nước có thể quét sạch cả chuỗi liên kết dài, phá luôn cận dưới
        /// về số lượt mà LossAnalyzer đang dựa vào — và người chơi cũng không lường
        /// trước nổi hậu quả của nước mình vừa đi.
        /// </summary>
        private void BreakLinkedPartners(int[] clearedCells, MoveResult result)
        {
            if (this.Level.Gravity || this.Level.Config.Links <= 0) return;

            var alreadyCleared = new HashSet<int>(clearedCells);

            foreach (int i in clearedCells)
            {
                CellMark m = this.Marks[i];
                if (m == null || m.Kind != CellKind.Link) continue;

                int partner = m.LinkPartner;
                if (partner < 0 || partner >= this.Board.Length) continue;
                if (alreadyCleared.Contains(partner)) continue;      // đã nằm trong chuỗi rồi
                if (!IsAlive(this.Board[partner])) continue;         // đã vỡ từ trước

                CellMark partnerMark = this.Marks[partner];
                if (partnerMark != null && partnerMark.Goal)
                {
                    this.GoalsLeft--;
                    result.GoalsCleared++;
                }

                this.Board[partner] = Empty;
                this.Marks[partner] = null;
                result.LinkedBroken.Add(partner);
            }
        }

        private void DamageStones(int[] clearedCells, MoveResult result)
        {
            if (this.Level.Gravity) return;                   // gravity không dùng đá/băng

            int[][] neighbors = this.Level.Geometry.Neighbors;
            var hit = new HashSet<int>();
            foreach (int i in clearedCells)
                foreach (int j in neighbors[i])
                {
                    // Đá VÀ băng cùng dùng một luật va: kề chuỗi vừa ăn thì mất 1 máu.
                    // Khác nhau ở chỗ HẾT máu — đá biến mất, băng thành ô ăn được.
                    if (this.Board[j] == Stone || IsFrozen(j)) hit.Add(j);
                }

            foreach (int j in hit)
            {
                CellMark m = this.Marks[j];
                if (m == null) continue;

                bool isIce = m.Kind == CellKind.Ice;
                m.Hp--;

                if (m.Hp > 0)
                {
                    if (isIce) result.CrackedIce.Add(j);
                    else result.CrackedStones.Add(j);
                    continue;
                }

                if (isIce)
                {
                    // Băng tan hết: ô GIỮ NGUYÊN màu và vẫn nằm trên bàn, chỉ bỏ dấu đi.
                    // Xoá ô như đá là mất luôn một ô mà lời giải tham chiếu còn cần tới.
                    this.Marks[j] = null;
                    result.ThawedIce.Add(j);
                }
                else
                {
                    this.Board[j] = Empty;
                    this.Marks[j] = null;
                    result.BrokenStones.Add(j);
                }
            }
        }

        private void ApplyGravity(int[] clearedCells, List<FallStep> falls)
        {
            int columns = this.Level.Geometry.Columns;
            int rows = this.Level.Geometry.Rows;

            // grid index -> (cột, bậc tính từ đáy)
            var byColumn = new Dictionary<int, HashSet<int>>();
            foreach (int i in clearedCells)
            {
                int x = i % columns;
                int y = i / columns;
                int slotIndex = rows - 1 - y;
                if (!byColumn.TryGetValue(x, out var set)) { set = new HashSet<int>(); byColumn[x] = set; }
                set.Add(slotIndex);
            }

            foreach (var pair in byColumn)
            {
                int x = pair.Key;
                HashSet<int> gone = pair.Value;
                List<SlotCell> stack = this.Stacks[x];
                var kept = new List<SlotCell>(stack.Count);
                int shift = 0;

                for (int index = 0; index < stack.Count; index++)
                {
                    if (gone.Contains(index)) { shift++; continue; }
                    if (shift > 0)
                        falls.Add(new FallStep { Column = x, FromSlotIndex = index, ToSlotIndex = index - shift });
                    kept.Add(stack[index]);
                }
                this.Stacks[x] = kept;
            }

            // Vô tận: đổ đầy lại NGAY, trước khi đồng bộ, để ô mới dùng chung một lượt
            // hoạt ảnh rơi với ô cũ — nhìn thành một dòng chảy liên tục chứ không phải
            // "rơi xong rồi mới thấy ô mới hiện ra".
            if (this.Level.Endless) RefillEndless(falls);
            else SyncBoardFromStacks();
        }

        /// <summary>Dựng lại lưới đang thấy từ các chồng cột.</summary>
        public void SyncBoardFromStacks()
        {
            int columns = this.Level.Geometry.Columns;
            int rows = this.Level.Geometry.Rows;
            for (int i = 0; i < this.Board.Length; i++) { this.Board[i] = Empty; this.Marks[i] = null; }

            for (int x = 0; x < columns; x++)
            {
                List<SlotCell> stack = this.Stacks[x];
                int visible = stack.Count < rows ? stack.Count : rows;
                for (int index = 0; index < visible; index++)
                {
                    int at = (rows - 1 - index) * columns + x;
                    this.Board[at] = stack[index].Color;
                    this.Marks[at] = stack[index].Mark;
                }
            }
        }

        // ------------------------------------------------------------------
        // Chế độ vô tận
        // ------------------------------------------------------------------

        /// <summary>Mạch combo hiện tại (chỉ dùng ở chế độ vô tận).</summary>
        public int Combo { get; private set; }

        public double EndlessMultiplier =>
            1.0 + Math.Min(this.Combo, EndlessRules.ComboCap) * 0.25;

        private Random endlessRandom;

        private SlotCell NewEndlessCell()
        {
            if (this.endlessRandom == null) this.endlessRandom = new Random();
            int colors = EndlessRules.ColorsFor(this.Score);
            CellMark mark = null;
            if (this.endlessRandom.Next(1000) < EndlessRules.WildPerMille)
                mark = new CellMark { Kind = CellKind.Wild };
            return new SlotCell(this.endlessRandom.Next(colors), 0, mark);
        }

        /// <summary>
        /// Đổ đầy lại các cột và ghi luôn quãng rơi cho hoạt ảnh. `FromSlotIndex` cố ý
        /// đặt cao hơn mép bàn để ô mới bay từ ngoài màn hình vào, dùng chung đúng
        /// đường hoạt ảnh với ô rơi bình thường.
        ///
        /// Bảo đảm duy nhất của chế độ này nằm ở đây: sau khi đổ đầy, bàn PHẢI còn ít
        /// nhất một nước đi — nếu không người chơi thua vì xúc xắc chứ không phải vì
        /// chơi dở.
        /// </summary>
        public void RefillEndless(List<FallStep> falls)
        {
            int columns = this.Level.Geometry.Columns;
            int rows = this.Level.Geometry.Rows;
            var fresh = new List<KeyValuePair<int, int>>();          // (cột, bậc)

            for (int x = 0; x < columns; x++)
            {
                List<SlotCell> stack = this.Stacks[x];
                int k = 0;
                while (stack.Count < rows)
                {
                    stack.Add(NewEndlessCell());
                    int to = stack.Count - 1;
                    falls?.Add(new FallStep { Column = x, FromSlotIndex = rows + k, ToSlotIndex = to });
                    fresh.Add(new KeyValuePair<int, int>(x, to));
                    k++;
                }
            }

            for (int tries = 0; tries < 60; tries++)
            {
                SyncBoardFromStacks();
                if (HasMove()) return;
                foreach (var f in fresh) this.Stacks[f.Key][f.Value] = NewEndlessCell();
            }
            SyncBoardFromStacks();
        }

        /// <summary>
        /// Một ô trong bản chụp bàn vô tận để lưu ra ngoài (PlayerPrefs) và phục hồi khi
        /// mở lại. Chỉ cần Color + có phải ô đa sắc không: vô tận không dùng đá/ngòi/
        /// đích nên phần còn lại của CellMark không có ý nghĩa gì để lưu.
        /// </summary>
        public struct EndlessCellSnapshot
        {
            public int Color;
            public bool Wild;
        }

        /// <summary>Chụp lại toàn bộ các cột của bàn vô tận, theo đúng thứ tự đáy lên.</summary>
        public EndlessCellSnapshot[][] CaptureEndlessColumns()
        {
            var result = new EndlessCellSnapshot[this.Stacks.Length][];
            for (int x = 0; x < this.Stacks.Length; x++)
            {
                result[x] = new EndlessCellSnapshot[this.Stacks[x].Count];
                for (int i = 0; i < result[x].Length; i++)
                {
                    SlotCell cell = this.Stacks[x][i];
                    result[x][i] = new EndlessCellSnapshot
                    {
                        Color = cell.Color,
                        Wild = cell.Mark != null && cell.Mark.Kind == CellKind.Wild
                    };
                }
            }
            return result;
        }

        /// <summary>
        /// Phục hồi một ván vô tận đã lưu. Gọi NGAY SAU khi tạo PuzzleSession cho màn
        /// Endless — nó THAY hẳn bàn vừa refill lúc khởi tạo bằng bàn đã lưu, nên gọi
        /// muộn hơn (sau khi đã render) sẽ để lại một khung hình sai trước khi kịp sửa.
        /// </summary>
        public void RestoreEndless(EndlessCellSnapshot[][] columns, int score, int movesUsed,
                                   int combo, int shufflesLeft)
        {
            for (int x = 0; x < this.Stacks.Length && x < columns.Length; x++)
            {
                this.Stacks[x].Clear();
                foreach (EndlessCellSnapshot cell in columns[x])
                {
                    CellMark mark = cell.Wild ? new CellMark { Kind = CellKind.Wild } : null;
                    this.Stacks[x].Add(new SlotCell(cell.Color, 0, mark));
                }
            }
            SyncBoardFromStacks();

            this.Score = score;
            this.MovesUsed = movesUsed;
            this.Combo = combo;
            this.ShufflesLeft = shufflesLeft;
            this.history.Clear();
            this.ShuffleImpossible = false;
            ClearSelection();
        }

        /// <summary>Gieo lại toàn bộ bàn vô tận (nút Xáo). Luôn ra bàn còn đi được.</summary>
        public bool ReshuffleEndless()
        {
            for (int tries = 0; tries < 80; tries++)
            {
                for (int x = 0; x < this.Stacks.Length; x++)
                    for (int i = 0; i < this.Stacks[x].Count; i++)
                        this.Stacks[x][i] = NewEndlessCell();
                SyncBoardFromStacks();
                if (HasMove())
                {
                    this.ShufflesLeft--;
                    this.Combo = 0;                 // xáo là mất mạch combo, đó là cái giá
                    ClearSelection();
                    return true;
                }
            }
            return false;
        }


        // ------------------------------------------------------------------
        // Vật phẩm dùng một lần
        //
        // Mua bằng sao rồi dùng NGAY, không có kho đồ: mỗi lần bấm là một lần tiêu.
        // Bỏ kho đồ đi vì kho đòi thêm màn hình quản lý, thêm chỗ lưu, mà không thêm
        // quyết định nào cho người chơi — quyết định thật chỉ có một: "dùng bây giờ,
        // hay để dành sao?".
        //
        // Vật phẩm KHÔNG tốn lượt. Tốn lượt thì búa vừa mất sao vừa mất lượt, và ở
        // đúng lúc cần nó nhất (sắp hết lượt) nó thành vô dụng.
        // ------------------------------------------------------------------

        public enum ItemKind { None = 0, Hammer = 1, Paint = 2, ExtraMove = 3 }

        public enum ItemUse { Ok, NotAllowedHere, BadTarget }

        /// <summary>
        /// Giá bằng SAO. Sơn đắt nhất vì nó là thứ duy nhất tạo ra khả năng mới thay vì
        /// chỉ dọn bớt: một ô đa sắc nối được mọi màu, giá trị của nó kéo dài tới cuối ván.
        /// "+1 lượt" rẻ nhất vì nó chỉ hoãn thất bại chứ không gỡ được thế bí.
        /// </summary>
        public static int ItemCost(ItemKind kind)
        {
            if (kind == ItemKind.Hammer) return 3;
            if (kind == ItemKind.Paint) return 5;
            if (kind == ItemKind.ExtraMove) return 2;
            return 0;
        }

        /// <summary>
        /// Vô tận không cho dùng vật phẩm: ở đó không có giới hạn lượt để mà nới, và
        /// búa thì biến việc giữ combo thành chuyện mua được bằng sao.
        /// </summary>
        public bool ItemsAllowed => !this.Level.Endless;

        /// <summary>Vật phẩm của bước vừa bị hoàn tác — để hoàn lại sao. None nếu là nước đi thường.</summary>
        public ItemKind LastUndoneItem { get; private set; }

        /// <summary>Vật phẩm này có dùng được lên ô đó ngay lúc này không.</summary>
        public bool CanUseItem(ItemKind kind, int cell)
        {
            if (!this.ItemsAllowed) return false;
            // "+1 lượt" luôn dùng được, kể cả khi ĐÃ hết lượt — đó chính là lúc nó có
            // nghĩa nhất, và cũng là chỗ nó xuất hiện: trên thẻ báo thua.
            if (kind == ItemKind.ExtraMove) return true;
            if (cell < 0 || cell >= this.Board.Length) return false;

            if (kind == ItemKind.Hammer) return IsAlive(this.Board[cell]);

            if (kind == ItemKind.Paint)
            {
                // Sơn cần một ô MÀU bình thường: đá không có màu, băng còn khoá, và
                // ô đã đa sắc thì sơn thêm chẳng đổi gì — cả ba đều là tiêu sao vô ích.
                if (!IsColor(this.Board[cell])) return false;
                if (IsFrozen(cell)) return false;
                CellMark m = MarkAt(cell);
                return m == null || m.Kind != CellKind.Wild;
            }
            return false;
        }

        /// <summary>
        /// Dùng vật phẩm. Trả ItemUse.Ok kèm effect mô tả những gì đổi trên bàn (để
        /// hoạt ảnh chạy đúng như một nước đi thường).
        ///
        /// Ghi ảnh chụp như một nước đi, nên HOÀN TÁC ĐƯỢC — và hoàn tác xong thì
        /// LastUndoneItem cho biết phải hoàn lại sao nào.
        /// </summary>
        public ItemUse UseItem(ItemKind kind, int cell, out MoveResult effect)
        {
            effect = null;
            if (!this.ItemsAllowed) return ItemUse.NotAllowedHere;
            if (!CanUseItem(kind, cell)) return ItemUse.BadTarget;

            Snapshot before = Capture();
            before.Item = kind;

            var result = new MoveResult { ClearedCells = new int[0], Color = -1, ScoreBefore = this.Score };

            switch (kind)
            {
                case ItemKind.ExtraMove:
                    this.Level.MaxMoves++;
                    break;

                case ItemKind.Paint:
                    SetMarkAt(cell, new CellMark { Kind = CellKind.Wild });
                    result.ClearedCells = new int[0];
                    break;

                case ItemKind.Hammer:
                    HammerCell(cell, result);
                    break;
            }

            this.history.Add(before);
            this.ShuffleImpossible = false;
            ClearSelection();
            effect = result;
            return ItemUse.Ok;
        }

        /// <summary>
        /// Búa lên một ô. Đá và băng chỉ MẤT MỘT MÁU chứ không vỡ ngay: cho búa xoá
        /// thẳng một tảng băng 3 lớp thì cả cơ chế băng biến thành "có sao là xong".
        /// </summary>
        private void HammerCell(int cell, MoveResult result)
        {
            CellMark m = MarkAt(cell);

            if (m != null && (m.Kind == CellKind.Stone || m.Kind == CellKind.Ice))
            {
                bool isIce = m.Kind == CellKind.Ice;
                m.Hp--;
                if (m.Hp > 0)
                {
                    if (isIce) result.CrackedIce.Add(cell); else result.CrackedStones.Add(cell);
                    return;
                }
                if (isIce) { SetMarkAt(cell, null); result.ThawedIce.Add(cell); }
                else { RemoveCell(cell, result); result.BrokenStones.Add(cell); }
                return;
            }

            // Ô đích bị đập PHẢI tính là đã dọn. Không tính thì người chơi đập mất ô
            // đích rồi màn thành không bao giờ thắng được — tự tay khoá chết ván mình.
            if (m != null && m.Goal && this.Level.GoalMode) { this.GoalsLeft--; result.GoalsCleared++; }

            // Ô liên kết kéo bạn nó theo, y như khi bị ăn — nếu không, đập một đầu sẽ
            // để lại một đầu vĩnh viễn không bao giờ ghép được với ai.
            if (m != null && m.Kind == CellKind.Link && !this.Level.Gravity)
            {
                int partner = m.LinkPartner;
                if (partner >= 0 && partner < this.Board.Length && IsAlive(this.Board[partner]))
                {
                    CellMark pm = this.Marks[partner];
                    if (pm != null && pm.Goal && this.Level.GoalMode) { this.GoalsLeft--; result.GoalsCleared++; }
                    this.Board[partner] = Empty;
                    this.Marks[partner] = null;
                    result.LinkedBroken.Add(partner);
                }
            }

            RemoveCell(cell, result);
        }

        /// <summary>
        /// Xoá hẳn một ô khỏi bàn. Ở màn gravity phải đi qua ApplyGravity để chồng cột
        /// tụt xuống — sửa thẳng Board là vô ích, SyncBoardFromStacks sẽ ghi đè lại ngay.
        /// </summary>
        private void RemoveCell(int cell, MoveResult result)
        {
            var cleared = new int[] { cell };
            var merged = new List<int>(result.ClearedCells) { cell };
            result.ClearedCells = merged.ToArray();

            if (this.Level.Gravity) ApplyGravity(cleared, result.Falls);
            else { this.Board[cell] = Empty; this.Marks[cell] = null; }
        }

        /// <summary>
        /// Đặt dấu cho một ô. Ở màn gravity dấu sống trong Stacks[x][bậc].Mark chứ
        /// không phải trong this.Marks — this.Marks chỉ là bản dựng lại, ghi vào đó
        /// thì lần rơi kế tiếp xoá sạch. Và SlotCell là STRUCT nên phải gán trả lại
        /// vào List, sửa bản sao lấy ra thì không có gì đổi cả.
        /// </summary>
        private void SetMarkAt(int cell, CellMark mark)
        {
            this.Marks[cell] = mark;
            if (!this.Level.Gravity || this.Stacks == null) return;

            int columns = this.Level.Geometry.Columns;
            int rows = this.Level.Geometry.Rows;
            int x = cell % columns;
            int slotIndex = rows - 1 - cell / columns;
            if (x < 0 || x >= this.Stacks.Length) return;

            List<SlotCell> stack = this.Stacks[x];
            if (slotIndex < 0 || slotIndex >= stack.Count) return;
            SlotCell sc = stack[slotIndex];
            stack[slotIndex] = new SlotCell(sc.Color, sc.Slot, mark);
        }

        // ------------------------------------------------------------------
        // Hoàn tác
        // ------------------------------------------------------------------

        private sealed class Snapshot
        {
            public int[] Board;
            public CellMark[] Marks;
            public List<SlotCell>[] Stacks;
            public List<List<int>> Paths;
            public List<List<SlotRef>> Solution;
            public int MovesUsed;
            public int Score;
            public int GoalsLeft;
            public int Combo;

            /// <summary>MaxMoves phải nằm trong ảnh chụp vì vật phẩm "+1 lượt" sửa nó.</summary>
            public int MaxMoves;
            public int FullChains;

            /// <summary>Vật phẩm đã dùng ở bước này; None nếu là nước đi thường.</summary>
            public ItemKind Item;
        }

        /// <summary>
        /// Ảnh chụp phải lưu cả LỜI GIẢI: xáo lại thay Paths/Solution, nên nếu hoàn
        /// tác về bàn cũ mà giữ lời giải mới thì nút gợi ý sẽ chỉ vào các ô sai màu.
        /// </summary>
        private Snapshot Capture()
        {
            var snapshot = new Snapshot
            {
                Board = (int[])this.Board.Clone(),
                // Dấu phải chép SÂU: máu đá và số ngòi bị sửa tại chỗ, chép nông thì
                // hoàn tác xong đá vẫn giữ máu đã mất.
                Marks = CellMark.CloneAll(this.Marks),
                Paths = this.Level.Paths,
                Solution = this.Level.Solution,
                MovesUsed = this.MovesUsed,
                Score = this.Score,
                GoalsLeft = this.GoalsLeft,
                Combo = this.Combo,
                MaxMoves = this.Level.MaxMoves,
                FullChains = this.FullChains,
                Item = ItemKind.None
            };
            if (this.Stacks != null)
            {
                snapshot.Stacks = new List<SlotCell>[this.Stacks.Length];
                for (int x = 0; x < this.Stacks.Length; x++)
                {
                    var copy = new List<SlotCell>(this.Stacks[x].Count);
                    foreach (SlotCell cell in this.Stacks[x])
                        copy.Add(new SlotCell(cell.Color, cell.Slot, cell.Mark?.Clone()));
                    snapshot.Stacks[x] = copy;
                }
            }
            return snapshot;
        }

        public enum UndoResult { Ok, NothingToUndo, NoQuotaLeft }

        public UndoResult Undo()
        {
            if (this.history.Count == 0) return UndoResult.NothingToUndo;
            if (this.UndosLeft <= 0) return UndoResult.NoQuotaLeft;

            Snapshot snapshot = this.history[this.history.Count - 1];
            this.history.RemoveAt(this.history.Count - 1);

            this.Board = snapshot.Board;
            this.Marks = snapshot.Marks;
            this.Stacks = snapshot.Stacks;
            this.Level.Paths = snapshot.Paths;
            this.Level.Solution = snapshot.Solution;
            this.MovesUsed = snapshot.MovesUsed;
            this.Score = snapshot.Score;
            this.GoalsLeft = snapshot.GoalsLeft;
            this.Combo = snapshot.Combo;
            this.Level.MaxMoves = snapshot.MaxMoves;
            this.FullChains = snapshot.FullChains;
            this.LastUndoneItem = snapshot.Item;
            this.UndosLeft--;
            this.ShuffleImpossible = false;
            ClearSelection();
            return UndoResult.Ok;
        }

        // ------------------------------------------------------------------
        // Xáo lại — giữ nguyên VỊ TRÍ và SỐ LƯỢNG ô còn lại, chỉ tô lại màu sao
        // cho tồn tại một lời giải mới lọt vào số lượt CÒN LẠI.
        // ------------------------------------------------------------------

        private void ShuffleTargetLength(int cellsLeft, int movesLeft, out int min, out int max)
        {
            int need = Math.Max(this.Level.MinChain, (int)Math.Ceiling(cellsLeft / (double)Math.Max(1, movesLeft)));
            int ceiling = this.Level.MaxChain == int.MaxValue ? 12 : this.Level.MaxChain;
            min = Math.Min(need, ceiling);
            max = Math.Min(min + 2, ceiling);
            if (min < this.Level.MinChain) min = this.Level.MinChain;
            if (max < min) max = min;
        }

        /// <summary>
        /// Lập kế hoạch xáo. Trả null nếu không có cách nào (kể cả cách tốt nhất tìm
        /// được) dọn sạch trong số lượt còn lại — thà nói thẳng còn hơn xáo ra một
        /// bàn đẹp mà vẫn thua.
        /// </summary>
        public ShufflePlan PlanShuffle()
        {
            int cellsLeft = TotalLeft();
            if (cellsLeft < 2) return null;

            int movesLeft = this.MovesLeft;
            this.shuffleCounter++;
            ShuffleTargetLength(cellsLeft, movesLeft, out int minLen, out int maxLen);

            ShufflePlan plan = this.Level.Gravity
                ? PlanShuffleGravity(minLen, maxLen, movesLeft)
                : PlanShuffleStatic(minLen, maxLen, movesLeft);

            if (plan == null || plan.RequiredMoves > movesLeft)
            {
                this.ShuffleImpossible = true;
                return null;
            }
            return plan;
        }

        /// <summary>
        /// Chọn một khối LIỀN NHAU gồm `count` ô để dồn các ô còn lại về.
        ///
        /// Xáo lại mà chỉ tô màu thì bó tay khi các ô còn lại nằm rải ở những góc xa —
        /// không cách tô nào làm chúng kề nhau được. Dồn về một khối liền thì luôn có
        /// lời giải. Khối lấy bằng BFS từ ô gần TÂM của các ô còn sống, nên chúng dồn
        /// về chỗ đang có ô chứ không nhảy sang góc trống bên kia bàn.
        /// </summary>
        private int[] PickCompactBlock(int count)
        {
            BoardGeometry geo = this.Level.Geometry;

            // tâm của các ô còn sống
            double sumX = 0, sumY = 0;
            int alive = 0;
            for (int i = 0; i < this.Board.Length; i++)
            {
                if (this.Board[i] < 0) continue;
                sumX += i % geo.Columns;
                sumY += i / geo.Columns;
                alive++;
            }
            if (alive == 0) return null;
            double centreX = sumX / alive, centreY = sumY / alive;

            int start = -1;
            double bestDistance = double.MaxValue;
            foreach (int cell in geo.Cells)
            {
                double dx = cell % geo.Columns - centreX;
                double dy = cell / geo.Columns - centreY;
                double d = dx * dx + dy * dy;
                if (d < bestDistance) { bestDistance = d; start = cell; }
            }
            if (start < 0) return null;

            var block = new List<int>(count);
            var seen = new bool[geo.CellCount];
            var queue = new Queue<int>();
            queue.Enqueue(start);
            seen[start] = true;

            while (queue.Count > 0 && block.Count < count)
            {
                int cell = queue.Dequeue();
                block.Add(cell);
                foreach (int j in geo.Neighbors[cell])
                    if (!seen[j]) { seen[j] = true; queue.Enqueue(j); }
            }

            return block.Count == count ? block.ToArray() : null;
        }

        /// <summary>
        /// Ghép mỗi ô ĐÍCH với ô nguồn còn trống gần nó nhất (tham lam gần nhất).
        /// Trả mảng cùng thứ tự với `targets`: phần tử i là chỉ số ô nguồn đi về đó.
        ///
        /// Tách riêng vì cả lúc LẬP KẾ HOẠCH (để kiểm dấu sau khi dời có còn chơi được
        /// không) lẫn lúc THI HÀNH (để dựng đường trượt) đều cần đúng phép ghép này —
        /// hai chỗ ghép khác nhau thì dấu sẽ đi một đằng, hoạt ảnh đi một nẻo.
        /// </summary>
        private int[] PairSourcesToTargets(int[] targets)
        {
            int columns = this.Level.Geometry.Columns;
            List<int> sources = AliveVisibleCells();
            var taken = new bool[sources.Count];
            var result = new int[targets.Length];

            for (int t = 0; t < targets.Length; t++)
            {
                int tx = targets[t] % columns, ty = targets[t] / columns;
                int bestIndex = -1, bestDistance = int.MaxValue;

                for (int s = 0; s < sources.Count; s++)
                {
                    if (taken[s]) continue;
                    int sx = sources[s] % columns, sy = sources[s] / columns;
                    int d = (sx - tx) * (sx - tx) + (sy - ty) * (sy - ty);
                    if (d < bestDistance) { bestDistance = d; bestIndex = s; }
                }
                if (bestIndex < 0) { result[t] = -1; continue; }

                taken[bestIndex] = true;
                result[t] = sources[bestIndex];
            }
            return result;
        }

        private ShufflePlan PlanShuffleStatic(int minLen, int maxLen, int movesLeft)
        {
            int cellsLeft = VisibleAliveCount();
            int[] block = PickCompactBlock(cellsLeft);
            if (block == null) return null;

            var active = new bool[this.Level.Geometry.CellCount];
            foreach (int cell in block) active[cell] = true;
            BoardGeometry geo = new BoardGeometry(
                this.Level.Geometry.Columns, this.Level.Geometry.Rows, active);

            // Dấu đi THEO Ô về chỗ mới. Tính trước một lần vì phép ghép không phụ thuộc
            // vào phân hoạch nào được chọn.
            int[] pairedSource = PairSourcesToTargets(block);
            CellMark[] movedMarks = BuildMovedMarks(block, pairedSource);

            List<List<int>> best = null;
            int bestSeed = 0;
            int bestMoves = int.MaxValue;

            for (int attempt = 0; attempt < 300; attempt++)
            {
                int seed = this.Level.Config.Seed + 100003 * this.shuffleCounter + attempt * 7919;
                // phải truyền luật chuỗi, không thì bàn xáo ra có nước không đánh được
                List<List<int>> paths = StaticLevelGenerator.TryPartition(
                    geo, minLen, maxLen, this.Level.MinChain, this.Level.MaxChain, new DeterministicRng(seed));
                if (paths == null) continue;

                // Bộ đường mới phải còn CHƠI ĐƯỢC với các dấu đã dời chỗ, nếu không thì
                // xáo xong người chơi nhận một bàn không mở khoá nổi — tệ hơn cả bàn bí
                // ban đầu, vì họ đã tiêu mất một lượt xáo.
                if (!MarksPlayableWith(paths, movedMarks)) continue;

                int need = RequiredMovesFor(paths, movedMarks);

                // Nhận phân hoạch ĐẦU TIÊN vừa ngân sách, KHÔNG đi tìm cái ít đường nhất.
                // Tìm cái ít nhất biến xáo lại từ phao cứu sinh thành công cụ tối ưu: bàn
                // xáo ra dễ hơn bàn cũ, nên người chơi xáo để ăn điểm chứ không phải để
                // thoát bí — đo được bot dùng xáo để thắng dư tới 8 lượt.
                if (need <= movesLeft) { best = paths; bestSeed = seed; bestMoves = need; break; }
                if (best == null || need < bestMoves) { best = paths; bestSeed = seed; bestMoves = need; }
            }
            if (best == null) return null;

            int[] colorOfPath = StaticLevelGenerator.AssignColors(
                geo, best, this.Level.Config.Colors, this.Level.Config.Fuse, new DeterministicRng(bestSeed + 13));

            RefreshBombFuses(best, movedMarks);

            return new ShufflePlan
            {
                RequiredMoves = bestMoves, Paths = best,
                ColorOfPath = colorOfPath, TargetCells = block, Marks = movedMarks
            };
        }

        /// <summary>Chép dấu từ ô nguồn sang đúng ô đích mà nó sẽ dời về.</summary>
        private CellMark[] BuildMovedMarks(int[] targets, int[] pairedSource)
        {
            var marks = new CellMark[this.Level.Geometry.CellCount];
            if (this.Marks == null) return marks;

            for (int t = 0; t < targets.Length; t++)
            {
                int source = pairedSource[t];
                if (source < 0 || this.Marks[source] == null) continue;
                marks[targets[t]] = this.Marks[source].Clone();
            }
            return marks;
        }

        /// <summary>
        /// Bộ đường mới có chơi được với các dấu này không.
        ///
        /// Hai điều kiện, đều là điều kiện mà lúc SINH MÀN đã phải bảo đảm — xáo lại
        /// dựng một bố cục hoàn toàn mới nên phải kiểm lại y hệt:
        ///   · mỗi đường tối đa 1 ô đa sắc (chuỗi chỉ được chứa 1);
        ///   · mỗi ô băng phải kề đủ số đường ĐƯỢC ĂN TRƯỚC nó bằng máu của nó.
        /// </summary>
        private bool MarksPlayableWith(List<List<int>> paths, CellMark[] marks)
        {
            var pathOf = new int[this.Level.Geometry.CellCount];
            for (int i = 0; i < pathOf.Length; i++) pathOf[i] = -1;
            for (int p = 0; p < paths.Count; p++)
                foreach (int c in paths[p]) pathOf[c] = p;

            // đa sắc: tối đa 1 mỗi đường
            foreach (List<int> path in paths)
            {
                int wilds = 0;
                foreach (int c in path)
                    if (marks[c] != null && marks[c].Kind == CellKind.Wild) wilds++;
                if (wilds > 1) return false;
            }

            // băng: phải có đủ nguồn tan nằm ở đường được ăn TRƯỚC
            var playedSet = GoalPathSet(paths, marks);
            for (int c = 0; c < marks.Length; c++)
            {
                CellMark m = marks[c];
                if (m == null || m.Kind != CellKind.Ice || m.Hp <= 0) continue;
                if (pathOf[c] < 0) return false;                    // băng rơi ra ngoài bộ đường
                if (playedSet != null && !playedSet.Contains(pathOf[c])) return false;

                int earlier = 0;
                var seen = new HashSet<int>();
                foreach (int j in this.Level.Geometry.Neighbors[c])
                {
                    int pj = pathOf[j];
                    if (pj < 0 || pj >= pathOf[c] || !seen.Add(pj)) continue;
                    if (playedSet != null && !playedSet.Contains(pj)) continue;
                    earlier++;
                }
                if (earlier < m.Hp) return false;
            }
            return true;
        }

        /// <summary>
        /// Tập chỉ số đường sẽ THẬT SỰ được chơi. Màn thường là tất cả (trả null cho
        /// gọn); màn mục tiêu chỉ là các đường có chứa ô đích.
        /// </summary>
        private HashSet<int> GoalPathSet(List<List<int>> paths, CellMark[] marks)
        {
            if (!this.Level.GoalMode) return null;
            var set = new HashSet<int>();
            for (int p = 0; p < paths.Count; p++)
                foreach (int c in paths[p])
                    if (marks[c] != null && marks[c].Goal) { set.Add(p); break; }
            return set;
        }

        /// <summary>Số nước bộ đường này thật sự đòi hỏi — màn mục tiêu chỉ tính đường có đích.</summary>
        private int RequiredMovesFor(List<List<int>> paths, CellMark[] marks)
        {
            HashSet<int> goalPaths = GoalPathSet(paths, marks);
            return goalPaths != null ? goalPaths.Count : paths.Count;
        }

        /// <summary>
        /// Đặt lại số đếm ngược của ngòi theo THỨ TỰ MỚI. Bàn vừa đổi hết bố cục, con
        /// số cũ được tính cho một lời giải không còn tồn tại nên có thể không còn kịp.
        /// </summary>
        private void RefreshBombFuses(List<List<int>> paths, CellMark[] marks)
        {
            int slack = this.Level.Config.BombSlack;
            for (int p = 0; p < paths.Count; p++)
                foreach (int c in paths[p])
                {
                    CellMark m = marks[c];
                    if (m != null && m.Kind == CellKind.Bomb)
                        m.Fuse = this.MovesUsed + p + 1 + slack;
                }
        }

        /// <summary>
        /// Chiều cao cột mới sau khi dồn: một dải cột LIỀN NHAU, căn giữa bàn, cao đều.
        ///
        /// Giữ nguyên chiều cao cũ là chỗ chết của xáo lại ở màn gravity: ô chỉ rơi
        /// trong cột, nên nếu ô còn lại nằm ở cột 0 và cột 6 thì tô màu kiểu gì chúng
        /// cũng không kề nhau được. Dải liền nhau thì mọi cột đều có ô, cột nào cũng
        /// kề cột bên, và luôn tồn tại lời giải.
        /// </summary>
        private int[] CompactHeights(int cellsLeft)
        {
            int columns = this.Level.Geometry.Columns;
            int rows = this.Level.Geometry.Rows;
            if (cellsLeft <= 0) return new int[columns];

            // Đủ cột để không phải đẩy ô vào hàng chờ, nhưng cũng đủ chiều ngang để nối:
            // nhắm mỗi cột cao khoảng 4 ô.
            int needForHeight = (cellsLeft + rows - 1) / rows;
            int preferred = (cellsLeft + 3) / 4;
            int used = Math.Max(needForHeight, Math.Min(preferred, columns));
            used = Math.Max(1, Math.Min(used, columns));

            var heights = new int[columns];
            int start = (columns - used) / 2;                  // căn giữa
            int baseHeight = cellsLeft / used;
            int extra = cellsLeft % used;
            for (int k = 0; k < used; k++)
                heights[start + k] = baseHeight + (k < extra ? 1 : 0);

            return heights;
        }

        private ShufflePlan PlanShuffleGravity(int minLen, int maxLen, int movesLeft)
        {
            int cellsLeft = TotalLeft();
            int[] heights = CompactHeights(cellsLeft);

            GravityPlan best = null;
            int bestMoves = int.MaxValue;
            for (int attempt = 0; attempt < 250; attempt++)
            {
                int seed = this.Level.Config.Seed + 100003 * this.shuffleCounter + attempt * 7919;
                GravityPlan plan = GravityLevelGenerator.Simulate(
                    this.Level.Config, new DeterministicRng(seed), heights, minLen, maxLen,
                    this.Level.MinChain, this.Level.MaxChain);
                if (plan == null) continue;

                // Đa sắc: một chuỗi chỉ được chứa 1 ô, nên nước nào gom 2 ô đa sắc là
                // nước KHÔNG đánh được — loại thẳng bộ đó.
                if (!GravityMarksPlayable(plan)) continue;

                // Nhận cái ĐẦU TIÊN vừa ngân sách — xem ghi chú ở PlanShuffleStatic.
                int need = GravityRequiredMoves(plan);
                if (need <= movesLeft) { best = plan; bestMoves = need; break; }
                if (best == null || need < bestMoves) { best = plan; bestMoves = need; }
            }
            if (best == null) return null;

            CellMark[][] markColumns = CarryGravityMarks(best);
            RefreshGravityBombFuses(best, markColumns);

            return new ShufflePlan
            {
                RequiredMoves = bestMoves, Gravity = best, TargetHeights = heights,
                MarkColumns = markColumns
            };
        }

        /// <summary>Dấu ở màn gravity giữ THEO VỊ TRÍ trong cột — ô đích không được nhảy chỗ.</summary>
        private CellMark[][] CarryGravityMarks(GravityPlan plan)
        {
            var result = new CellMark[plan.Columns.Length][];
            for (int x = 0; x < plan.Columns.Length; x++)
            {
                result[x] = new CellMark[plan.Columns[x].Length];
                if (this.Stacks == null || x >= this.Stacks.Length) continue;
                for (int k = 0; k < result[x].Length && k < this.Stacks[x].Count; k++)
                    result[x][k] = this.Stacks[x][k].Mark?.Clone();
            }
            return result;
        }

        private CellMark MarkAtSlot(int column, int slot)
        {
            if (this.Stacks == null || column >= this.Stacks.Length) return null;
            return slot < this.Stacks[column].Count ? this.Stacks[column][slot].Mark : null;
        }

        private bool GravityMarksPlayable(GravityPlan plan)
        {
            foreach (List<SlotRef> move in plan.Solution)
            {
                int wilds = 0;
                foreach (SlotRef s in move)
                {
                    CellMark m = MarkAtSlot(s.Column, s.Slot);
                    if (m != null && m.Kind == CellKind.Wild) wilds++;
                }
                if (wilds > 1) return false;
            }
            return true;
        }

        /// <summary>Màn mục tiêu chỉ cần chạy tới nước cuối cùng còn chạm ô đích.</summary>
        private int GravityRequiredMoves(GravityPlan plan)
        {
            if (!this.Level.GoalMode) return plan.Solution.Count;

            int last = -1;
            for (int t = 0; t < plan.Solution.Count; t++)
                foreach (SlotRef s in plan.Solution[t])
                {
                    CellMark m = MarkAtSlot(s.Column, s.Slot);
                    if (m != null && m.Goal) { last = t; break; }
                }
            return last < 0 ? int.MaxValue : last + 1;      // bỏ sót đích => bộ này vô dụng
        }

        private void RefreshGravityBombFuses(GravityPlan plan, CellMark[][] marks)
        {
            int slack = this.Level.Config.BombSlack;
            for (int t = 0; t < plan.Solution.Count; t++)
                foreach (SlotRef s in plan.Solution[t])
                {
                    if (s.Column >= marks.Length || s.Slot >= marks[s.Column].Length) continue;
                    CellMark m = marks[s.Column][s.Slot];
                    if (m != null && m.Kind == CellKind.Bomb)
                        m.Fuse = this.MovesUsed + t + 1 + slack;
                }
        }

        /// <summary>
        /// Áp dụng kế hoạch xáo. Tiêu 1 lượt xáo, đẩy snapshot nên hoàn tác được.
        /// Trả về danh sách ô nào trượt từ đâu tới đâu, để tầng hiển thị vẽ đường trượt.
        /// </summary>
        public List<ShuffleMove> ApplyShuffle(ShufflePlan plan)
        {
            this.history.Add(Capture());
            this.ShufflesLeft--;
            ClearSelection();

            List<ShuffleMove> moves = this.Level.Gravity
                ? BuildGravityMoves(plan)
                : BuildStaticMoves(plan);

            if (this.Level.Gravity)
            {
                int columns = this.Level.Geometry.Columns;
                this.Stacks = new List<SlotCell>[columns];
                for (int x = 0; x < columns; x++)
                {
                    int[] column = plan.Gravity.Columns[x];
                    var stack = new List<SlotCell>(column.Length);
                    // slot được đánh lại từ 0 để khớp lời giải mới; dấu giữ THEO VỊ TRÍ
                    // trong cột, không thì ngòi nổ và vòng đích biến mất sau khi xáo.
                    for (int k = 0; k < column.Length; k++)
                    {
                        CellMark mark = plan.MarkColumns != null && x < plan.MarkColumns.Length &&
                                        k < plan.MarkColumns[x].Length
                            ? plan.MarkColumns[x][k]
                            : null;
                        stack.Add(new SlotCell(column[k], k, mark));
                    }
                    this.Stacks[x] = stack;
                }
                this.Level.Solution = plan.Gravity.Solution;
                SyncBoardFromStacks();
            }
            else
            {
                // Phải DỌN hết ô sống cũ trước: các ô nằm ngoài khối được dồn về sẽ
                // không được ghi lại, không dọn thì chúng còn nguyên và tổng số ô tăng lên.
                for (int i = 0; i < this.Board.Length; i++)
                    if (this.Board[i] >= 0) { this.Board[i] = Empty; this.Marks[i] = null; }

                for (int p = 0; p < plan.Paths.Count; p++)
                    foreach (int i in plan.Paths[p]) this.Board[i] = plan.ColorOfPath[p];

                // Dấu đã đi theo ô về chỗ mới ngay từ lúc lập kế hoạch.
                if (plan.Marks != null) this.Marks = plan.Marks;
                this.Level.Paths = plan.Paths;
            }

            // Ô đích có thể đã dời chỗ; đếm lại cho chắc thay vì tin số cũ.
            if (this.Level.GoalMode) this.GoalsLeft = CountGoalsOnBoard();

            return moves;
        }

        /// <summary>
        /// Ghép ô cũ với chỗ mới theo kiểu THAM LAM GẦN NHẤT: mỗi đích lấy ô nguồn còn
        /// trống gần nó nhất. Ghép bừa thì các đường trượt cắt chéo nhau loạn xạ.
        /// </summary>
        private List<ShuffleMove> BuildStaticMoves(ShufflePlan plan)
        {
            var moves = new List<ShuffleMove>();
            if (plan.TargetCells == null) return moves;

            int columns = this.Level.Geometry.Columns;
            List<int> sources = AliveVisibleCells();
            var taken = new bool[sources.Count];

            // màu mà từng ô đích sẽ nhận, tra nhanh khi dựng ánh xạ
            var targetColor = new Dictionary<int, int>();
            for (int p = 0; p < plan.Paths.Count; p++)
                foreach (int cell in plan.Paths[p]) targetColor[cell] = plan.ColorOfPath[p];

            foreach (int target in plan.TargetCells)
            {
                int tx = target % columns, ty = target / columns;
                int bestIndex = -1, bestDistance = int.MaxValue;

                for (int s = 0; s < sources.Count; s++)
                {
                    if (taken[s]) continue;
                    int sx = sources[s] % columns, sy = sources[s] / columns;
                    int d = (sx - tx) * (sx - tx) + (sy - ty) * (sy - ty);
                    if (d < bestDistance) { bestDistance = d; bestIndex = s; }
                }
                if (bestIndex < 0) break;

                taken[bestIndex] = true;
                int source = sources[bestIndex];
                moves.Add(new ShuffleMove
                {
                    FromColumn = source % columns,
                    FromRow = source / columns,
                    ToColumn = tx,
                    ToRow = ty,
                    FromColor = this.Board[source],
                    ToColor = targetColor.TryGetValue(target, out int c) ? c : this.Board[source]
                });
            }
            return moves;
        }

        /// <summary>
        /// Ghép theo thứ tự trái-sang-phải, dưới-lên. Ô ở cột ngoài cùng bên trái đi về
        /// cột trái nhất của dải mới, nên cả bàn trông như dồn vào giữa thay vì đảo lộn.
        /// </summary>
        private List<ShuffleMove> BuildGravityMoves(ShufflePlan plan)
        {
            var moves = new List<ShuffleMove>();
            if (plan.Gravity == null) return moves;

            int rows = this.Level.Geometry.Rows;

            var sources = new List<ShuffleMove>();
            for (int x = 0; x < this.Stacks.Length; x++)
                for (int k = 0; k < this.Stacks[x].Count; k++)
                    sources.Add(new ShuffleMove
                    {
                        FromColumn = x,
                        FromRow = rows - 1 - k,
                        FromColor = this.Stacks[x][k].Color
                    });

            int index = 0;
            for (int x = 0; x < plan.Gravity.Columns.Length; x++)
                for (int k = 0; k < plan.Gravity.Columns[x].Length; k++)
                {
                    if (index >= sources.Count) break;
                    ShuffleMove move = sources[index++];
                    move.ToColumn = x;
                    move.ToRow = rows - 1 - k;
                    move.ToColor = plan.Gravity.Columns[x][k];
                    moves.Add(move);
                }
            return moves;
        }

        // ------------------------------------------------------------------
        // Gợi ý — một nhóm trong lời giải tham chiếu còn nguyên vẹn và đang thấy
        // ------------------------------------------------------------------

        public int[] FindHint()
        {
            if (this.Level.Gravity)
            {
                int columns = this.Level.Geometry.Columns;
                int rows = this.Level.Geometry.Rows;
                var position = new Dictionary<long, int>();
                for (int x = 0; x < columns; x++)
                {
                    List<SlotCell> stack = this.Stacks[x];
                    int visible = stack.Count < rows ? stack.Count : rows;
                    for (int index = 0; index < visible; index++)
                        position[SlotKey(x, stack[index].Slot)] = (rows - 1 - index) * columns + x;
                }

                foreach (List<SlotRef> move in this.Level.Solution)
                {
                    var cells = new int[move.Count];
                    bool complete = true;
                    for (int i = 0; i < move.Count; i++)
                    {
                        if (!position.TryGetValue(SlotKey(move[i].Column, move[i].Slot), out int cell))
                        {
                            complete = false;
                            break;
                        }
                        cells[i] = cell;
                    }
                    if (complete) return cells;
                }
                return null;
            }

            foreach (List<int> path in this.Level.Paths)
            {
                bool intact = true;
                foreach (int i in path) if (this.Board[i] < 0) { intact = false; break; }
                if (intact) return path.ToArray();
            }
            return null;
        }

        internal static long SlotKey(int column, int slot) => ((long)column << 32) | (uint)slot;

        // ------------------------------------------------------------------

        public LossReason Analyze() => LossAnalyzer.Analyze(this);

        /// <summary>
        /// Chỉ dùng để KIỂM THỬ: dựng trạng thái nhân tạo (bàn đã hỏng, sát hết lượt)
        /// để kiểm tra bộ phát hiện thua mà không phải chơi thật tới đó.
        /// Không gọi trong luồng game.
        /// </summary>
        internal void TestSetState(int movesUsed, int[] board, List<SlotCell>[] stacks)
        {
            this.MovesUsed = movesUsed;
            if (board != null) this.Board = board;
            if (stacks != null)
            {
                this.Stacks = stacks;
                SyncBoardFromStacks();
            }
            ClearSelection();
        }
    }
}
