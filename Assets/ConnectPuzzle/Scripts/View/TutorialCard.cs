using System.Collections;
using System.Collections.Generic;
using ConnectPuzzle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Thẻ hướng dẫn một cơ chế: câu luật + một bàn nhỏ TỰ CHƠI nước minh hoạ, lặp lại.
    ///
    /// Bàn minh hoạ là một PuzzleSession THẬT chạy trên một BoardView THẬT, không phải
    /// hình vẽ mô tả luật. Lý do không phải để tiết kiệm code mà để bài học không nói
    /// sai được: nước đi đi qua đúng TryExtendSelection và Commit của engine, nên nếu
    /// một ngày luật đá đổi thì hình minh hoạ đổi theo, thay vì âm thầm dạy luật cũ.
    ///
    /// Vì sao không dùng OverlayCard.Header(): Header chỉ biết ĐO và XẾP các khối Text.
    /// Bàn minh hoạ không phải chữ và có tỉ lệ riêng phải giữ, nên thẻ này tự xếp và chỉ
    /// nhờ OverlayCard hai việc: khung thẻ (Begin) và nút (AddButton).
    /// </summary>
    public sealed class TutorialCard
    {
        // --- nhịp của hoạt ảnh, giây ---
        private const float StepDelay = 0.34f;      // giữa hai ô được tô sáng
        private const float BeforeEat = 0.5f;       // chuỗi đã đủ, khoan hãy ăn
        private const float AfterEat = 1.9f;        // giữ kết quả cho người ta đọc
        private const float BeforeLoop = 0.45f;     // trước khi dựng lại bàn

        private const float SideMargin = 40f;
        private const float TopPadding = 34f;
        private const float Gap = 18f;
        private const float ButtonBlock = 138f;     // nút + lề đáy của OverlayCard

        private readonly OverlayCard card;

        private BoardView board;
        private RectTransform boardHolder;
        private PuzzleSession session;
        private TutorialLesson lesson;

        public bool Visible => this.card != null && this.card.Visible;

        public TutorialCard(OverlayCard overlayCard)
        {
            this.card = overlayCard;
        }

        /// <summary>
        /// Dựng thẻ cho một bài học. Trả về coroutine hoạt ảnh — bên gọi (MonoBehaviour)
        /// chạy nó, vì lớp này cố ý không phải MonoBehaviour: nó không cần node riêng,
        /// nó sống trên node của OverlayCard đã có trong prefab.
        /// </summary>
        /// <param name="buttonLabel">
        /// "Bắt đầu" khi thẻ tự hiện trước một màn, "Đóng" khi người chơi bấm nhãn thế
        /// giới để xem lại — ở đó không có màn nào sắp bắt đầu, nên "Bắt đầu" là nói sai.
        /// </param>
        public IEnumerator Show(TutorialLesson newLesson, string buttonLabel, System.Action onClose)
        {
            this.lesson = newLesson;
            this.card.Begin(1);

            RectTransform root = this.card.Root;
            float width = root.sizeDelta.x;

            Text title = Ui.Text("TutTitle", root,
                "Thế giới " + newLesson.World + " · " + newLesson.Title, 52,
                PuzzlePalette.Foreground, TextAnchor.MiddleCenter, FontStyle.Bold);

            Text rule = Ui.Text("TutRule", root, newLesson.Rule, 30, PuzzlePalette.Foreground);

            Text note = newLesson.Note == null ? null
                      : Ui.Text("TutNote", root, newLesson.Note, 27, PuzzlePalette.Dim);

            // Bàn nằm trong một node riêng: BoardView tự chốt sizeDelta của node "Board"
            // theo cỡ ô, nên nó cần một chỗ để tự do làm việc đó mà không phá bố cục thẻ.
            this.boardHolder = Ui.Node("TutBoard", root);

            // ---- xếp bố cục: đo chữ, rồi lấy phần cao còn lại cho bàn
            float textWidth = width - SideMargin * 2f;
            Ui.TopBand(title.rectTransform, TopPadding, 10f, SideMargin);
            float titleHeight = Ui.MeasureTextHeight(title);
            Ui.TopBand(title.rectTransform, TopPadding, titleHeight, SideMargin);

            Ui.TopBand(rule.rectTransform, 0f, 10f, SideMargin);
            float ruleHeight = Ui.MeasureTextHeight(rule);

            float noteHeight = 0f;
            if (note != null)
            {
                Ui.TopBand(note.rectTransform, 0f, 10f, SideMargin);
                noteHeight = Ui.MeasureTextHeight(note);
            }

            int columns = newLesson.Columns;
            int rows = newLesson.VisibleRows;

            // Cỡ bàn: bám chiều rộng trước, rồi co lại nếu thẻ không đủ cao. Không co thì
            // trên máy màn thấp phần chữ dưới bàn bị đẩy ra ngoài khung thẻ.
            float chrome = TopPadding + titleHeight + Gap + Gap + ruleHeight
                         + (note != null ? Gap + noteHeight : 0f) + Gap + ButtonBlock;
            float boardWidth = Mathf.Min(textWidth, 460f);
            float boardHeight = boardWidth / columns * rows;

            float room = this.card.AvailableHeight - chrome;
            if (boardHeight > room && room > 40f)
            {
                boardHeight = room;
                boardWidth = boardHeight / rows * columns;
            }

            float y = TopPadding + titleHeight + Gap;
            this.boardHolder.anchorMin = this.boardHolder.anchorMax = new Vector2(0.5f, 1f);
            this.boardHolder.pivot = new Vector2(0.5f, 1f);
            this.boardHolder.sizeDelta = new Vector2(boardWidth, boardHeight);
            this.boardHolder.anchoredPosition = new Vector2(0f, -y);
            y += boardHeight + Gap;

            Ui.TopBand(rule.rectTransform, y, ruleHeight, SideMargin);
            y += ruleHeight + Gap;
            if (note != null)
            {
                Ui.TopBand(note.rectTransform, y, noteHeight, SideMargin);
                y += noteHeight + Gap;
            }

            this.card.SetHeight(y + ButtonBlock);

            // ---- bàn thật. Dựng ô MỘT LẦN ở đây; vòng lặp chỉ đặt lại trạng thái.
            LevelData level = newLesson.Build();
            this.session = new PuzzleSession(level);
            this.board = new BoardView(this.boardHolder);
            this.board.Build(level);
            Rewind();
            this.board.Layout(new Vector2(boardWidth, boardHeight));

            this.card.AddButton(buttonLabel, 0, true, () =>
            {
                Close();
                onClose?.Invoke();
            });

            return Loop();
        }

        /// <summary>
        /// Đưa bàn về trạng thái đầu bài.
        ///
        /// KHÔNG dựng lại các ô: hình học của bài không đổi, nên dựng lại là instantiate
        /// rồi huỷ 15 prefab ô mỗi vòng lặp (~4 giây một lần) cho đúng một kết quả đã có.
        /// Đây cũng là đường mà nút "Chơi lại" của ván thật đi: Restart + Refresh +
        /// ResetScales, không Build.
        /// </summary>
        private void Rewind()
        {
            this.session.Restart();
            this.board.ClearChain();
            this.board.ResetScales();
            this.board.Refresh(this.session, PuzzleProgress.Symbols);
        }

        /// <summary>
        /// Diễn nước minh hoạ, lặp mãi tới khi thẻ đóng.
        ///
        /// Dùng WaitForSecondsRealtime chứ không WaitForSeconds: thẻ này hiện ra lúc mở
        /// màn, và nếu về sau có chỗ nào dừng game bằng timeScale = 0 thì hoạt ảnh đứng
        /// im mà không có gì báo — người chơi nhìn một hình tĩnh và tưởng nó bị treo.
        /// </summary>
        private IEnumerator Loop()
        {
            while (Visible)
            {
                yield return new WaitForSecondsRealtime(BeforeLoop);
                if (!Visible) break;

                // Tô sáng từng ô một, ĐI QUA ENGINE. Nếu một ô không nối được thì bài học
                // sai, và nó phải hiện ra ở đây chứ không âm thầm bỏ qua ô đó.
                foreach (int cell in this.lesson.Chain)
                {
                    if (!Visible) yield break;
                    if (this.session.TryExtendSelection(cell) != SelectionChange.Added)
                    {
                        Debug.LogError("[Tutorial] Thế giới " + this.lesson.World +
                                       ": ô " + cell + " không nối được vào chuỗi.");
                        yield break;
                    }
                    this.board.SetSelected(cell, true);
                    this.board.DrawChain(this.session.Selection, this.session.SelectionColor);
                    yield return new WaitForSecondsRealtime(StepDelay);
                }

                yield return new WaitForSecondsRealtime(BeforeEat);
                if (!Visible) break;

                foreach (int cell in this.session.Selection) this.board.SetSelected(cell, false);
                this.board.ClearChain();

                MoveResult result = this.session.Commit();
                if (result == null)
                {
                    Debug.LogError("[Tutorial] Thế giới " + this.lesson.World +
                                   ": chuỗi minh hoạ không hợp lệ.");
                    yield break;
                }

                this.board.FastForward = false;
                yield return this.board.PlayPop(result.ClearedCells);

                // Ô vỡ theo dây trói nổ CÙNG lúc với chuỗi — cùng một nước đi.
                if (result.LinkedBroken.Count > 0)
                    yield return this.board.PlayPop(result.LinkedBroken.ToArray());

                this.board.Refresh(this.session, PuzzleProgress.Symbols);

                if (result.Falls.Count > 0) yield return this.board.PlayFalls(result.Falls);

                if (result.CrackedIce.Count > 0 || result.ThawedIce.Count > 0)
                    yield return this.board.PlayIce(result.CrackedIce, result.ThawedIce);

                yield return new WaitForSecondsRealtime(AfterEat);
                if (!Visible) break;

                Rewind();
            }
        }

        /// <summary>Lò xo phóng to và quầng chuỗi — bên gọi nhắc mỗi khung hình.</summary>
        public void Tick(float deltaTime)
        {
            if (this.board != null) this.board.TickChain(deltaTime);
        }

        public void Close()
        {
            this.card.Hide();
            this.board = null;
            this.session = null;
        }
    }
}
