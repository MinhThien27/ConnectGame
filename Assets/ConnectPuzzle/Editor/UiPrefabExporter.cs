using System.Collections.Generic;
using System.IO;
using ConnectPuzzle.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.EditorTools
{
    /// <summary>
    /// Xuất toàn bộ UI đang dựng bằng code ra một prefab để XEM và SỬA trong Editor.
    ///
    /// Không viết tay hierarchy: chạy chính `PuzzleGame.BuildAll()` đã được kiểm tra rồi
    /// chụp lại kết quả. Nhờ vậy prefab không phải là một bản chép tay có thể lệch với
    /// game, mà đúng là cái game dựng ra.
    ///
    /// Việc khó nằm ở SPRITE: chúng được sinh trong bộ nhớ với HideAndDontSave, lưu
    /// prefab thẳng thì mọi tham chiếu thành null và prefab mở ra trắng bệch. Nên trước
    /// khi lưu, mỗi texture được ghi thành file PNG thật, import lại kèm border 9-slice,
    /// rồi nối lại vào Image.
    ///
    /// Prefab này là BẢN THAM CHIẾU để nhìn và đo, KHÔNG phải thứ game dùng lúc chạy —
    /// game vẫn tự dựng UI bằng code. Sửa trên prefab rồi thì chép số về file .cs.
    /// </summary>
    public static class UiPrefabExporter
    {
        private const string PrefabPath = "Assets/ConnectPuzzle/Prefabs/PuzzleUI.prefab";
        private const string SpriteFolder = "Assets/ConnectPuzzle/Art/Generated";

        [MenuItem("Connect Puzzle/Xuất UI ra prefab", priority = 62)]
        public static void Export()
        {
            Directory.CreateDirectory(SpriteFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

            var host = new GameObject("PuzzleUI");
            try
            {
                PuzzleGame game = host.AddComponent<PuzzleGame>();
                game.BuildAll();
                game.ShowMenu();
                Canvas.ForceUpdateCanvases();

                int sprites = BakeSprites(host, pruneOrphans: true);

                // Bỏ component điều khiển: prefab này để NHÌN. Giữ lại thì kéo vào scene
                // là nó dựng thêm một bộ UI nữa chồng lên bộ đã có sẵn trong prefab.
                Object.DestroyImmediate(game);

                PrefabUtility.SaveAsPrefabAsset(host, PrefabPath, out bool ok);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(ok
                    ? "EXPORT_OK " + PrefabPath + " — đã nướng " + sprites + " sprite ra " + SpriteFolder
                    : "EXPORT_FAIL không lưu được prefab");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        public static void ExportBatch()
        {
            Export();
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Ghi mọi texture sinh runtime thành file PNG rồi trỏ Image sang sprite asset.
        /// Trả về số sprite đã nướng.
        /// </summary>
        /// <summary>
        /// Nướng sprite runtime của một cây thành PNG rồi trỏ Image sang asset. Công khai để
        /// các bộ dựng prefab đơn lẻ (LevelButton, Cell) dùng chung — nhờ vậy prefab TỰ CHỨA
        /// cả hình, không cần gán sprite lúc chạy, và bạn sửa được hình trong Editor.
        ///
        /// pruneOrphans CHỈ được bật cho lần xuất TOÀN BỘ UI. Bật nó khi nướng một prefab
        /// đơn lẻ sẽ xoá sạch sprite của các prefab khác, vì danh sách "còn sống" lúc đó chỉ
        /// có hình của riêng cây này.
        /// </summary>
        public static int BakeSprites(GameObject host, bool pruneOrphans)
        {
            var baked = new Dictionary<Sprite, Sprite>();
            var pending = new List<(string path, Vector4 border, Sprite source)>();
            var live = new HashSet<string>();

            foreach (Image image in host.GetComponentsInChildren<Image>(true))
            {
                Sprite source = image.sprite;
                if (source == null || baked.ContainsKey(source)) continue;
                if (AssetDatabase.Contains(source)) { baked[source] = source; continue; }

                // Tên theo NỘI DUNG, không theo GetInstanceID.
                //
                // Instance ID đổi mỗi lần chạy, nên bản cũ đặt tên kiểu đó tạo ra một bộ
                // file MỚI ở mỗi lần xuất và để nguyên bộ cũ làm rác. Đo được: sau hai lần
                // xuất có 88 file, trong đó 64 là rác không ai tham chiếu — và chúng còn che
                // mất việc bản sửa mipmap đã ăn hay chưa.
                //
                // Băm nội dung thì cùng một hình luôn ra cùng một file: xuất lại là ghi đè,
                // và hai chỗ dùng chung một sprite thì dùng chung một file.
                byte[] png = ToPng(source.texture);
                string name = "sprite_" + source.texture.width + "x" + source.texture.height +
                              "_" + Hash8(png, source.border);
                string path = SpriteFolder + "/" + name + ".png";

                File.WriteAllBytes(path, png);
                live.Add(Path.GetFileName(path));
                pending.Add((path, source.border, source));
                baked[source] = null;                        // điền sau khi import xong
            }

            AssetDatabase.Refresh();

            foreach (var item in pending)
            {
                var importer = AssetImporter.GetAtPath(item.path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    // MIPMAP PHẢI BẬT, và lọc Trilinear.
                    //
                    // Đây không phải tinh chỉnh cho đẹp hơn: sprite ô bàn được sinh ở 256px
                    // rồi hiện ra quanh 80px. Không mipmap thì đúng công thức gây lỗi "hình
                    // tròn bị vỡ viền" đã mất ba vòng để sửa (mipmap + Trilinear + 256px +
                    // shader SDF). Nướng ra PNG với mipmap TẮT là ném đi bản sửa đó.
                    //
                    // Bilinear chỉ nội suy TRONG một mức rồi nhảy cứng sang mức kế; chỗ nhảy
                    // nằm ngay trên mép cong và hiện ra thành viền gãy khúc.
                    importer.mipmapEnabled = true;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.anisoLevel = 4;
                    importer.alphaIsTransparency = true;
                    importer.spritePixelsPerUnit = 100f;

                    // Border 9-slice PHẢI được chép sang: mất nó thì mọi nút, panel, thẻ
                    // đều bị kéo méo góc bo khi co giãn — hỏng đúng thứ đang muốn xem.
                    if (item.border != Vector4.zero)
                    {
                        TextureImporterSettings settings = new TextureImporterSettings();
                        importer.ReadTextureSettings(settings);
                        settings.spriteBorder = item.border;
                        settings.spriteMeshType = SpriteMeshType.FullRect;
                        importer.SetTextureSettings(settings);
                    }
                    importer.SaveAndReimport();
                }
                baked[item.source] = AssetDatabase.LoadAssetAtPath<Sprite>(item.path);
            }

            if (pruneOrphans)
            {
                int orphans = DeleteOrphans(live);
                if (orphans > 0) Debug.Log("[Export] Đã xoá " + orphans + " sprite không còn ai dùng.");
            }

            int count = 0;
            foreach (Image image in host.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null) continue;
                if (baked.TryGetValue(image.sprite, out Sprite asset) && asset != null)
                {
                    image.sprite = asset;
                    count++;
                }
            }

            // Material SDF cũng nằm trong bộ nhớ; lưu ra asset để prefab vẽ đúng hình.
            var materials = new Dictionary<Material, Material>();
            foreach (Image image in host.GetComponentsInChildren<Image>(true))
            {
                Material m = image.material;
                if (m == null || m == image.defaultMaterial || AssetDatabase.Contains(m)) continue;
                if (!materials.TryGetValue(m, out Material asset))
                {
                    string path = SpriteFolder + "/mat_" + Mathf.Abs(m.GetInstanceID()) + ".mat";
                    var copy = new Material(m);
                    AssetDatabase.CreateAsset(copy, path);
                    materials[m] = asset = copy;
                }
                image.material = asset;
            }

            return count;
        }

        /// <summary>
        /// Texture sinh runtime có thể không đọc được; chép qua RenderTexture để lấy pixel.
        /// </summary>
        private static byte[] ToPng(Texture2D texture)
        {
            if (texture.isReadable) return texture.EncodeToPNG();

            RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture, rt);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            return readable.EncodeToPNG();
        }

        /// <summary>
        /// Băm 8 ký tự từ nội dung PNG và viền 9-slice.
        ///
        /// Gộp cả viền vào băm vì hai sprite có cùng pixel nhưng khác viền là hai thứ khác
        /// nhau khi co giãn — trộn chúng vào một file thì góc bo của một trong hai bị méo.
        /// </summary>
        private static string Hash8(byte[] data, Vector4 border)
        {
            unchecked
            {
                ulong h = 14695981039346656037UL;
                foreach (byte b in data) { h ^= b; h *= 1099511628211UL; }
                foreach (float f in new[] { border.x, border.y, border.z, border.w })
                {
                    int bits = Mathf.RoundToInt(f);
                    for (int i = 0; i < 4; i++) { h ^= (byte)(bits >> (i * 8)); h *= 1099511628211UL; }
                }
                return h.ToString("x16").Substring(0, 8);
            }
        }

        /// <summary>
        /// Xoá sprite không còn ai dùng. Không xoá thì thư mục phình mãi và không cách nào
        /// biết file nào còn sống — đúng tình trạng vừa đo được (64/88 file là rác).
        /// </summary>
        /// <summary>
        /// Xoá sprite không còn ai dùng — CÓ TRA CỨU TOÀN PROJECT trước khi xoá.
        ///
        /// Bản cũ chỉ so với `live` của MỘT cây vừa nướng, nên xuất một bảng là xoá sạch
        /// sprite của mọi prefab khác. Nó hỏng IM LẶNG: prefab vẫn lưu đúng guid, chỉ có
        /// điều file đứng sau guid ấy không còn — mở ra thì ô "Sprite" trống trơn.
        ///
        /// Đo được lúc phát hiện: 8/9 ảnh của Cell, 10/26 của DuelPanel, 5/14 của
        /// ItemPanel, 16/289 của PuzzleRoot đều trỏ vào guid đã bị xoá.
        ///
        /// Nên `live` giờ chỉ là gợi ý; quyết định thật là "có prefab nào còn nhắc tới
        /// guid này không". Chậm hơn (đọc mọi .prefab) nhưng chạy tay vài lần một tháng.
        /// </summary>
        private static int DeleteOrphans(HashSet<string> live)
        {
            HashSet<string> referenced = GuidsReferencedByPrefabs();

            int removed = 0;
            foreach (string file in Directory.GetFiles(SpriteFolder, "*.png"))
            {
                string assetPath = file.Replace((char)92, (char)47);
                if (live.Contains(Path.GetFileName(file))) continue;

                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrEmpty(guid) && referenced.Contains(guid)) continue;

                if (AssetDatabase.DeleteAsset(assetPath)) removed++;
            }
            return removed;
        }

        /// <summary>Mọi guid được nhắc tới trong bất kỳ .prefab nào dưới Assets.</summary>
        private static HashSet<string> GuidsReferencedByPrefabs()
        {
            var found = new HashSet<string>();
            foreach (string id in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(id);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")) continue;
                foreach (string dep in AssetDatabase.GetDependencies(path, true))
                {
                    string g = AssetDatabase.AssetPathToGUID(dep);
                    if (!string.IsNullOrEmpty(g)) found.Add(g);
                }
            }
            return found;
        }

        /// <summary>
        /// Đếm Image trỏ vào sprite KHÔNG CÒN TỒN TẠI, trên mọi prefab.
        ///
        /// Kiểu hỏng này không làm gì đổ vỡ: prefab vẫn nạp, cây node vẫn đủ, Unity chỉ
        /// vẽ một ô trắng thay cho hình. Nên nó sống rất lâu mà không ai báo — phải đi
        /// tìm mới thấy.
        /// </summary>
        [MenuItem("Connect Puzzle/Prefab/Kiểm sprite chết", priority = 67)]
        public static void CheckDeadSprites()
        {
            int deadTotal = 0, prefabsHit = 0;
            var report = new System.Text.StringBuilder();

            foreach (string id in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(id);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                int dead = 0;
                var names = new List<string>();
                foreach (Image image in prefab.GetComponentsInChildren<Image>(true))
                {
                    // Ảnh trống = hỏng, trong project này không có ngoại lệ: rig đã chốt
                    // "mọi Image phải có sprite sau khi instantiate", nên không ô nào cố ý
                    // để trống.
                    //
                    // Tôi từng lọc thêm bằng SerializedProperty.objectReferenceInstanceIDValue
                    // để tách "gãy" khỏi "cố ý trống". Đem đo thì Unity trả 0 cho asset đã
                    // mất, nên bộ lọc đó nuốt sạch mọi ca thật — đối chứng âm xoá một sprite
                    // và hàm này im lặng. Bỏ đi.
                    if (image.sprite != null) continue;
                    dead++;
                    names.Add(image.name);
                }
                if (dead == 0) continue;
                prefabsHit++;
                deadTotal += dead;
                report.AppendLine("  " + path + " — " + dead + " Image trống: " +
                                  string.Join(", ", names.ToArray()));
            }

            if (deadTotal == 0) Debug.Log("DEAD_SPRITES 0 — mọi Image trong prefab đều có ảnh.");
            else Debug.LogError("DEAD_SPRITES " + deadTotal + " trên " + prefabsHit +
                                " prefab\n" + report);
        }

    }
}
