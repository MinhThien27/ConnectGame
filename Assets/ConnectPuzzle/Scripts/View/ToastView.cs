using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Câu nhắn ngắn nổi lên rồi tự tắt.
    ///
    /// Đặt trên CHÍNH node Toast, và tự giữ tham chiếu tới dòng chữ của mình. Trước đây
    /// hai tham chiếu này nằm trên PuzzleGame — nghĩa là muốn đổi cách toast hiện ra thì
    /// phải sửa lớp điều khiển cả game.
    ///
    /// Là MonoBehaviour chứ không phải lớp C# thuần vì nó cần coroutine để tự tắt.
    /// </summary>
    public sealed class ToastView : MonoBehaviour
    {
        private const float Seconds = 3.4f;
        private const float Width = 940f;
        private const float MinHeight = 96f;

        [SerializeField] private Text label;

        private RectTransform rect;
        private Coroutine routine;

        private RectTransform Rect =>
            this.rect != null ? this.rect : (this.rect = (RectTransform)this.transform);

        /// <summary>Tham chiếu còn trống. Rỗng nghĩa là prefab nối đủ.</summary>
        public System.Collections.Generic.List<string> MissingFields()
        {
            var missing = new System.Collections.Generic.List<string>();
            if (this.label == null) missing.Add(nameof(this.label));
            return missing;
        }

        /// <summary>Nối tham chiếu theo TÊN con, chỉ dùng lúc dựng prefab.</summary>
        public void BindByNameForAuthoring()
        {
            Transform found = this.transform.Find("Text");
            if (found != null) this.label = found.GetComponent<Text>();
        }

        public void Show(string message)
        {
            if (this.label == null) return;
            this.label.text = message;

            // Chiều cao theo chữ THẬT: câu giới thiệu cơ chế dài gấp mấy lần câu ngắn,
            // để cố định là chữ tràn ra ngoài khung.
            float height = Ui.MeasureTextHeight(this.label) + 34f;
            Rect.sizeDelta = new Vector2(Width, Mathf.Max(MinHeight, height));

            gameObject.SetActive(true);
            if (this.routine != null) StopCoroutine(this.routine);
            this.routine = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(Seconds);

            // Xoá cờ TRƯỚC khi tắt. Coroutine này chạy trên chính node sắp bị tắt, mà
            // tắt GameObject là Unity dừng coroutine của nó ngay tại đó — dòng nào đặt
            // sau SetActive(false) sẽ không bao giờ chạy. Ở bản cũ coroutine sống trên
            // PuzzleGame nên thứ tự này không quan trọng.
            this.routine = null;
            gameObject.SetActive(false);
        }
    }
}
