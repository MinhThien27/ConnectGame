using UnityEngine;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Ép một RectTransform vào vùng an toàn của màn hình — tai thỏ, đục lỗ, thanh
    /// trạng thái, vạch home. Tương đương env(safe-area-inset-*) của bản HTML.
    ///
    /// Chỉ đặt NỘI DUNG vào node này, KHÔNG đặt ảnh nền: nền phải tràn hết màn hình
    /// để màu phủ cả dưới tai thỏ, đúng như viewport-fit=cover. Nền bị ép vào vùng an
    /// toàn thì sẽ hở ra viền đen quanh mép máy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SafeAreaPanel : MonoBehaviour
    {
        /// <summary>Bật để giả lập lề trong Editor, xem bố cục mà không cần máy thật.</summary>
        public bool SimulateInsets;

        /// <summary>Lề giả lập theo pixel: x = trái, y = trên, z = phải, w = dưới.</summary>
        public Vector4 SimulatedInsets = new Vector4(0f, 132f, 0f, 68f);

        private RectTransform rect;
        private Rect appliedSafeArea = new Rect(-1f, -1f, -1f, -1f);
        private Vector2Int appliedResolution;
        private bool appliedSimulation;
        private Vector4 appliedInsets;

        /// <summary>Vùng an toàn đang áp dụng, theo pixel màn hình.</summary>
        public Rect CurrentSafeArea => this.appliedSafeArea;

        private void Awake()
        {
            this.rect = (RectTransform)this.transform;
            Apply();
        }

        private void OnEnable() => Apply();

        // Vùng an toàn đổi khi quay máy hoặc khi hệ thống hiện/ẩn thanh điều hướng,
        // không có callback nào cho việc đó nên phải đối chiếu mỗi frame. Apply() tự
        // thoát sớm khi không có gì đổi, nên chi phí gần bằng không.
        private void Update() => Apply();

        /// <summary>Tính lại ngay. Gọi từ ngoài sau khi đổi lề giả lập.</summary>
        public void Apply()
        {
            if (this.rect == null) this.rect = (RectTransform)this.transform;

            int width = Screen.width;
            int height = Screen.height;
            if (width <= 0 || height <= 0) return;

            Rect safe = ResolveSafeArea(width, height);
            var resolution = new Vector2Int(width, height);

            if (safe == this.appliedSafeArea && resolution == this.appliedResolution &&
                this.SimulateInsets == this.appliedSimulation && this.SimulatedInsets == this.appliedInsets)
                return;

            this.appliedSafeArea = safe;
            this.appliedResolution = resolution;
            this.appliedSimulation = this.SimulateInsets;
            this.appliedInsets = this.SimulatedInsets;

            // Dùng anchor CHUẨN HOÁ nên không phụ thuộc CanvasScaler đang scale bao nhiêu;
            // đặt theo offset pixel sẽ sai ngay khi đổi độ phân giải tham chiếu.
            this.rect.anchorMin = new Vector2(safe.xMin / width, safe.yMin / height);
            this.rect.anchorMax = new Vector2(safe.xMax / width, safe.yMax / height);
            this.rect.offsetMin = Vector2.zero;
            this.rect.offsetMax = Vector2.zero;
        }

        private Rect ResolveSafeArea(int width, int height)
        {
            if (this.SimulateInsets)
            {
                float left = Mathf.Max(0f, this.SimulatedInsets.x);
                float top = Mathf.Max(0f, this.SimulatedInsets.y);
                float right = Mathf.Max(0f, this.SimulatedInsets.z);
                float bottom = Mathf.Max(0f, this.SimulatedInsets.w);
                // Screen.safeArea lấy gốc ở góc DƯỚI-trái, nên lề trên trừ vào chiều cao
                // chứ không dịch gốc.
                return new Rect(left, bottom,
                    Mathf.Max(1f, width - left - right),
                    Mathf.Max(1f, height - top - bottom));
            }

            Rect safe = Screen.safeArea;
            // Một số nền tảng (và batchmode) trả về rect rỗng — coi như không có lề.
            if (safe.width <= 0f || safe.height <= 0f) return new Rect(0f, 0f, width, height);
            return safe;
        }
    }
}
