using System.IO;
using ConnectPuzzle.Core;
using ConnectPuzzle.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ConnectPuzzle.EditorTools
{
    /// <summary>
    /// Tạo scene chạy được cho prototype. Toàn bộ UI do PuzzleGame dựng lúc runtime,
    /// nên scene chỉ cần một GameObject duy nhất — không prefab, không wire tay.
    /// </summary>
    public static class PuzzlePrototypeSetup
    {
        private const string SceneFolder = "Assets/ConnectPuzzle/Scenes";
        private const string ScenePath = SceneFolder + "/ConnectPuzzlePrototype.unity";

        [MenuItem("Connect Puzzle/Tạo scene prototype", false, 10)]
        public static void CreateScene()
        {
            if (!Directory.Exists(SceneFolder)) Directory.CreateDirectory(SceneFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = PuzzlePalette.Background;
            cameraGo.transform.position = new Vector3(0, 0, -10);

            var gameGo = new GameObject("ConnectPuzzle");
            gameGo.AddComponent<PuzzleGame>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log("[ConnectPuzzle] Đã tạo scene: " + ScenePath);
        }

        [MenuItem("Connect Puzzle/Kiểm tra sinh 24 màn", false, 20)]
        public static void VerifyCatalog()
        {
            int failures = 0;
            var report = new System.Text.StringBuilder();
            report.AppendLine("[ConnectPuzzle] Bảng màn:");

            for (int i = 0; i < LevelCatalog.Levels.Length; i++)
            {
                LevelConfig cfg = LevelCatalog.Levels[i];
                try
                {
                    LevelData level = LevelBuilder.Build(cfg);
                    var session = new PuzzleSession(level);

                    bool ok = session.HasMove() && session.Analyze() == null;
                    if (!ok) failures++;

                    report.AppendLine(string.Format(
                        "  {0,2}. {1,-14} {2}  tổng {3,3}  hiện {4,3}  par {5,2}  max {6,2}  undo {7}  xáo {8}  {9}",
                        i + 1, cfg.Name, level.Gravity ? "▼" : " ", level.TotalCells, level.VisibleCells,
                        level.Par, level.MaxMoves, level.Undos, level.Shuffles, ok ? "OK" : "LỖI"));
                }
                catch (System.Exception e)
                {
                    failures++;
                    report.AppendLine("  " + (i + 1) + ". " + cfg.Name + " LỖI: " + e.Message);
                }
            }

            report.AppendLine(failures == 0 ? "  => 24/24 màn hợp lệ" : "  => " + failures + " màn LỖI");
            if (failures == 0) Debug.Log(report.ToString());
            else Debug.LogError(report.ToString());
        }

        /// <summary>Dùng cho batchmode: compile + tạo scene + kiểm tra bảng màn rồi thoát.</summary>
        public static void BatchSetup()
        {
            CreateScene();

            var scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorBuildSettings.scenes = scenes;

            VerifyCatalog();
            Debug.Log("[ConnectPuzzle] BATCH_SETUP_OK");
            EditorApplication.Exit(0);
        }
    }
}
