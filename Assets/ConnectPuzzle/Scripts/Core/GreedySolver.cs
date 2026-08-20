using System;
using System.Collections.Generic;

namespace ConnectPuzzle.Core
{
    /// <summary>
    /// Bot chơi tham lam: mỗi lượt ăn chuỗi DÀI NHẤT tìm được trên bàn.
    ///
    /// Dùng cho hai việc:
    ///  1. Ước lượng par. Lời giải của generator chia bàn thành nhóm 3-5 ô, nhưng bàn
    ///     thật (có `fuse`) thường cho phép quét con rắn dài hơn nhiều — đo được par 7
    ///     mà bot chỉ cần 4 lượt. Lấy par = lời giải TỐT NHẤT biết được thì ngân sách
    ///     lượt mới bám sát mức thật sự cần, và mốc 3 sao mới có nghĩa.
    ///  2. Đo độ khó. Màn nào bot này thắng mà còn dư nhiều lượt thì màn đó quá dễ.
    ///
    /// Tất định (không dùng ngẫu nhiên) nên par ổn định giữa các lần chạy.
    /// </summary>
    public static class GreedySolver
    {
        /// <summary>Giới hạn số nhánh DFS cho mỗi ô xuất phát, chặn bùng nổ tổ hợp.</summary>
        private const int NodeBudget = 6000;

        /// <summary>Độ sâu tối đa khi dò, kể cả khi luật không đặt trần chuỗi.</summary>
        private const int SearchDepthCap = 14;

        public sealed class Result
        {
            public bool Cleared;
            public int Moves;
            public int CellsLeft;
        }

        /// <summary>Chơi hết ván bằng lối tham lam. moveCap chặn vòng lặp vô hạn.</summary>
        public static Result Solve(LevelData level, int moveCap)
        {
            var session = new PuzzleSession(level);
            var scratch = new bool[session.Board.Length];

            while (session.TotalLeft() > 0 && session.MovesUsed < moveCap)
            {
                List<int> chain = LongestChain(session, scratch);
                if (chain.Count < level.MinChain) break;

                session.ClearSelection();
                foreach (int cell in chain) session.TryExtendSelection(cell);
                if (session.Selection.Count != chain.Count) break;   // luật từ chối => dừng
                if (session.Commit() == null) break;
            }

            return new Result
            {
                Cleared = session.TotalLeft() == 0,
                Moves = session.MovesUsed,
                CellsLeft = session.TotalLeft()
            };
        }

        /// <summary>Chuỗi dài nhất trên cả bàn, tôn trọng trần độ dài của luật.</summary>
        public static List<int> LongestChain(PuzzleSession session, bool[] scratch)
        {
            int depthCap = Math.Min(session.Level.MaxChain, SearchDepthCap);
            var overall = new List<int>();
            var path = new List<int>();

            for (int i = 0; i < session.Board.Length; i++)
            {
                // Ô ĐANG ĐÓNG BĂNG chưa chọn được. Bỏ sót chỗ này thì bộ giải dựng ra
                // chuỗi mà TryExtendSelection từ chối — và vì ApplyPar dùng chính bộ
                // giải này để chốt par, par sẽ được tính từ một lời giải KHÔNG đánh
                // được, tức ngân sách lượt của màn bị siết theo một con số ảo.
                if (session.Board[i] < 0 || session.IsFrozen(i)) continue;
                // đã có chuỗi đạt trần thì không cần dò tiếp
                if (overall.Count >= depthCap) break;

                Array.Clear(scratch, 0, scratch.Length);
                path.Clear();
                path.Add(i);
                scratch[i] = true;

                int budget = NodeBudget;
                Explore(session, path, scratch, depthCap, ref budget, ref overall);
            }
            return overall;
        }

        private static void Explore(PuzzleSession session, List<int> path, bool[] used,
                                    int depthCap, ref int budget, ref List<int> best)
        {
            if (path.Count > best.Count) best = new List<int>(path);
            if (path.Count >= depthCap || --budget < 0) return;

            int last = path[path.Count - 1];
            int colour = session.Board[last];
            foreach (int j in session.Level.Geometry.Neighbors[last])
            {
                if (used[j] || session.Board[j] != colour || session.IsFrozen(j)) continue;
                used[j] = true;
                path.Add(j);
                Explore(session, path, used, depthCap, ref budget, ref best);
                path.RemoveAt(path.Count - 1);
                used[j] = false;
            }
        }
    }
}
