namespace ConnectPuzzle.Core
{
    /// <summary>
    /// Màn của chế độ vô tận.
    ///
    /// Không lượt, không mục tiêu, không par, và KHÔNG có lời giải tham chiếu — ở đây
    /// không có gì để bảo đảm giải được, vì bàn đổ đầy lại sau mỗi nước. Đổi lại phải
    /// bảo đảm điều khác, và chỗ đó nằm trong PuzzleSession.RefillEndless: sau mỗi lần
    /// rớt, bàn phải còn ít nhất một nước đi.
    /// </summary>
    public static class EndlessLevel
    {
        public static LevelData Build()
        {
            var cfg = new LevelConfig
            {
                World = 0,
                Name = "Vô tận",
                Columns = EndlessRules.Columns,
                Rows = EndlessRules.Rows,
                Colors = EndlessRules.ColorsFor(0),
                Gravity = true,
                MinChain = 2,
                MaxChain = 0,
                Undos = 0,
                Shuffles = EndlessRules.Shuffles
            };

            return new LevelData
            {
                Config = cfg,
                Geometry = BoardGeometry.Rectangle(EndlessRules.Columns, EndlessRules.Rows),
                Gravity = true,
                Endless = true,
                Columns = new int[EndlessRules.Columns][],
                Solution = new System.Collections.Generic.List<System.Collections.Generic.List<SlotRef>>(),
                TotalCells = EndlessRules.Columns * EndlessRules.Rows,
                VisibleCells = EndlessRules.Columns * EndlessRules.Rows,
                Par = 0,

                // Không có trần lượt: mọi phép so "còn bao nhiêu lượt" trong game đều
                // phải trở thành vô nghĩa thay vì trở thành 0.
                MaxMoves = int.MaxValue,
                TwoStarMoves = int.MaxValue,
                Undos = 0,
                Shuffles = EndlessRules.Shuffles,
                MinChain = 2,
                MaxChain = int.MaxValue
            };
        }
    }
}
