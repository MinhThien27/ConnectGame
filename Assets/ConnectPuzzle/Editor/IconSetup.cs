using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Android;
using UnityEngine;

namespace ConnectPuzzle.EditorTools
{
    /// <summary>
    /// Gán icon app và ảnh splash vào PlayerSettings.
    ///
    /// Phải làm bằng code chứ không kéo thả tay vì Unity giữ icon theo TỪNG CỠ và từng
    /// loại; bỏ sót một cỡ là Android quay về icon mặc định của Unity ở đúng mật độ màn
    /// hình đó — máy này đúng, máy kia sai, rất khó lần ra.
    /// </summary>
    public static class IconSetup
    {
        private const string Art = "Assets/ConnectPuzzle/Art/";

        [MenuItem("Connect Puzzle/Gán icon + splash vào PlayerSettings", priority = 61)]
        public static void Apply()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "icon.png");
            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "icon_foreground.png");
            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "icon_background.png");
            var splash = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "splash.png");

            if (icon == null)
            {
                Debug.LogError("Chưa có " + Art + "icon.png — chạy 'Sinh logo + ảnh splash' trước.");
                return;
            }

            // Icon phải ĐỌC ĐƯỢC và không nén, nếu không Unity từ chối dùng làm icon
            MakeIconReadable(Art + "icon.png");
            MakeIconReadable(Art + "icon_foreground.png");
            MakeIconReadable(Art + "icon_background.png");

            // --- icon chung (dùng cho mọi cỡ Unity hỏi tới)
            foreach (NamedBuildTarget target in new[] { NamedBuildTarget.Android, NamedBuildTarget.Standalone })
            {
                int[] sizes = PlayerSettings.GetIconSizes(target, IconKind.Application);
                var icons = new Texture2D[sizes.Length];
                for (int i = 0; i < icons.Length; i++) icons[i] = icon;
                PlayerSettings.SetIcons(target, icons, IconKind.Application);
            }

            // --- Android adaptive: hai lớp rời, đây mới là icon mà Android 8+ thật sự dùng
            if (foreground != null && background != null)
            {
                // API nhận PlatformIconKind (lớp cha) và BuildTargetGroup, không nhận
                // AndroidPlatformIconKind/NamedBuildTarget trực tiếp.
                PlatformIconKind adaptive = AndroidPlatformIconKind.Adaptive;
                var kinds = new PlatformIconKind[]
                {
                    adaptive, AndroidPlatformIconKind.Round, AndroidPlatformIconKind.Legacy
                };

                foreach (PlatformIconKind kind in kinds)
                {
                    PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(BuildTargetGroup.Android, kind);

                    // Duyệt bằng CHỈ SỐ, không dùng foreach: PlatformIcon là struct nên
                    // foreach đưa ra BẢN SAO, SetTextures sửa vào bản sao rồi vứt đi —
                    // kết quả chỉ đúng một ô được gán, năm mật độ còn lại trống.
                    for (int i = 0; i < slots.Length; i++)
                    {
                        if (kind == adaptive && slots[i].maxLayerCount >= 2)
                            slots[i].SetTextures(background, foreground);
                        else
                            slots[i].SetTextures(icon);
                    }
                    PlayerSettings.SetPlatformIcons(BuildTargetGroup.Android, kind, slots);

                    int filled = 0;
                    PlatformIcon[] check = PlayerSettings.GetPlatformIcons(BuildTargetGroup.Android, kind);
                    for (int i = 0; i < check.Length; i++)
                        if (check[i].GetTextures().Length > 0 && check[i].GetTextures()[0] != null) filled++;
                    Debug.Log("  " + kind + ": gán " + filled + "/" + check.Length + " mật độ");
                }
            }

            // --- splash
            if (splash != null)
            {
                PlayerSettings.SplashScreen.show = true;
                PlayerSettings.SplashScreen.showUnityLogo = false;   // bản Pro/Plus mới bỏ được
                PlayerSettings.SplashScreen.backgroundColor = new Color32(0x0F, 0x12, 0x20, 0xFF);
                PlayerSettings.SplashScreen.logos = new[]
                {
                    PlayerSettings.SplashScreenLogo.Create(2.5f, splash)
                };
            }

            AssetDatabase.SaveAssets();
            Debug.Log("ICONS_OK — đã gán icon" + (splash != null ? " + splash" : "") +
                      ". Kiểm lại ở Project Settings > Player.");
        }

        /// <summary>Batchmode: -executeMethod ConnectPuzzle.EditorTools.IconSetup.ApplyBatch</summary>
        public static void ApplyBatch()
        {
            Apply();
            EditorApplication.Exit(0);
        }

        private static void MakeIconReadable(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                dirty = true;
            }
            if (!importer.isReadable) { importer.isReadable = true; dirty = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                dirty = true;
            }
            if (importer.maxTextureSize < 1024) { importer.maxTextureSize = 1024; dirty = true; }
            if (dirty) importer.SaveAndReimport();
        }
    }
}
