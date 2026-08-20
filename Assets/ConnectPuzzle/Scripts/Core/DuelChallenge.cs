using System;

namespace ConnectPuzzle.Core
{
    /// <summary>
    /// Đấu seed bạn bè: hai người nhập cùng một mã thì gặp cùng một bàn, rồi so xem
    /// ai ít lượt / điểm cao hơn.
    ///
    /// Không cần server. Tính giống nhau giữa các máy đã được ĐO chứ không phải giả
    /// định: bộ mẫu 150 bàn cho ra cùng vân tay trên PC (.NET JIT, x64), giả lập
    /// (IL2CPP, x86_64) và điện thoại thật (IL2CPP, ARM64) — xem BoardFingerprint.
    /// </summary>
    public static class DuelChallenge
    {
        /// <summary>
        /// Cấu hình bàn theo preset. Preset nằm TRONG mã, nên đổi hàm này là đổi bàn
        /// của mọi mã đã phát ra — phải tăng DuelCode.Version cùng lúc.
        /// </summary>
        public static LevelConfig ConfigFor(int seed, int preset)
        {
            var cfg = new LevelConfig
            {
                World = 0,
                Name = "Đấu " + seed,
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
            switch (preset)
            {
                case 1: cfg.Stones = 5; break;
                case 2: cfg.Ices = 4; break;
                case 3: cfg.Links = 2; cfg.Wilds = 1; cfg.Colors = 5; break;
            }
            return cfg;
        }

        public static LevelData Build(int seed, int preset) => LevelBuilder.Build(ConfigFor(seed, preset));

        public static string PresetName(int preset)
        {
            switch (preset)
            {
                case 1: return "Đá";
                case 2: return "Băng";
                case 3: return "Dây trói";
                default: return "Cơ bản";
            }
        }

        /// <summary>
        /// Sinh một seed mới từ một mầm bất kỳ (giờ hệ thống, số ngẫu nhiên...).
        /// Trộn qua hàm băm rồi mới cắt: cắt thẳng thì hai lần bấm cách nhau vài giây
        /// ra hai seed sát nhau, mà seed sát nhau cho ra bàn na ná.
        /// </summary>
        public static int SeedFrom(int entropy)
        {
            unchecked
            {
                uint h = (uint)entropy * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (int)(h % DuelCode.MaxSeed);
            }
        }
    }
}
