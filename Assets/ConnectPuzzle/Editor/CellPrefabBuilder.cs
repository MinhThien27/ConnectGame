using System.IO;
using ConnectPuzzle.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.EditorTools
{
    /// <summary>
    /// Dựng prefab MỘT ô bàn. BoardView instantiate nó theo số ô của màn (25–64 ô, đổi
    /// theo màn) — đó chính là lý do ô bàn không thể nằm trong prefab tổng: prefab tổng
    /// là ảnh chụp một thời điểm, mà số ô thì không cố định.
    ///
    /// Prefab TỰ CHỨA sprite (nướng ra PNG), nên sửa được cả hình lẫn bố cục trong Editor.
    /// </summary>
    public static class CellPrefabBuilder
    {
        public const string ResourcePath = "UI/Cell";
        private const string AssetPath = "Assets/ConnectPuzzle/Resources/UI/Cell.prefab";

        [MenuItem("Connect Puzzle/Dựng lại prefab ô bàn", priority = 62)]
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));

            GameObject root = BuildHierarchy();
            try
            {
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
        /// Cùng thứ tự dựng như BoardView đang làm — thứ tự QUAN TRỌNG vì nó quyết định
        /// lớp nào vẽ đè lớp nào: băng phải sau Fill/Sheen, còn vòng đích và huy hiệu ngòi
        /// phải nằm trên Root chứ không trên Bubble (hoạt ảnh nổ co Bubble về 0, gắn vào
        /// đó thì chúng biến mất theo).
        /// </summary>
        public static GameObject BuildHierarchy()
        {
            RectTransform root = Ui.Node("Cell", null);
            var view = root.gameObject.AddComponent<CellView>();

            Ui.Image("Slot", root, new Color(1f, 1f, 1f, 0.045f), PuzzleSprites.RoundedSlot);

            RectTransform bubble = Ui.Node("Bubble", root);

            Image glow = Ui.Image("Glow", bubble, new Color(1, 1, 1, 0), PuzzleSprites.SoftGlow);
            glow.enabled = false;
            Ui.Image("Shadow", bubble, new Color(0f, 0f, 0f, 0.42f), PuzzleSprites.SoftGlow);
            Ui.Image("Fill", bubble, Color.white, PuzzleSprites.Circle);
            Ui.Image("Sheen", bubble, Color.white, PuzzleSprites.BubbleSheen);
            Image ring = Ui.Image("Ring", bubble, new Color(1, 1, 1, 0), PuzzleSprites.Ring);
            ring.enabled = false;

            Image ice = Ui.Image("Ice", bubble, Color.white, PuzzleSprites.IceOverlay(false));
            ice.enabled = false;

            // Đọc THẲNG hằng số của BoardView, không chép lại. Bản đầu tôi chép tay và gõ
            // 0.72 trong khi thật là 0.33 — ký hiệu đậm gấp đôi, mà bố cục vẫn đúng nên
            // chỉ bài kiểm so đúng con số mới bắt được.
            Ui.Text("Glyph", bubble, "", 20, new Color(0f, 0f, 0f, BoardView.GlyphAlpha),
                    TextAnchor.MiddleCenter, FontStyle.Bold);

            Image goalRing = Ui.Image("GoalRing", root, new Color(0.98f, 0.75f, 0.14f, 1f),
                                      PuzzleSprites.Ring);
            goalRing.enabled = false;

            Image fuseBadge = Ui.Image("FuseBadge", root, Color.white, PuzzleSprites.FuseBadge);
            fuseBadge.enabled = false;

            Text fuse = Ui.Text("Fuse", root, "", 16, Color.white, TextAnchor.MiddleCenter,
                                FontStyle.Bold);
            fuse.enabled = false;

            view.BindByNameForAuthoring();
            return root.gameObject;
        }
    }
}
