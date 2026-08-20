using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ConnectPuzzle.EditorTools
{
    /// <summary>
    /// Build APK không qua cửa sổ Build Profiles.
    ///
    /// Lý do có file này: hộp thoại chọn nơi lưu của Unity có thể chặn build bằng một
    /// dialog modal (đường dẫn nằm trong project, còn lỗi biên dịch, thiếu module...) mà
    /// KHÔNG ghi gì vào Editor.log — nên khi build thất bại ở đó thì không có gì để đọc.
    /// Đường này chốt sẵn đường dẫn ra ngoài project và in mọi lỗi ra Console.
    /// </summary>
    public static class AndroidBuild
    {
        private const string OutputFolder = "BuildFile/Android";

        [MenuItem("Connect Puzzle/Build APK (Android)", priority = 40)]
        public static void BuildApk()
        {
            string path = Run(out BuildReport report);
            if (report != null && report.summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(path);
        }

        /// <summary>Dùng cho batchmode: -executeMethod ConnectPuzzle.EditorTools.AndroidBuild.BuildApkBatch</summary>
        public static void BuildApkBatch()
        {
            Run(out BuildReport report);
            bool ok = report != null && report.summary.result == BuildResult.Succeeded;
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static string Run(out BuildReport report)
        {
            report = null;

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[Build] Không có scene nào được bật trong Build Settings. " +
                               "Mở File > Build Profiles và bật ConnectPuzzlePrototype.");
                return null;
            }

            // Ra NGOÀI thư mục project: Unity từ chối build vào bên trong project và đó là
            // một trong những dialog chặn build mà không ghi log.
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outputRoot = Path.Combine(Directory.GetParent(projectRoot).FullName, OutputFolder);
            Directory.CreateDirectory(outputRoot);

            string name = "ConnectPuzzle-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".apk";
            string output = Path.Combine(outputRoot, name);

            EnsureClassicActivity();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[Build] Đang đổi platform sang Android, việc này mất một lúc để " +
                          "biên dịch lại script. Bấm lại menu sau khi xong.");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                return null;
            }

            Debug.Log("[Build] scene=" + scenes.Length + " → " + output +
                      "\n  package=" + PlayerSettings.GetApplicationIdentifier(
                          UnityEditor.Build.NamedBuildTarget.Android) +
                      "  arch=" + PlayerSettings.Android.targetArchitectures +
                      "  minSdk=" + PlayerSettings.Android.minSdkVersion +
                      "  backend=" + PlayerSettings.GetScriptingBackend(
                          UnityEditor.Build.NamedBuildTarget.Android));

            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            });

            BuildSummary summary = report.summary;

            // In TỪNG lỗi của từng bước. Console mặc định gộp và cắt bớt, mà đúng chỗ bị
            // cắt thường lại là dòng nói ra nguyên nhân thật.
            foreach (BuildStep step in report.steps)
                foreach (BuildStepMessage message in step.messages)
                    if (message.type == LogType.Error || message.type == LogType.Exception)
                        Debug.LogError("[Build] " + step.name + ": " + message.content);

            if (summary.result == BuildResult.Succeeded)
                Debug.Log("[Build] XONG: " + output + "  (" +
                          (new FileInfo(output).Length / 1048576f).ToString("F1") + " MB)");
            else
                Debug.LogError("[Build] THẤT BẠI: " + summary.result +
                               " — " + summary.totalErrors + " lỗi. Xem các dòng [Build] phía trên.");

            return output;
        }

        /// <summary>
        /// Ép Application Entry Point về Activity CỔ ĐIỂN, không phải GameActivity.
        ///
        /// Vì sao: trên GameActivity (mặc định của Unity 6), InputField của uGUI cũ không
        /// dùng được TouchScreenKeyboard.hideInput. Unity ghi cảnh báo
        ///     "Hiding input field is not supported when using Game Activity"
        /// rồi bàn phím ảo bật lên và TẮT NGAY — ô nhập mã đấu thành không gõ được.
        ///
        /// Đã ĐO trên máy thật (POCO X6 Pro, ARM64), cùng một APK, chỉ khác setting này:
        ///     GameActivity : mInputShown=false, chữ không vào
        ///     Activity     : mInputShown=true,  chữ vào đủ "K7M2QX9F", focus=True
        ///
        /// Đặt ở đây chứ không chỉ sửa tay trong Player Settings: sửa tay thì lần sau ai
        /// đổi lại (hoặc Unity đổi mặc định khi nâng cấp) sẽ hỏng lại đúng chỗ này, mà
        /// triệu chứng thì không hề gợi ra nguyên nhân.
        /// </summary>
        private static void EnsureClassicActivity()
        {
            if (PlayerSettings.Android.applicationEntry == AndroidApplicationEntry.Activity) return;

            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            AssetDatabase.SaveAssets();
            Debug.Log("[Build] Đã đổi Application Entry Point sang Activity (cổ điển). " +
                      "GameActivity làm ô nhập không gõ được — xem chú thích EnsureClassicActivity.");
        }

        [MenuItem("Connect Puzzle/Sửa Entry Point cho ô nhập gõ được", priority = 41)]
        public static void FixEntryPoint()
        {
            AndroidApplicationEntry before = PlayerSettings.Android.applicationEntry;
            EnsureClassicActivity();
            Debug.Log("[Build] Entry Point: " + before + " -> " +
                      PlayerSettings.Android.applicationEntry);
        }
    }
}
