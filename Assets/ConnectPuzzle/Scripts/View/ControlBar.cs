using ConnectPuzzle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Hàng nút dưới màn chơi: hoàn tác, xáo lại, gợi ý, chơi lại — cộng nút quay lại và
    /// nút âm thanh ở thanh trên.
    ///
    /// Nút vật phẩm KHÔNG nằm ở đây dù nó cùng hàng: nó thuộc ItemPanel, vì hành vi của
    /// nó (mở bảng, hiện số sao, sáng lên khi đang cầm món) là việc của cửa hàng chứ
    /// không phải của hàng nút.
    ///
    /// Lớp này chỉ nối nút và bật/tắt theo hạn mức còn lại. Cái gì xảy ra khi bấm thì
    /// PuzzleGame quyết — nó là luồng ván chơi.
    /// </summary>
    public sealed class ControlBar : MonoBehaviour
    {
        [SerializeField] private Button undo;
        [SerializeField] private Button shuffle;
        [SerializeField] private Button hint;
        [SerializeField] private Button restart;
        [SerializeField] private Button sound;
        [SerializeField] private Button back;
        [SerializeField] private Text undoCount;
        [SerializeField] private Text shuffleCount;

        public Button SoundButton => this.sound;

        public System.Collections.Generic.List<string> MissingFields()
        {
            var missing = new System.Collections.Generic.List<string>();
            if (this.undo == null) missing.Add(nameof(this.undo));
            if (this.shuffle == null) missing.Add(nameof(this.shuffle));
            if (this.hint == null) missing.Add(nameof(this.hint));
            if (this.restart == null) missing.Add(nameof(this.restart));
            if (this.sound == null) missing.Add(nameof(this.sound));
            if (this.back == null) missing.Add(nameof(this.back));
            if (this.undoCount == null) missing.Add(nameof(this.undoCount));
            if (this.shuffleCount == null) missing.Add(nameof(this.shuffleCount));
            return missing;
        }

        public void BindForAuthoring(Button undoButton, Text undoBadge,
                                     Button shuffleButton, Text shuffleBadge,
                                     Button hintButton, Button restartButton,
                                     Button soundButton, Button backButton)
        {
            this.undo = undoButton;
            this.undoCount = undoBadge;
            this.shuffle = shuffleButton;
            this.shuffleCount = shuffleBadge;
            this.hint = hintButton;
            this.restart = restartButton;
            this.sound = soundButton;
            this.back = backButton;
        }

        /// <summary>
        /// Nối hành động. Gọi lại được: mọi nút đều gỡ listener trước.
        ///
        /// Nhận từng delegate thay vì một IHost: sáu nút, sáu việc, không có trạng thái
        /// nào cần đọc ngược — một interface ở đây chỉ thêm một tầng để đi qua.
        /// </summary>
        public void Wire(UnityEngine.Events.UnityAction onUndo,
                         UnityEngine.Events.UnityAction onShuffle,
                         UnityEngine.Events.UnityAction onHint,
                         UnityEngine.Events.UnityAction onRestart,
                         UnityEngine.Events.UnityAction onSound,
                         UnityEngine.Events.UnityAction onBack)
        {
            Bind(this.undo, onUndo);
            Bind(this.shuffle, onShuffle);
            Bind(this.hint, onHint);
            Bind(this.restart, onRestart);
            Bind(this.sound, onSound);
            Bind(this.back, onBack);
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        /// <summary>Bật/tắt và cập nhật số còn lại của hoàn tác và xáo.</summary>
        public void Refresh(PuzzleSession session, bool endless)
        {
            if (endless)
            {
                // Vô tận không cho hoàn tác: bàn đổ đầy lại sau mỗi nước nên "lùi một
                // bước" không có nghĩa gì.
                this.undoCount.text = "0";
                this.undo.interactable = false;
                this.shuffleCount.text = session.ShufflesLeft.ToString();
                this.shuffle.interactable = session.ShufflesLeft > 0;
                return;
            }

            this.undoCount.text = session.UndosLeft.ToString();
            this.undo.interactable = session.CanUndo;
            this.shuffleCount.text = session.ShufflesLeft.ToString();
            this.shuffle.interactable = session.CanShuffle;
        }

        /// <summary>
        /// Nút âm thanh dùng ♪ và đổi MÀU để phân biệt bật/tắt.
        ///
        /// KHÔNG dùng emoji: font mặc định của Unity chỉ có BMP nên 🔊 hiện ra ô trống.
        /// </summary>
        public void SetSoundOn(bool on)
        {
            if (this.sound == null) return;
            Text label = Ui.LabelOf(this.sound);
            label.text = "♪";
            label.color = on ? PuzzlePalette.Foreground : new Color(0.4f, 0.43f, 0.6f, 0.7f);
        }
    }
}
