using System.Collections.Generic;

namespace ConnectPuzzle.Core
{
    /// <summary>
    /// Hình học bàn: ô nào thuộc bàn, và danh sách ô kề của từng ô.
    /// Kề = 8 hướng, giống luật gốc của Ilumisoft (Vector2.Distance &lt; 1.5 * CellSize).
    ///
    /// Chỉ số ô là index phẳng: index = row * Columns + column, row 0 ở TRÊN.
    /// </summary>
    public sealed class BoardGeometry
    {
        private static readonly int[] DirX = { -1, 0, 1, -1, 1, -1, 0, 1 };
        private static readonly int[] DirY = { -1, -1, -1, 0, 0, 1, 1, 1 };

        public readonly int Columns;
        public readonly int Rows;
        public readonly bool[] Active;
        public readonly int[][] Neighbors;
        public readonly int[] Cells;

        public int CellCount => this.Columns * this.Rows;

        public BoardGeometry(int columns, int rows, bool[] active)
        {
            this.Columns = columns;
            this.Rows = rows;
            this.Active = active;

            this.Neighbors = new int[columns * rows][];
            var cells = new List<int>();
            var buffer = new List<int>(8);

            for (int i = 0; i < columns * rows; i++)
            {
                if (!active[i])
                {
                    this.Neighbors[i] = new int[0];
                    continue;
                }

                cells.Add(i);
                int x = i % columns;
                int y = i / columns;
                buffer.Clear();

                for (int d = 0; d < 8; d++)
                {
                    int nx = x + DirX[d];
                    int ny = y + DirY[d];
                    if (nx < 0 || ny < 0 || nx >= columns || ny >= rows) continue;
                    int j = ny * columns + nx;
                    if (active[j]) buffer.Add(j);
                }

                this.Neighbors[i] = buffer.ToArray();
            }

            this.Cells = cells.ToArray();
        }

        /// <summary>Hình học từ config: dùng Shape nếu có, không thì chữ nhật đầy.</summary>
        public static BoardGeometry FromConfig(LevelConfig cfg)
        {
            int columns, rows;
            if (cfg.Shape != null && cfg.Shape.Length > 0)
            {
                rows = cfg.Shape.Length;
                columns = cfg.Shape[0].Length;
            }
            else
            {
                columns = cfg.Columns;
                rows = cfg.Rows;
            }

            var active = new bool[columns * rows];
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                    active[y * columns + x] = cfg.Shape == null || cfg.Shape[y][x] == '#';

            return new BoardGeometry(columns, rows, active);
        }

        /// <summary>Hình học từ một mặt nạ ô sống bất kỳ.</summary>
        public static BoardGeometry FromMask(int columns, int rows, bool[] active)
        {
            return new BoardGeometry(columns, rows, active);
        }

        /// <summary>Hình chữ nhật đầy.</summary>
        public static BoardGeometry Rectangle(int columns, int rows)
        {
            var active = new bool[columns * rows];
            for (int i = 0; i < active.Length; i++) active[i] = true;
            return new BoardGeometry(columns, rows, active);
        }

        /// <summary>
        /// Hình học chỉ gồm các ô đang còn sống — dùng khi XÁO LẠI, để phân hoạch
        /// lại đúng phần bàn còn lại thay vì cả bàn.
        /// </summary>
        public static BoardGeometry FromAliveCells(int columns, int rows, int[] board)
        {
            var active = new bool[columns * rows];
            for (int i = 0; i < board.Length; i++) active[i] = board[i] >= 0;
            return new BoardGeometry(columns, rows, active);
        }
    }
}
