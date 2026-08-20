using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Một nút chọn màn. Instantiate 90 lần từ prefab thay vì dựng 90 lần bằng code.
    ///
    /// Lợi ích chính đúng với điều bạn cần: sửa MỘT prefab thì cả 90 nút đổi theo, và thêm
    /// màn mới không phải xuất lại prefab nào. Bản trước lưới màn chiếm 270 trong 613 node
    /// của prefab tổng, tức là gần nửa file chỉ để lặp lại một hình dạng duy nhất.
    /// </summary>
    public sealed class LevelButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text label;
        [SerializeField] private Text stars;

        /// <summary>Dấu ▼ của màn gravity. Luôn có trong prefab, TẮT sẵn.</summary>
        [SerializeField] private GameObject gravityBadge;

        public Button Button => this.button;
        public Text Label => this.label;
        public Text Stars => this.stars;
        public GameObject GravityBadge => this.gravityBadge;

        /// <summary>
        /// Dấu gravity nằm sẵn trong prefab và TẮT, chứ không tạo thêm node cho riêng màn
        /// gravity. Hai lý do: một prefab thay vì hai, và bật/tắt rẻ hơn Instantiate.
        /// </summary>
        public void SetGravity(bool on)
        {
            if (this.gravityBadge != null) this.gravityBadge.SetActive(on);
        }

        public System.Collections.Generic.List<string> MissingFields()
        {
            var missing = new System.Collections.Generic.List<string>();
            if (this.button == null) missing.Add(nameof(this.button));
            if (this.label == null) missing.Add(nameof(this.label));
            if (this.stars == null) missing.Add(nameof(this.stars));
            if (this.gravityBadge == null) missing.Add(nameof(this.gravityBadge));
            return missing;
        }

        /// <summary>Nối theo tên MỘT LẦN, chỉ dùng lúc dựng prefab — xem LanPanelView.</summary>
        public void BindByNameForAuthoring()
        {
            this.button = GetComponent<Button>();
            this.label = Find<Text>("Label");
            this.stars = Find<Text>("Stars");
            Transform badge = FindNode("Gravity");
            this.gravityBadge = badge == null ? null : badge.gameObject;
        }

        private Transform FindNode(string name)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private T Find<T>(string name) where T : Component
        {
            Transform t = FindNode(name);
            return t == null ? null : t.GetComponent<T>();
        }
    }
}
