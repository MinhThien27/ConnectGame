using System.Collections.Generic;

namespace ConnectPuzzle.Core
{
    public enum LossKind
    {
        OutOfMoves,
        Deadlock,
        LonelyColor,
        ColumnIsolated,
        NotEnoughMovesForColors,
        NotEnoughMovesForGroups
    }

    /// <summary>
    /// Lý do thua, kèm BẰNG CHỨNG là các ô cụ thể để màn hình chỉ thẳng vào chỗ sai
    /// thay vì chỉ hiện một panel.
    /// </summary>
    public sealed class LossReason
    {
        public LossKind Kind;
        public string Title;

        /// <summary>Câu dài cho panel.</summary>
        public string Detail;

        /// <summary>Câu ngắn cho banner trên bàn.</summary>
        public string Hint;

        /// <summary>
        /// Các nhóm ô để thắp sáng lần lượt. Nhiều nhóm thì màn hình đánh số 1-2-3-4,
        /// biến "4 nhóm rời nhau" thành thứ đếm được bằng mắt. Có thể rỗng khi bằng
        /// chứng đang nằm trong hàng chờ, không hiện trên bàn.
        /// </summary>
        public List<int[]> EvidenceGroups = new List<int[]>();
    }

    /// <summary>
    /// Xác định khi nào KHÔNG THỂ THẮNG NỮA.
    ///
    /// Mọi kiểm tra ở đây đều CHẮC CHẮN ĐÚNG, không phỏng đoán: nếu báo thua thì
    /// thật sự không còn cách nào thắng. Chiều ngược lại KHÔNG được bảo đảm — trả
    /// về null chỉ nghĩa là chưa chứng minh được là thua, bàn vẫn có thể đã hỏng
    /// theo cách tinh vi hơn. Muốn biết chắc phải chạy solver đầy đủ.
    /// </summary>
    public static class LossAnalyzer
    {
        public static LossReason OutOfMoves(PuzzleSession session)
        {
            string what = session.Level.GoalMode
                ? session.GoalsLeft + " ô đích"
                : session.TotalLeft() + " ô";
            return new LossReason
            {
                Kind = LossKind.OutOfMoves,
                Title = "Hết lượt",
                Detail = "Còn " + what + " chưa dọn.",
                Hint = "Còn " + what + " chưa dọn",
                EvidenceGroups = { session.GoalOrAliveCells().ToArray() }
            };
        }

        /// <summary>Ngòi nổ đã cháy hết — lý do cụ thể hơn hết lượt nên xét trước.</summary>
        public static LossReason BombsBlown(PuzzleSession session)
        {
            List<int> blown = session.BlownBombs();
            if (blown.Count == 0) return null;
            return new LossReason
            {
                Kind = LossKind.OutOfMoves,
                Title = "Ngòi nổ cháy hết",
                Detail = blown.Count + " ô mang ngòi hết giờ trước khi bạn dọn tới.",
                Hint = "Ngòi nổ hết giờ",
                EvidenceGroups = { blown.ToArray() }
            };
        }

        public static LossReason Analyze(PuzzleSession session)
        {
            LevelData level = session.Level;
            int movesLeft = session.MovesLeft;

            /* Các cận (2)-(5) đều suy ra từ giả định "phải dọn SẠCH bàn". Giả định đó
               SAI ở hai chỗ:
                 · màn mục tiêu — thắng chỉ cần chạm hết đích, phần bàn còn lại kệ nó;
                 · chế độ vô tận — bàn đổ đầy lại nên "còn bao nhiêu ô" vô nghĩa.
               Dùng lại các cận đó ở đây sẽ báo thua oan ngay lúc người chơi vẫn còn
               đường thắng, nên hai chế độ này chỉ giữ kiểm tra (1). */
            bool clearAll = !level.GoalMode && !level.Endless;

            // (1) Không còn hai ô cùng màu kề nhau => không đi được nước nào.
            if (!session.HasMove())
            {
                var reason = new LossReason
                {
                    Kind = LossKind.Deadlock,
                    Title = "Bế tắc",
                    Detail = "Không còn hai ô cùng màu nào kề nhau" +
                             (level.Gravity && session.QueueLeft() > 0
                                 ? ", mà hàng chờ chỉ tụt xuống khi có ô bị ăn."
                                 : "."),
                    Hint = "Không ô nào còn ô cùng màu bên cạnh"
                };
                reason.EvidenceGroups.Add(session.AliveVisibleCells().ToArray());
                return reason;
            }

            if (!clearAll) return null;

            int[] count = session.CountByColor();
            int minChain = level.MinChain;

            // Ô đa sắc ghép được mọi màu, nên nó vừa cứu được ô lẻ vừa nối được hai
            // nhóm khác màu. Còn ô đa sắc trên bàn thì mọi cận bên dưới thôi đúng.
            bool anyWild = session.WildsLeft() > 0;

            // (2) Một màu còn ít hơn MinChain ô => số ô đó không bao giờ ăn được.
            for (int c = 0; c < count.Length && !anyWild; c++)
            {
                if (count[c] == 0 || count[c] >= minChain) continue;

                var spot = new List<int>();
                foreach (int i in session.AliveVisibleCells()) if (session.Board[i] == c) spot.Add(i);
                bool visible = spot.Count > 0;

                var reason = new LossReason
                {
                    Kind = LossKind.LonelyColor,
                    Title = "Không thể dọn sạch",
                    Detail = "Màu này chỉ còn " + count[c] + " ô mà chuỗi phải từ " + minChain +
                             " ô trở lên — số ô đó không bao giờ ăn được nữa." +
                             (visible ? "" : "\nChúng đang nằm trong hàng chờ nên chưa thấy trên bàn."),
                    Hint = visible
                        ? (count[c] == 1 ? "Ô này bị bỏ lại một mình" : "Còn " + count[c] + " ô, thiếu để tạo chuỗi")
                        : "Số ô màu này còn lại đang nằm trong hàng chờ"
                };
                if (visible) reason.EvidenceGroups.Add(spot.ToArray());
                return reason;
            }

            // (2b) Gravity: ô rơi xuống nhưng KHÔNG BAO GIỜ đổi cột. Hai ô cách nhau từ
            //      2 cột trở lên thì vĩnh viễn không thể kề nhau, dù bàn rơi thế nào.
            //      Nếu một màu chỉ còn đúng 1 ô trong cả dải cột liền nhau của nó thì ô
            //      đó không bao giờ tìm được bạn cùng màu.
            if (level.Gravity && !anyWild)
            {
                LossReason isolated = FindColumnIsolated(session, count);
                if (isolated != null) return isolated;
            }

            // (2c) Đá chỉ vỡ khi có chuỗi bị ăn KỀ nó. Hòn nào không còn ô màu nào bên
            //      cạnh thì vĩnh viễn không ai chạm tới được.
            // (2b-bis) Băng chỉ tan khi có chuỗi bị ăn KỀ nó. Hòn nào không còn ô dùng
            //          được bên cạnh thì vĩnh viễn không ai mở khoá được — và vì băng
            //          là ô CÓ MÀU, nó vẫn nằm trong "còn phải dọn", nên đây là thua.
            if (level.Config.Ices > 0)
            {
                var stuck = new List<int>();
                int[][] iceNeighbors = level.Geometry.Neighbors;
                for (int i = 0; i < session.Board.Length; i++)
                {
                    if (!session.IsFrozen(i)) continue;
                    bool reachable = false;
                    foreach (int j in iceNeighbors[i])
                        if (PuzzleSession.IsColor(session.Board[j]) && !session.IsFrozen(j))
                        { reachable = true; break; }
                    if (!reachable) stuck.Add(i);
                }
                if (stuck.Count > 0)
                {
                    var reason = new LossReason
                    {
                        Kind = LossKind.LonelyColor,
                        Title = "Băng không tan được",
                        Detail = stuck.Count + " ô băng không còn ô nào dùng được bên cạnh. Băng chỉ tan " +
                                 "khi có chuỗi bị ăn ngay cạnh nó, nên chúng sẽ đóng băng mãi.",
                        Hint = "Ô băng này không còn gì bên cạnh để làm tan"
                    };
                    reason.EvidenceGroups.Add(stuck.ToArray());
                    return reason;
                }
            }

            if (level.Config.Stones > 0)
            {
                var deadStones = new List<int>();
                int[][] neighbors = level.Geometry.Neighbors;
                for (int i = 0; i < session.Board.Length; i++)
                {
                    if (session.Board[i] != PuzzleSession.Stone) continue;
                    bool reachable = false;
                    foreach (int j in neighbors[i])
                        if (PuzzleSession.IsColor(session.Board[j])) { reachable = true; break; }
                    if (!reachable) deadStones.Add(i);
                }
                if (deadStones.Count > 0)
                {
                    var reason = new LossReason
                    {
                        Kind = LossKind.LonelyColor,
                        Title = "Đá không phá được",
                        Detail = deadStones.Count + " hòn đá không còn ô màu nào bên cạnh. Đá chỉ vỡ khi có " +
                                 "chuỗi bị ăn ngay cạnh nó, nên chúng sẽ nằm đó mãi.",
                        Hint = "Đá này không còn ô nào bên cạnh để phá"
                    };
                    reason.EvidenceGroups.Add(deadStones.ToArray());
                    return reason;
                }
            }

            // (3) Mỗi nước chỉ ăn ĐÚNG 1 MÀU và tối đa MaxChain ô, nên riêng màu c đã
            //     cần ceil(count/MaxChain) lượt. Cộng lại được cận dưới cho cả bàn —
            //     mạnh hơn hẳn cận "số màu còn lại" khi có trần chuỗi.
            int colorsLeft = 0;
            long movesNeeded = 0;
            for (int c = 0; c < count.Length; c++)
            {
                if (count[c] == 0) continue;
                colorsLeft++;
                movesNeeded += level.MaxChain == int.MaxValue
                    ? 1
                    : (count[c] + level.MaxChain - 1) / level.MaxChain;
            }

            if (movesNeeded > movesLeft && movesNeeded > colorsLeft)
            {
                var reason = new LossReason
                {
                    Kind = LossKind.NotEnoughMovesForColors,
                    Title = "Không đủ lượt",
                    Detail = "Mỗi lượt chỉ ăn được 1 màu và nhiều nhất " + level.MaxChain + " ô, nên riêng " +
                             "số ô còn lại đã cần ít nhất " + movesNeeded + " lượt, trong khi chỉ còn " +
                             movesLeft + " lượt.",
                    Hint = "Cần ít nhất " + movesNeeded + " lượt nữa mà chỉ còn " + movesLeft
                };
                AddColorGroups(session, count, reason);
                return reason;
            }

            if (colorsLeft > movesLeft)
            {
                var reason = new LossReason
                {
                    Kind = LossKind.NotEnoughMovesForColors,
                    Title = "Không đủ lượt",
                    Detail = "Còn " + colorsLeft + " màu trên bàn mà mỗi lượt chỉ ăn được 1 màu, " +
                             "trong khi chỉ còn " + movesLeft + " lượt.",
                    Hint = colorsLeft + " màu còn lại, mỗi lượt chỉ ăn được 1 màu — mà còn " + movesLeft + " lượt"
                };
                AddColorGroups(session, count, reason);
                return reason;
            }

            // (4) Bàn tĩnh: mỗi nước chỉ ăn trong MỘT nhóm liên thông, và ăn ô không bao
            //     giờ làm hai nhóm nhập lại => cần ít nhất "số nhóm" lượt.
            //     Với gravity thì cận này KHÔNG đúng: ô rơi xuống có thể nhập hai nhóm.
            if (!level.Gravity && !anyWild)
            {
                List<List<int>> components = session.ColorComponents();

                // (4a) Bàn tĩnh: nhóm rời nhau KHÔNG BAO GIỜ nhập lại được. Nhóm nào
                //      nhỏ hơn MinChain thì cả nhóm đó chết.
                foreach (List<int> component in components)
                {
                    if (component.Count >= minChain) continue;
                    var dead = new LossReason
                    {
                        Kind = LossKind.LonelyColor,
                        Title = "Không thể dọn sạch",
                        Detail = "Nhóm này chỉ có " + component.Count + " ô mà chuỗi phải từ " + minChain +
                                 " ô trở lên. Trên bàn tĩnh các nhóm rời nhau không bao giờ nhập lại, " +
                                 "nên nhóm đó chết hẳn.",
                        Hint = "Nhóm " + component.Count + " ô này quá nhỏ để ăn"
                    };
                    dead.EvidenceGroups.Add(component.ToArray());
                    return dead;
                }

                // (4b) Mỗi lượt chỉ dọn TRONG một nhóm và nhiều nhất MaxChain ô.
                long groupMoves = 0;
                foreach (List<int> component in components)
                    groupMoves += level.MaxChain == int.MaxValue
                        ? 1
                        : (component.Count + level.MaxChain - 1) / level.MaxChain;

                if (groupMoves > movesLeft)
                {
                    var reason = new LossReason
                    {
                        Kind = LossKind.NotEnoughMovesForGroups,
                        Title = "Không đủ lượt",
                        Detail = "Bàn còn " + components.Count + " nhóm rời nhau, mỗi lượt chỉ dọn được trong " +
                                 "một nhóm" + (level.MaxChain == int.MaxValue ? "" : " và nhiều nhất " + level.MaxChain + " ô") +
                                 ", nên cần ít nhất " + groupMoves + " lượt — mà chỉ còn " + movesLeft + " lượt.",
                        Hint = "Cần ít nhất " + groupMoves + " lượt cho các nhóm còn lại, chỉ còn " + movesLeft
                    };
                    foreach (List<int> component in components) reason.EvidenceGroups.Add(component.ToArray());
                    return reason;
                }
            }

            return null;
        }

        private static void AddColorGroups(PuzzleSession session, int[] count, LossReason reason)
        {
            List<int> alive = session.AliveVisibleCells();
            for (int c = 0; c < count.Length; c++)
            {
                if (count[c] == 0) continue;
                var group = new List<int>();
                foreach (int i in alive) if (session.Board[i] == c) group.Add(i);
                if (group.Count > 0) reason.EvidenceGroups.Add(group.ToArray());
            }
        }

        private static LossReason FindColumnIsolated(PuzzleSession session, int[] count)
        {
            int columns = session.Level.Geometry.Columns;
            int rows = session.Level.Geometry.Rows;

            for (int c = 0; c < count.Length; c++)
            {
                if (count[c] == 0) continue;

                var perColumn = new int[columns];
                for (int x = 0; x < columns; x++)
                    foreach (SlotCell cell in session.Stacks[x]) if (cell.Color == c) perColumn[x]++;

                // Cụm = dải cột LIỀN NHAU có ô màu này. Một cột trống là đủ để cắt cụm,
                // vì lúc đó hai bên cách nhau 2 cột và không bao giờ kề được.
                int x0 = 0;
                while (x0 < columns)
                {
                    if (perColumn[x0] == 0) { x0++; continue; }

                    int sum = 0, from = x0;
                    while (x0 < columns && perColumn[x0] > 0) { sum += perColumn[x0]; x0++; }
                    if (sum != 1) continue;

                    int index = session.Stacks[from].FindIndex(cell => cell.Color == c);
                    bool visible = index >= 0 && index < rows;

                    var reason = new LossReason
                    {
                        Kind = LossKind.ColumnIsolated,
                        Title = "Không thể dọn sạch",
                        Detail = "Ô màu này chỉ còn 1 ô trong phạm vi cột quanh nó. Ô rơi xuống nhưng " +
                                 "không bao giờ đổi cột, nên nó vĩnh viễn không có bạn cùng màu để ăn." +
                                 (visible ? "" : "\nNó đang nằm trong hàng chờ nên chưa thấy trên bàn."),
                        Hint = visible ? "Ô này không còn bạn cùng màu nào trong tầm cột"
                                       : "Ô bị cô lập theo cột đang nằm trong hàng chờ"
                    };
                    if (visible)
                        reason.EvidenceGroups.Add(new[] { (rows - 1 - index) * columns + from });
                    return reason;
                }
            }
            return null;
        }
    }
}
