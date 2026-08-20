using System.Collections.Generic;
using System.Text;
using ConnectPuzzle.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.EditorTools
{
    /// <summary>
    /// So sánh prefab UI (bạn đã sửa tay) với UI mà code dựng ra, rồi liệt kê từng chỗ
    /// lệch kèm số cụ thể.
    ///
    /// Vì sao không ghi thẳng ngược vào code: phần lớn vị trí trong PuzzleGame là CÔNG
    /// THỨC, không phải hằng số — ví dụ nút màn nằm ở `left + column * (buttonSize +
    /// MenuGap)`, còn chiều cao khối tiêu đề thì đo từ chữ thật lúc chạy. Không có ô nào
    /// để nhét con số mới vào. Nên công cụ này nói cho bạn biết ĐÃ ĐỔI GÌ, còn sửa ở đâu
    /// trong .cs thì bạn quyết — thường là một trong các hằng số ở đầu vùng bố cục.
    ///
    /// Quy trình: Xuất prefab → sửa trong Editor → chạy So sánh → chép số về .cs →
    /// Xuất prefab lại (lúc này So sánh phải sạch).
    /// </summary>
    public static class UiPrefabDiff
    {
        // So với PREFAB GỐC — thứ scene thật sự dùng. Bản xuất cũ ở Prefabs/PuzzleUI.prefab
        // chỉ là ảnh chụp để xem, không ai nạp nó lúc chạy, nên so với nó là so với thứ
        // không ảnh hưởng gì tới game.
        private const string PrefabPath = "Assets/ConnectPuzzle/Resources/UI/PuzzleRoot.prefab";
        private const float Tolerance = 0.5f;      // dưới nửa đơn vị thì coi như không đổi

        [MenuItem("Connect Puzzle/So sánh prefab với code", priority = 63)]
        public static void Compare()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("Chưa có " + PrefabPath + " — chạy 'Xuất UI ra prefab' trước.");
                return;
            }

            var host = new GameObject("PuzzleUI_Reference");
            try
            {
                PuzzleGame game = host.AddComponent<PuzzleGame>();
                game.BuildAll();
                game.ShowMenu();
                Canvas.ForceUpdateCanvases();

                Dictionary<string, RectTransform> fromCode = Index(host.transform);
                Dictionary<string, RectTransform> fromPrefab = Index(prefab.transform);

                var report = new StringBuilder();
                int changed = 0, onlyInPrefab = 0, onlyInCode = 0;

                foreach (var pair in fromPrefab)
                {
                    if (!fromCode.TryGetValue(pair.Key, out RectTransform code))
                    {
                        onlyInPrefab++;
                        report.AppendLine("  [THÊM MỚI trong prefab] " + pair.Key);
                        continue;
                    }
                    // Bỏ qua chính Canvas: trong prefab nó không có màn hình để căn nên
                    // RectTransform luôn (0,0) — báo lệch ở đây là báo nhầm, và nếu để
                    // thì lần nào chạy cũng có một dòng rác che mất các lệch thật.
                    if (pair.Value.GetComponent<Canvas>() != null) continue;
                    changed += Describe(pair.Key, code, pair.Value, report);
                }
                foreach (var pair in fromCode)
                    if (!fromPrefab.ContainsKey(pair.Key)) onlyInCode++;

                Debug.Log("PREFAB_DIFF lệch " + changed + " chỗ · thêm mới " + onlyInPrefab +
                          " · chỉ có trong code " + onlyInCode + "\n" +
                          (report.Length > 0 ? report.ToString() : "  (không có gì lệch)"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        public static void CompareBatch()
        {
            Compare();
            EditorApplication.Exit(0);
        }

        /// <summary>Lập chỉ mục theo ĐƯỜNG DẪN trong cây, vì tên trùng nhau rất nhiều.</summary>
        private static Dictionary<string, RectTransform> Index(Transform root)
        {
            var map = new Dictionary<string, RectTransform>();
            Walk(root, "", map);
            return map;
        }

        private static void Walk(Transform node, string prefix, Dictionary<string, RectTransform> map)
        {
            for (int i = 0; i < node.childCount; i++)
            {
                Transform child = node.GetChild(i);
                string path = prefix.Length == 0 ? child.name : prefix + "/" + child.name;
                if (child is RectTransform rect && !map.ContainsKey(path)) map[path] = rect;
                Walk(child, path, map);
            }
        }

        /// <summary>Trả về 1 nếu có lệch, và ghi mô tả vào báo cáo.</summary>
        private static int Describe(string path, RectTransform code, RectTransform prefab, StringBuilder report)
        {
            var lines = new List<string>();

            if (Differs(code.sizeDelta, prefab.sizeDelta))
                lines.Add("cỡ " + V(code.sizeDelta) + " -> " + V(prefab.sizeDelta));
            if (Differs(code.anchoredPosition, prefab.anchoredPosition))
                lines.Add("vị trí " + V(code.anchoredPosition) + " -> " + V(prefab.anchoredPosition));
            if (Differs(code.offsetMin, prefab.offsetMin) || Differs(code.offsetMax, prefab.offsetMax))
                lines.Add("lề " + V(code.offsetMin) + "/" + V(code.offsetMax) +
                          " -> " + V(prefab.offsetMin) + "/" + V(prefab.offsetMax));
            if (Differs(code.anchorMin, prefab.anchorMin) || Differs(code.anchorMax, prefab.anchorMax))
                lines.Add("neo " + V(code.anchorMin) + "-" + V(code.anchorMax) +
                          " -> " + V(prefab.anchorMin) + "-" + V(prefab.anchorMax));

            if (code.gameObject.activeSelf != prefab.gameObject.activeSelf)
                lines.Add("bật/tắt " + code.gameObject.activeSelf + " -> " + prefab.gameObject.activeSelf);

            var codeText = code.GetComponent<Text>();
            var prefabText = prefab.GetComponent<Text>();
            if (codeText != null && prefabText != null)
            {
                if (codeText.fontSize != prefabText.fontSize)
                    lines.Add("cỡ chữ " + codeText.fontSize + " -> " + prefabText.fontSize);

                // NỘI DUNG chữ: nhãn sai là lỗi nhìn thấy ngay, mà hình học thì vẫn khớp.
                //
                // Trừ vài ô sinh NGẪU NHIÊN mỗi lần chạy (mã đấu) — lệch là đúng, không
                // phải lỗi. Vẫn IN RA để thấy, nhưng không đếm vào số lệch: che hẳn thì lần
                // sau có ô nào thật sự sai ta cũng không biết.
                if (codeText.text != prefabText.text)
                {
                    if (IsVolatileText(path))
                        report.AppendLine("  (bỏ qua, sinh ngẫu nhiên) " + path + ": \"" +
                                          Short(codeText.text) + "\" vs \"" +
                                          Short(prefabText.text) + "\"");
                    else
                        lines.Add("chữ \"" + Short(codeText.text) + "\" -> \"" +
                                  Short(prefabText.text) + "\"");
                }
                if (codeText.alignment != prefabText.alignment)
                    lines.Add("căn lề " + codeText.alignment + " -> " + prefabText.alignment);
                if (codeText.fontStyle != prefabText.fontStyle)
                    lines.Add("kiểu chữ " + codeText.fontStyle + " -> " + prefabText.fontStyle);
            }

            var codeGraphic = code.GetComponent<Graphic>();
            var prefabGraphic = prefab.GetComponent<Graphic>();
            if (codeGraphic != null && prefabGraphic != null)
            {
                if (!Near(codeGraphic.color, prefabGraphic.color))
                    lines.Add("màu " + C(codeGraphic.color) + " -> " + C(prefabGraphic.color));
                if (codeGraphic.raycastTarget != prefabGraphic.raycastTarget)
                    lines.Add("nhận chạm " + codeGraphic.raycastTarget + " -> " + prefabGraphic.raycastTarget);
            }

            var codeImage = code.GetComponent<Image>();
            var prefabImage = prefab.GetComponent<Image>();
            if (codeImage != null && prefabImage != null)
            {
                // Sprite runtime và sprite đã nướng là HAI đối tượng khác nhau, nên không so
                // được bằng tham chiếu. So thứ QUAN SÁT ĐƯỢC: có/không, kiểu vẽ, cỡ, và viền
                // 9-slice — mất viền là mọi góc bo bị kéo méo khi co giãn.
                bool codeHas = codeImage.sprite != null, prefabHas = prefabImage.sprite != null;
                if (codeHas != prefabHas)
                    lines.Add("sprite " + (codeHas ? "có" : "KHÔNG") + " -> " +
                              (prefabHas ? "có" : "KHÔNG"));
                else if (codeHas)
                {
                    if (codeImage.type != prefabImage.type)
                        lines.Add("kiểu vẽ " + codeImage.type + " -> " + prefabImage.type);
                    Vector2 a = codeImage.sprite.rect.size, b = prefabImage.sprite.rect.size;
                    if (Differs(a, b)) lines.Add("cỡ sprite " + V(a) + " -> " + V(b));
                    if (Differs(codeImage.sprite.border, prefabImage.sprite.border))
                        lines.Add("viền 9-slice " + codeImage.sprite.border +
                                  " -> " + prefabImage.sprite.border);
                }
            }

            if (lines.Count == 0) return 0;

            report.AppendLine("  " + path);
            foreach (string line in lines) report.AppendLine("      " + line);
            return 1;
        }

        private static bool Differs(Vector4 a, Vector4 b)
        {
            return Mathf.Abs(a.x - b.x) > Tolerance || Mathf.Abs(a.y - b.y) > Tolerance ||
                   Mathf.Abs(a.z - b.z) > Tolerance || Mathf.Abs(a.w - b.w) > Tolerance;
        }

        private static bool Near(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f &&
                   Mathf.Abs(a.b - b.b) < 0.004f && Mathf.Abs(a.a - b.a) < 0.004f;
        }

        private static string C(Color c)
        {
            return "#" + ColorUtility.ToHtmlStringRGBA(c);
        }

        private static string Short(string s)
        {
            if (s == null) return "";
            s = s.Replace((char)10, (char)124);
            return s.Length <= 24 ? s : s.Substring(0, 24) + "…";
        }

        private static bool Differs(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) > Tolerance || Mathf.Abs(a.y - b.y) > Tolerance;
        }

        private static string V(Vector2 v)
        {
            return "(" + v.x.ToString("F0") + "," + v.y.ToString("F0") + ")";
        }

        /// <summary>
        /// Ô chữ có nội dung sinh ngẫu nhiên mỗi lần chạy, nên lệch là bình thường.
        ///
        /// Giữ danh sách NGẮN và tường minh. Mỗi mục ở đây là một chỗ mà công cụ so sánh
        /// tự nguyện mù, nên thêm bừa vào đây là cách dễ nhất làm công cụ trở nên vô dụng.
        /// </summary>
        private static bool IsVolatileText(string path)
        {
            return path.EndsWith("DuelMine/DuelCode");      // mã đấu, sinh mới mỗi phiên
        }
    }
}
