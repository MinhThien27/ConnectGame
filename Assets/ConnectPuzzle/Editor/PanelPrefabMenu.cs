using ConnectPuzzle.View;
using UnityEditor;
using UnityEngine;

namespace ConnectPuzzle.EditorTools
{
    /// <summary>
    /// Cắt các bảng còn lại ra thành prefab, dùng chung UiPanelExtractor.
    ///
    /// Chạy MỘT LẦN cho mỗi bảng. Sau khi chạy và đã đổi code sang Instantiate, prefab là
    /// sự thật duy nhất — chạy lại menu này sẽ GHI ĐÈ mọi chỉnh sửa bạn làm trong Editor,
    /// nên chỉ dùng khi muốn dựng lại từ đầu.
    /// </summary>
    public static class PanelPrefabMenu
    {
        [MenuItem("Connect Puzzle/Prefab/Cắt bảng đấu seed", priority = 70)]
        public static void ExtractDuelPanel()
        {
            UiPanelExtractor.Extract<DuelPanelView>(
                "DuelPanel",
                "Assets/ConnectPuzzle/Resources/UI/DuelPanel.prefab",
                v => v.BindByNameForAuthoring());
        }

        [MenuItem("Connect Puzzle/Prefab/Cắt bảng vật phẩm", priority = 71)]
        public static void ExtractItemPanel()
        {
            UiPanelExtractor.Extract<ItemPanelView>(
                "ItemPanel",
                "Assets/ConnectPuzzle/Resources/UI/ItemPanel.prefab",
                v => v.BindByNameForAuthoring());
        }

        // KHÔNG cắt thẻ overlay ra prefab, có chủ ý.
        //
        // Nó chỉ là một khung 3 node (Card/Fill/Border) cỡ 760x620; toàn bộ nội dung dựng
        // ĐỘNG theo loại thẻ (thắng / thua / vô tận / phán quyết) qua ClearCard và
        // AddCardButton. Prefab hoá nó cho bạn đúng một hình chữ nhật để kéo, đổi lại thêm
        // một tầng gián tiếp và một prefab nữa phải giữ đồng bộ. Cùng lý do đã bỏ HUD.
    }
}
