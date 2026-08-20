using ConnectPuzzle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Khung chứa bàn: vùng nhận chạm và chip xem trước điểm bay theo đầu chuỗi.
    ///
    /// Đặt trên chính node BoardArea. Bản thân BÀN (BoardView) không sống ở đây — nó là
    /// lớp C# thuần dựng lại mỗi lần chạy, vì cây ô thay đổi theo từng màn. Lớp này chỉ
    /// giữ những thứ CỐ ĐỊNH của khung: kích thước vùng, chỗ nhận chạm, và cái chip.
    /// </summary>
    public sealed class BoardArea : MonoBehaviour
    {
        [SerializeField] private BoardPointerInput pointerInput;
        [SerializeField] private RectTransform chainPreview;
        [SerializeField] private Text chainPreviewLabel;

        private RectTransform rect;

        /// <summary>Khung vùng bàn — bố cục đọc để biết bàn được bao nhiêu chỗ.</summary>
        public RectTransform Rect =>
            this.rect != null ? this.rect : (this.rect = (RectTransform)this.transform);

        public System.Collections.Generic.List<string> MissingFields()
        {
            var missing = new System.Collections.Generic.List<string>();
            if (this.pointerInput == null) missing.Add(nameof(this.pointerInput));
            if (this.chainPreview == null) missing.Add(nameof(this.chainPreview));
            if (this.chainPreviewLabel == null) missing.Add(nameof(this.chainPreviewLabel));
            return missing;
        }

        public void BindForAuthoring(BoardPointerInput input, RectTransform preview, Text label)
        {
            this.pointerInput = input;
            this.chainPreview = preview;
            this.chainPreviewLabel = label;
        }

        /// <summary>
        /// Nối ba delegate chạm. Gọi lại được.
        ///
        /// Configure(null) vì canvas là ScreenSpaceOverlay — truyền camera vào đó thì
        /// phép đổi toạ độ lệch đi cả màn hình.
        /// </summary>
        public void Wire(System.Action<Vector3> down, System.Action<Vector3> drag, System.Action up)
        {
            if (this.pointerInput == null) return;
            this.pointerInput.Configure(null);
            this.pointerInput.PointerDown = down;
            this.pointerInput.PointerDrag = drag;
            this.pointerInput.PointerUp = up;
        }

        public void HidePreview() { this.chainPreview.gameObject.SetActive(false); }

        /// <summary>
        /// Hiện chip xem trước ngay trên đầu chuỗi, và nói rõ chuỗi đang thiếu hay đã
        /// kịch trần.
        ///
        /// Nói "cần N ô" chứ không im lặng: thiếu ô mà không báo thì người chơi thả tay,
        /// chẳng thấy gì xảy ra, và không hiểu vì sao.
        /// </summary>
        public void ShowPreview(Vector2 anchoredPosition, int count, int minChain, int maxChain)
        {
            this.chainPreview.gameObject.SetActive(true);
            this.chainPreview.anchoredPosition = anchoredPosition;

            if (count < minChain)
            {
                this.chainPreviewLabel.text = "cần " + minChain + " ô";
                this.chainPreviewLabel.color = PuzzlePalette.Dim;
            }
            else if (maxChain != int.MaxValue && count >= maxChain)
            {
                this.chainPreviewLabel.text = "tối đa " + maxChain + " ô  +" + PuzzleSession.ChainScore(count);
                this.chainPreviewLabel.color = PuzzlePalette.Star;
            }
            else
            {
                this.chainPreviewLabel.text = count + " ô  +" + PuzzleSession.ChainScore(count);
                this.chainPreviewLabel.color = PuzzlePalette.Foreground;
            }
        }
    }
}
