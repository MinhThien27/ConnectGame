using System;
using System.Text;
using ConnectPuzzle.Core;
using UnityEngine;

namespace ConnectPuzzle.View
{
    /// <summary>Tiến độ và tuỳ chọn, lưu bằng PlayerPrefs.</summary>
    public static class PuzzleProgress
    {
        private const string StarsKey    = "connectPuzzle.stars.";
        private const string BestKey     = "connectPuzzle.best.";
        private const string SoundKey    = "connectPuzzle.sound";
        private const string SymbolsKey  = "connectPuzzle.symbols";
        private const string LevelCount  = "connectPuzzle.levelCount";

        public static int Stars(int levelIndex) => PlayerPrefs.GetInt(StarsKey + levelIndex, 0);
        public static int Best(int levelIndex)  => PlayerPrefs.GetInt(BestKey + levelIndex, 0);

        public static bool Sound
        {
            get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
            set { PlayerPrefs.SetInt(SoundKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool Symbols
        {
            get => PlayerPrefs.GetInt(SymbolsKey, 0) == 1;
            set { PlayerPrefs.SetInt(SymbolsKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        private const string HapticsKey = "connectPuzzle.haptics";

        /// <summary>
        /// Rung phản hồi. Mặc định BẬT: nó chỉ cảm được khi đang chơi, nên để mặc định tắt
        /// thì gần như không ai đi tìm để bật lên — tức là viết xong rồi không ai dùng.
        /// </summary>
        public static bool Haptics
        {
            get => PlayerPrefs.GetInt(HapticsKey, 1) == 1;
            set { PlayerPrefs.SetInt(HapticsKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>
        /// Vòng ba trạng thái của nút phản hồi ở chân menu:
        /// tắt hết → chỉ âm thanh → âm thanh + rung → tắt hết.
        ///
        /// Ba trạng thái dồn vào MỘT nút chứ không thêm nút thứ năm, vì bố cục chân menu
        /// và ảnh chụp prefab đều đang chốt theo bốn nút hiện có — thêm nút là sửa prefab
        /// và chốt lại ảnh chụp, một cái giá quá đắt cho một tuỳ chọn.
        /// </summary>
        public static void CycleFeedback()
        {
            if (!Sound) { Sound = true; Haptics = false; }
            else if (!Haptics) Haptics = true;
            else { Sound = false; Haptics = false; }
        }

        /// <summary>
        /// Nhãn đọc từ CẶP (âm thanh, rung) chứ không từ vị trí trong vòng, nên nó nói
        /// đúng cả tổ hợp mà vòng không sinh ra: nút ♪ trong ván tắt tiếng riêng, để lại
        /// trạng thái còn rung mà không còn tiếng.
        /// </summary>
        public static string FeedbackLabel()
        {
            if (Sound && Haptics) return "Âm thanh + Rung";
            if (Sound) return "Âm thanh: Bật";
            if (Haptics) return "Chỉ rung";
            return "Âm thanh: Tắt";
        }

        // ------------------------------------------------------------------
        // Bài hướng dẫn — mỗi thế giới một bài, hiện MỘT LẦN
        // ------------------------------------------------------------------
        private const string TutorialKey = "connectPuzzle.tutorial.";

        /// <summary>Đã xem bài hướng dẫn của thế giới này chưa.</summary>
        public static bool TutorialSeen(int world) =>
            PlayerPrefs.GetInt(TutorialKey + world, 0) == 1;

        public static void MarkTutorialSeen(int world)
        {
            PlayerPrefs.SetInt(TutorialKey + world, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Câu nhắc ngắn (toast) cho BIẾN THỂ của một cơ chế đã được thẻ hướng dẫn dạy —
        /// ví dụ "đá dày cần va 2 lần". Đánh số theo thứ tự trong bảng ở PuzzleGame.
        ///
        /// Trước đây danh sách đã-hiện chỉ là một HashSet trong bộ nhớ, nên mọi câu nhắc
        /// hiện LẠI sau mỗi lần mở app — câu "lần đầu gặp" mà gặp lại mãi.
        /// </summary>
        private const string IntroKey = "connectPuzzle.intro.";

        /// <summary>
        /// Cận trên số câu nhắc, chỉ dùng để dọn khoá khi xoá tiến độ. Là cận trên chứ
        /// không phải số thật vì bảng câu nhắc nằm ở PuzzleGame (tầng trên), và để tầng
        /// dưới hỏi ngược lên thì đổi được một con số không đáng.
        /// </summary>
        private const int IntroCapacity = 16;

        public static bool IntroSeen(int rule) => PlayerPrefs.GetInt(IntroKey + rule, 0) == 1;

        public static void MarkIntroSeen(int rule)
        {
            PlayerPrefs.SetInt(IntroKey + rule, 1);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------
        // Leo tháp — kết quả tốt nhất của mỗi thế giới
        //
        // Chỉ lưu THÀNH TÍCH, không cấp sao. Chặng leo tháp chơi lại được vô hạn lần, nên
        // cho nó đẻ ra sao là mở một vòng cày: chạy lại chặng dễ nhất để mua vật phẩm.
        // ------------------------------------------------------------------
        private const string TowerDoneKey = "connectPuzzle.tower.done.";
        private const string TowerLeftKey = "connectPuzzle.tower.left.";

        /// <summary>Số màn qua được nhiều nhất trong một chặng của thế giới này.</summary>
        public static int TowerBest(int world) => PlayerPrefs.GetInt(TowerDoneKey + world, 0);

        /// <summary>Số lượt còn lại của lần tốt nhất — mốc để lần sau vượt.</summary>
        public static int TowerBestLeft(int world) => PlayerPrefs.GetInt(TowerLeftKey + world, 0);

        /// <summary>
        /// Ghi kết quả một chặng. Trả true nếu tốt hơn lần trước.
        ///
        /// So THEO THỨ TỰ: số màn qua được trước, rồi mới tới lượt còn lại. Qua 5 màn mà
        /// cạn lượt vẫn hơn qua 4 màn mà dư nhiều — nếu so lượt trước thì bảng thành tích
        /// sẽ khen người bỏ chặng giữa đường.
        /// </summary>
        public static bool RecordTower(int world, int cleared, int movesLeft)
        {
            int bestDone = TowerBest(world);
            if (cleared < bestDone) return false;
            if (cleared == bestDone && movesLeft <= TowerBestLeft(world)) return false;

            PlayerPrefs.SetInt(TowerDoneKey + world, cleared);
            PlayerPrefs.SetInt(TowerLeftKey + world, movesLeft);
            PlayerPrefs.Save();
            return true;
        }

        private const string FreeKey        = "connectPuzzle.freePlay";
        private const string EndlessBestKey = "connectPuzzle.endlessBest";

        /// <summary>
        /// Chơi tự do: bỏ hẳn hàng rào tiến trình. Tiến độ VẪN ghi bình thường — bật
        /// cái này chỉ mở cửa, không xoá gì, nên tắt đi là mọi thứ trở về như cũ.
        /// </summary>
        public static bool FreePlay
        {
            get => PlayerPrefs.GetInt(FreeKey, 0) == 1;
            set { PlayerPrefs.SetInt(FreeKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static int EndlessBest => PlayerPrefs.GetInt(EndlessBestKey, 0);

        /// <summary>Trả true nếu là kỷ lục mới của chế độ vô tận.</summary>
        public static bool RecordEndless(int score)
        {
            if (score <= EndlessBest) return false;
            PlayerPrefs.SetInt(EndlessBestKey, score);
            PlayerPrefs.Save();
            return true;
        }

        public static bool IsUnlocked(int levelIndex) =>
            FreePlay || levelIndex == 0 || Stars(levelIndex - 1) > 0;

        // ------------------------------------------------------------------
        // Ván vô tận đang chơi — lưu để TIẾP TỤC, không phải chơi lại từ đầu.
        //
        // Trước đây thoát ra (nút ←) hay đóng app là mất luôn ván đang chơi: vào lại
        // Vô tận luôn là bàn mới. Giờ lưu nguyên bàn + điểm + combo lúc rời đi, và nạp
        // lại đúng chỗ đó khi mở lại — giống hầu hết game di động cùng dạng.
        //
        // CHỈ lưu khi người chơi CHỦ ĐỘNG rời màn hình (bấm ←) — bấm "Chơi lại" là ý
        // định ngược lại (bỏ ván này, bắt đầu ván mới) nên phải XOÁ save, không phải
        // ghi đè; và khi ván tự kết thúc vì hết nước đi thì cũng xoá, vì không còn gì
        // để tiếp tục nữa.
        // ------------------------------------------------------------------
        private const string EndlessStateKey = "connectPuzzle.endlessState";

        public static bool HasEndlessSave() => PlayerPrefs.HasKey(EndlessStateKey);

        public static void SaveEndlessState(PuzzleSession session)
        {
            if (session == null || session.Level == null || !session.Level.Endless) return;

            var sb = new StringBuilder();
            sb.Append("v1;").Append(session.Score).Append(';').Append(session.MovesUsed).Append(';')
              .Append(session.Combo).Append(';').Append(session.ShufflesLeft).Append(';');

            PuzzleSession.EndlessCellSnapshot[][] columns = session.CaptureEndlessColumns();
            for (int x = 0; x < columns.Length; x++)
            {
                if (x > 0) sb.Append('|');
                for (int i = 0; i < columns[x].Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(columns[x][i].Color).Append(columns[x][i].Wild ? 'w' : 'n');
                }
            }

            PlayerPrefs.SetString(EndlessStateKey, sb.ToString());
            PlayerPrefs.Save();
        }

        public static void ClearEndlessState()
        {
            PlayerPrefs.DeleteKey(EndlessStateKey);
        }

        /// <summary>
        /// Nạp save (nếu có) vào một PuzzleSession vừa tạo cho màn Endless. Trả false
        /// nếu không có save hoặc dữ liệu hỏng — dữ liệu hỏng thì XOÁ LUÔN, để không lỗi
        /// lặp lại mỗi lần mở game về sau.
        /// </summary>
        public static bool TryLoadEndlessState(PuzzleSession session)
        {
            if (!PlayerPrefs.HasKey(EndlessStateKey)) return false;
            string raw = PlayerPrefs.GetString(EndlessStateKey, "");

            try
            {
                string[] head = raw.Split(';');
                if (head.Length < 6 || head[0] != "v1") { ClearEndlessState(); return false; }

                int score = int.Parse(head[1]);
                int moves = int.Parse(head[2]);
                int combo = int.Parse(head[3]);
                int shuffles = int.Parse(head[4]);
                string[] colStrings = head[5].Split('|');

                var columns = new PuzzleSession.EndlessCellSnapshot[colStrings.Length][];
                for (int x = 0; x < colStrings.Length; x++)
                {
                    if (colStrings[x].Length == 0) { columns[x] = new PuzzleSession.EndlessCellSnapshot[0]; continue; }

                    string[] cells = colStrings[x].Split(',');
                    columns[x] = new PuzzleSession.EndlessCellSnapshot[cells.Length];
                    for (int i = 0; i < cells.Length; i++)
                    {
                        string c = cells[i];
                        bool wild = c[c.Length - 1] == 'w';
                        int color = int.Parse(c.Substring(0, c.Length - 1));
                        columns[x][i] = new PuzzleSession.EndlessCellSnapshot { Color = color, Wild = wild };
                    }
                }

                session.RestoreEndless(columns, score, moves, combo, shuffles);
                return true;
            }
            catch (Exception)
            {
                ClearEndlessState();
                return false;
            }
        }

        /// <summary>Chỉ ghi khi tốt hơn lần trước. Trả true nếu điểm là kỷ lục mới.</summary>
        public static bool Record(int levelIndex, int stars, int score)
        {
            bool newRecord = score > Best(levelIndex);
            if (stars > Stars(levelIndex)) PlayerPrefs.SetInt(StarsKey + levelIndex, stars);
            if (newRecord) PlayerPrefs.SetInt(BestKey + levelIndex, score);
            PlayerPrefs.Save();
            return newRecord;
        }

        public static void ResetAll(int levelCount)
        {
            for (int i = 0; i < levelCount; i++)
            {
                PlayerPrefs.DeleteKey(StarsKey + i);
                PlayerPrefs.DeleteKey(BestKey + i);
            }

            // Chuỗi ngày cũng là tiến độ. Bỏ sót nó thì "Xoá tiến độ" để lại một chuỗi
            // 30 ngày treo trên menu của một hồ sơ vừa bị xoá trắng.
            PlayerPrefs.DeleteKey(DailyDayKey);
            PlayerPrefs.DeleteKey(DailyScoreKey);
            PlayerPrefs.DeleteKey(DailyStarsKey);
            PlayerPrefs.DeleteKey(DailyWonKey);
            PlayerPrefs.DeleteKey(DailyStreakKey);
            PlayerPrefs.DeleteKey(DailyLastKey);
            PlayerPrefs.DeleteKey(StarsSpentKey);
            for (int i = 0; i < levelCount; i++) PlayerPrefs.DeleteKey(MedalKey + i);

            // Bài hướng dẫn cũng là tiến độ: hồ sơ vừa xoá trắng thì phải được dạy lại
            // từ đầu, không thì người chơi mới nhận một bàn có đá mà chưa ai nói đá là gì.
            foreach (TutorialLesson lesson in TutorialLessons.All)
                PlayerPrefs.DeleteKey(TutorialKey + lesson.World);
            for (int i = 0; i < IntroCapacity; i++) PlayerPrefs.DeleteKey(IntroKey + i);

            foreach (TutorialLesson lesson in TutorialLessons.All)
            {
                PlayerPrefs.DeleteKey(TowerDoneKey + lesson.World);
                PlayerPrefs.DeleteKey(TowerLeftKey + lesson.World);
            }

            PlayerPrefs.SetInt(LevelCount, levelCount);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------
        // Thử thách hằng ngày
        //
        // Chỉ giữ tiến độ của ĐÚNG một ngày (ngày gần nhất có chơi). Không lưu lịch sử:
        // bàn của mọi ngày đều sinh lại được từ khoá ngày, nên lịch sử chẳng thêm gì mà
        // PlayerPrefs thì phình mãi không có ai dọn.
        // ------------------------------------------------------------------
        private const string DailyDayKey    = "connectPuzzle.daily.day";     // ngày của bản ghi dưới đây
        private const string DailyScoreKey  = "connectPuzzle.daily.score";
        private const string DailyStarsKey  = "connectPuzzle.daily.stars";
        private const string DailyWonKey    = "connectPuzzle.daily.won";
        private const string DailyStreakKey = "connectPuzzle.daily.streak";
        private const string DailyLastKey   = "connectPuzzle.daily.last";    // ngày cuối HOÀN THÀNH

        public static int DailyStreak   => PlayerPrefs.GetInt(DailyStreakKey, 0);
        public static int DailyLastDone => PlayerPrefs.GetInt(DailyLastKey, 0);

        /// <summary>Đã thắng thử thách của ngày này chưa.</summary>
        public static bool DailyWon(int dayKey) =>
            PlayerPrefs.GetInt(DailyDayKey, 0) == dayKey && PlayerPrefs.GetInt(DailyWonKey, 0) == 1;

        /// <summary>Điểm cao nhất trong ngày này; 0 nếu bản ghi đang thuộc về ngày khác.</summary>
        public static int DailyBest(int dayKey) =>
            PlayerPrefs.GetInt(DailyDayKey, 0) == dayKey ? PlayerPrefs.GetInt(DailyScoreKey, 0) : 0;

        public static int DailyStars(int dayKey) =>
            PlayerPrefs.GetInt(DailyDayKey, 0) == dayKey ? PlayerPrefs.GetInt(DailyStarsKey, 0) : 0;

        /// <summary>
        /// Chuỗi ngày HIỆN CÒN HIỆU LỰC. Khác với DailyStreak thô: bỏ lỡ một ngày thì
        /// con số lưu trong prefs vẫn còn nguyên cho tới lần chơi kế tiếp, nên hiện thẳng
        /// nó lên menu sẽ khoe "chuỗi 12 ngày" cho người đã nghỉ cả tháng.
        /// </summary>
        public static int DailyStreakLive(int todayKey)
        {
            int last = DailyLastDone;
            if (last == todayKey || DailyChallenge.IsConsecutive(last, todayKey)) return DailyStreak;
            return 0;
        }

        /// <summary>
        /// Ghi kết quả một ván thử thách. Trả true nếu là điểm cao mới trong ngày.
        /// Chuỗi ngày chỉ cộng khi THẮNG — vào xem bàn rồi thoát không tính.
        /// </summary>
        public static bool RecordDaily(int dayKey, int stars, int score, bool won)
        {
            bool sameDay = PlayerPrefs.GetInt(DailyDayKey, 0) == dayKey;
            bool newBest = !sameDay || score > PlayerPrefs.GetInt(DailyScoreKey, 0);

            if (!sameDay)
            {
                // sang ngày mới: bản ghi cũ hết nghĩa, ghi đè sạch thay vì cộng dồn
                PlayerPrefs.SetInt(DailyDayKey, dayKey);
                PlayerPrefs.SetInt(DailyScoreKey, 0);
                PlayerPrefs.SetInt(DailyStarsKey, 0);
                PlayerPrefs.SetInt(DailyWonKey, 0);
            }

            if (newBest) PlayerPrefs.SetInt(DailyScoreKey, score);
            if (stars > PlayerPrefs.GetInt(DailyStarsKey, 0)) PlayerPrefs.SetInt(DailyStarsKey, stars);

            if (won && PlayerPrefs.GetInt(DailyWonKey, 0) == 0)
            {
                PlayerPrefs.SetInt(DailyWonKey, 1);
                PlayerPrefs.SetInt(DailyStreakKey,
                    DailyChallenge.StreakAfter(DailyStreak, DailyLastDone, dayKey));
                PlayerPrefs.SetInt(DailyLastKey, dayKey);
            }

            PlayerPrefs.Save();
            return newBest;
        }

        // ------------------------------------------------------------------
        // Ví sao — tiền để mua vật phẩm
        //
        // Sao KHÔNG bị trừ khỏi bảng thành tích: số sao hiện trên từng màn vẫn là
        // thành tích vĩnh viễn, còn ví chỉ là "tổng đã kiếm trừ tổng đã tiêu". Trừ
        // thẳng vào Stars(i) thì tiêu sao xong màn đã 3★ tụt xuống 1★, và lịch sử
        // chơi bị viết lại — thứ không ai chấp nhận ở một game giải đố.
        // ------------------------------------------------------------------
        private const string StarsSpentKey = "connectPuzzle.starsSpent";
        private const string MedalKey      = "connectPuzzle.medal.";

        /// <summary>Sao thưởng cho mỗi huy hiệu kỹ thuật. Đây là lý do đi săn huy hiệu.</summary>
        public const int MedalBonus = 2;

        public static bool Medal(int levelIndex) => PlayerPrefs.GetInt(MedalKey + levelIndex, 0) == 1;

        /// <summary>Trả true nếu đây là lần ĐẦU lấy được huy hiệu của màn này.</summary>
        public static bool RecordMedal(int levelIndex)
        {
            if (levelIndex < 0 || Medal(levelIndex)) return false;
            PlayerPrefs.SetInt(MedalKey + levelIndex, 1);
            PlayerPrefs.Save();
            return true;
        }

        public static int MedalCount(int levelCount)
        {
            int n = 0;
            for (int i = 0; i < levelCount; i++) if (Medal(i)) n++;
            return n;
        }

        /// <summary>Tổng sao đã kiếm được trên toàn bộ chiến dịch.</summary>
        public static int StarsEarnedTotal(int levelCount)
        {
            int total = 0;
            for (int i = 0; i < levelCount; i++)
            {
                total += Stars(i);
                if (Medal(i)) total += MedalBonus;      // huy hiệu cũng là sao tiêu được
            }
            return total;
        }

        public static int StarsSpent => PlayerPrefs.GetInt(StarsSpentKey, 0);

        /// <summary>Sao còn tiêu được. Không bao giờ âm, kể cả khi tiến độ bị xoá giữa chừng.</summary>
        public static int StarsBalance(int levelCount)
        {
            int left = StarsEarnedTotal(levelCount) - StarsSpent;
            return left < 0 ? 0 : left;
        }

        /// <summary>Trừ sao. Trả false (và không trừ gì) nếu không đủ.</summary>
        public static bool SpendStars(int cost, int levelCount)
        {
            if (cost <= 0) return true;
            if (StarsBalance(levelCount) < cost) return false;
            PlayerPrefs.SetInt(StarsSpentKey, StarsSpent + cost);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Hoàn sao khi người chơi hoàn tác bước đã dùng vật phẩm.</summary>
        public static void RefundStars(int cost)
        {
            if (cost <= 0) return;
            int spent = StarsSpent - cost;
            PlayerPrefs.SetInt(StarsSpentKey, spent < 0 ? 0 : spent);
            PlayerPrefs.Save();
        }
    }
}
