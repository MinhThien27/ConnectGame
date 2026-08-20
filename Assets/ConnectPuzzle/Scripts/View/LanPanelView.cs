using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Tham chiếu tới các phần tử của bảng "Đấu cùng Wi-Fi" trong prefab.
    ///
    /// Dùng [SerializeField] chứ KHÔNG tìm theo tên. Tìm theo tên nghĩa là đổi tên một
    /// node trong Editor sẽ làm code vỡ âm thầm — mà "dễ quản lý trong Editor" chính là
    /// lý do chuyển sang prefab, nên không thể để việc sửa trong Editor thành nguy hiểm.
    ///
    /// Đổi lại, prefab sinh ra một loại lỗi mới mà code-dựng-UI không có: QUÊN GÁN một
    /// field. Lúc chạy nó nổ NullReferenceException ở một chỗ chẳng liên quan. Vì vậy có
    /// Validate() và một bài kiểm chạy nó trên prefab thật.
    /// </summary>
    public sealed class LanPanelView : MonoBehaviour
    {
        [Header("Khung")]
        [SerializeField] private RectTransform panel;

        [Header("Chữ")]
        [SerializeField] private Text head;
        [SerializeField] private Text note;
        [SerializeField] private Text status;

        [Header("Nút")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button seekButton;
        [SerializeField] private Button closeButton;

        public RectTransform Panel => this.panel;
        public Text Head => this.head;
        public Text Note => this.note;
        public Text Status => this.status;
        public Button HostButton => this.hostButton;
        public Button SeekButton => this.seekButton;
        public Button CloseButton => this.closeButton;

        /// <summary>
        /// Liệt kê MỌI field còn trống. Trả về danh sách rỗng nghĩa là prefab gán đủ.
        ///
        /// Trả về danh sách thay vì bool: "prefab thiếu tham chiếu" là câu vô dụng khi
        /// có tám field, còn "thiếu status, seekButton" thì sửa được ngay.
        /// </summary>
        public System.Collections.Generic.List<string> MissingFields()
        {
            var missing = new System.Collections.Generic.List<string>();
            if (this.panel == null) missing.Add(nameof(this.panel));
            if (this.head == null) missing.Add(nameof(this.head));
            if (this.note == null) missing.Add(nameof(this.note));
            if (this.status == null) missing.Add(nameof(this.status));
            if (this.hostButton == null) missing.Add(nameof(this.hostButton));
            if (this.seekButton == null) missing.Add(nameof(this.seekButton));
            if (this.closeButton == null) missing.Add(nameof(this.closeButton));
            return missing;
        }

        /// <summary>
        /// Nối các field bằng cách tìm theo tên MỘT LẦN, dùng cho lúc dựng prefab.
        ///
        /// Đây là công cụ của editor script tạo prefab, KHÔNG phải đường chạy lúc chơi:
        /// lúc chơi thì mọi thứ đã được gán sẵn trong prefab. Nếu gọi hàm này ở runtime
        /// thì ta lại quay về tìm-theo-tên, tức là quay lại đúng thứ vừa bỏ.
        /// </summary>
        public void BindByNameForAuthoring()
        {
            this.panel = GetComponent<RectTransform>();
            this.head = Find<Text>("LanHead");
            this.note = Find<Text>("LanNote");
            this.status = Find<Text>("LanStatus");
            this.hostButton = Find<Button>("LanHost");
            this.seekButton = Find<Button>("LanSeek");
            this.closeButton = Find<Button>("LanClose");
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

        /// <summary>Mọi Image trong bảng còn thiếu sprite — bài kiểm đọc để bắt lỗi quên gán.</summary>
        public System.Collections.Generic.List<string> ImagesWithoutSprite()
        {
            var missing = new System.Collections.Generic.List<string>();
            foreach (Image image in GetComponentsInChildren<Image>(true))
                if (image.sprite == null) missing.Add(image.name);
            return missing;
        }
    }
}
