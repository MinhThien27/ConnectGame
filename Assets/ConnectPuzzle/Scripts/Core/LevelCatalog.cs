namespace ConnectPuzzle.Core
{
    /// <summary>
    /// Khai báo một màn chơi. Không chứa bàn cụ thể — bàn được SINH từ seed,
    /// nên bảng này là toàn bộ phần cân bằng game, sửa ở đây là đủ.
    /// </summary>
    public sealed class LevelConfig
    {
        /// <summary>Thế giới, chỉ dùng để nhóm trên màn chọn màn.</summary>
        public int World = 1;
        public string Name = "";

        /// <summary>Kích thước bàn. Với màn gravity, Rows là số hàng NHÌN THẤY.</summary>
        public int Columns = 6;
        public int Rows = 6;

        /// <summary>
        /// Hình dạng bàn: mỗi chuỗi là một hàng, '#' là ô thuộc bàn, '.' là lỗ.
        /// Null nghĩa là bàn chữ nhật đầy. Chỉ dùng cho màn tĩnh.
        /// </summary>
        public string[] Shape = null;

        public int Colors = 3;
        public int Seed = 1;

        /// <summary>Số lượt dư ngoài par. Càng nhỏ càng ngặt.</summary>
        public int Slack = 4;

        /// <summary>
        /// Xác suất hai nhóm kề nhau ĐƯỢC trùng màu (0..1).
        /// 0   = mỗi nhóm màu là đúng một đường, nhìn là thấy.
        /// Cao = các nhóm dính thành cục lớn, người chơi phải tự tìm cách chẻ.
        /// Đây là núm độ khó thật, không phải số lượt.
        /// </summary>
        public double Fuse = 0;

        public int Undos = 4;
        public int Shuffles = -1;          // -1 = lấy mặc định theo thế giới

        /// <summary>Độ dài nhóm khi sinh. Nhóm ngắn thì cần nhiều lượt hơn.</summary>
        public int MinPathLength = 3;
        public int MaxPathLength = 5;

        /// <summary>
        /// Số ô TỐI THIỂU của một chuỗi hợp lệ. Mặc định 2 như luật gốc.
        /// Nâng lên 3-4 thì bỏ sót một cặp lẻ là cặp đó chết vĩnh viễn — sai lầm bị
        /// trừng phạt sớm thay vì trôi tới cuối ván.
        /// Bắt buộc &lt;= MinPathLength, nếu không lời giải tham chiếu thành bất hợp lệ.
        /// </summary>
        public int MinChain = 2;

        /// <summary>
        /// Số ô TỐI ĐA của một chuỗi; 0 = không giới hạn.
        ///
        /// Đây là núm mạnh nhất để chặn lối chơi tham lam: không có trần thì người chơi
        /// quét nguyên một cục lớn trong một nước và ngân sách lượt thành thừa thãi.
        /// Có trần thì phải CHỌN CHẺ Ở ĐÂU, và chẻ sai thì phần dư không ăn được.
        /// Bắt buộc &gt;= MaxPathLength, nếu không lời giải tham chiếu thành bất hợp lệ.
        /// </summary>
        public int MaxChain = 0;

        public int ResolvedMaxChain => this.MaxChain > 0 ? this.MaxChain : int.MaxValue;

        /// <summary>
        /// Số cột TỐI ĐA mà một màu được phép trải ra; 0 = không giới hạn.
        ///
        /// HIỆN KHÔNG DÙNG (0 ở mọi màn) — giữ lại làm phương án dự phòng.
        ///
        /// Ý tưởng: ô chỉ rơi TRONG cột, nên hai ô cùng màu cách nhau từ 2 cột trở lên
        /// vĩnh viễn không kề nhau được; bó mỗi màu vào một dải cột hẹp thì chúng luôn
        /// còn cơ hội gặp lại. Nó CÓ sửa được việc bế tắc giữa ván, nhưng đo ra thì cái
        /// giá quá đắt: span 4 đẩy tỉ lệ cặp ô kề nhau cùng màu từ 31% lên 50%, bàn hiện
        /// ra từng vùng một màu, nhìn là thấy chuỗi.
        ///
        /// Cách rẻ hơn cho cùng mục tiêu: giữ Fuse &lt;= 0.30 ở màn gravity. Quét (span x
        /// fuse) cho thấy span tắt + fuse 0.30 vừa công bằng nhất vừa ít vón cục nhất.
        /// Fuse cao tạo cục lớn, người chơi ăn hết cục rồi để lại ô lẻ rải rác.
        /// </summary>
        public int ColorColumnSpan = 0;

        /// <summary>Bật thì ô rơi xuống sau mỗi lượt và hàng chờ tụt vào.</summary>
        public bool Gravity = false;

        /// <summary>
        /// Số slot ẨN phía trên MỖI CỘT (chỉ dùng khi Gravity).
        /// Tổng ô = Columns * (Rows + QueueRows), nhiều hơn số ô nhìn thấy.
        /// </summary>
        public int QueueRows = 0;

        // ------------------------------------------------------------------
        // Ô đặc biệt. Xem LevelDecorator để biết vì sao gắn chúng KHÔNG phá vỡ
        // bảo đảm "màn nào cũng giải được".
        // ------------------------------------------------------------------

        /// <summary>Số hòn đá (chỉ màn tĩnh — xem ghi chú trong LevelDecorator).</summary>
        public int Stones = 0;

        /// <summary>Số lần đá phải bị va mới vỡ. 0/1 = vỡ ngay lần đầu.</summary>
        public int StoneHp = 1;

        /// <summary>Số ô đa sắc.</summary>
        public int Wilds = 0;

        /// <summary>Số ô mang ngòi nổ.</summary>
        public int Bombs = 0;

        /// <summary>Số lượt dư cho ngòi nổ ngoài nước ăn được nó theo lời giải.</summary>
        public int BombSlack = 3;

        /// <summary>Số ô đích. &gt; 0 thì thắng khi dọn hết đích, không cần sạch bàn.</summary>
        public int Goals = 0;

        /// <summary>
        /// Số ô băng — ô CÓ MÀU nhưng đóng băng, không chọn được tới khi tan hết.
        /// Chỉ dùng cho màn tĩnh; xem ghi chú lý do trong LevelDecorator.
        /// </summary>
        public int Ices = 0;

        /// <summary>Số lớp băng phải tan mới ăn được ô. 0/1 = tan ngay lần va đầu.</summary>
        public int IceHp = 1;

        /// <summary>
        /// Số CẶP liên kết (mỗi cặp = 2 ô). Ăn một đầu thì đầu kia tự vỡ dù ở xa.
        /// Chỉ dùng cho màn tĩnh: ở gravity ô rơi liên tục nên chỉ số ô bạn đổi mỗi
        /// nước, mà liên kết lại được lưu theo chỉ số lưới.
        /// </summary>
        public int Links = 0;

        /// <summary>
        /// Màn CHÍNH XÁC: ngân sách lượt bằng ĐÚNG par, không dư một lượt nào.
        ///
        /// Không suy ra từ Slack = 0 mà là một cờ riêng, vì nó đổi ba thứ khác nữa: cấm
        /// vật phẩm (xem PuzzleSession.ItemsAllowed), đổi câu trên HUD, và bỏ hẳn nút xáo.
        /// Đọc Slack = 0 rồi ngầm hiểu ra cả ba là kiểu ràng buộc mà sáu tháng sau không
        /// ai nhớ nữa.
        ///
        /// Vẫn bảo đảm giải được: par là số nước của lời giải tham chiếu (hoặc của bot
        /// tham lam, cái nào ngắn hơn), tức là một dãy nước CÓ THẬT vừa đúng ngân sách.
        /// Nó KHÔNG được chứng minh là ngắn nhất — và không cần: cái cần là "có đường
        /// đi", còn việc người chơi phải tìm ra một đường ngắn bằng thế mới chính là bài.
        /// </summary>
        public bool Exact = false;

        public int ResolvedShuffles => this.Shuffles >= 0 ? this.Shuffles : (this.World == 1 ? 3 : 2);
    }

    /// <summary>Bảng 24 màn. Toàn bộ cân bằng game nằm ở đây.</summary>
    public static class LevelCatalog
    {
        public static readonly string[] DiamondShape =
            { "...#...", "..###..", ".#####.", "#######", ".#####.", "..###..", "...#..." };

        public static readonly string[] PlusShape =
            { "..###..", "..###..", "#######", "#######", "#######", "..###..", "..###.." };

        public static readonly string[] RingShape =
            { "#######", "#######", "##...##", "##...##", "##...##", "#######", "#######" };

        public static readonly string[] StairsShape =
            { "###.....", "####....", "#####...", "..#####.", "...#####", ".....###" };

        public static readonly string[] HourglassShape =
            { "#######", ".#####.", "..###..", "..###..", "..###..", ".#####.", "#######" };

        public static readonly string[] CrossShape =
            { "##...##", "###.###", ".#####.", "..###..", ".#####.", "###.###", "##...##" };

        public static readonly string[] WideShape =
            { "..####..", ".######.", "########", "########", "########", ".######.", "..####.." };

        public static readonly string[] ArrowShape =
            { "...#...", "..###..", ".#####.", "#######", "..###..", "..###..", "..###.." };

        public static readonly string[] BowlShape =
            { "#.....#", "#.....#", "#.....#", "##...##", "#######", ".#####.", "..###.." };

        public static readonly string[] TowerShape =
            { "..###..", "..###..", ".#####.", "#######", "#######", ".#####.", "..###.." };

        /// <summary>Vành ngoài và vành trong cách nhau 2 ô nên RỜI hẳn nhau — hai sân riêng.</summary>
        public static readonly string[] FrameShape =
            { "#######", "#.....#", "#.###.#", "#.#.#.#", "#.###.#", "#.....#", "#######" };

        public static string WorldName(int world)
        {
            switch (world)
            {
                case 1:  return "Thế giới 1 · Nhập môn";
                case 2:  return "Thế giới 2 · Hình dạng";
                case 3:  return "Thế giới 3 · Gravity";
                case 4:  return "Thế giới 4 · Đá tảng";
                case 5:  return "Thế giới 5 · Đa sắc";
                case 6:  return "Thế giới 6 · Ngòi nổ";
                case 7:  return "Thế giới 7 · Mục tiêu";
                case 8:  return "Thế giới 8 · Băng giá";
                case 9:  return "Thế giới 9 · Dây trói";
                case 10: return "Thế giới 10 · Chính xác";
                default: return "Thế giới " + world;
            }
        }

        // Ghi chú cân bằng:
        //  - MaxChain = MaxPathLength ở MỌI màn. Không có trần thì người chơi quét
        //    nguyên cục lớn trong một nước và ngân sách lượt thành thừa thãi — đo được
        //    bot tham lam thắng 14/24 màn, có màn dư tới 14 lượt.
        //  - Fuse KHÔNG còn màn nào để 0. Fuse = 0 nghĩa là hai đường kề nhau luôn khác
        //    màu, tức mỗi cụm màu chính là đúng một đường trong lời giải — bàn tự hiện
        //    ra đáp án.
        //  - MinChain lên 3 từ màn 4 trở đi: bỏ sót một cặp lẻ là cặp đó chết hẳn.
        public static readonly LevelConfig[] Levels =
        {
            // ---------- Thế giới 1 · Bàn tĩnh, học cơ chế ----------
            // Hai màn đầu KHÔNG có trần chuỗi (MaxChain=0): dạy từng luật một, ở đây chỉ
            // dạy nối và dọn sạch. Trần chuỗi xuất hiện từ màn 3, chuỗi tối thiểu 3 ô từ
            // màn 4 — mỗi màn thêm đúng một ràng buộc.
            new LevelConfig { World=1, Name="Khởi động",    Columns=5, Rows=5, Colors=3, Seed=1101, Slack=5, Fuse=0.10, Undos=5, MinPathLength=3, MaxPathLength=4, MinChain=2 },
            new LevelConfig { World=1, Name="Bắt nhịp",     Columns=5, Rows=6, Colors=3, Seed=1207, Slack=5, Fuse=0.15, Undos=5, MinPathLength=3, MaxPathLength=4, MinChain=2 },
            new LevelConfig { World=1, Name="Sáu nhân sáu", Columns=6, Rows=6, Colors=3, Seed=1319, Slack=4, Fuse=0.25, Undos=5, MinChain=2, MaxChain=5 },
            new LevelConfig { World=1, Name="Kim cương",    Shape=DiamondShape, Colors=3, Seed=1427, Slack=3, Fuse=0.35, Undos=4, MinChain=3, MaxChain=5 },
            new LevelConfig { World=1, Name="Chật hơn",     Columns=6, Rows=7, Colors=3, Seed=1523, Slack=3, Fuse=0.40, Undos=4, MinChain=3, MaxChain=5 },
            new LevelConfig { World=1, Name="Mũi tên",      Shape=ArrowShape,  Colors=3, Seed=1601, Slack=3, Fuse=0.40, Undos=4, MinChain=3, MaxChain=5 },
            new LevelConfig { World=1, Name="Cái chén",     Shape=BowlShape,   Colors=3, Seed=1709, Slack=3, Fuse=0.45, Undos=4, MinChain=3, MaxChain=5 },
            new LevelConfig { World=1, Name="Bảy nhân sáu", Columns=7, Rows=6, Colors=3, Seed=1811, Slack=3, Fuse=0.45, Undos=4, MinChain=3, MaxChain=5 },
            new LevelConfig { World=1, Name="Ngọn tháp",    Shape=TowerShape,  Colors=3, Seed=1913, Slack=2, Fuse=0.50, Undos=4, MinChain=3, MaxChain=5 },
            new LevelConfig { World=1, Name="Tốt nghiệp",   Columns=7, Rows=7, Colors=3, Seed=2003, Slack=2, Fuse=0.50, Undos=3, MinChain=3, MaxChain=5 },

            // ---------- Thế giới 2 · Hình dạng & cục dính ----------
            new LevelConfig { World=2, Name="Bốn màu",      Columns=6, Rows=6, Colors=4, Seed=2111, Slack=3, Fuse=0.60, Undos=4, MinChain=3, MaxChain=5 },
            new LevelConfig { World=2, Name="Dấu cộng",     Shape=PlusShape,     Colors=4, Seed=2213, Slack=3, Fuse=0.65, Undos=4, MinChain=3, MaxChain=5 },
            new LevelConfig { World=2, Name="Vành khuyên",  Shape=RingShape,     Colors=4, Seed=2317, Slack=2, Fuse=0.55, Undos=3, MinChain=3, MaxChain=5 },
            new LevelConfig { World=2, Name="Bậc thang",    Shape=StairsShape,   Colors=4, Seed=2423, Slack=2, Fuse=0.75, Undos=3, MinChain=3, MaxChain=5 },
            new LevelConfig { World=2, Name="Đồng hồ cát",  Shape=HourglassShape,Colors=4, Seed=2531, Slack=2, Fuse=0.60, Undos=3, MinChain=3, MaxChain=5 },
            new LevelConfig { World=2, Name="Bảy nhân bảy", Columns=7, Rows=7, Colors=4, Seed=2637, Slack=2, Fuse=0.60, Undos=3, MinChain=3, MaxChain=5 },
            new LevelConfig { World=2, Name="Chữ X",        Shape=CrossShape,    Colors=4, Seed=2741, Slack=2, Fuse=0.65, Undos=3, MinChain=3, MaxChain=5 },
            new LevelConfig { World=2, Name="Hai sân",      Shape=FrameShape,    Colors=4, Seed=2851, Slack=3, Fuse=0.55, Undos=3, MinChain=3, MaxChain=5 },
            new LevelConfig { World=2, Name="Năm màu",      Columns=7, Rows=7, Colors=5, Seed=3113, Slack=2, Fuse=0.70, Undos=3, MinChain=3, MaxChain=5 },
            new LevelConfig { World=2, Name="Bành trướng",  Shape=WideShape,     Colors=5, Seed=3217, Slack=2, Fuse=0.65, Undos=2, MinChain=3, MaxChain=5 },

            // ---------- Thế giới 3 · Gravity + hàng chờ ----------
            new LevelConfig { World=3, Gravity=true, QueueRows=4,  Name="Thác màu",    Columns=6, Rows=6, Colors=3, Seed=4101, Slack=3, Fuse=0.22, Undos=4, MinChain=3, MaxChain=5 },
            new LevelConfig { World=3, Gravity=true, QueueRows=5,  Name="Rơi tự do",   Columns=6, Rows=7, Colors=3, Seed=4207, Slack=3, Fuse=0.24, Undos=4, MinChain=3, MaxChain=5 },
            new LevelConfig { World=3, Gravity=true, QueueRows=5,  Name="Dòng chảy",   Columns=7, Rows=7, Colors=4, Seed=4313, Slack=3, Fuse=0.26, Undos=3, MinChain=3, MaxChain=5 },
            new LevelConfig { World=3, Gravity=true, QueueRows=6,  Name="Bốn sắc rơi", Columns=7, Rows=7, Colors=4, Seed=4419, Slack=3, Fuse=0.26, Undos=3, MinChain=3, MaxChain=5 },
            new LevelConfig { World=3, Gravity=true, QueueRows=6,  Name="Hàng chờ",    Columns=7, Rows=8, Colors=4, Seed=4523, Slack=3, Fuse=0.28, Undos=3, MinChain=3, MaxChain=5 },
            new LevelConfig { World=3, Gravity=true, QueueRows=6,  Name="Sâu hơn",     Columns=8, Rows=8, Colors=4, Seed=4629, Slack=3, Fuse=0.28, Undos=3, MinChain=3, MaxChain=5 },
            new LevelConfig { World=3, Gravity=true, QueueRows=7,  Name="Năm sắc rơi", Columns=8, Rows=8, Colors=5, Seed=4733, Slack=3, Fuse=0.28, Undos=2, MinChain=3, MaxChain=5 },
            new LevelConfig { World=3, Gravity=true, QueueRows=8,  Name="Ngặt nghèo",  Columns=8, Rows=8, Colors=4, Seed=4841, Slack=2, Fuse=0.30, Undos=2, Shuffles=2, MinPathLength=3, MaxPathLength=6, MinChain=3, MaxChain=6 },
            new LevelConfig { World=3, Gravity=true, QueueRows=8,  Name="Trút xuống",  Columns=8, Rows=9, Colors=4, Seed=4943, Slack=2, Fuse=0.30, Undos=2, Shuffles=2, MinPathLength=3, MaxPathLength=6, MinChain=3, MaxChain=6 },
            new LevelConfig { World=3, Gravity=true, QueueRows=10, Name="Vô cực",      Columns=8, Rows=9, Colors=5, Seed=5051, Slack=2, Fuse=0.30, Undos=2, Shuffles=2, MinPathLength=3, MaxPathLength=6, MinChain=3, MaxChain=6 },

            // ---------- Thế giới 4 · ĐÁ ----------
            // Đá không nối được, chỉ vỡ khi có chuỗi bị ăn KỀ nó. Nó là cơ chế đầu tiên
            // khiến vị trí của chuỗi có ý nghĩa: trước đây ăn ở đâu cũng như nhau, giờ
            // phải ăn ĐÚNG CHỖ mới phá được đá.
            new LevelConfig { World=4, Name="Vỡ đá",        Columns=6, Rows=6, Colors=3, Seed=6101, Slack=4, Fuse=0.35, Undos=4, MinChain=3, MaxChain=5, Stones=3 },
            new LevelConfig { World=4, Name="Kẹt giữa",     Columns=6, Rows=7, Colors=4, Seed=6207, Slack=3, Fuse=0.45, Undos=4, MinChain=3, MaxChain=5, Stones=4 },
            new LevelConfig { World=4, Name="Đá rải",       Columns=7, Rows=6, Colors=4, Seed=6311, Slack=3, Fuse=0.50, Undos=4, MinChain=3, MaxChain=5, Stones=5 },
            new LevelConfig { World=4, Name="Tường đôi",    Columns=7, Rows=7, Colors=4, Seed=6413, Slack=3, Fuse=0.55, Undos=3, MinChain=3, MaxChain=5, Stones=5, StoneHp=2 },
            new LevelConfig { World=4, Name="Mê đá",        Shape=WideShape,  Colors=4, Seed=6519, Slack=2, Fuse=0.60, Undos=3, MinChain=3, MaxChain=5, Stones=6, StoneHp=2 },
            new LevelConfig { World=4, Name="Đá trong khuyên", Shape=RingShape, Colors=4, Seed=6623, Slack=3, Fuse=0.55, Undos=3, MinChain=3, MaxChain=5, Stones=4, StoneHp=2 },
            new LevelConfig { World=4, Name="Đá dựng tháp", Shape=TowerShape, Colors=4, Seed=6729, Slack=2, Fuse=0.60, Undos=3, MinChain=3, MaxChain=5, Stones=5, StoneHp=2 },
            new LevelConfig { World=4, Name="Đá dày",       Columns=7, Rows=7, Colors=5, Seed=6833, Slack=2, Fuse=0.60, Undos=3, MinChain=3, MaxChain=5, Stones=6, StoneHp=2 },
            new LevelConfig { World=4, Name="Bãi đá",       Columns=8, Rows=7, Colors=5, Seed=6937, Slack=2, Fuse=0.65, Undos=2, MinChain=3, MaxChain=5, Stones=7, StoneHp=2 },
            new LevelConfig { World=4, Name="Núi đá",       Columns=8, Rows=8, Colors=5, Seed=7043, Slack=2, Fuse=0.65, Undos=2, MinPathLength=3, MaxPathLength=6, MinChain=3, MaxChain=6, Stones=8, StoneHp=2 },

            // ---------- Thế giới 5 · ĐA SẮC ----------
            // Ô đa sắc ghép được mọi màu, mỗi chuỗi tối đa 1 ô. Nó cho phép đẩy số màu
            // lên 6 mà bàn vẫn chơi được, và là phao cứu ô lẻ.
            new LevelConfig { World=5, Name="Cầu vồng",     Columns=6, Rows=6, Colors=5, Seed=7101, Slack=3, Fuse=0.55, Undos=4, MinChain=3, MaxChain=5, Wilds=2 },
            new LevelConfig { World=5, Name="Một điểm sáng",Columns=6, Rows=7, Colors=5, Seed=7207, Slack=3, Fuse=0.60, Undos=4, MinChain=3, MaxChain=5, Wilds=1 },
            new LevelConfig { World=5, Name="Sáu sắc",      Columns=7, Rows=7, Colors=6, Seed=7313, Slack=3, Fuse=0.60, Undos=3, MinChain=3, MaxChain=5, Wilds=3 },
            new LevelConfig { World=5, Name="Ít mà tinh",   Columns=7, Rows=7, Colors=6, Seed=7419, Slack=2, Fuse=0.65, Undos=3, MinChain=3, MaxChain=5, Wilds=2 },
            new LevelConfig { World=5, Name="Đá & sắc",     Columns=7, Rows=7, Colors=5, Seed=7523, Slack=2, Fuse=0.60, Undos=3, MinChain=3, MaxChain=5, Wilds=2, Stones=4 },
            new LevelConfig { World=5, Name="Sắc trong khuyên", Shape=RingShape, Colors=6, Seed=7629, Slack=3, Fuse=0.60, Undos=3, MinChain=3, MaxChain=5, Wilds=2 },
            new LevelConfig { World=5, Name="Sắc chật",     Columns=7, Rows=8, Colors=6, Seed=7733, Slack=2, Fuse=0.65, Undos=3, MinChain=3, MaxChain=5, Wilds=3 },
            new LevelConfig { World=5, Name="Bốn điểm sáng",Columns=8, Rows=7, Colors=6, Seed=7837, Slack=2, Fuse=0.65, Undos=3, MinChain=3, MaxChain=5, Wilds=4 },
            new LevelConfig { World=5, Name="Sắc & đá dày", Columns=8, Rows=7, Colors=5, Seed=7943, Slack=2, Fuse=0.65, Undos=2, MinChain=3, MaxChain=5, Wilds=3, Stones=5, StoneHp=2 },
            new LevelConfig { World=5, Name="Vạn sắc",      Columns=8, Rows=8, Colors=6, Seed=8047, Slack=2, Fuse=0.70, Undos=2, MinPathLength=3, MaxPathLength=6, MinChain=3, MaxChain=6, Wilds=3 },

            // ---------- Thế giới 6 · NGÒI NỔ ----------
            // Ngòi đếm ngược theo lượt. Trước đây thứ tự đi gần như tuỳ ý; ngòi tạo ra
            // THỨ TỰ ƯU TIÊN — có nước đúng nhưng đi sai lúc là thua.
            new LevelConfig { World=6, Name="Ngòi ngắn",    Columns=6, Rows=6, Colors=4, Seed=8101, Slack=4, Fuse=0.50, Undos=4, MinChain=3, MaxChain=5, Bombs=2 },
            new LevelConfig { World=6, Name="Hai ngòi",     Columns=6, Rows=7, Colors=4, Seed=8207, Slack=3, Fuse=0.55, Undos=4, MinChain=3, MaxChain=5, Bombs=2 },
            new LevelConfig { World=6, Name="Ba ngòi",      Columns=7, Rows=7, Colors=4, Seed=8311, Slack=3, Fuse=0.60, Undos=3, MinChain=3, MaxChain=5, Bombs=3 },
            new LevelConfig { World=6, Name="Ngòi trong đá",Columns=7, Rows=7, Colors=4, Seed=8417, Slack=3, Fuse=0.60, Undos=3, MinChain=3, MaxChain=5, Bombs=3, Stones=4 },
            new LevelConfig { World=6, Gravity=true, QueueRows=5, Name="Nổ trong dòng", Columns=6, Rows=7, Colors=4, Seed=8523, Slack=3, Fuse=0.26, Undos=4, MinChain=3, MaxChain=5, Bombs=2 },
            new LevelConfig { World=6, Gravity=true, QueueRows=6, Name="Chạy đua",      Columns=7, Rows=7, Colors=5, Seed=8629, Slack=3, Fuse=0.28, Undos=3, MinChain=3, MaxChain=5, Bombs=3, Wilds=1 },
            new LevelConfig { World=6, Name="Bốn ngòi",     Columns=7, Rows=8, Colors=5, Seed=8733, Slack=3, Fuse=0.60, Undos=3, MinChain=3, MaxChain=5, Bombs=4 },
            new LevelConfig { World=6, Name="Ngòi & sắc",   Columns=8, Rows=7, Colors=5, Seed=8837, Slack=2, Fuse=0.65, Undos=3, MinChain=3, MaxChain=5, Bombs=3, Wilds=2 },
            new LevelConfig { World=6, Gravity=true, QueueRows=7, Name="Dòng lửa",      Columns=7, Rows=8, Colors=5, Seed=8941, Slack=3, Fuse=0.28, Undos=3, MinChain=3, MaxChain=5, Bombs=4 },
            new LevelConfig { World=6, Gravity=true, QueueRows=8, Name="Năm ngòi",      Columns=8, Rows=8, Colors=5, Seed=9047, Slack=2, Fuse=0.30, Undos=2, Shuffles=2, MinPathLength=3, MaxPathLength=6, MinChain=3, MaxChain=6, Bombs=5, Wilds=1 },

            // ---------- Thế giới 7 · MỤC TIÊU ----------
            // Đổi hẳn điều kiện thắng: chỉ cần dọn ô có vòng đích, không cần sạch bàn.
            // Bài toán thành "đường ngắn nhất tới đích", bỏ qua phần bàn thừa — nên par
            // ở đây nhỏ hơn hẳn các thế giới khác, đó là đúng chứ không phải lỗi.
            new LevelConfig { World=7, Name="Bốn đích",     Columns=6, Rows=6, Colors=4, Seed=9101, Slack=3, Fuse=0.55, Undos=4, MinChain=3, MaxChain=5, Goals=4 },
            new LevelConfig { World=7, Name="Năm đích",     Columns=6, Rows=7, Colors=4, Seed=9207, Slack=3, Fuse=0.60, Undos=4, MinChain=3, MaxChain=5, Goals=5 },
            new LevelConfig { World=7, Name="Đích sâu",     Columns=7, Rows=7, Colors=5, Seed=9311, Slack=3, Fuse=0.60, Undos=3, MinChain=3, MaxChain=5, Goals=6, Wilds=1 },
            new LevelConfig { World=7, Name="Đích trong đá",Columns=7, Rows=7, Colors=4, Seed=9417, Slack=2, Fuse=0.60, Undos=3, MinChain=3, MaxChain=5, Goals=5, Stones=4 },
            new LevelConfig { World=7, Gravity=true, QueueRows=5, Name="Đích rơi",      Columns=6, Rows=7, Colors=4, Seed=9523, Slack=3, Fuse=0.26, Undos=4, MinChain=3, MaxChain=5, Goals=4 },
            new LevelConfig { World=7, Name="Sáu đích",     Columns=7, Rows=7, Colors=5, Seed=9629, Slack=2, Fuse=0.65, Undos=3, MinChain=3, MaxChain=5, Goals=6 },
            new LevelConfig { World=7, Name="Đích & ngòi",  Columns=7, Rows=8, Colors=5, Seed=9733, Slack=2, Fuse=0.65, Undos=3, MinChain=3, MaxChain=5, Goals=5, Bombs=2 },
            new LevelConfig { World=7, Gravity=true, QueueRows=6, Name="Đích xa",       Columns=7, Rows=7, Colors=5, Seed=9837, Slack=3, Fuse=0.28, Undos=3, MinChain=3, MaxChain=5, Goals=6, Wilds=1 },
            new LevelConfig { World=7, Name="Bảy đích",     Columns=8, Rows=7, Colors=5, Seed=9941, Slack=2, Fuse=0.65, Undos=3, MinChain=3, MaxChain=5, Goals=7, Wilds=2 },
            new LevelConfig { World=7, Gravity=true, QueueRows=6, Name="Tổng lực", Columns=7, Rows=8, Colors=5, Seed=10047, Slack=3, Fuse=0.30, Undos=3, Shuffles=2, MinPathLength=3, MaxPathLength=6, MinChain=3, MaxChain=6, Goals=6, Wilds=2, Bombs=2 },

            // ---------- Thế giới 8 · BĂNG GIÁ ----------
            // Ô băng là ô CÓ MÀU nhưng đóng băng: phải ăn một chuỗi ngay cạnh cho tan
            // rồi mới ăn được nó. Đá là "gỡ vật cản", băng là "mở khoá đường đi" — nên
            // nó thêm một NHỊP CHUẨN BỊ mà các cơ chế trước chưa có.
            new LevelConfig { World=8, Name="Sương giá",    Columns=6, Rows=6, Colors=3, Seed=11238, Slack=4, Fuse=0.35, Undos=4, MinChain=3, MaxChain=5, Ices=3 },
            new LevelConfig { World=8, Name="Đóng băng",    Columns=6, Rows=7, Colors=4, Seed=11207, Slack=4, Fuse=0.45, Undos=4, MinChain=3, MaxChain=5, Ices=4 },
            new LevelConfig { World=8, Name="Băng dày",     Columns=7, Rows=6, Colors=4, Seed=11311, Slack=3, Fuse=0.50, Undos=4, MinChain=3, MaxChain=5, Ices=4, IceHp=2 },
            new LevelConfig { World=8, Name="Hai lớp",      Columns=7, Rows=7, Colors=4, Seed=11413, Slack=3, Fuse=0.55, Undos=3, MinChain=3, MaxChain=5, Ices=5, IceHp=2 },
            new LevelConfig { World=8, Name="Hồ băng",      Shape=WideShape,  Colors=4, Seed=11519, Slack=3, Fuse=0.55, Undos=3, MinChain=3, MaxChain=5, Ices=6, IceHp=2 },
            new LevelConfig { World=8, Name="Băng trong khuyên", Shape=RingShape, Colors=4, Seed=11623, Slack=3, Fuse=0.55, Undos=3, MinChain=3, MaxChain=5, Ices=5, IceHp=2 },
            new LevelConfig { World=8, Name="Băng & sắc",   Columns=7, Rows=7, Colors=5, Seed=11729, Slack=3, Fuse=0.60, Undos=3, MinChain=3, MaxChain=5, Ices=5, IceHp=2, Wilds=2 },
            new LevelConfig { World=8, Name="Băng trong đá",Columns=7, Rows=7, Colors=4, Seed=11833, Slack=3, Fuse=0.60, Undos=3, MinChain=3, MaxChain=5, Ices=4, IceHp=2, Stones=4 },
            new LevelConfig { World=8, Name="Băng & ngòi",  Columns=8, Rows=7, Colors=5, Seed=11937, Slack=3, Fuse=0.60, Undos=3, MinChain=3, MaxChain=5, Ices=5, IceHp=2, Bombs=2 },
            new LevelConfig { World=8, Name="Băng vĩnh cửu",Columns=8, Rows=8, Colors=5, Seed=12043, Slack=3, Fuse=0.65, Undos=2, MinPathLength=3, MaxPathLength=6, MinChain=3, MaxChain=6, Ices=6, IceHp=2, Wilds=2, Goals=5 },

            // ---------- Thế giới 9 · DÂY TRÓI ----------
            // Cặp liên kết: ăn một đầu thì đầu kia tự vỡ dù ở tận đầu bàn bên kia. Đây
            // là cơ chế đầu tiên phá giả định "muốn ăn thì phải kề", nên nó đổi hẳn kiểu
            // suy nghĩ: không còn là tìm chuỗi tại chỗ, mà là tính xem ăn ở đây thì mất
            // gì ở kia. Xem LevelDecorator để biết ba ràng buộc giữ cho màn vẫn giải được.
            new LevelConfig { World=9, Name="Sợi dây",      Columns=6, Rows=6, Colors=3, Seed=13101, Slack=4, Fuse=0.35, Undos=4, MinChain=3, MaxChain=5, Links=1 },
            new LevelConfig { World=9, Name="Hai sợi",      Columns=6, Rows=7, Colors=4, Seed=13207, Slack=4, Fuse=0.45, Undos=4, MinChain=3, MaxChain=5, Links=2 },
            new LevelConfig { World=9, Name="Trói chéo",    Columns=7, Rows=6, Colors=4, Seed=13311, Slack=3, Fuse=0.50, Undos=4, MinChain=3, MaxChain=5, Links=2 },
            new LevelConfig { World=9, Name="Ba mối",       Columns=7, Rows=7, Colors=4, Seed=13417, Slack=3, Fuse=0.55, Undos=3, MinChain=3, MaxChain=5, Links=3 },
            new LevelConfig { World=9, Name="Lưới trói",    Shape=WideShape,  Colors=4, Seed=13523, Slack=3, Fuse=0.55, Undos=3, MinChain=3, MaxChain=5, Links=3 },
            new LevelConfig { World=9, Name="Trói trong đá",Columns=7, Rows=7, Colors=4, Seed=13629, Slack=3, Fuse=0.55, Undos=3, MinChain=3, MaxChain=5, Links=2, Stones=4 },
            new LevelConfig { World=9, Name="Trói & băng",  Columns=7, Rows=7, Colors=4, Seed=13733, Slack=3, Fuse=0.55, Undos=3, MinChain=3, MaxChain=5, Links=2, Ices=4 },
            new LevelConfig { World=9, Name="Trói & sắc",   Columns=8, Rows=7, Colors=5, Seed=13837, Slack=3, Fuse=0.60, Undos=3, MinChain=3, MaxChain=5, Links=3, Wilds=2 },
            new LevelConfig { World=9, Name="Trói & ngòi",  Columns=8, Rows=7, Colors=5, Seed=13941, Slack=3, Fuse=0.60, Undos=3, MinChain=3, MaxChain=5, Links=3, Bombs=2 },
            new LevelConfig { World=9, Name="Nút thắt",     Columns=8, Rows=8, Colors=5, Seed=14047, Slack=3, Fuse=0.65, Undos=2, MinPathLength=3, MaxPathLength=6, MinChain=3, MaxChain=6, Links=4, Ices=4, Goals=5 },

            // ---------- Thế giới 10 · CHÍNH XÁC ----------
            // Ngân sách lượt bằng ĐÚNG par: không dư một nước nào. Đây là thế giới duy
            // nhất mà độ khó KHÔNG đến từ một cơ chế mới — nó đến từ việc bỏ hết lượt dư.
            // Chín thế giới trước đều cho Slack 2-5, nên một nước hớ vẫn trôi được tới
            // cuối ván; ở đây một nước hớ là hết.
            //
            // Ba lựa chọn cân bằng đi kèm, cả ba đều cần chứ không phải trang trí:
            //  - BÀN NHỎ (5x5 đến 6x7). Không dư lượt nghĩa là phải nhìn ra cả kế hoạch
            //    trước khi đi nước đầu, mà bàn 8x8 thì đó là việc không làm nổi bằng mắt.
            //  - NHIỀU HOÀN TÁC (6-8). Một nước hớ phải sửa được tại chỗ; không có nó thì
            //    mỗi sai sót đều bắt chơi lại từ đầu, và người chơi bỏ chứ không cố.
            //  - KHÔNG XÁO (Shuffles=0). Xáo lại dựng một lời giải mới cần RequiredMoves
            //    lượt rồi so với số lượt CÒN LẠI; với ngân sách khít thì nó gần như luôn
            //    thất bại, nên để nút đó sống chỉ là mời người chơi bấm vào một chỗ chết.
            //
            // Cơ chế thì DÙNG LẠI những gì chín thế giới trước đã dạy, mỗi màn một thứ.
            // Chồng cơ chế mới lên ngân sách khít là cộng hai cái khó vào nhau, và khi
            // thua thì người chơi không biết mình thua vì cái nào.
            new LevelConfig { World=10, Exact=true, Name="Khít khao",      Columns=5, Rows=5, Colors=3, Seed=15101, Slack=0, Fuse=0.30, Undos=8, Shuffles=0, MinPathLength=3, MaxPathLength=4, MinChain=2, MaxChain=4 },
            new LevelConfig { World=10, Exact=true, Name="Không dư",       Columns=5, Rows=5, Colors=3, Seed=15207, Slack=0, Fuse=0.35, Undos=8, Shuffles=0, MinPathLength=3, MaxPathLength=4, MinChain=3, MaxChain=4 },
            new LevelConfig { World=10, Exact=true, Name="Đúng bấy nhiêu", Columns=5, Rows=6, Colors=3, Seed=15311, Slack=0, Fuse=0.40, Undos=7, Shuffles=0, MinChain=3, MaxChain=5 },
            new LevelConfig { World=10, Exact=true, Name="Kim cương khít", Shape=DiamondShape, Colors=3, Seed=15413, Slack=0, Fuse=0.40, Undos=7, Shuffles=0, MinChain=3, MaxChain=5 },
            new LevelConfig { World=10, Exact=true, Name="Bốn màu khít",   Columns=6, Rows=6, Colors=4, Seed=15519, Slack=0, Fuse=0.45, Undos=7, Shuffles=0, MinChain=3, MaxChain=5 },
            new LevelConfig { World=10, Exact=true, Name="Đá khít",        Columns=6, Rows=6, Colors=4, Seed=15623, Slack=0, Fuse=0.45, Undos=7, Shuffles=0, MinChain=3, MaxChain=5, Stones=3 },
            new LevelConfig { World=10, Exact=true, Name="Sắc khít",       Columns=6, Rows=6, Colors=4, Seed=15729, Slack=0, Fuse=0.50, Undos=6, Shuffles=0, MinChain=3, MaxChain=5, Wilds=1 },
            new LevelConfig { World=10, Exact=true, Name="Băng khít",      Columns=6, Rows=6, Colors=4, Seed=15833, Slack=0, Fuse=0.50, Undos=6, Shuffles=0, MinChain=3, MaxChain=5, Ices=3 },
            new LevelConfig { World=10, Exact=true, Name="Trói khít",      Columns=6, Rows=6, Colors=4, Seed=15937, Slack=0, Fuse=0.50, Undos=6, Shuffles=0, MinChain=3, MaxChain=5, Links=2 },
            new LevelConfig { World=10, Exact=true, Name="Không sai một nước", Columns=6, Rows=7, Colors=4, Seed=16043, Slack=0, Fuse=0.55, Undos=6, Shuffles=0, MinChain=3, MaxChain=5, Stones=3, Wilds=1 }
        };
    }
}
