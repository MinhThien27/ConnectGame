using System;
using System.IO;
using ConnectPuzzle.View;
using UnityEditor;
using UnityEngine;

namespace ConnectPuzzle.EditorTools
{
    /// <summary>
    /// Cắt MỘT nhánh của UI đang chạy ra thành prefab riêng.
    ///
    /// Vì sao không viết lại bố cục trong một bộ dựng như LanPanelPrefabBuilder: chép lại
    /// ~190 dòng bố cục cho mỗi bảng là ~400 dòng chép tay, và tôi đã thử biến đổi máy móc
    /// thì nó sinh ra trùng tên biến và sai thứ tự cha-con. Tệ hơn: bản chép sẽ TRÔI khỏi
    /// prefab ngay khi bạn sửa prefab, vì lúc đó prefab mới là sự thật còn bộ dựng thành
    /// một bản sao cũ nằm im chờ gây nhầm lẫn.
    ///
    /// Cách này dùng chính đoạn code đã cho ra bố cục ĐÃ ĐƯỢC KIỂM làm nguồn, chạy một lần,
    /// rồi từ đó trở đi prefab là sự thật duy nhất và code dựng bị xoá.
    /// </summary>
    public static class UiPanelExtractor
    {
        /// <summary>
        /// Dựng UI thật, cắt nhánh tên nodeName ra, gắn component TView, nướng sprite,
        /// lưu thành prefab. bind được gọi để nối các [SerializeField] theo tên.
        /// </summary>
        public static bool Extract<TView>(string nodeName, string assetPath, Action<TView> bind)
            where TView : Component
        {
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));

            var host = new GameObject("ExtractHost");
            GameObject detached = null;
            try
            {
                // BuildAll() phải gọi TAY. AddComponent có gọi Awake ở edit mode, nhưng
                // PuzzleGame dựng UI trong một lối vào riêng — không gọi thì cây rỗng và
                // bộ trích xuất báo "không thấy node", đúng lỗi vừa gặp.
                PuzzleGame game = host.AddComponent<PuzzleGame>();
                game.BuildAll();
                game.ShowMenu();
                Canvas.ForceUpdateCanvases();

                Transform node = FindDeep(host.transform, nodeName);
                if (node == null)
                {
                    Debug.LogError("[Extract] Không thấy node '" + nodeName + "' trong UI.");
                    return false;
                }

                // Tách khỏi cha TRƯỚC khi thêm component: prefab phải là một cây độc lập,
                // còn giữ cha thì SaveAsPrefabAsset kéo theo cả canvas.
                node.SetParent(null, worldPositionStays: false);
                detached = node.gameObject;

                TView view = detached.GetComponent<TView>();
                if (view == null) view = detached.AddComponent<TView>();
                if (bind != null) bind(view);

                int baked = UiPrefabExporter.BakeSprites(detached, pruneOrphans: false);

                PrefabUtility.SaveAsPrefabAsset(detached, assetPath, out bool ok);
                AssetDatabase.Refresh();

                Debug.Log(ok
                    ? "[Extract] " + assetPath + " · " + CountNodes(detached.transform) +
                      " node · nướng " + baked + " sprite"
                    : "[Extract] LƯU THẤT BẠI: " + assetPath);
                return ok;
            }
            finally
            {
                if (detached != null) UnityEngine.Object.DestroyImmediate(detached);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static int CountNodes(Transform root)
        {
            int n = 1;
            foreach (Transform child in root) n += CountNodes(child);
            return n;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
