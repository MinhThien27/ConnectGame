using System.Collections.Generic;

namespace ConnectPuzzle.Core
{
    /// <summary>
    /// mulberry32 — PRNG có seed, cùng seed cho ra cùng dãy số trên mọi máy.
    /// Bắt buộc phải tất định: toàn bộ màn chơi được sinh từ seed, nên một thay
    /// đổi nhỏ trong dãy số sẽ ra bàn khác.
    ///
    /// Port từ JS. Vài chi tiết phải giữ nguyên từng bit:
    ///  - JS `a + 0x6D2B79F5 | 0` là cộng tràn 32-bit  -> unchecked int
    ///  - JS `>>>` là dịch phải KHÔNG dấu             -> (int)((uint)x >> n)
    ///  - JS `Math.imul` là nhân tràn 32-bit           -> unchecked int *
    ///  - JS `+` bind chặt hơn `^`, nên thứ tự ngoặc dưới đây là cố ý
    /// </summary>
    public sealed class DeterministicRng
    {
        private int state;

        public DeterministicRng(int seed)
        {
            this.state = seed;
        }

        /// <summary>Số thực trong [0, 1).</summary>
        public double NextDouble()
        {
            unchecked
            {
                this.state = this.state + 0x6D2B79F5;
                int a = this.state;
                int t = (a ^ (int)((uint)a >> 15)) * (1 | a);
                t = (t + (t ^ (int)((uint)t >> 7)) * (61 | t)) ^ t;
                return (uint)(t ^ (int)((uint)t >> 14)) / 4294967296.0;
            }
        }

        /// <summary>Số nguyên trong [0, exclusiveMax).</summary>
        public int NextInt(int exclusiveMax)
        {
            return (int)(NextDouble() * exclusiveMax);
        }

        /// <summary>Số nguyên trong [min, max] (bao gồm hai đầu).</summary>
        public int NextRange(int min, int max)
        {
            return min + (int)(NextDouble() * (max - min + 1));
        }
    }

    /// <summary>
    /// Tập số nguyên GIỮ THỨ TỰ CHÈN, xoá không làm đảo thứ tự.
    ///
    /// Đây không phải chi tiết trang trí: bộ sinh màn duyệt tập ô trống để chọn
    /// ô "bị bó buộc nhất", và khi nhiều ô bằng điểm thì kết quả phụ thuộc thứ
    /// tự duyệt. HashSet của .NET không bảo đảm thứ tự, dùng nó vào đây thì mỗi
    /// lần chạy có thể ra bàn khác nhau — mất tính tất định. JS Set giữ thứ tự
    /// chèn, nên bản port phải làm đúng vậy.
    /// </summary>
    public sealed class OrderedIntSet
    {
        private readonly List<int> order = new List<int>();
        private readonly HashSet<int> member = new HashSet<int>();

        public int Count => this.order.Count;

        public OrderedIntSet() { }

        public OrderedIntSet(IEnumerable<int> items)
        {
            foreach (int i in items) Add(i);
        }

        public bool Contains(int value) => this.member.Contains(value);

        public void Add(int value)
        {
            if (this.member.Add(value)) this.order.Add(value);
        }

        public void Remove(int value)
        {
            if (this.member.Remove(value)) this.order.Remove(value);
        }

        /// <summary>Duyệt theo thứ tự chèn. Không được sửa tập trong lúc duyệt.</summary>
        public List<int> Items => this.order;
    }
}
