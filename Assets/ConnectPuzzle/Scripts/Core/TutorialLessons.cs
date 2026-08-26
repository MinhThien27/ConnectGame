using System.Collections.Generic;

namespace ConnectPuzzle.Core
{
    /// <summary>
    /// Một bài học: bàn nhỏ, chuỗi cần đi, và câu luật.
    ///
    /// Bàn được viết bằng CHỮ thay vì dựng bằng code, vì bài học phải chỉ vào đúng một
    /// tình huống — sinh ngẫu nhiên rồi mong nó minh hoạ được cơ chế thì không bao giờ
    /// chắc. Bù lại, bàn viết tay có thể sai (chuỗi không hợp lệ, ô đá không kề chuỗi),
    /// nên mọi bài học đều được CHẠY QUA ENGINE THẬT trong bài kiểm chứ không phải chỉ
    /// đọc bằng mắt.
    ///
    /// Ký tự:
    ///   .  lỗ (không phải ô của bàn)      1-6  ô màu thường
    ///   #  đá                             *    ô đa sắc
    ///   !  ngòi nổ                        ~    băng
    ///   o  ô đích                         =    một đầu cặp liên kết
    /// Bốn ký tự cuối lấy màu từ MarkColor: mỗi bài chỉ dạy MỘT cơ chế, nên một màu
    /// dùng chung là đủ và bàn đọc ra gọn hơn.
    /// </summary>
    public sealed class TutorialLesson
    {
        public int World;
        public string Title;

        /// <summary>Câu luật, hiện trên hình.</summary>
        public string Rule;

        /// <summary>
        /// Câu "khác chỗ nào" — cái mà người chơi ĐÃ biết bị cơ chế này phá vỡ. Có thể
        /// null. Đây là câu đáng giá nhất của bài: luật thì nhìn hình cũng đoán ra,
        /// còn "nó khác đá ở chỗ nào" thì không.
        /// </summary>
        public string Note;

        /// <summary>Các hàng NHÌN THẤY, từ trên xuống.</summary>
        public string[] Rows;

        /// <summary>
        /// Hàng chờ của màn gravity, từ trên xuống, nằm PHÍA TRÊN các hàng nhìn thấy.
        /// null nếu bài không dùng gravity.
        /// </summary>
        public string[] Queue;

        public int MarkColor;
        public int StoneHp = 1;
        public int IceHp = 1;
        public int Fuse = 3;
        public int MinChain = 2;

        /// <summary>0 = không có trần chuỗi.</summary>
        public int MaxChain;

        /// <summary>Chuỗi cần đi, theo ĐÚNG thứ tự — hoạt ảnh tô sáng lần lượt.</summary>
        public int[] Chain;

        public bool Gravity;

        public int Columns => this.Rows[0].Length;
        public int VisibleRows => this.Rows.Length;

        // ------------------------------------------------------------------

        private static bool IsMark(char c) => c == '!' || c == '~' || c == 'o' || c == '=';

        /// <summary>Màu của một ký tự, hoặc Wall/Stone. Ô đa sắc lấy màu MarkColor.</summary>
        private int ValueOf(char c)
        {
            if (c == '.') return PuzzleSession.Wall;
            if (c == '#') return PuzzleSession.Stone;
            if (c == '*' || IsMark(c)) return this.MarkColor;
            return c - '1';
        }

        private CellMark MarkOf(char c, int cell, IList<int> linkEnds)
        {
            switch (c)
            {
                case '#': return new CellMark { Kind = CellKind.Stone, Hp = this.StoneHp };
                case '*': return new CellMark { Kind = CellKind.Wild };
                case '~': return new CellMark { Kind = CellKind.Ice, Hp = this.IceHp };
                case '!': return new CellMark { Kind = CellKind.Bomb, Fuse = this.Fuse };
                case 'o': return new CellMark { Goal = true };
                case '=': return new CellMark { Kind = CellKind.Link, LinkId = 0 };
                default: return null;
            }
        }

        /// <summary>
        /// Dựng LevelData của bài học.
        ///
        /// Không đi qua LevelBuilder: LevelBuilder SINH bàn từ seed và bảo đảm giải được,
        /// còn ở đây bàn đã được viết sẵn và phải giữ nguyên từng ô. Đổi lại, mọi trường
        /// mà PuzzleSession đọc tới đều phải tự điền cho đủ.
        /// </summary>
        public LevelData Build()
        {
            int columns = this.Columns;
            int visible = this.VisibleRows;
            int queueRows = this.Queue == null ? 0 : this.Queue.Length;

            var cfg = new LevelConfig
            {
                World = this.World,
                Name = this.Title,
                Columns = columns,
                Rows = visible,
                Colors = PuzzleSession.MaxColors,
                Gravity = this.Gravity,
                QueueRows = queueRows,
                MinChain = this.MinChain,
                MaxChain = this.MaxChain,

                // BẮT BUỘC cho bài dây trói: PuzzleSession.BreakLinkedPartners thoát ngay
                // khi Config.Links <= 0, nên để 0 thì ăn một đầu mà đầu kia KHÔNG vỡ —
                // bài học sẽ minh hoạ ngược lại điều nó đang dạy.
                Links = CountChar('=') / 2
            };

            var level = new LevelData
            {
                Config = cfg,
                Gravity = this.Gravity,
                MinChain = this.MinChain,
                MaxChain = this.MaxChain > 0 ? this.MaxChain : int.MaxValue,

                // Bài học không tính sao và không có nút gợi ý, nhưng các trường này vẫn
                // bị đọc tới, nên phải là số dùng được chứ không phải 0.
                MaxMoves = 9,
                Par = 9,
                TwoStarMoves = 9,
                Undos = 0,
                Shuffles = 0,
                MedalChains = 0,
                Paths = new List<List<int>>(),
                Solution = new List<List<SlotRef>>()
            };

            if (this.Gravity) BuildGravity(level, columns, visible, queueRows);
            else BuildStatic(level, columns, visible);

            return level;
        }

        private int CountChar(char want)
        {
            int n = 0;
            foreach (string row in this.Rows) foreach (char c in row) if (c == want) n++;
            if (this.Queue != null)
                foreach (string row in this.Queue) foreach (char c in row) if (c == want) n++;
            return n;
        }

        private void BuildStatic(LevelData level, int columns, int rows)
        {
            var active = new bool[columns * rows];
            var template = new int[columns * rows];
            var marks = new CellMark[columns * rows];
            var linkEnds = new List<int>();

            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    int cell = y * columns + x;
                    char c = this.Rows[y][x];
                    active[cell] = c != '.';
                    template[cell] = ValueOf(c);
                    marks[cell] = MarkOf(c, cell, linkEnds);
                    if (c == '=') linkEnds.Add(cell);
                }

            // Nối hai đầu cặp liên kết SAU khi quét xong: đầu thứ nhất không biết đầu thứ
            // hai nằm ở đâu tới khi cả bàn đã đọc hết.
            for (int i = 0; i + 1 < linkEnds.Count; i += 2)
            {
                marks[linkEnds[i]].LinkPartner = linkEnds[i + 1];
                marks[linkEnds[i + 1]].LinkPartner = linkEnds[i];
                marks[linkEnds[i]].LinkId = i / 2;
                marks[linkEnds[i + 1]].LinkId = i / 2;
            }

            int alive = 0, goals = 0;
            for (int i = 0; i < template.Length; i++)
            {
                if (PuzzleSession.IsAlive(template[i])) alive++;
                if (marks[i] != null && marks[i].Goal) goals++;
            }

            level.Geometry = BoardGeometry.FromMask(columns, rows, active);
            level.Template = template;
            level.Marks = marks;
            level.GoalTotal = goals;
            level.TotalCells = alive;
            level.VisibleCells = alive;
        }

        /// <summary>
        /// Màn gravity: chồng cột dựng từ ĐÁY LÊN, gộp hàng chờ ở trên với hàng nhìn thấy.
        /// Hàng chờ viết từ trên xuống như hàng thường, nên nó là phần ĐẦU của lưới gộp.
        /// </summary>
        private void BuildGravity(LevelData level, int columns, int visible, int queueRows)
        {
            int total = visible + queueRows;
            var grid = new string[total];
            for (int y = 0; y < queueRows; y++) grid[y] = this.Queue[y];
            for (int y = 0; y < visible; y++) grid[queueRows + y] = this.Rows[y];

            var stacks = new int[columns][];
            var markColumns = new CellMark[columns][];

            for (int x = 0; x < columns; x++)
            {
                var colors = new List<int>(total);
                var marks = new List<CellMark>(total);

                // từ đáy lưới gộp lên đỉnh
                for (int y = total - 1; y >= 0; y--)
                {
                    char c = grid[y][x];
                    if (c == '.') continue;
                    colors.Add(ValueOf(c));
                    marks.Add(MarkOf(c, -1, null));
                }
                stacks[x] = colors.ToArray();
                markColumns[x] = marks.ToArray();
            }

            int alive = 0, goals = 0;
            for (int x = 0; x < columns; x++)
            {
                alive += stacks[x].Length;
                foreach (CellMark m in markColumns[x]) if (m != null && m.Goal) goals++;
            }

            level.Geometry = BoardGeometry.Rectangle(columns, visible);
            level.Columns = stacks;
            level.MarkColumns = markColumns;
            level.GoalTotal = goals;
            level.TotalCells = alive;
            level.VisibleCells = columns * visible;
        }
    }

    /// <summary>
    /// Chín bài học, một bài cho mỗi thế giới.
    ///
    /// Thứ tự và nội dung khớp với LevelCatalog.WorldName: bài học của thế giới N dạy
    /// đúng cơ chế mà thế giới N mới đưa vào. Bảng này KHÔNG tự suy ra từ LevelCatalog —
    /// nó là lời giải thích do người viết, còn LevelCatalog là số cân bằng.
    /// </summary>
    public static class TutorialLessons
    {
        public static readonly TutorialLesson[] All =
        {
            new TutorialLesson
            {
                World = 1,
                Title = "Nối và dọn sạch",
                Rule = "Kéo ngón qua các ô CÙNG MÀU nằm cạnh nhau rồi thả ra để ăn. " +
                       "Cạnh nhau tính cả bốn hướng CHÉO, không chỉ ngang dọc.",
                Note = "Dọn sạch bàn trong số lượt cho phép là thắng.",
                Rows = new[] { "12323",
                               "31232",
                               "23112" },
                MinChain = 2,
                Chain = new[] { 0, 6, 12, 13 }
            },

            new TutorialLesson
            {
                World = 2,
                Title = "Trần chuỗi",
                Rule = "Bàn có lỗ, và các ô cùng màu dính thành cục lớn. Mỗi chuỗi chỉ " +
                       "được tối đa 5 ô, nên cục lớn không ăn hết trong một nước.",
                Note = "Câu hỏi thật không còn là ăn ở đâu, mà là CHẺ cục lớn ở chỗ nào.",
                Rows = new[] { ".111.",
                               "11111",
                               ".111." },
                MinChain = 3,
                MaxChain = 5,
                Chain = new[] { 1, 2, 3, 8, 7 }
            },

            new TutorialLesson
            {
                World = 3,
                Title = "Trọng lực",
                Rule = "Ăn xong, các ô phía trên RƠI xuống lấp chỗ trống, và ô mới từ " +
                       "hàng chờ phía trên bàn tụt vào.",
                Note = "Ô chỉ rơi TRONG cột của nó, nên hai ô cùng màu ở hai cột xa nhau " +
                       "có thể không bao giờ gặp được nhau.",
                Rows = new[] { "12312",
                               "23123",
                               "11233" },
                Queue = new[] { "23131",
                                "31212" },
                Gravity = true,
                MinChain = 3,
                MaxChain = 5,
                Chain = new[] { 10, 11, 7 }
            },

            new TutorialLesson
            {
                World = 4,
                Title = "Đá",
                Rule = "Đá không có màu và không nối được. Nó chỉ vỡ khi có chuỗi bị ăn " +
                       "NGAY CẠNH nó.",
                Note = "Đây là cơ chế đầu tiên làm VỊ TRÍ của chuỗi có ý nghĩa: hòn đá xa " +
                       "chuỗi vẫn còn nguyên.",
                Rows = new[] { "11#22",
                               "12212",
                               "2111#" },
                MinChain = 3,
                MaxChain = 5,
                Chain = new[] { 0, 1, 5 }
            },

            new TutorialLesson
            {
                World = 5,
                Title = "Ô đa sắc",
                Rule = "Ô đa sắc ghép được với MỌI màu, nên nó nối liền hai cụm cùng màu " +
                       "đang bị chia cắt.",
                Note = "Mỗi chuỗi chỉ được dùng MỘT ô đa sắc — nó là phao cứu ô lẻ, không " +
                       "phải chìa khoá mở mọi thứ.",
                Rows = new[] { "12321",
                               "32*23",
                               "21132" },
                MarkColor = 1,
                MinChain = 3,
                MaxChain = 5,
                Chain = new[] { 6, 7, 8 }
            },

            new TutorialLesson
            {
                World = 6,
                Title = "Ngòi nổ",
                Rule = "Ô mang ngòi đếm ngược mỗi lượt bạn đi. Ngòi về 0 mà chưa ăn được " +
                       "ô đó là thua ngay, dù bàn còn dư lượt.",
                Note = "Nó không thêm ràng buộc về chỗ, mà về THỨ TỰ: có nước đúng nhưng " +
                       "đi sai lúc vẫn thua.",
                Rows = new[] { "12312",
                               "23121",
                               "11!23" },
                MarkColor = 0,
                Fuse = 3,
                MinChain = 3,
                MaxChain = 5,
                Chain = new[] { 10, 11, 12 }
            },

            new TutorialLesson
            {
                World = 7,
                Title = "Ô đích",
                Rule = "Màn này thắng khi dọn hết ô có VÒNG ĐÍCH vàng — không cần dọn " +
                       "sạch bàn.",
                Note = "Phần bàn không có đích là phần được phép bỏ lại, nên par ở đây " +
                       "nhỏ hơn hẳn các thế giới khác.",
                Rows = new[] { "1o312",
                               "23121",
                               "o1123" },
                MarkColor = 0,
                MinChain = 3,
                MaxChain = 5,
                Chain = new[] { 0, 1, 7 }
            },

            new TutorialLesson
            {
                World = 8,
                Title = "Băng",
                Rule = "Ô băng CÓ màu nhưng chưa chọn được. Ăn một chuỗi ngay cạnh thì " +
                       "băng tan, và ô hiện ra thành ô ăn được bình thường.",
                Note = "Khác đá đúng một điểm: đá VỠ ĐI MẤT, băng thì Ở LẠI thành ô dùng " +
                       "được. Đá là gỡ vật cản, băng là mở khoá đường đi.",
                Rows = new[] { "12312",
                               "21~13",
                               "11223" },
                MarkColor = 0,
                MinChain = 3,
                MaxChain = 5,
                Chain = new[] { 10, 11, 6 }
            },

            new TutorialLesson
            {
                World = 9,
                Title = "Dây trói",
                Rule = "Hai ô cùng số hiệu bị trói vào nhau. Ăn một đầu thì đầu kia VỠ " +
                       "THEO, dù nó ở tận góc bàn bên kia.",
                Note = "Cơ chế đầu tiên phá luật \"muốn ăn thì phải kề\": phải tính xem ăn " +
                       "ở đây thì mất gì ở kia.",
                Rows = new[] { "=1322",
                               "21321",
                               "1123=" },
                MarkColor = 0,
                MinChain = 3,
                MaxChain = 5,
                Chain = new[] { 0, 1, 6 }
            },

            new TutorialLesson
            {
                World = 10,
                Title = "Chính xác",
                Rule = "Thế giới này cho ĐÚNG số lượt của lời giải, không dư một nước nào. " +
                       "Ăn hụt một ô là mất luôn một lượt không lấy lại được.",

                // Bài duy nhất mà hình không minh hoạ được luật: "không dư lượt" là một con
                // số trên HUD, không phải một thứ nằm trên bàn. Nên hình ở đây làm việc
                // khác — nó diễn đúng KỸ NĂNG mà chế độ này đòi: chuỗi nào cũng ăn cho
                // kịch trần. Đó là lý do chuỗi minh hoạt dài đúng 5 ô.
                Note = "Nên chuỗi nào cũng ăn cho kịch trần. Bù lại: hoàn tác rất nhiều, " +
                       "còn vật phẩm và xáo lại thì tắt — mua thêm lượt thì còn gì là chính xác.",
                Rows = new[] { "12321",
                               "21132",
                               "13112",
                               "22313",
                               "13221" },
                MinChain = 3,
                MaxChain = 5,
                Chain = new[] { 6, 7, 12, 13, 18 }
            }
        };

        /// <summary>Bài học của một thế giới, hoặc null nếu thế giới đó không có bài.</summary>
        public static TutorialLesson For(int world)
        {
            foreach (TutorialLesson lesson in All) if (lesson.World == world) return lesson;
            return null;
        }
    }
}
