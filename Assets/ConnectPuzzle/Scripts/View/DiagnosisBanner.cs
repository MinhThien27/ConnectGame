using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Dải băng báo lý do thua, hiện trong lúc bàn đang chỉ vào chỗ sai.
    ///
    /// Lớp này CHỈ lo cái băng và cú chạm để bỏ qua. Đoạn chỉ-vào-chỗ-sai (làm mờ bàn,
    /// thắp từng nhóm ô, rung, đánh số) ở lại PuzzleGame vì nó là luồng kết ván, không
    /// phải một tấm băng.
    ///
    /// Lớp bắt chạm KHÔNG phải con của băng — nó phủ cả màn hình, kể cả dải tai thỏ.
    /// Component vẫn giữ tham chiếu tới nó được: tham chiếu trong prefab không đòi quan
    /// hệ cha con.
    /// </summary>
    public sealed class DiagnosisBanner : MonoBehaviour
    {
        [SerializeField] private Text title;
        [SerializeField] private Text hint;
        [SerializeField] private Button skipCatcher;

        /// <summary>Người chơi đã chạm để bỏ qua phần chẩn đoán.</summary>
        public bool SkipRequested { get; private set; }

        public System.Collections.Generic.List<string> MissingFields()
        {
            var missing = new System.Collections.Generic.List<string>();
            if (this.title == null) missing.Add(nameof(this.title));
            if (this.hint == null) missing.Add(nameof(this.hint));
            if (this.skipCatcher == null) missing.Add(nameof(this.skipCatcher));
            return missing;
        }

        /// <summary>
        /// Nối tham chiếu lúc dựng prefab.
        ///
        /// Nhận thẳng đối tượng chứ không dò theo tên như các View khác: lớp bắt chạm
        /// là ANH EM nằm trên canvas, không phải con của băng, nên transform.Find không
        /// với tới được.
        /// </summary>
        public void BindForAuthoring(Text titleText, Text hintText, Button catcher)
        {
            this.title = titleText;
            this.hint = hintText;
            this.skipCatcher = catcher;
        }

        /// <summary>
        /// Nối cú chạm bỏ qua. Gọi lại được.
        ///
        /// Không làm trong Awake vì bài kiểm dựng UI ở edit mode, nơi Awake không chạy —
        /// nối ở đó thì rig kiểm một bàn phím câm mà vẫn báo xanh.
        /// </summary>
        public void Wire()
        {
            if (this.skipCatcher == null) return;
            this.skipCatcher.onClick.RemoveAllListeners();
            this.skipCatcher.onClick.AddListener(() => this.SkipRequested = true);
        }

        public void Show(string titleText, string hintText)
        {
            this.SkipRequested = false;
            if (this.title != null) this.title.text = titleText;
            if (this.hint != null) this.hint.text = hintText;
            gameObject.SetActive(true);
            if (this.skipCatcher != null) this.skipCatcher.gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            if (this.skipCatcher != null) this.skipCatcher.gameObject.SetActive(false);
        }
    }
}
