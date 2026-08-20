using System.IO;
using ConnectPuzzle.View;
using UnityEditor;
using UnityEngine;

namespace ConnectPuzzle.EditorTools
{
    /// <summary>
    /// Sinh biểu tượng game và ảnh splash ra file PNG.
    ///
    /// Chỉ vẽ HÌNH, không vẽ chữ: rasterize font phải có GPU nên chạy headless không
    /// làm được. Phần chữ để font của game vẽ lúc chạy, ghép cạnh biểu tượng này —
    /// nhờ vậy đổi tên game hay đổi ngôn ngữ không phải vẽ lại ảnh.
    ///
    /// Biểu tượng: bốn ô màu nối thành một chuỗi gấp khúc, đúng thứ người chơi làm
    /// suốt ván. Dùng luôn bảng màu của bàn chơi nên logo và game là một khối.
    /// </summary>
    public static class LogoGenerator
    {
        private const string ArtFolder = "Assets/ConnectPuzzle/Art";

        [MenuItem("Connect Puzzle/Sinh logo + ảnh splash", priority = 60)]
        public static void Generate()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string folder = Path.Combine(root, ArtFolder.Replace('/', Path.DirectorySeparatorChar));
            Write(folder);
            AssetDatabase.Refresh();
            Debug.Log("Đã sinh logo vào " + ArtFolder);
        }

        /// <summary>Chạy được cả trong batchmode; ghi thẳng ra thư mục cho trước.</summary>
        public static void Write(string folder)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "logo.png"),
                Emblem(512, transparent: true, fill: 1f).EncodeToPNG());
            File.WriteAllBytes(Path.Combine(folder, "splash.png"),
                Emblem(1024, transparent: false, fill: 1f).EncodeToPNG());

            // --- Icon app: KHÔNG dùng lại logo.png.
            // Icon bị hệ điều hành cắt theo mặt nạ (tròn trên Android, bo góc trên iOS),
            // và icon nền trong suốt thì Android tự độn nền trắng. Nên icon phải ĐỤC nền
            // và hình phải co vào giữa để không bị mặt nạ ăn mất.
            File.WriteAllBytes(Path.Combine(folder, "icon.png"),
                Emblem(1024, transparent: false, fill: 0.88f).EncodeToPNG());

            // Android adaptive: hai lớp rời. Vùng an toàn chỉ là 66% giữa ảnh, phần
            // ngoài có thể bị cắt hoặc bị che khi hệ thống chạy hoạt ảnh icon.
            File.WriteAllBytes(Path.Combine(folder, "icon_foreground.png"),
                Emblem(1024, transparent: true, fill: 0.60f).EncodeToPNG());
            File.WriteAllBytes(Path.Combine(folder, "icon_background.png"),
                Emblem(1024, transparent: false, fill: 0f).EncodeToPNG());

            Debug.Log("LOGO_OK " + folder);
        }

        public static void WriteFromCommandLine()
        {
            string target = null;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-logoOut") target = args[i + 1];

            Write(target ?? Path.Combine(Directory.GetParent(Application.dataPath).FullName, "logo-out"));
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Bốn ô nối thành chuỗi gấp khúc, có đường nối phát sáng bên dưới.
        /// `fill` = phần khung mà hình chiếm (1 = kín khung, 0 = chỉ vẽ nền).
        /// </summary>
        private static Texture2D Emblem(int size, bool transparent, float fill)
        {
            var pixels = new Color[size * size];

            Color bgTop = PuzzlePalette.BackgroundTop;
            Color bgBottom = PuzzlePalette.Background;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    if (transparent) { pixels[y * size + x] = new Color(0, 0, 0, 0); continue; }
                    // nền radial như bàn chơi: sáng ở trên, tối dần xuống dưới
                    float nx = (x / (float)size - 0.5f) * 1.4f;
                    float ny = (y / (float)size - 0.72f) * 1.4f;
                    float t = Mathf.Clamp01(Mathf.Sqrt(nx * nx + ny * ny));
                    pixels[y * size + x] = Color.Lerp(bgTop, bgBottom, t);
                }

            // Chuỗi gấp khúc: xuống, chéo lên, ngang — đủ để thấy đây là một ĐƯỜNG NỐI
            // chứ không phải bốn chấm rời.
            if (fill <= 0f)
            {
                var plain = new Texture2D(size, size, TextureFormat.RGBA32, false);
                plain.SetPixels(pixels);
                plain.Apply();
                return plain;
            }

            float u = size / 100f;

            // Bố cục cân: chuỗi zig-zag nằm giữa khung, cao bằng ngang, không chạm mép.
            // Toạ độ viết theo hệ 0..100 rồi CO VỀ TÂM theo `fill`, nên đổi mức co không
            // phải sửa lại từng con số.
            var baseNodes = new[]
            {
                new Vector2(30f, 68f), new Vector2(30f, 36f),
                new Vector2(62f, 32f), new Vector2(70f, 64f)
            };
            var nodes = new Vector2[baseNodes.Length];
            for (int i = 0; i < nodes.Length; i++)
                nodes[i] = (new Vector2(50f, 50f) + (baseNodes[i] - new Vector2(50f, 50f)) * fill) * u;

            int[] colors = { 0, 3, 2, 1 };                 // đỏ, thiên thanh, lục, hổ phách
            float radius = 13f * u * fill;

            // 1. đường nối, vẽ TRƯỚC để nằm dưới các ô.
            //    Vẽ bằng ba lớp mềm dần chứ không phải một vệt đặc: một vệt đặc trên nền
            //    tối chỉ ra một thanh xám cứng cạnh, nhìn như que nối đồ chơi.
            for (int i = 0; i < nodes.Length - 1; i++)
            {
                Color from = PuzzlePalette.Colors[colors[i]];
                Color to = PuzzlePalette.Colors[colors[i + 1]];
                // Vệt phải MANG MÀU, không phải que trắng: lõi trắng chỉ là ánh sáng
                // mỏng chạy giữa, để mờ thôi. Lần trước lõi dày và đục 0.9 nên nó nuốt
                // hết phần màu bên dưới.
                DrawLink(pixels, size, nodes[i], nodes[i + 1], 11f * u * fill, from, to, 0.30f, soft: true);
                DrawLink(pixels, size, nodes[i], nodes[i + 1], 6.2f * u * fill, from, to, 1f, soft: true);
                DrawLink(pixels, size, nodes[i], nodes[i + 1], 1.6f * u * fill, Color.white, Color.white, 0.45f, soft: true);
            }

            // 2. các ô, có chóa sáng và bóng đổ như bubble trên bàn
            for (int i = 0; i < nodes.Length; i++)
                DrawBubble(pixels, size, nodes[i], radius, PuzzlePalette.Colors[colors[i]]);

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Vệt nối hai ô. Màu chạy dần từ màu ô này sang màu ô kia, và `soft` làm alpha
        /// tắt dần ra hai mép thay vì cắt phựt — không có nó thì lớp quầng rộng hiện ra
        /// thành một thanh chữ nhật xám.
        /// </summary>
        private static void DrawLink(Color[] buffer, int size, Vector2 a, Vector2 b,
                                     float width, Color from, Color to, float alpha, bool soft)
        {
            Vector2 ab = b - a;
            float lengthSq = Mathf.Max(0.0001f, ab.sqrMagnitude);

            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - width - 2));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + width + 2));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - width - 2));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + width + 2));

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
                    float d = Vector2.Distance(p, a + ab * t);

                    float cover;
                    if (soft)
                    {
                        // tắt dần theo bình phương: giữa đậm, ra mép nhạt hẳn
                        float k = Mathf.Clamp01(1f - d / width);
                        cover = k * k;
                    }
                    else
                    {
                        cover = Mathf.Clamp01(width - d);            // lõi sắc, mép mềm 1px
                    }
                    if (cover <= 0.002f) continue;

                    Blend(buffer, size, x, y, Color.Lerp(from, to, t), cover * alpha);
                }
        }

        private static void DrawBubble(Color[] buffer, int size, Vector2 center, float radius, Color color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius * 2f));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(center.x + radius * 2f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius * 2f));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(center.y + radius * 2f));

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x + 0.5f - center.x;
                    float dy = y + 0.5f - center.y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    // bóng đổ mềm, lệch xuống dưới
                    float shadow = Mathf.Clamp01((radius * 1.5f - Vector2.Distance(
                        new Vector2(x + 0.5f, y + 0.5f), center + new Vector2(0, -radius * 0.22f))) / (radius * 0.9f));
                    if (shadow > 0f) Blend(buffer, size, x, y, Color.black, shadow * shadow * 0.45f);

                    float cover = Mathf.Clamp01(radius - d);
                    if (cover <= 0f) continue;

                    // chóa sáng ở 32%/72% giống .bub của bản HTML
                    var hi = new Vector2(dx / radius + 0.36f, dy / radius - 0.44f);
                    float highlight = Mathf.Clamp01(1f - hi.magnitude / 0.85f);
                    Color body = Color.Lerp(color, Color.white, highlight * highlight * 0.5f);

                    // tối dần ở đáy trong
                    float depth = Mathf.Clamp01((-dy / radius - 0.35f) / 0.65f);
                    body = Color.Lerp(body, body * 0.68f, depth);

                    Blend(buffer, size, x, y, body, cover);
                }
        }

        private static void Blend(Color[] buffer, int size, int x, int y, Color color, float alpha)
        {
            int i = y * size + x;
            Color under = buffer[i];
            float a = alpha + under.a * (1f - alpha);
            if (a <= 0f) { buffer[i] = new Color(0, 0, 0, 0); return; }
            buffer[i] = new Color(
                (color.r * alpha + under.r * under.a * (1f - alpha)) / a,
                (color.g * alpha + under.g * under.a * (1f - alpha)) / a,
                (color.b * alpha + under.b * under.a * (1f - alpha)) / a,
                a);
        }
    }
}
