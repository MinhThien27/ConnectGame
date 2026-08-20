using System.IO;
using ConnectPuzzle.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ConnectPuzzle.EditorTools
{
    /// <summary>
    /// Gói TOÀN BỘ UI thành một prefab gốc có sẵn PuzzleGame, rồi đặt một bản vào scene.
    ///
    /// Vì sao prefab gốc chứ không dựng thẳng vào scene file: rig kiểm thử phải dựng lại
    /// được UI, mà nạp cả một scene đòi đồng bộ scene + mọi asset nó tham chiếu sang project
    /// rác của rig. Prefab thì rig đã nạp được sẵn — đúng cách nó đang làm với 5 prefab kia.
    /// Bạn vẫn thấy và sửa mọi thứ trong scene, vì scene chứa một instance của prefab này.
    ///
    /// KHÔNG cần bản đồ tên nào: BuildAll() vốn đã gán đúng cả ~40 tham chiếu, và từ khi
    /// chúng mang [SerializeField] thì Unity tự lưu lại những gì đã gán.
    /// </summary>
    public static class RootPrefabBuilder
    {
        public const string ResourcePath = "UI/PuzzleRoot";
        private const string AssetPath = "Assets/ConnectPuzzle/Resources/UI/PuzzleRoot.prefab";

        [MenuItem("Connect Puzzle/Prefab/Dựng prefab gốc (toàn bộ UI)", priority = 65)]
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));

            var host = new GameObject("ConnectPuzzle");
            try
            {
                PuzzleGame game = host.AddComponent<PuzzleGame>();
                game.BuildAll();
                game.ShowMenu();
                Canvas.ForceUpdateCanvases();

                System.Collections.Generic.List<string> missing = game.MissingSceneRefs();
                if (missing.Count > 0)
                {
                    Debug.LogError("[Root] BuildAll không nối đủ tham chiếu: " +
                                   string.Join(", ", missing));
                    return;
                }

                int baked = UiPrefabExporter.BakeSprites(host, pruneOrphans: false);

                PrefabUtility.SaveAsPrefabAsset(host, AssetPath, out bool ok);
                AssetDatabase.Refresh();
                if (!ok)
                {
                    Debug.LogError("[Root] LƯU THẤT BẠI: " + AssetPath);
                    return;
                }

                // Đếm component MẤT SCRIPT trong file vừa ghi.
                //
                // Đây là lỗi câm nhất trong cả chuỗi việc này: prefab lưu thành công, cây
                // node đủ, nhìn không khác gì — nhưng ô Script trống nên component không
                // chạy. Đã dính hai lần (CellView, BoardPointerInput), cùng một nguyên
                // nhân: MonoBehaviour nằm trong file khác tên lớp.
                int orphanScripts = 0;
                foreach (string line in File.ReadAllLines(AssetPath))
                    if (line.Contains("m_Script: {fileID: 0}")) orphanScripts++;

                if (orphanScripts > 0)
                    Debug.LogError("[Root] " + orphanScripts + " component MẤT SCRIPT trong prefab. " +
                                   "Mỗi MonoBehaviour phải nằm trong file trùng tên lớp.");

                Debug.Log("[Root] Đã lưu " + AssetPath + " · nướng " + baked + " sprite · " +
                          orphanScripts + " script mất");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// Thay GameObject ConnectPuzzle rỗng trong scene bằng MỘT instance của prefab gốc.
        ///
        /// Chạy sau khi đã dựng prefab. Từ lúc này mở scene ra là thấy cả cây UI, chọn và
        /// kéo được tại chỗ, và Game view có hình khi chưa bấm Play.
        /// </summary>
        [MenuItem("Connect Puzzle/Prefab/Đặt prefab gốc vào scene", priority = 66)]
        public static void PlaceInScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath);
            if (prefab == null)
            {
                Debug.LogError("[Root] Chưa có " + AssetPath +
                               ". Chạy 'Dựng prefab gốc' trước.");
                return;
            }

            // Xoá bản cũ trước khi đặt bản mới. Không xoá thì scene có hai bộ UI chồng lên
            // nhau, và cái nằm dưới vẫn nhận chạm — lỗi rất khó nhìn ra.
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager
                         .GetActiveScene().GetRootGameObjects())
            {
                if (root.GetComponent<PuzzleGame>() != null)
                    Object.DestroyImmediate(root);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "ConnectPuzzle";
            Undo.RegisterCreatedObjectUndo(instance, "Đặt prefab gốc");

            EditorSceneManager.MarkSceneDirty(instance.scene);
            EditorSceneManager.SaveScene(instance.scene);
            Debug.Log("[Root] Đã đặt prefab gốc vào scene và lưu scene.");
        }
    }
}
