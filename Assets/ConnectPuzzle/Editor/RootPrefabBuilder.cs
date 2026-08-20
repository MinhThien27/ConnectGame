using System.IO;
using ConnectPuzzle.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ConnectPuzzle.EditorTools
{
    /// <summary>
    /// Đặt prefab gốc vào scene, và các lối vào batch để kiểm prefab.
    ///
    /// Phần DỰNG prefab từ code đã bị xoá cùng code dựng UI: prefab giờ là nguồn duy
    /// nhất, sửa nó bằng Editor. Cái còn lại ở đây là đặt nó vào scene và canh cho nó
    /// không lệch khỏi ảnh chụp đã chốt.
    ///
    /// Vì sao là prefab gốc chứ không dựng thẳng vào scene file: rig kiểm thử phải nạp
    /// lại được UI, mà nạp cả một scene đòi đồng bộ scene + mọi asset nó tham chiếu sang
    /// project rác của rig. Prefab thì rig đã nạp được sẵn. Bạn vẫn thấy và sửa mọi thứ
    /// trong scene, vì scene chứa một instance của prefab này.
    /// </summary>
    public static class RootPrefabBuilder
    {
        public const string ResourcePath = "UI/PuzzleRoot";
        private const string AssetPath = "Assets/ConnectPuzzle/Resources/UI/PuzzleRoot.prefab";

        /// <summary>Ghi lại ảnh chụp bố cục prefab. Chạy sau khi sửa prefab có chủ ý.</summary>
        public static void SnapshotBatch()
        {
            PrefabSnapshot.Write();
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Kiểm prefab: không ô ảnh nào trống, và bố cục đúng như ảnh chụp đã chốt.
        ///
        /// Thoát mã 1 khi có lỗi, để script gọi biết mà dừng — một bài kiểm không chặn
        /// được gì thì gần như không phải bài kiểm.
        /// </summary>
        public static void CheckBatch()
        {
            bool failed = false;

            if (UiPrefabExporter.CountDeadSprites() > 0)
            {
                Debug.LogError("[Batch] còn Image trống trong prefab");
                failed = true;
            }
            if (PrefabSnapshot.CountDifferences() != 0)
            {
                Debug.LogError("[Batch] prefab lệch so với ảnh chụp");
                failed = true;
            }

            Debug.Log(failed ? "CHECK_FAILED" : "CHECK_OK");
            EditorApplication.Exit(failed ? 1 : 0);
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
