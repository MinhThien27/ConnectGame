using System;

namespace ConnectPuzzle.Core
{
    /// <summary>
    /// Thử thách hằng ngày: mỗi ngày một bàn, GIỐNG NHAU trên mọi máy.
    ///
    /// Không cần cơ chế chơi mới — nó dùng lại đúng bộ sinh màn đã có, chỉ khác ở chỗ
    /// seed lấy từ NGÀY thay vì từ bảng màn. Nhờ vậy hai người ở hai nơi mở cùng một
    /// ngày sẽ gặp cùng một bàn và so điểm với nhau được.
    ///
    /// Dùng NGÀY UTC, không dùng giờ máy: lấy giờ máy thì hai người ở hai múi giờ nhận
    /// hai bàn khác nhau trong cùng một "hôm nay", và bảng so điểm mất nghĩa.
    /// </summary>
    public static class DailyChallenge
    {
        /// <summary>Khoá ngày dạng yyyyMMdd — vừa làm seed vừa làm khoá lưu tiến độ.</summary>
        public static int DayKey(DateTime utcDate)
        {
            return utcDate.Year * 10000 + utcDate.Month * 100 + utcDate.Day;
        }

        public static int TodayKey() => DayKey(DateTime.UtcNow);

        /// <summary>
        /// Seed của một ngày. Trộn khoá ngày qua một hàm băm nhỏ thay vì dùng thẳng:
        /// dùng thẳng thì hai ngày liền nhau chỉ khác nhau 1 đơn vị, mà mulberry32 với
        /// hai seed sát nhau cho ra bàn na ná — cảm giác "hôm nay giống hôm qua".
        /// </summary>
        public static int SeedFor(int dayKey)
        {
            unchecked
            {
                uint h = (uint)dayKey * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (int)(h & 0x7FFFFFFF);
            }
        }

        /// <summary>
        /// Cấu hình bàn của một ngày. Xoay theo THỨ trong tuần để mỗi ngày một vị, mà
        /// vẫn đoán trước được — thứ Hai luôn là ngày có đá, nên người chơi biết mình
        /// đang chờ gì.
        ///
        /// Mọi ngày đều là màn TĨNH: gravity không dùng được đá/băng/liên kết, mà đó
        /// lại là phần lớn cái hay của vòng xoay này.
        /// </summary>
        public static LevelConfig ConfigFor(int dayKey)
        {
            int seed = SeedFor(dayKey);
            // Lấy THỨ thật, không lấy dayKey % 7: dayKey là số yyyyMMdd nên phép chia dư
            // của nó không trùng với thứ (31/1 và 1/2 cách nhau 70 đơn vị, cùng số dư —
            // hai ngày liền nhau lại ra cùng một kiểu bàn).
            int flavour = (int)FromKey(dayKey).DayOfWeek;

            var cfg = new LevelConfig
            {
                World = 0,
                Name = "Thử thách " + (dayKey % 100) + "/" + (dayKey / 100 % 100),
                Columns = 7,
                Rows = 7,
                Colors = 4,
                Seed = seed,
                Slack = 3,
                Fuse = 0.55,
                Undos = 3,
                Shuffles = 2,
                MinChain = 3,
                MaxChain = 5
            };

            switch (flavour)
            {
                case 0: break;                                   // bàn trơn
                case 1: cfg.Stones = 5; break;
                case 2: cfg.Wilds = 2; cfg.Colors = 5; break;
                case 3: cfg.Ices = 4; break;
                case 4: cfg.Bombs = 3; break;
                case 5: cfg.Links = 2; break;
                default:                                          // ngày tổng hợp
                    cfg.Ices = 3;
                    cfg.Wilds = 1;
                    cfg.Links = 1;
                    cfg.Colors = 5;
                    break;
            }
            return cfg;
        }

        public static LevelData BuildFor(int dayKey) => LevelBuilder.Build(ConfigFor(dayKey));

        public static LevelData BuildToday() => BuildFor(TodayKey());


        /// <summary>Ngược của DayKey. Ném nếu khoá không phải ngày có thật.</summary>
        public static DateTime FromKey(int dayKey)
        {
            return new DateTime(dayKey / 10000, dayKey / 100 % 100, dayKey % 100);
        }

        /// <summary>
        /// key có phải là NGÀY LIỀN SAU prevKey không.
        ///
        /// Phải đi qua DateTime, không được so "key - prevKey == 1": ngày 1 mọi tháng
        /// đều gãy (20260101 - 1 = 20260100, không phải 20251231), và ngày 1/3 còn phụ
        /// thuộc năm nhuận. Đây là kiểu lỗi chỉ nổ vài lần một năm nên không ai bắt
        /// được bằng cách chơi thử.
        /// </summary>
        public static bool IsConsecutive(int prevKey, int key)
        {
            try { return FromKey(prevKey).AddDays(1) == FromKey(key); }
            catch (ArgumentOutOfRangeException) { return false; }
        }

        /// <summary>
        /// Chuỗi ngày mới sau khi hoàn thành ngày key.
        /// - liền sau ngày trước  -> +1
        /// - vẫn đúng ngày đó     -> giữ nguyên (chơi lại trong ngày không cộng thêm)
        /// - cách quãng / lần đầu -> về 1
        /// </summary>
        public static int StreakAfter(int prevStreak, int prevKey, int key)
        {
            if (prevKey == key) return prevStreak < 1 ? 1 : prevStreak;
            if (IsConsecutive(prevKey, key)) return prevStreak + 1;
            return 1;
        }
    }
}
