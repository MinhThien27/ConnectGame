using System.Collections;
using ConnectPuzzle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Bảng số liệu trên đầu màn chơi: tên màn, lượt còn, ô còn, điểm, ba sao, và dòng
    /// luật chuỗi.
    ///
    /// Chỉ ĐỌC session và level rồi vẽ ra chữ — không quyết định gì. Nhờ vậy nó nhận
    /// tham số thay vì cần một IHost: một hàm Refresh(session, level, isDaily) là đủ, và
    /// danh sách tham số đó nói thẳng nó phụ thuộc vào những gì.
    ///
    /// Là MonoBehaviour vì hoạt ảnh chạy số điểm cần coroutine.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        [SerializeField] private Text levelName;
        [SerializeField] private Text levelSub;
        [SerializeField] private Text movesValue;
        [SerializeField] private Text movesMax;
        [SerializeField] private Text movesLabel;
        [SerializeField] private Text cellsValue;
        [SerializeField] private Text cellsLabel;
        [SerializeField] private Text scoreValue;
        [SerializeField] private Text par;
        [SerializeField] private Text queue;
        [SerializeField] private Text[] stars;

        /// <summary>
        /// Dòng tiến độ đối thủ trong ván đấu Wi-Fi; rỗng nghĩa là không có gì để hiện.
        ///
        /// Là trạng thái của HUD chứ không phải tham số của Refresh, vì nó đến từ MẠNG —
        /// tức đổi vào lúc khác hẳn với lúc Refresh chạy. Truyền qua tham số thì mọi chỗ
        /// gọi Refresh (có nhiều) đều phải biết về đấu Wi-Fi.
        /// </summary>
        private string opponentLine = "";

        public void SetOpponentLine(string line)
        {
            this.opponentLine = line ?? "";
        }

        private int displayedScore;
        private Coroutine scoreRoutine;

        /// <summary>Điểm ĐANG hiện trên HUD, có thể còn đang chạy tới điểm thật.</summary>
        public int DisplayedScore => this.displayedScore;

        public System.Collections.Generic.List<string> MissingFields()
        {
            var missing = new System.Collections.Generic.List<string>();
            if (this.levelName == null) missing.Add(nameof(this.levelName));
            if (this.levelSub == null) missing.Add(nameof(this.levelSub));
            if (this.movesValue == null) missing.Add(nameof(this.movesValue));
            if (this.movesMax == null) missing.Add(nameof(this.movesMax));
            if (this.movesLabel == null) missing.Add(nameof(this.movesLabel));
            if (this.cellsValue == null) missing.Add(nameof(this.cellsValue));
            if (this.cellsLabel == null) missing.Add(nameof(this.cellsLabel));
            if (this.scoreValue == null) missing.Add(nameof(this.scoreValue));
            if (this.par == null) missing.Add(nameof(this.par));
            if (this.queue == null) missing.Add(nameof(this.queue));
            if (this.stars == null || this.stars.Length != 3) missing.Add("stars(3)");
            else for (int i = 0; i < 3; i++) if (this.stars[i] == null) missing.Add("stars[" + i + "]");
            return missing;
        }

        /// <summary>Nối tham chiếu lúc dựng. Nhận thẳng vì các Text nằm rải trên màn chơi.</summary>
        public void BindForAuthoring(Text name, Text sub, Text moves, Text max, Text movesCap,
                                     Text cells, Text cellsCap, Text score, Text parLine,
                                     Text queueLine, Text[] starRow)
        {
            this.levelName = name;
            this.levelSub = sub;
            this.movesValue = moves;
            this.movesMax = max;
            this.movesLabel = movesCap;
            this.cellsValue = cells;
            this.cellsLabel = cellsCap;
            this.scoreValue = score;
            this.par = parLine;
            this.queue = queueLine;
            this.stars = starRow;
        }

        public void SetTitle(string title, string subtitle)
        {
            this.levelName.text = title;
            this.levelSub.text = subtitle;
        }

        public void SetRuleLine(string text) { this.par.text = text; }

        /// <summary>Đặt điểm về đúng con số, cắt ngang hoạt ảnh đang chạy nếu có.</summary>
        public void SetScore(int value)
        {
            if (this.scoreRoutine != null) { StopCoroutine(this.scoreRoutine); this.scoreRoutine = null; }
            this.displayedScore = value;
            this.scoreValue.text = value.ToString();
        }

        public void AnimateScore(int from, int to)
        {
            if (this.scoreRoutine != null) StopCoroutine(this.scoreRoutine);
            this.scoreRoutine = StartCoroutine(ScoreRoutine(from, to));
        }

        private IEnumerator ScoreRoutine(int from, int to)
        {
            const float duration = 0.42f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                this.displayedScore = Mathf.RoundToInt(Mathf.Lerp(from, to, 1f - Mathf.Pow(1f - t, 3f)));
                this.scoreValue.text = this.displayedScore.ToString();
                yield return null;
            }
            this.displayedScore = to;
            this.scoreValue.text = to.ToString();
            this.scoreRoutine = null;
        }

        /// <summary>
        /// Vẽ lại toàn bộ số liệu.
        ///
        /// Vô tận đi một nhánh riêng vì ở đó không có lượt để đếm ngược và không có mốc
        /// sao — hai ô đó đổi hẳn ý nghĩa chứ không phải chỉ đổi con số.
        /// </summary>
        public void Refresh(PuzzleSession session, LevelData level, bool isDaily)
        {
            if (level.Endless) { RefreshEndless(session); return; }

            this.movesLabel.text = "Lượt còn";
            this.movesValue.text = session.MovesLeft.ToString();
            this.movesMax.text = "/" + level.MaxMoves;
            this.movesValue.color = session.MovesLeft <= 2 ? PuzzlePalette.Bad : PuzzlePalette.Foreground;

            // Màn mục tiêu đếm ô ĐÍCH: người chơi cần biết còn cách thắng bao xa, mà ở đó
            // phần bàn thừa không liên quan.
            this.cellsLabel.text = level.GoalMode ? "Ô đích còn" : "Ô còn lại";
            this.cellsValue.text = (level.GoalMode ? session.GoalsLeft : session.TotalLeft()).ToString();

            for (int i = 0; i < 3; i++)
            {
                int threshold = i == 0 ? level.Par : (i == 1 ? level.TwoStarMoves : level.MaxMoves);
                bool on = session.MovesUsed <= threshold;
                this.stars[i].color = on
                    ? PuzzlePalette.Star
                    : new Color(PuzzlePalette.Star.r, PuzzlePalette.Star.g, PuzzlePalette.Star.b, 0.2f);
            }

            // Ô này chở ba thứ khác nhau, theo thứ tự ưu tiên rõ ràng.
            //
            // Tiến độ ĐỐI THỦ đứng trước tất cả: nó là thứ duy nhất ở đây đến từ bên ngoài
            // và đổi theo thời gian thật, nên nó cũng là thứ duy nhất người chơi cần nhìn
            // đúng lúc. Trong ván đấu thì hai thứ còn lại cũng chẳng có nghĩa: huy hiệu
            // không được cấp khi đấu, và bàn đấu thì không dùng gravity.
            if (!string.IsNullOrEmpty(this.opponentLine))
                this.queue.text = this.opponentLine;

            // Tiến độ huy hiệu phải hiện TRONG lúc chơi, không phải chỉ ở thẻ kết ván:
            // biết mình còn thiếu mấy chuỗi đầy là thứ đổi được cách đi nước tiếp theo,
            // biết sau khi xong ván thì chỉ còn là lời trách.
            else if (level.MedalChains > 0 && !isDaily)
                this.queue.text = "◆ " + session.FullChains + "/" + level.MedalChains;
            else
                this.queue.text = level.Gravity ? "▼ hàng chờ " + session.QueueLeft() : "";
        }

        private void RefreshEndless(PuzzleSession session)
        {
            // Vô tận không có lượt để đếm ngược, nên ô đó chuyển thành số nước ĐÃ đi, và
            // ô "ô còn lại" thành hệ số combo — hai thứ duy nhất còn nghĩa ở đây.
            this.movesLabel.text = "Nước đã đi";
            this.movesValue.text = session.MovesUsed.ToString();
            this.movesMax.text = "";
            this.movesValue.color = PuzzlePalette.Foreground;

            this.cellsLabel.text = "Combo";
            this.cellsValue.text = session.Combo > 0
                ? "x" + session.EndlessMultiplier.ToString("0.##")
                : "—";

            foreach (Text star in this.stars)
                star.color = new Color(PuzzlePalette.Star.r, PuzzlePalette.Star.g, PuzzlePalette.Star.b, 0f);

            this.par.text = EndlessRules.ColorsFor(session.Score) + " màu · kỷ lục " +
                            PuzzleProgress.EndlessBest;
            this.queue.text = "";
        }
    }
}
