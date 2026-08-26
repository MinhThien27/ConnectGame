using System.Collections.Generic;
using System.Text;
using ConnectPuzzle.Core;
using ConnectPuzzle.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.EditorTools
{
    /// <summary>
    /// Kiểm BỐ CỤC của thẻ hướng dẫn ở nhiều cỡ màn hình.
    ///
    /// Vì sao cần: thẻ hướng dẫn KHÔNG dùng OverlayCard.Header() (Header chỉ biết xếp
    /// chữ, còn thẻ này có một bàn minh hoạ phải giữ tỉ lệ), nên nó tự xếp bố cục bằng
    /// tay. Bố cục tự xếp là chỗ dễ sai nhất và cũng là chỗ khó thấy nhất — sai thì chữ
    /// tụt xuống dưới nút, hoặc thẻ cao quá khung, và chỉ lộ ra trên đúng một tỉ lệ máy.
    ///
    /// Rig ở đây KHÔNG dùng prefab gốc: nó tự dựng một Canvas + lớp phủ + khung thẻ tối
    /// giản, đúng những gì OverlayCard cần. Nhờ vậy CHỌN ĐƯỢC cỡ khung (Canvas
    /// ScreenSpaceOverlay lấy cỡ từ màn hình thật, mà trong batchmode thì không có màn
    /// hình nào), và mỗi tỉ lệ máy đều kiểm được.
    ///
    /// Chỉ kiểm phần XẾP, không kiểm hoạt ảnh: coroutine không chạy ở edit mode.
    /// </summary>
    public static class TutorialCardCheck
    {
        /// <summary>Nút của OverlayCard: cao 92, lề đáy 34 — mép trên nút cách đáy thẻ 126.</summary>
        private const float ButtonTopFromBottom = 126f;

        private static readonly Vector2[] Frames =
        {
            new Vector2(1080f, 1920f),   // 16:9, phổ biến nhất
            new Vector2(1080f, 2400f),   // 20:9, máy cao
            new Vector2(1080f, 1350f),   // 5:4, tablet dựng
            new Vector2(1080f, 810f)     // 4:3 — trường hợp thấp nhất mà bố cục menu từng vỡ
        };

        [MenuItem("Connect Puzzle/Kiểm bố cục thẻ hướng dẫn", priority = 70)]
        public static void Run() { Check(out string report); Debug.Log(report); }

        /// <summary>Batchmode: -executeMethod ConnectPuzzle.EditorTools.TutorialCardCheck.CheckBatch</summary>
        public static void CheckBatch()
        {
            int problems = Check(out string report);
            Debug.Log(report);
            Debug.Log(problems == 0 ? "TUTORIAL_LAYOUT_OK" : "TUTORIAL_LAYOUT_FAILED");
            EditorApplication.Exit(problems == 0 ? 0 : 1);
        }

        public static int Check(out string report)
        {
            var log = new StringBuilder();
            int problems = 0;

            foreach (Vector2 frame in Frames)
            {
                log.Append("--- khung ").Append(frame.x).Append('x').Append(frame.y).Append('\n');

                foreach (TutorialLesson lesson in TutorialLessons.All)
                {
                    GameObject rig = BuildRig(frame, out OverlayCard card, out RectTransform cardRect);
                    try
                    {
                        var tutorial = new TutorialCard(card);
                        tutorial.Show(lesson, "Bắt đầu", null);       // chỉ chạy phần xếp; coroutine bỏ

                        problems += Verify(lesson, card, cardRect, frame, log);
                    }
                    finally
                    {
                        Object.DestroyImmediate(rig);
                    }
                }
            }

            report = log.ToString();
            return problems;
        }

        private static int Verify(TutorialLesson lesson, OverlayCard card, RectTransform cardRect,
                                  Vector2 frame, StringBuilder log)
        {
            int problems = 0;
            string boardInfo = "";
            float cardHeight = cardRect.sizeDelta.y;
            float cardWidth = cardRect.sizeDelta.x;
            string who = "TG" + lesson.World;

            void Fail(string what)
            {
                problems++;
                log.Append("    LOI ").Append(who).Append(": ").Append(what).Append('\n');
            }

            // 1. Thẻ không được cao quá chỗ cho phép.
            if (cardHeight > card.AvailableHeight + 0.5f)
                Fail($"thẻ cao {cardHeight:F0} > trần {card.AvailableHeight:F0}");

            // 2. Mọi khối nội dung phải nằm TRÊN nút, và trong khung thẻ.
            float contentLimit = cardHeight - ButtonTopFromBottom;
            float lowest = 0f;
            string lowestName = "";

            foreach (string name in new[] { "TutTitle", "TutBoard", "TutRule", "TutNote" })
            {
                RectTransform node = Ui.Reuse(name, cardRect);
                if (node == null)
                {
                    if (name != "TutNote" || lesson.Note != null) Fail("thiếu node " + name);
                    continue;
                }

                // Mọi khối đều neo ở ĐỈNH thẻ (anchor y = 1) nên đáy của nó, đo từ đỉnh
                // thẻ xuống, là -anchoredPosition.y + chiều cao.
                float top = -node.anchoredPosition.y;
                float bottom = top + node.sizeDelta.y;
                if (bottom > lowest) { lowest = bottom; lowestName = name; }

                if (node.sizeDelta.y <= 0.5f) Fail(name + " cao 0");
                if (name == "TutBoard" && node.sizeDelta.x > cardWidth + 0.5f)
                    Fail($"bàn rộng {node.sizeDelta.x:F0} > thẻ {cardWidth:F0}");
            }

            if (lowest > contentLimit + 0.5f)
                Fail($"{lowestName} chạm đáy {lowest:F0} > mốc nút {contentLimit:F0}" +
                     " (chữ sẽ tụt xuống dưới nút)");

            // 3. Bàn phải giữ đúng tỉ lệ cột/hàng, không thì ô méo.
            RectTransform holder = Ui.Reuse("TutBoard", cardRect);
            if (holder != null && holder.sizeDelta.y > 0f)
            {
                float want = lesson.Columns / (float)lesson.VisibleRows;
                float got = holder.sizeDelta.x / holder.sizeDelta.y;
                if (Mathf.Abs(want - got) > 0.02f)
                    Fail($"tỉ lệ bàn {got:F3} lệch khỏi {want:F3}");

                // 4. BoardView phải đã dựng và có cỡ thật.
                RectTransform boardNode = Ui.Reuse("Board", holder);
                if (boardNode == null) Fail("BoardView chưa dựng node Board");
                else if (boardNode.sizeDelta.x <= 1f || boardNode.sizeDelta.y <= 1f)
                    Fail($"bàn cỡ {boardNode.sizeDelta.x:F0}x{boardNode.sizeDelta.y:F0}");
                else if (boardNode.sizeDelta.x > holder.sizeDelta.x + 0.5f ||
                         boardNode.sizeDelta.y > holder.sizeDelta.y + 0.5f)
                    Fail("bàn tràn khỏi chỗ dành cho nó");
                else
                {
                    // Ngưỡng ĐỌC ĐƯỢC, không phải ngưỡng tồn tại. Một bàn 40px vẫn lọt mọi
                    // phép kiểm bố cục ở trên mà nhìn không ra ô nào — với canvas logic
                    // rộng 1080 thì ô dưới 44px là không còn phân biệt được màu và ký hiệu.
                    float cellSize = boardNode.sizeDelta.x / lesson.Columns;
                    if (cellSize < 44f) Fail($"ô chỉ {cellSize:F0}px — quá nhỏ để đọc");
                    boardInfo = $" · ô {cellSize:F0}px";
                }
            }

            if (problems == 0)
                log.Append("    ok ").Append(who).Append(" · thẻ cao ").Append(cardHeight.ToString("F0"))
                   .Append(" · nội dung tới ").Append(lowest.ToString("F0"))
                   .Append(" · mốc nút ").Append(contentLimit.ToString("F0"))
                   .Append(boardInfo).Append('\n');

            return problems;
        }

        /// <summary>
        /// Canvas + lớp phủ + khung thẻ, đúng mức OverlayCard cần.
        ///
        /// RenderMode.WorldSpace chứ không ScreenSpaceOverlay: chỉ WorldSpace mới cho
        /// GÁN cỡ Canvas bằng tay, mà cỡ khung chính là biến của bài kiểm này.
        /// </summary>
        private static GameObject BuildRig(Vector2 frame, out OverlayCard card, out RectTransform cardRect)
        {
            var root = new GameObject("TutCheckRig", typeof(Canvas));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var canvasRect = (RectTransform)root.transform;
            canvasRect.sizeDelta = frame;

            RectTransform overlay = Ui.Node("Overlay", canvasRect);
            Ui.Stretch(overlay, 0, 0, 0, 0);
            overlay.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.6f);

            // Khung thẻ PHẢI tên "Card": OverlayCard.BindByNameForAuthoring tìm theo tên.
            Ui.Panel("Card", overlay, PuzzlePalette.Panel, PuzzlePalette.Line, PuzzlePalette.RadiusCard);

            card = overlay.gameObject.AddComponent<OverlayCard>();
            card.BindByNameForAuthoring();
            cardRect = card.Root;

            Canvas.ForceUpdateCanvases();
            return root;
        }
    }
}
