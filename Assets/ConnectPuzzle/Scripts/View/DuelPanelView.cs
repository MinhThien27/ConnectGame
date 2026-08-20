using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{
    /// <summary>Tham chiếu tới các phần tử của bảng "Đấu seed bạn bè" trong prefab.</summary>
    /// <summary>
    /// Tham chiếu tới bảng "Đấu seed bạn bè" trong prefab.
    ///
    /// KHÔNG gồm lớp chặn (DuelCatcher): nó là em ruột của bảng, dựng bằng code, và giữ
    /// nguyên như vậy có chủ ý. Nó là hình chữ nhật trong suốt phủ màn hình — không có gì
    /// để sửa trong Editor — còn đưa nó thành CON của bảng chính là thứ đã gây lỗi "chạm
    /// đâu cũng tắt bảng" ở bảng Wi-Fi.
    /// </summary>
    public sealed class DuelPanelView : MonoBehaviour
    {
        [SerializeField] private Text code;
        [SerializeField] private Button reroll;
        [SerializeField] private Button copy;
        [SerializeField] private Button playMine;
        [SerializeField] private InputField input;
        [SerializeField] private Text status;
        [SerializeField] private Button paste;
        [SerializeField] private Button playTheirs;
        [SerializeField] private Button lanOpen;

        public Text Code => this.code;
        public Button Reroll => this.reroll;
        public Button Copy => this.copy;
        public Button PlayMine => this.playMine;
        public InputField Input => this.input;
        public Text Status => this.status;
        public Button Paste => this.paste;
        public Button PlayTheirs => this.playTheirs;
        public Button LanOpen => this.lanOpen;

        public List<string> MissingFields()
        {
            var missing = new List<string>();
            if (this.code == null) missing.Add(nameof(this.code));
            if (this.reroll == null) missing.Add(nameof(this.reroll));
            if (this.copy == null) missing.Add(nameof(this.copy));
            if (this.playMine == null) missing.Add(nameof(this.playMine));
            if (this.input == null) missing.Add(nameof(this.input));
            if (this.status == null) missing.Add(nameof(this.status));
            if (this.paste == null) missing.Add(nameof(this.paste));
            if (this.playTheirs == null) missing.Add(nameof(this.playTheirs));
            if (this.lanOpen == null) missing.Add(nameof(this.lanOpen));
            return missing;
        }

        public void BindByNameForAuthoring()
        {
            this.code = Find<Text>("DuelCode");
            this.reroll = Find<Button>("DuelReroll");
            this.copy = Find<Button>("DuelCopy");
            this.playMine = Find<Button>("DuelPlayMine");
            this.input = Find<InputField>("DuelFieldBg");
            this.status = Find<Text>("DuelStatus");
            this.paste = Find<Button>("DuelPaste");
            this.playTheirs = Find<Button>("DuelPlayTheirs");
            this.lanOpen = Find<Button>("DuelLanOpen");
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
    }
}
