using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Một ô bàn. Là MonoBehaviour để instantiate được từ prefab.
    ///
    /// Giữ nguyên PUBLIC FIELD chứ không đổi sang [SerializeField] + property: Unity tự
    /// serialize field public, nên prefab vẫn giữ đủ tham chiếu, mà hàng chục dòng
    /// cell.Fill / cell.Glyph / cell.Ice trong BoardView không phải sửa dòng nào. Đổi
    /// sang property là một lần sửa lan rộng không mang lại gì.
    /// </summary>
    public sealed class CellView : MonoBehaviour
    {
        public RectTransform Root;
        public Image SlotBackground;
        public RectTransform BubbleRoot;
        public Image Glow;          // chỉ dùng cho lúc chẩn đoán thua
        public Image Shadow;
        public Image Fill;
        public Image Sheen;
        public Image Ring;          // chỉ dùng cho lúc chẩn đoán thua
        public Text Glyph;

        /// <summary>Vòng đích — nằm NGOÀI BubbleRoot nên hoạt ảnh nổ ô không nuốt nó.</summary>
        public Image GoalRing;

        /// <summary>Lớp băng phủ lên ô — nằm TRONG BubbleRoot để co giãn cùng ô.</summary>
        public Image Ice;

        /// <summary>Số đếm ngược của ngòi nổ, và nền tròn của nó.</summary>
        public Text Fuse;
        public Image FuseBadge;

        public bool IsWall;

        /// <summary>Tham chiếu còn trống — bài kiểm đọc để bắt lỗi quên gán trong prefab.</summary>
        public System.Collections.Generic.List<string> MissingFields()
        {
            var missing = new System.Collections.Generic.List<string>();
            if (this.Root == null) missing.Add("Root");
            if (this.SlotBackground == null) missing.Add("SlotBackground");
            if (this.BubbleRoot == null) missing.Add("BubbleRoot");
            if (this.Glow == null) missing.Add("Glow");
            if (this.Shadow == null) missing.Add("Shadow");
            if (this.Fill == null) missing.Add("Fill");
            if (this.Sheen == null) missing.Add("Sheen");
            if (this.Ring == null) missing.Add("Ring");
            if (this.Glyph == null) missing.Add("Glyph");
            if (this.GoalRing == null) missing.Add("GoalRing");
            if (this.Ice == null) missing.Add("Ice");
            if (this.Fuse == null) missing.Add("Fuse");
            if (this.FuseBadge == null) missing.Add("FuseBadge");
            return missing;
        }

        /// <summary>Nối theo tên MỘT LẦN, chỉ dùng lúc dựng prefab.</summary>
        public void BindByNameForAuthoring()
        {
            this.Root = (RectTransform)transform;
            this.SlotBackground = Find<Image>("Slot");
            this.BubbleRoot = Find<Image>("Fill") == null ? null
                            : (RectTransform)Find<Image>("Fill").transform.parent;
            this.Glow = Find<Image>("Glow");
            this.Shadow = Find<Image>("Shadow");
            this.Fill = Find<Image>("Fill");
            this.Sheen = Find<Image>("Sheen");
            this.Ring = Find<Image>("Ring");
            this.Ice = Find<Image>("Ice");
            this.Glyph = Find<Text>("Glyph");
            this.GoalRing = Find<Image>("GoalRing");
            this.FuseBadge = Find<Image>("FuseBadge");
            this.Fuse = Find<Text>("Fuse");
        }

        private T Find<T>(string name) where T : Component
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                {
                    T found = t.GetComponent<T>();
                    if (found != null) return found;
                }
            return null;
        }

        // Lò xo cho việc phóng to khi chọn — giữ riêng để không giật khi đổi mục tiêu
        public float ScaleTarget = 1f;
        public float ScaleCurrent = 1f;
        public float ScaleVelocity;

        public void SetAlive(bool alive, bool showSymbols)
        {
            this.Fill.enabled = alive;
            this.Sheen.enabled = alive;
            this.Shadow.enabled = alive;
            this.Glyph.enabled = alive && showSymbols;
            if (this.GoalRing != null) this.GoalRing.enabled = false;
            if (this.Ice != null) this.Ice.enabled = false;
            if (this.Fuse != null) this.Fuse.enabled = false;
            if (this.FuseBadge != null) this.FuseBadge.enabled = false;

            // LUÔN tắt, không chỉ khi ô đã chết. Ring/Glow chỉ bật trong lúc chẩn đoán
            // thua để chỉ chỗ sai, và ô bị chỉ mặt thường VẪN CÒN SỐNG — trước đây chỉ
            // tắt trong nhánh !alive nên viền đỏ sống sót qua cả Chơi lại.
            this.Glow.enabled = false;
            this.Ring.enabled = false;
        }

        public void ResetScale()
        {
            this.ScaleTarget = 1f;
            this.ScaleCurrent = 1f;
            this.ScaleVelocity = 0f;
            if (this.BubbleRoot != null) this.BubbleRoot.localScale = Vector3.one;
        }
    }
}
