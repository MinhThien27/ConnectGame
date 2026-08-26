using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.EditorTools
{
    /// <summary>
    /// Chụp bố cục của prefab gốc thành một file văn bản, và so lại được.
    ///
    /// Đây là lưới an toàn THAY CHO UiPrefabDiff. Bài kiểm cũ so prefab với UI mà code
    /// dựng ra — nó chết ngay khi code dựng bị xoá, mà xoá code dựng chính là mục đích
    /// của bước cuối. Ảnh chụp không cần code dựng: nó so prefab với chính nó ở lần
    /// chốt trước, và file ảnh chụp nằm trong git nên mọi thay đổi hiện ra thành diff.
    ///
    /// Nó canh đúng thứ UiPrefabDiff từng canh: ai đó kéo nhầm một node trong Editor.
    /// Nó KHÔNG canh được "code và prefab trôi khỏi nhau" — nhưng sau bước cuối thì
    /// không còn hai bên để trôi nữa.
    ///
    /// Có ghi THỨ TỰ ANH EM (trường after=). Trước đây không ghi, và đó là một lỗ thật:
    /// các dòng được sắp theo thứ tự chữ cái rồi so theo khoá đường dẫn, nên kéo đổi chỗ
    /// hai node cho ra ảnh chụp Y HỆT — dù trong Unity UI thứ tự anh em chính là thứ tự
    /// vẽ, tức là một thay đổi hiển thị thật. Lỗ đó lọt một lần rồi: Board và ChainPreview
    /// đổi chỗ trong BoardArea mà ảnh chụp không nói gì.
    /// </summary>
    public static class PrefabSnapshot
    {
        private const string PrefabPath = "Assets/ConnectPuzzle/Resources/UI/PuzzleRoot.prefab";
        private const string SnapshotPath = "Assets/ConnectPuzzle/Editor/PuzzleRoot.layout.txt";

        [MenuItem("Connect Puzzle/Prefab/Ghi lại ảnh chụp bố cục", priority = 68)]
        public static void Write()
        {
            string dump = Dump();
            if (dump == null) return;
            File.WriteAllText(SnapshotPath, dump);
            AssetDatabase.Refresh();
            Debug.Log("SNAPSHOT đã ghi " + SnapshotPath + " · " +
                      dump.Split('\n').Length + " dòng");
        }

        [MenuItem("Connect Puzzle/Prefab/So prefab với ảnh chụp", priority = 69)]
        public static void Compare() { CountDifferences(); }

        /// <summary>Số dòng lệch. -1 nghĩa là chưa có ảnh chụp hoặc chưa có prefab.</summary>
        public static int CountDifferences()
        {
            string now = Dump();
            if (now == null) return -1;

            if (!File.Exists(SnapshotPath))
            {
                Debug.LogError("SNAPSHOT chưa có " + SnapshotPath +
                               " — chạy 'Ghi lại ảnh chụp bố cục' một lần.");
                return -1;
            }

            string[] before = File.ReadAllLines(SnapshotPath);
            string[] after = now.Split('\n');

            // So theo KHOÁ (đường dẫn node) chứ không so từng dòng theo thứ tự: thêm một
            // node ở giữa sẽ làm lệch mọi dòng sau nó và báo cáo thành vô dụng.
            var oldByPath = new Dictionary<string, string>();
            foreach (string line in before)
            {
                int at = line.IndexOf('|');
                if (at > 0) oldByPath[line.Substring(0, at)] = line;
            }

            var report = new StringBuilder();
            int changed = 0, added = 0;
            var seen = new HashSet<string>();

            foreach (string line in after)
            {
                int at = line.IndexOf('|');
                if (at <= 0) continue;
                string path = line.Substring(0, at);
                seen.Add(path);

                if (!oldByPath.TryGetValue(path, out string old))
                {
                    added++;
                    report.AppendLine("  [THÊM] " + path);
                }
                else if (old != line)
                {
                    changed++;
                    report.AppendLine("  [ĐỔI] " + path);
                    report.AppendLine("      cũ: " + old.Substring(at + 1));
                    report.AppendLine("      mới: " + line.Substring(at + 1));
                }
            }

            int removed = 0;
            foreach (string path in oldByPath.Keys)
                if (!seen.Contains(path)) { removed++; report.AppendLine("  [MẤT] " + path); }

            int total = changed + added + removed;
            string summary = "SNAPSHOT lệch " + changed + " · thêm " + added + " · mất " + removed;
            if (total == 0) Debug.Log(summary + " — prefab đúng như ảnh chụp.");
            else Debug.LogError(summary + "\n" + report +
                                "\nSửa có chủ ý thì chạy 'Ghi lại ảnh chụp bố cục' rồi commit file đó.");
            return total;
        }

        private static string Dump()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("SNAPSHOT chưa có " + PrefabPath);
                return null;
            }

            var lines = new List<string>();
            Walk(prefab.transform, "", lines);
            lines.Sort(System.StringComparer.Ordinal);   // ổn định giữa các lần chạy
            return string.Join("\n", lines.ToArray());
        }

        private static void Walk(Transform node, string prefix, List<string> lines)
        {
            for (int i = 0; i < node.childCount; i++)
            {
                Transform child = node.GetChild(i);
                string path = prefix.Length == 0 ? child.name : prefix + "/" + child.name;

                // Anh em đứng NGAY TRƯỚC, hoặc "-" nếu là con đầu. Đây là cách ghi lại
                // THỨ TỰ ANH EM, mà trong Unity UI thứ tự anh em chính là thứ tự vẽ.
                string after = i == 0 ? "-" : node.GetChild(i - 1).name;

                if (child is RectTransform rect) lines.Add(path + "|" + Describe(rect, path, after));
                Walk(child, path, lines);
            }
        }

        private static string Describe(RectTransform rect, string path, string after)
        {
            var sb = new StringBuilder();
            sb.Append("size=").Append(V(rect.sizeDelta));
            sb.Append(" pos=").Append(V(rect.anchoredPosition));
            sb.Append(" anchor=").Append(V(rect.anchorMin)).Append('-').Append(V(rect.anchorMax));
            sb.Append(" pivot=").Append(V(rect.pivot));
            sb.Append(" on=").Append(rect.gameObject.activeSelf ? 1 : 0);

            // Ghi TÊN ANH EM ĐỨNG TRƯỚC, không ghi chỉ số.
            //
            // Chỉ số thì đơn giản hơn nhưng nó dựng lại đúng cái hỏng mà CountDifferences
            // đã cố tránh: chèn một node ở giữa làm lệch chỉ số của MỌI anh em phía sau,
            // nên một lần chèn báo ra hàng chục dòng "đã đổi" và báo cáo thành vô dụng.
            // Tên anh em trước thì ổn định: chèn một node chỉ đổi đúng dòng nằm sau nó,
            // còn đảo chỗ hai node thì đổi đúng hai dòng.
            sb.Append(" after=").Append(after);

            var text = rect.GetComponent<Text>();
            if (text != null)
            {
                sb.Append(" font=").Append(text.fontSize);
                sb.Append(" align=").Append(text.alignment);
                // Mã đấu sinh NGẪU NHIÊN mỗi lần dựng — chụp nội dung của nó thì ảnh chụp
                // lệch mỗi lần chạy và không ai còn tin nó nữa.
                if (!IsVolatile(path)) sb.Append(" text=").Append(Short(text.text));
            }

            var image = rect.GetComponent<Image>();
            if (image != null)
            {
                sb.Append(" img=").Append(image.sprite == null ? "-" : image.sprite.name);
                sb.Append(" type=").Append(image.type);
                sb.Append(" color=").Append(ColorUtility.ToHtmlStringRGBA(image.color));
            }
            return sb.ToString();
        }

        private static bool IsVolatile(string path) => path.EndsWith("/DuelCode");

        private static string Short(string s)
        {
            if (s == null) return "";
            s = s.Replace("\n", "\\n").Replace("|", "/");
            return s.Length <= 60 ? s : s.Substring(0, 60) + "…";
        }

        private static string V(Vector2 v) =>
            v.x.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "," +
            v.y.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
    }
}
