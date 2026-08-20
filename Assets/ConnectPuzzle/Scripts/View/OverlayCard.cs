using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Khung thẻ nổi cuối ván: đo, co, và xếp nút.
    ///
    /// Lớp này KHÔNG biết gì về ván chơi — không biết thắng thua, không biết sao, không
    /// biết đấu seed. Nó chỉ trả lời một câu: "cho tôi n nút và mấy khối chữ, thẻ phải
    /// rộng cao bao nhiêu và nút nằm ở đâu". Nhờ vậy bốn loại thẻ (thắng, thua, vô tận,
    /// phán quyết đấu) dùng chung đúng một bộ số đo thay vì mỗi chỗ tự tính.
    ///
    /// Đặt trên CHÍNH node Overlay (lớp phủ tối), và tự giữ tham chiếu tới khung thẻ.
    /// Trước đây cả hai tham chiếu nằm trên PuzzleGame và được truyền vào qua hàm dựng —
    /// nghĩa là PuzzleGame phải biết thẻ gồm những node nào.
    /// </summary>
    public sealed class OverlayCard : MonoBehaviour
    {
        /// <summary>
        /// Số con ĐẦU của thẻ phải giữ lại khi dọn: Ui.Panel dựng nền VÀ viền, xoá cả
        /// hai thì thẻ mất khung ngay từ lần hiện thứ hai.
        /// </summary>
        private const int ChromeChildren = 2;

        private const float HeaderHeightGuess = 330f;   // chỉ dùng khi chưa đo được
        private const float ButtonHeight = 92f;
        private const float ButtonGap = 12f;
        private const float BottomPadding = 34f;
        private const float HeaderTop = 36f;
        private const float HeaderGap = 10f;
        private const float HeaderBottomPad = 22f;
        private const float HeaderSideMargin = 40f;

        /// <summary>Khung thẻ. Lớp phủ tối chính là node mang component này.</summary>
        [SerializeField] private RectTransform card;

        private RectTransform overlayRect;
        private RectTransform Overlay =>
            this.overlayRect != null ? this.overlayRect : (this.overlayRect = (RectTransform)this.transform);

        private float scale = 1f;
        private int buttonCount = 1;

        public System.Collections.Generic.List<string> MissingFields()
        {
            var missing = new System.Collections.Generic.List<string>();
            if (this.card == null) missing.Add(nameof(this.card));
            return missing;
        }

        /// <summary>Nối tham chiếu theo TÊN con, chỉ dùng lúc dựng prefab.</summary>
        public void BindByNameForAuthoring()
        {
            Transform found = this.transform.Find("Card");
            if (found != null) this.card = (RectTransform)found;
        }

        /// <summary>Khung thẻ — nơi bên gọi tạo chữ lên, và là thứ bài kiểm đo bố cục.</summary>
        public RectTransform Root => this.card;

        public bool Visible => gameObject.activeSelf;

        public void Hide() { gameObject.SetActive(false); }

        /// <summary>
        /// Mở một thẻ mới với đúng buttonCount nút.
        ///
        /// Chốt chiều RỘNG ngay tại đây, TRƯỚC khi bên gọi tạo chữ: preferredHeight của
        /// Text phụ thuộc chiều rộng, chưa biết rộng bao nhiêu thì chưa biết chữ ngắt
        /// dòng ở đâu, và mọi phép đo sau đó đều sai theo.
        /// </summary>
        public void Begin(int buttonCount)
        {
            Ui.ClearChildren(this.card, ChromeChildren);
            gameObject.SetActive(true);
            this.buttonCount = buttonCount;
            this.scale = 1f;

            float width = Mathf.Max(420f, Mathf.Min(760f, Overlay.rect.width - 80f));
            this.card.sizeDelta = new Vector2(width, Mathf.Max(320f, this.card.sizeDelta.y));
        }

        /// <summary>
        /// Đo → xếp → co khối chữ đầu thẻ, rồi chốt chiều cao thẻ.
        ///
        /// Gọi SAU khi đã tạo xong chữ, TRƯỚC khi thêm nút: nút neo từ đáy thẻ lên nên
        /// chúng cần biết thẻ cao bao nhiêu và co bao nhiêu.
        /// </summary>
        public void Header(Text[] blocks, int[] baseFontSizes)
        {
            float header = StackHeader(blocks);

            // Lặp hai lượt vì co chữ lại thì chiều cao đo được cũng đổi: lượt 1 đo ở cỡ
            // gốc để biết cần co bao nhiêu, lượt 2 đo lại ở cỡ đã co để lấy số thật.
            float want = ScaleFor(header);
            if (want < 0.99f)
            {
                for (int i = 0; i < blocks.Length; i++)
                    if (blocks[i] != null)
                        blocks[i].fontSize = Mathf.Max(18, Mathf.RoundToInt(baseFontSizes[i] * want));
                header = StackHeader(blocks);
            }

            this.scale = ScaleFor(header);
            float available = Available;
            float needed = header + this.buttonCount * (ButtonHeight + ButtonGap) * this.scale
                         + BottomPadding * this.scale;
            this.card.sizeDelta = new Vector2(this.card.sizeDelta.x, Mathf.Min(needed, available));
        }

        /// <summary>
        /// Thêm một nút vào slot thứ `slot` (đếm từ trên xuống trong khối nút).
        ///
        /// Nút neo từ ĐÁY thẻ lên chứ không từ đỉnh xuống: neo từ đỉnh thì nút cuối trôi
        /// ra ngoài khung ngay khi thẻ phải co lại.
        /// </summary>
        public void AddButton(string label, int slot, bool primary,
                              UnityEngine.Events.UnityAction action)
        {
            Button button = Ui.Button("CardBtn" + slot, this.card, label, 32,
                primary ? PuzzlePalette.Accent : PuzzlePalette.PanelLight,
                primary ? new Color(0.05f, 0.06f, 0.14f) : PuzzlePalette.Foreground,
                PuzzlePalette.RadiusPanel, primary);

            float height = ButtonHeight * this.scale;
            float step = (ButtonHeight + ButtonGap) * this.scale;
            float width = Mathf.Min(620f, this.card.sizeDelta.x - 70f);

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(0,
                BottomPadding * this.scale + (this.buttonCount - 1 - slot) * step);

            Ui.LabelOf(button).fontSize = Mathf.RoundToInt(Mathf.Max(20f, 32f * this.scale));
            button.onClick.AddListener(action);
        }

        /// <summary>
        /// Chiều cao thẻ tính TỪ SỐ NÚT. Để cố định là lỗi đã thấy trên ảnh: thẻ thua
        /// 4 nút cần ~776 mà thẻ chỉ 620, nút cuối rơi ra ngoài khung.
        ///
        /// Công khai và static để bài kiểm tính được con số mong đợi mà không phải dựng
        /// cả một ván.
        /// </summary>
        public static float Height(int buttonCount, float maxHeight)
        {
            return Height(buttonCount, maxHeight, HeaderHeightGuess);
        }

        /// <summary>headerHeight = chiều cao ĐÃ ĐO của khối chữ đầu thẻ.</summary>
        public static float Height(int buttonCount, float maxHeight, float headerHeight)
        {
            float needed = headerHeight + buttonCount * (ButtonHeight + ButtonGap) + BottomPadding;
            return Mathf.Min(needed, Mathf.Max(320f, maxHeight));
        }

        private float Available => Mathf.Max(320f, Overlay.rect.height - 80f);

        /// <summary>Tỉ lệ co khi chiều cao dùng được không đủ cho khối chữ + nút.</summary>
        private float ScaleFor(float headerHeight)
        {
            float needed = headerHeight + this.buttonCount * (ButtonHeight + ButtonGap) + BottomPadding;
            float available = Available;
            return needed <= available ? 1f : available / needed;
        }

        /// <summary>
        /// ĐO rồi xếp các khối chữ từ trên xuống, trả về tổng chiều cao thật.
        /// Đặt lề ngang trước khi đo vì preferredHeight phụ thuộc chiều rộng.
        /// </summary>
        private static float StackHeader(Text[] blocks)
        {
            float y = HeaderTop;
            bool any = false;
            for (int i = 0; i < blocks.Length; i++)
            {
                Text block = blocks[i];
                if (block == null) continue;
                Ui.TopBand(block.rectTransform, y, 10f, HeaderSideMargin);
                float height = Ui.MeasureTextHeight(block);
                Ui.TopBand(block.rectTransform, y, height, HeaderSideMargin);
                y += height + HeaderGap;
                any = true;
            }
            return any ? y - HeaderGap + HeaderBottomPad : HeaderTop;
        }
    }
}
