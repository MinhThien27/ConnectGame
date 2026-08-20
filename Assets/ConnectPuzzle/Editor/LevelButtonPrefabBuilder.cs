using System.IO;
using ConnectPuzzle.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.EditorTools
{
    /// <summary>
    /// Dựng prefab MỘT nút chọn màn. Code instantiate nó 90 lần.
    ///
    /// Prefab này TỰ CHỨA cả sprite (nướng ra PNG qua UiPrefabExporter.BakeSprites), nên
    /// mở ra trong Editor là sửa được cả hình lẫn bố cục, không cần gán gì lúc chạy.
    /// </summary>
    public static class LevelButtonPrefabBuilder
    {
        public const string ResourcePath = "UI/LevelButton";
        private const string AssetPath = "Assets/ConnectPuzzle/Resources/UI/LevelButton.prefab";

        [MenuItem("Connect Puzzle/Dựng lại prefab nút chọn màn", priority = 61)]
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));

            GameObject root = BuildHierarchy();
            try
            {
                // Nướng sprite TRƯỚC khi lưu. pruneOrphans = false: danh sách "còn sống" ở
                // đây chỉ có hình của một cái nút, bật prune là xoá sạch sprite của prefab
                // tổng.
                int baked = UiPrefabExporter.BakeSprites(root, pruneOrphans: false);

                PrefabUtility.SaveAsPrefabAsset(root, AssetPath, out bool ok);
                AssetDatabase.Refresh();
                Debug.Log(ok
                    ? "[Prefab] Đã lưu " + AssetPath + " · nướng " + baked + " sprite"
                    : "[Prefab] LƯU THẤT BẠI: " + AssetPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Cùng cấu trúc mà BuildLevelGrid đang dựng, để prefab khớp UI runtime từng thuộc
        /// tính — đó là điều kiện mà công cụ "So sánh prefab với code" sẽ kiểm.
        ///
        /// Khác một chỗ có chủ ý: nhãn tên là "Label" cố định thay vì "Level{N}Label". Tên
        /// theo chỉ số không đặt được trong prefab, và code giờ lấy nhãn qua [SerializeField]
        /// nên không còn ai tìm nó theo tên.
        /// </summary>
        public static GameObject BuildHierarchy()
        {
            Button button = Ui.Button("LevelButton", null, "", 40,
                PuzzlePalette.Panel, PuzzlePalette.Foreground, PuzzlePalette.RadiusSmall);

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);

            // Ui.Button đặt tên nhãn theo tên nút; đổi lại thành "Label" cho ổn định.
            Text label = Ui.LabelOf(button);
            label.name = "Label";
            label.alignment = TextAnchor.MiddleCenter;

            Text stars = Ui.Text("Stars", rect, "", 20, Color.white, TextAnchor.MiddleCenter);
            stars.supportRichText = true;          // màu sao đầy/rỗng đặt bằng thẻ <color>
            stars.rectTransform.anchorMin = new Vector2(0, 0);
            stars.rectTransform.anchorMax = new Vector2(1, 0);
            stars.rectTransform.pivot = new Vector2(0.5f, 0);

            Text badge = Ui.Text("Gravity", rect, "▼", 22, new Color(1, 1, 1, 0.45f),
                                 TextAnchor.UpperRight);
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(1, 1);
            badge.rectTransform.pivot = new Vector2(1, 1);
            badge.rectTransform.sizeDelta = new Vector2(40, 30);
            badge.rectTransform.anchoredPosition = new Vector2(-8, -6);
            badge.gameObject.SetActive(false);     // chỉ màn gravity bật lên

            var view = button.gameObject.AddComponent<LevelButtonView>();
            view.BindByNameForAuthoring();
            return button.gameObject;
        }
    }
}
