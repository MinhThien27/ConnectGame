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
    /// Kiểm BỐ CỤC của các thẻ nổi ở nhiều cỡ màn hình.
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

            problems += CheckGameCards(log);
            log.Append('\n');

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

        // ==================================================================
        // Ba thẻ dựng bởi PuzzleGame: thẻ thế giới, thẻ giữa chặng, thẻ tổng kết chặng
        //
        // Khác thẻ hướng dẫn ở chỗ chúng đi qua OverlayCard.Header() — đường mà bốn thẻ cũ
        // đã dùng — nên rủi ro đo chữ thấp. Cái đáng canh ở đây là chuyện khác: Begin(n)
        // phải KHỚP với số nút thật sự tạo ra. AddButton xếp nút từ đáy lên theo công thức
        // (n - 1 - slot), nên khai thiếu một nút là nút cuối rơi xuống DƯỚI đáy thẻ. Thẻ
        // thế giới có số nút thay đổi 1-3 tuỳ thế giới, nên đó là chỗ dễ lệch nhất.
        //
        // Mở thẻ THẬT trên một instance prefab thật, không dựng lại hình dạng thẻ trong bài
        // kiểm: bản mô phỏng sẽ trôi khỏi bản thật ngay lần sửa kế tiếp.
        // ==================================================================

        private const string RootPrefabPath = "Assets/ConnectPuzzle/Resources/UI/PuzzleRoot.prefab";

        private static int CheckGameCards(StringBuilder log)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RootPrefabPath);
            if (prefab == null)
            {
                log.Append("LOI: khong nap duoc ").Append(RootPrefabPath).Append('\n');
                return 1;
            }

            int problems = 0;

            foreach (Vector2 frame in Frames)
            {
                log.Append("--- the cua PuzzleGame · khung ")
                   .Append(frame.x).Append('x').Append(frame.y).Append('\n');

                for (int world = 1; world <= 12; world++)
                {
                    if (TutorialLessons.For(world) == null && !GauntletRun.AvailableFor(world))
                        continue;                       // thế giới không có gì để hiện

                    problems += OneCard(prefab, frame, "the-gioi-" + world, log,
                        game => game.DebugShowWorldCard(world));

                    if (!GauntletRun.AvailableFor(world)) continue;

                    problems += OneCard(prefab, frame, "thap-giua-" + world, log,
                        game => game.DebugShowTowerStepCard(world, 9));
                    problems += OneCard(prefab, frame, "thap-xong-" + world, log,
                        game => game.DebugShowTowerCard(world, true));
                    problems += OneCard(prefab, frame, "thap-hong-" + world, log,
                        game => game.DebugShowTowerCard(world, false));
                }
            }
            return problems;
        }

        private static int OneCard(GameObject prefab, Vector2 frame, string who, StringBuilder log,
                                   System.Func<PuzzleGame, RectTransform> open)
        {
            GameObject instance = null;
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

                // Canvas là CON, không phải gốc: gốc prefab (PuzzleRoot) mang Transform
                // thường, còn PuzzleCanvas mới là RectTransform. Ép kiểu transform của gốc
                // là InvalidCastException — đúng chỗ bản đầu của bài kiểm này đã sai.
                var canvas = instance.GetComponentInChildren<Canvas>(true);
                if (canvas != null)
                {
                    // Chỉ WorldSpace mới cho GÁN cỡ Canvas bằng tay. ScreenSpaceOverlay lấy
                    // cỡ từ màn hình thật, mà batchmode thì không có màn hình nào.
                    canvas.renderMode = RenderMode.WorldSpace;
                    var canvasRect = canvas.transform as RectTransform;
                    if (canvasRect != null) canvasRect.sizeDelta = frame;
                }
                Canvas.ForceUpdateCanvases();

                var game = instance.GetComponentInChildren<PuzzleGame>(true);
                if (game == null)
                {
                    log.Append("    LOI ").Append(who).Append(": prefab khong co PuzzleGame\n");
                    return 1;
                }

                RectTransform card = open(game);
                if (card == null)
                {
                    log.Append("    bo qua ").Append(who).Append(" (khong dung duoc)\n");
                    return 0;
                }
                Canvas.ForceUpdateCanvases();

                return VerifyCard(card, who, log);
            }
            catch (System.Exception e)
            {
                log.Append("    LOI ").Append(who).Append(": ").Append(e.GetType().Name)
                   .Append(" — ").Append(e.Message).Append('\n');
                return 1;
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// Bất biến chung của mọi thẻ dùng Header + AddButton: chữ neo từ đỉnh, nút neo từ
        /// đáy, và cả hai phải nằm trong khung thẻ mà không đè lên nhau.
        /// </summary>
        private static int VerifyCard(RectTransform card, string who, StringBuilder log)
        {
            int problems = 0;
            void Fail(string what)
            {
                problems++;
                log.Append("    LOI ").Append(who).Append(": ").Append(what).Append('\n');
            }

            float height = card.sizeDelta.y;
            if (height <= 0f) { Fail("thẻ cao 0"); return problems; }

            float lowestText = 0f;
            float highestButtonTop = 0f;
            float lowestButtonBottom = float.MaxValue;
            int buttons = 0;

            foreach (RectTransform child in card)
            {
                string name = child.name;
                if (name == "Fill" || name == "Border" || name == "Sheen") continue;

                if (name.StartsWith("CardBtn"))
                {
                    buttons++;

                    // Nút neo từ đáy (anchor y = 0): anchoredPosition.y là mép DƯỚI.
                    float bottom = child.anchoredPosition.y;
                    float top = bottom + child.sizeDelta.y;

                    if (bottom < -0.5f)
                        Fail(name + " tụt xuống dưới đáy thẻ (" + bottom.ToString("F0") +
                             ") — dấu hiệu Begin() khai thiếu nút");
                    if (top > height + 0.5f)
                        Fail(name + " vượt lên trên đỉnh thẻ (" + top.ToString("F0") +
                             " > " + height.ToString("F0") + ")");
                    if (child.sizeDelta.y <= 0f) Fail(name + " cao 0");

                    if (top > highestButtonTop) highestButtonTop = top;
                    if (bottom < lowestButtonBottom) lowestButtonBottom = bottom;
                    continue;
                }

                // Khối chữ neo từ đỉnh (anchor y = 1): -anchoredPosition.y là mép TRÊN.
                float textTop = -child.anchoredPosition.y;
                float textBottom = textTop + child.sizeDelta.y;
                if (textBottom > lowestText) lowestText = textBottom;
                if (textBottom > height + 0.5f)
                    Fail(name + " tràn khỏi đáy thẻ (" + textBottom.ToString("F0") +
                         " > " + height.ToString("F0") + ")");
            }

            if (buttons == 0) Fail("thẻ không có nút nào");

            // Chữ không được chạm vào khối nút.
            if (buttons > 0 && lowestText > height - highestButtonTop + 0.5f)
                Fail("chữ chạm " + lowestText.ToString("F0") + " nhưng khối nút bắt đầu ở " +
                     (height - highestButtonTop).ToString("F0") + " — chữ nằm dưới nút");

            if (problems == 0)
                log.Append("    ok ").Append(who).Append(" · cao ").Append(height.ToString("F0"))
                   .Append(" · ").Append(buttons).Append(" nút · chữ tới ")
                   .Append(lowestText.ToString("F0")).Append('\n');

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
