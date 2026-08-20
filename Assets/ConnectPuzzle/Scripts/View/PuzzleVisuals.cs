using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{
    /// <summary>Bảng màu + ký hiệu, lấy đúng giá trị từ bản prototype HTML.</summary>
    public static class PuzzlePalette
    {
        public static readonly Color[] Colors =
        {
            new Color32(0xEF, 0x44, 0x44, 0xFF),   // đỏ
            new Color32(0xF5, 0x9E, 0x0B, 0xFF),   // hổ phách
            new Color32(0x22, 0xC5, 0x5E, 0xFF),   // lục
            new Color32(0x38, 0xBD, 0xF8, 0xFF),   // thiên thanh
            new Color32(0xA7, 0x8B, 0xFA, 0xFF),   // tím
            new Color32(0xF4, 0x72, 0xB6, 0xFF)    // hồng
        };

        public static readonly string[] Glyphs = { "●", "▲", "■", "◆", "★", "✚" };

        public static readonly Color Background = new Color32(0x0F, 0x12, 0x20, 0xFF);  // --bg
        public static readonly Color BackgroundTop = new Color32(0x16, 0x1A, 0x2E, 0xFF); // --bg2
        public static readonly Color Panel      = new Color32(0x1C, 0x21, 0x40, 0xFF);
        public static readonly Color PanelLight = new Color32(0x24, 0x2A, 0x4F, 0xFF);
        public static readonly Color Line       = new Color32(0x2E, 0x35, 0x60, 0xFF);
        public static readonly Color Foreground = new Color32(0xEE, 0xF1, 0xFF, 0xFF);
        public static readonly Color Dim        = new Color32(0x9A, 0xA2, 0xC9, 0xFF);
        public static readonly Color Accent     = new Color32(0x7C, 0x8C, 0xFF, 0xFF);
        public static readonly Color AccentTop  = new Color32(0x8B, 0x9B, 0xFF, 0xFF);
        public static readonly Color Good       = new Color32(0x34, 0xD3, 0x99, 0xFF);
        public static readonly Color Bad        = new Color32(0xFB, 0x71, 0x85, 0xFF);
        public static readonly Color Star       = new Color32(0xFB, 0xBF, 0x24, 0xFF);

        /// <summary>Viền đỏ sẫm của banner chẩn đoán (#7f1d33).</summary>
        public static readonly Color DiagBorder = new Color32(0x7F, 0x1D, 0x33, 0xFF);
        /// <summary>Nền banner chẩn đoán (#1a0c14).</summary>
        public static readonly Color DiagPanel  = new Color32(0x1A, 0x0C, 0x14, 0xFA);

        // Bán kính bo góc, quy đổi từ HTML sang hệ 1080 rộng của canvas (~2.7x).
        //
        // RÀNG BUỘC: phần tử dùng bán kính R phải cao và rộng >= 2R. Sprite 9-slice có
        // border = R mỗi cạnh, nếu phần tử nhỏ hơn 2R thì hai border chồng lên nhau và
        // góc bo bị khuyết. Chip cao 64 nên phải dùng bán kính riêng nhỏ hơn.
        public const int RadiusSmall = 32;   // 12px HTML — nút chọn màn
        public const int RadiusPanel = 38;   // 14px HTML — panel, nút
        public const int RadiusChip  = 28;   // viên thuốc — chip xem trước điểm (cao 64)
        public const int RadiusCard  = 58;   // 22px HTML — thẻ overlay
    }

    /// <summary>
    /// Sprite dựng tại runtime, không cần asset nào — giữ prototype tự chứa.
    /// Bo góc dùng 9-slice nên co giãn không méo; sprite đặt pixelsPerUnit 100 khớp
    /// referencePixelsPerUnit của Canvas, nên 1 pixel texture = 1 đơn vị UI.
    /// </summary>
    public static class PuzzleSprites
    {
        private const float PixelsPerUnit = 100f;

        private static readonly Dictionary<int, Sprite> roundedFill = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> roundedOutline = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> roundedTopSheen = new Dictionary<int, Sprite>();

        private static Sprite circle, ring, square;
        private static Sprite bubbleSheen, softGlow, bloom, softLine, roundedSlot;
        private static Sprite backgroundGradient;
        private static Texture2D dashTexture;

        public static Sprite Circle => circle != null ? circle : (circle = MakeCircle(256, 0f));
        public static Sprite Ring   => ring   != null ? ring   : (ring   = MakeCircle(256, 0.085f));
        public static Sprite Square => square != null ? square : (square = MakeSolid());

        /// <summary>Ô nền của lưới — bo 28% như CSS, dùng type Simple vì ô luôn vuông.</summary>
        public static Sprite RoundedSlot =>
            roundedSlot != null ? roundedSlot : (roundedSlot = MakeRounded(96, 27, -1f, 0f, false));

        /// <summary>Chóa sáng + tối đáy của bubble. KHÔNG tint — màu đã nằm trong texture.</summary>
        public static Sprite BubbleSheen =>
            bubbleSheen != null ? bubbleSheen : (bubbleSheen = MakeBubbleSheen(256));

        /// <summary>Quầng mềm dùng cho BÓNG ĐỔ của bubble. Rơi nhanh nên bóng gọn.</summary>
        public static Sprite SoftGlow =>
            softGlow != null ? softGlow : (softGlow = MakeSoftGlow(128, 2f));

        /// <summary>
        /// Quầng cho hiệu ứng phát sáng. Rơi CHẬM hơn bóng đổ nhiều để có đuôi dài,
        /// chồng nhiều lớp lên nhau mới ra được cảm giác bloom thay vì một đĩa mờ.
        /// </summary>
        public static Sprite Bloom =>
            bloom != null ? bloom : (bloom = MakeSoftGlow(192, 1.7f));

        /// <summary>Vạch có biên mềm theo trục dọc — lớp phát sáng dưới đường nối.</summary>
        public static Sprite SoftLine =>
            softLine != null ? softLine : (softLine = MakeSoftLine(8, 64));

        public static Sprite BackgroundGradient =>
            backgroundGradient != null ? backgroundGradient : (backgroundGradient = MakeBackground(128));

        /// <summary>Texture nét đứt cho đường nối, lặp theo trục ngang.</summary>
        public static Texture2D DashTexture =>
            dashTexture != null ? dashTexture : (dashTexture = MakeDash(48, 4));

        // Texture rộng 2R+8 với border 9-slice = R: bốn góc chiếm [0,R] và [R+8, 2R+8],
        // chừa lại dải giữa 8px KHÔNG bo. Nếu đặt border = R+4 thì trái+phải bằng đúng
        // cả chiều rộng, dải giữa rộng 0 và góc bo bị khuyết khi co giãn.
        private const int SliceMargin = 8;

        /// <summary>Khối bo góc đặc, 9-slice.</summary>
        public static Sprite RoundedFill(int radius)
        {
            if (roundedFill.TryGetValue(radius, out Sprite s)) return s;
            s = MakeRounded(radius * 2 + SliceMargin, radius, -1f, radius, true);
            roundedFill[radius] = s;
            return s;
        }

        /// <summary>Viền bo góc dày ~1px HTML (2.7 đơn vị UI), 9-slice.</summary>
        public static Sprite RoundedOutline(int radius)
        {
            if (roundedOutline.TryGetValue(radius, out Sprite s)) return s;
            s = MakeRounded(radius * 2 + SliceMargin, radius, 2.7f, radius, true);
            roundedOutline[radius] = s;
            return s;
        }

        /// <summary>Khối bo góc với alpha giảm dần từ trên xuống — làm gradient nút primary.</summary>
        public static Sprite RoundedTopSheen(int radius)
        {
            if (roundedTopSheen.TryGetValue(radius, out Sprite s)) return s;
            s = MakeRounded(radius * 2 + SliceMargin, radius, -1f, radius, true, verticalFade: true);
            roundedTopSheen[radius] = s;
            return s;
        }

        // ------------------------------------------------------------------
        // Material SDF — hình được TÍNH cho từng pixel, không lấy mẫu texture
        // ------------------------------------------------------------------

        private static Shader sdfShader;
        private static readonly Dictionary<string, Material> sdfMaterials = new Dictionary<string, Material>();

        /// <summary>
        /// Material vẽ hình bo góc bằng SDF.
        /// corner 0.5 = hình tròn; ring &gt; 0 = vòng rỗng; sheen = tự vẽ chóa bubble.
        ///
        /// Cache theo bộ tham số: mỗi material là một draw call riêng nếu khác nhau, nên
        /// tạo mới cho từng ô là giết batching của UI.
        /// </summary>
        public static Material SdfMaterial(float corner, float ring, bool sheen)
        {
            string key = corner.ToString("F3") + "|" + ring.ToString("F3") + "|" + (sheen ? 1 : 0);
            if (sdfMaterials.TryGetValue(key, out Material cached) && cached != null) return cached;

            if (sdfShader == null) sdfShader = Resources.Load<Shader>("UiRoundedSdf");
            if (sdfShader == null) return null;              // thiếu shader thì quay về sprite

            var material = new Material(sdfShader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetFloat("_Corner", corner);
            material.SetFloat("_Ring", ring);
            material.SetFloat("_Sheen", sheen ? 1f : 0f);
            sdfMaterials[key] = material;
            return material;
        }

        private static Sprite wildDisc, stoneTile, stoneCrack, fuseBadge;
        private static Sprite iceThin, iceThick;

        /// <summary>
        /// Lớp băng PHỦ LÊN ô màu — không thay ô như đá, vì ô băng vẫn là ô có màu và
        /// người chơi cần thấy màu đó để tính trước nước đi sau khi băng tan.
        /// `thick` = còn 2 lớp: đục hơn và có nhiều vân hơn.
        /// </summary>
        public static Sprite IceOverlay(bool thick)
        {
            if (thick) return iceThick != null ? iceThick : (iceThick = MakeIce(256, true));
            return iceThin != null ? iceThin : (iceThin = MakeIce(256, false));
        }

        /// <summary>
        /// Ô đa sắc: quang phổ quay quanh tâm, lõi trắng — đúng hình conic-gradient của
        /// bản HTML. Vẽ hẳn phổ màu thay vì tô một màu để không ai nhầm nó với một màu
        /// cụ thể trên bàn.
        /// KHÔNG tint: màu nằm sẵn trong texture, nên Image phải để màu trắng.
        /// </summary>
        public static Sprite WildDisc =>
            wildDisc != null ? wildDisc : (wildDisc = MakeWildDisc(256));

        /// <summary>Đá: khối bo góc vuông vức, xám, có gờ sáng trên và tối dưới.</summary>
        public static Sprite StoneTile =>
            stoneTile != null ? stoneTile : (stoneTile = MakeStoneTile(256, false));

        /// <summary>Đá dày: cùng khối nhưng có vết nứt chéo, để phân biệt máu 2.</summary>
        public static Sprite StoneCracked =>
            stoneCrack != null ? stoneCrack : (stoneCrack = MakeStoneTile(256, true));

        private static Sprite chainRing;

        /// <summary>
        /// Vòng XÍCH cho cặp liên kết — vòng đứt quãng thành từng mắt, khác hẳn vòng
        /// LIỀN của ô đích.
        ///
        /// Phân biệt bằng HÌNH chứ không chỉ bằng màu: bàn có thể có 4 cặp, mỗi cặp một
        /// màu, mà ô đích cũng là một vòng — chỉ khác màu thì người mù màu không tách
        /// được ô đích với ô trói, và cả người thường cũng phải nhớ bảng màu.
        /// </summary>
        public static Sprite ChainRing =>
            chainRing != null ? chainRing : (chainRing = MakeChainRing(256, 5));

        /// <summary>Nền tròn đậm cho số đếm ngược của ngòi nổ.</summary>
        public static Sprite FuseBadge =>
            fuseBadge != null ? fuseBadge : (fuseBadge = MakeFuseBadge(96));

        // ------------------------------------------------------------------
        // Sinh texture
        // ------------------------------------------------------------------

        /// <summary>Đổi mã màu CSS (#rrggbb hoặc #rrggbbaa) thành Color.</summary>
        private static Color Hex(string hex)
        {
            hex = hex.TrimStart('#');
            float r = System.Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
            float g = System.Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
            float b = System.Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
            float a = hex.Length >= 8 ? System.Convert.ToInt32(hex.Substring(6, 2), 16) / 255f : 1f;
            return new Color(r, g, b, a);
        }

        /// <summary>Đặt màu `src` (có alpha) lên trên `dst` — đúng phép chồng của CSS.</summary>
        private static Color Over(Color src, Color dst)
        {
            float a = src.a + dst.a * (1f - src.a);
            if (a <= 0f) return new Color(0, 0, 0, 0);
            Color rgb = (src * src.a + dst * dst.a * (1f - src.a)) / a;
            rgb.a = a;
            return rgb;
        }

        /// <summary>Nội suy nhiều chặng màu giống gradient nhiều stop của CSS.</summary>
        private static Color GradientAt(float t, float[] stops, Color[] colors)
        {
            if (t <= stops[0]) return colors[0];
            for (int i = 1; i < stops.Length; i++)
            {
                if (t > stops[i]) continue;
                float k = Mathf.InverseLerp(stops[i - 1], stops[i], t);
                return Color.Lerp(colors[i - 1], colors[i], k);
            }
            return colors[colors.Length - 1];
        }

        /// <summary>
        /// Ô đa sắc — dịch từng phần của CSS:
        ///   background: conic-gradient(#ef4444,#f59e0b,#22c55e,#38bdf8,#a78bfa,#f472b6,#ef4444)
        ///   ::after    inset 22%; radial-gradient(circle at 34% 30%, #fff, #ffffffcc 45%, #ffffff55)
        ///
        /// Lõi trắng của HTML KHÔNG đục hoàn toàn — nó mờ dần ra tới #ffffff55, nên vẫn
        /// nhìn thấy phổ màu hắt qua. Bản trước tôi tô trắng đặc và bán kính 46% thay vì
        /// 28%, nên viên ngọc to và bệt hơn hẳn bản HTML.
        /// </summary>
        private static Sprite MakeWildDisc(int size)
        {
            Color[] wheel =
            {
                Hex("#ef4444"), Hex("#f59e0b"), Hex("#22c55e"),
                Hex("#38bdf8"), Hex("#a78bfa"), Hex("#f472b6"), Hex("#ef4444")
            };
            Color coreIn = Hex("#ffffff");
            Color coreMid = Hex("#ffffffcc");
            Color coreOut = Hex("#ffffff55");

            Texture2D texture = NewTexture(size, size, mipmap: true);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            float coreRadius = half * 0.56f;                 // inset 22% => bán kính 28% của ô
            // tâm gradient lõi ở 34%/30% tính từ góc trên-trái của hình vuông lõi
            float coreCx = -coreRadius * 0.32f;
            float coreCy = coreRadius * 0.40f;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = x - half + 0.5f;
                    float py = y - half + 0.5f;
                    float dist = Mathf.Sqrt(px * px + py * py);
                    float edge = Mathf.Clamp01(half - dist);
                    if (edge <= 0f) { pixels[y * size + x] = new Color32(0, 0, 0, 0); continue; }

                    // conic: góc quay theo chiều kim đồng hồ từ đỉnh, như CSS
                    float angle = Mathf.Atan2(px, py);                    // 0 ở đỉnh
                    float t = (angle < 0f ? angle + 2f * Mathf.PI : angle) / (2f * Mathf.PI);
                    float f = t * (wheel.Length - 1);
                    int i0 = Mathf.Clamp((int)f, 0, wheel.Length - 2);
                    Color color = Color.Lerp(wheel[i0], wheel[i0 + 1], f - i0);

                    // Lõi trắng: đĩa ĐẶT GIỮA (inset 22% của CSS là đều bốn phía), còn
                    // điểm sáng 34%/30% chỉ lệch bên trong đĩa đó. Trước đây tôi lấy
                    // luôn tâm lệch làm tâm đĩa nên viên ngọc méo hẳn sang trên-trái.
                    float coreEdge = Mathf.Clamp01(coreRadius - dist + 0.5f);
                    if (coreEdge > 0f)
                    {
                        float cd = Mathf.Sqrt((px - coreCx) * (px - coreCx) + (py - coreCy) * (py - coreCy));
                        Color core = GradientAt(Mathf.Clamp01(cd / (coreRadius * 1.7f)),
                                                new[] { 0f, 0.45f, 1f },
                                                new[] { coreIn, coreMid, coreOut });
                        core.a *= coreEdge;
                        color = Over(core, color);
                    }

                    color.a *= edge;
                    pixels[y * size + x] = color;
                }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);;
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
        }

        /// <summary>
        /// Đá — dịch từng phần của CSS:
        ///   border-radius: 22%
        ///   background: linear-gradient(155deg, #8b93ad, #59607a 55%, #454b60)
        ///   box-shadow: inset 0 2px 0 #ffffff35, inset 0 -3px 6px -2px #0009
        ///   hp2 ::after: inset 32% 18%, #ffffff30, xoay 24 độ  (VẠCH SÁNG, không phải vết nứt)
        ///
        /// 155deg trong CSS đo từ hướng "lên trên" và quay theo chiều kim đồng hồ, nên
        /// trục gradient chạy từ trên-trái xuống dưới-phải — bản trước tôi đổ dọc nên
        /// mất hẳn hướng nghiêng.
        /// </summary>
        private static Sprite MakeStoneTile(int size, bool cracked)
        {
            Color c0 = Hex("#8b93ad"), c1 = Hex("#59607a"), c2 = Hex("#454b60");
            Color topEdge = Hex("#ffffff35");
            Color bottomShade = Hex("#000000cc");
            Color bar = Hex("#ffffff30");

            Texture2D texture = NewTexture(size, size, mipmap: true);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            float radius = size * 0.22f;

            // trục của linear-gradient(155deg)
            float rad = 155f * Mathf.Deg2Rad;
            float ax = Mathf.Sin(rad), ay = Mathf.Cos(rad);
            float span = Mathf.Abs(size * ax) + Mathf.Abs(size * ay);

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = x - half + 0.5f;
                    float py = y - half + 0.5f;
                    float d = RoundedRectDistance(px, py, half, half, radius);
                    float alpha = Mathf.Clamp01(0.5f - d);
                    if (alpha <= 0f) { pixels[y * size + x] = new Color32(0, 0, 0, 0); continue; }

                    // Trục 155deg của CSS đo từ hướng LÊN và quay theo chiều kim đồng hồ,
                    // trong khi y của texture tăng LÊN TRÊN — nên phải cộng py*ay, không
                    // phải trừ. Sai dấu thì gradient chạy ngược từ dưới-trái lên.
                    float t = Mathf.Clamp01((px * ax + py * ay) / span + 0.5f);
                    Color color = GradientAt(t, new[] { 0f, 0.55f, 1f }, new[] { c0, c1, c2 });

                    // Bóng inset: đo thẳng khoảng cách tới MÉP TRÊN / MÉP DƯỚI.
                    //
                    // Không lấy hiệu hai SDF hình bo góc: số hạng min(max(qx,qy),0) của
                    // SDF đổi nhánh theo đường chéo, nên hiệu của nó vẽ ra một vệt chữ X
                    // to đùng giữa viên đá — thấy ngay trên ảnh xuất ra.
                    float fromTop = half - py;
                    float topBand = Mathf.Clamp01(1f - fromTop / (size * 0.05f));
                    if (topBand > 0f)
                        color = Over(new Color(topEdge.r, topEdge.g, topEdge.b, topEdge.a * topBand), color);

                    float fromBottom = py + half;
                    float bottomBand = Mathf.Clamp01(1f - fromBottom / (size * 0.16f));
                    if (bottomBand > 0f)
                        color = Over(new Color(bottomShade.r, bottomShade.g, bottomShade.b,
                                               bottomShade.a * bottomBand * bottomBand * 0.65f), color);

                    if (cracked)
                    {
                        // vạch sáng nghiêng 24 độ, inset 32% 18%
                        float r2 = -24f * Mathf.Deg2Rad;
                        float lx = px * Mathf.Cos(r2) - py * Mathf.Sin(r2);
                        float ly = px * Mathf.Sin(r2) + py * Mathf.Cos(r2);
                        float hw = size * 0.32f, hh = size * 0.18f;
                        if (Mathf.Abs(lx) < hw && Mathf.Abs(ly) < hh)
                        {
                            float fade = Mathf.Clamp01((hh - Mathf.Abs(ly)) / (size * 0.03f)) *
                                         Mathf.Clamp01((hw - Mathf.Abs(lx)) / (size * 0.03f));
                            color = Over(new Color(bar.r, bar.g, bar.b, bar.a * fade), color);
                        }
                    }

                    color.a = alpha;
                    pixels[y * size + x] = color;
                }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);;
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
        }

        /// <summary>
        /// Badge ngòi nổ — CSS: nền #111827, box-shadow 0 0 0 2px #ffffff55.
        /// Đó là một VÒNG rõ nét dày 2px, không phải chuyển màu mềm ra mép như bản trước.
        /// Vẽ trắng đục ở đây; lúc chạy tint sang đỏ khi sắp hết giờ.
        /// </summary>

        /// <summary>
        /// Lớp băng phủ lên ô.
        ///
        /// Bản trước chỉ là một lớp sương PHẲNG cộng hai vạch chéo: nó làm nhạt màu ô
        /// (đỏ thành hồng) mà không ra chất băng, còn hai vạch thì trông như dấu gạch
        /// cấm. Bản này dựng bằng ba thứ làm nên cảm giác băng thật:
        ///
        ///   1. MẶT CẮT TINH THỂ — chia theo góc thành các múi, mỗi múi một độ sáng
        ///      hơi khác, nên bề mặt gãy khúc như khối pha lê thay vì phẳng lì.
        ///   2. VIỀN SƯƠNG MUỐI — đục và sáng ở sát mép, loãng dần vào giữa.
        ///   3. TIA LOÉ — vài vạch sáng NGẮN nằm ở vành ngoài, không xuyên qua tâm.
        ///
        /// Ràng buộc bắt buộc giữ: GIỮA Ô PHẢI CÒN TRONG. Người chơi cần đọc được màu
        /// bên dưới để tính trước nước đi sau khi băng tan; băng đục kín là lấy mất
        /// thông tin đó và biến ô băng thành ô mù.
        /// </summary>
        private static Sprite MakeIce(int size, bool thick)
        {
            Color frost = Hex("#dff2ff");             // sương muối ở mép
            Color body  = Hex("#a9dcff");             // thân băng, xanh hơn
            int facets  = thick ? 9 : 7;

            float coreAlpha = thick ? 0.30f : 0.14f;  // giữa: rất trong, để lộ màu ô
            float rimAlpha  = thick ? 0.94f : 0.72f;  // mép: đục hẳn

            Texture2D texture = NewTexture(size, size, mipmap: true);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            float radius = half - 2f;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = x - half + 0.5f;
                    float py = y - half + 0.5f;
                    float dist = Mathf.Sqrt(px * px + py * py);
                    float edge = Mathf.Clamp01((radius - dist) / 1.5f + 0.5f);
                    if (edge <= 0f) { pixels[y * size + x] = new Color32(0, 0, 0, 0); continue; }

                    float t = Mathf.Clamp01(dist / radius);          // 0 tâm, 1 mép
                    float ang = Mathf.Atan2(py, px);

                    // (2) SƯƠNG MUỐI BÒ VÀO TỪ MÉP, ranh giới KHÔNG ĐỀU.
                    //     Ranh giới phẳng (chỉ theo bán kính) cho ra một vành tròn đều
                    //     như cái nhẫn; băng thật thì mọc vào trong lồi lõm. Ranh giới
                    //     gợn sóng theo góc là thứ tạo ra cảm giác "đóng băng" đó.
                    float frontBase = thick ? 0.30f : 0.46f;
                    float front = frontBase
                                + Mathf.Sin(ang * 3.1f + 1.1f) * 0.13f
                                + Mathf.Sin(ang * 6.7f - 0.4f) * 0.06f;
                    float creep = Mathf.Clamp01((t - front) / (1f - front + 0.001f));
                    float alpha = Mathf.Lerp(coreAlpha, rimAlpha, creep * creep);

                    // (1) mặt cắt tinh thể: mỗi múi lệch sáng một chút, và múi nào cũng
                    //     tối dần về phía tâm nên các mặt gãy tụ lại thành hình sao.
                    float angle = Mathf.Atan2(py, px) + Mathf.PI;    // 0..2pi

                    // Bẻ nhẹ góc trước khi chia múi: chia đều tăm tắp thì ra hình chong
                    // chóng đều đặn, nhìn máy móc chứ không phải khối băng vỡ tự nhiên.
                    float warped = angle + Mathf.Sin(angle * 3f + 0.7f) * 0.12f;
                    float facet = warped / (2f * Mathf.PI) * facets;
                    int facetIndex = Mathf.FloorToInt(facet);
                    float within = facet - facetIndex;               // 0..1 trong múi

                    // Biến thiên tất định theo chỉ số múi, nhưng TẮT DẦN VỀ TÂM: để
                    // nguyên tới tâm thì các nêm sáng-tối chụm lại thành cái rốn chong
                    // chóng — đúng thứ nhìn thấy ở bản trước.
                    // Mặt cắt chỉ hiện Ở PHẦN ĐÃ ĐÓNG BĂNG. Cho nó chạy khắp đĩa thì
                    // các nan hoa xuyên vào giữa và cả ô đọc thành tia mặt trời.
                    float wobble = Mathf.Sin(facetIndex * 2.399f) * 0.5f + 0.5f;
                    float facetDepth = creep;
                    float shade = 1f + (wobble - 0.5f) * 0.34f * facetDepth;

                    // cạnh giữa hai múi sáng lên: đó là đường gãy của tinh thể
                    float toSeam = Mathf.Min(within, 1f - within);
                    float seam = Mathf.Clamp01(1f - toSeam / 0.06f) * facetDepth * facetDepth;

                    Color color = Color.Lerp(body, frost, t * 0.75f + seam * 0.4f);
                    color *= shade;
                    alpha = Mathf.Min(1f, alpha + seam * (thick ? 0.42f : 0.30f));

                    // (3) tia loé: hai vạch NGẮN ở vành ngoài, nghiêng khác nhau. Ngắn
                    //     là điểm mấu chốt — vạch dài xuyên tâm đọc thành dấu gạch cấm.
                    alpha = Mathf.Min(1f, alpha + Glint(px, py, radius, -0.55f, 0.62f, 0.30f, size));
                    if (thick)
                        alpha = Mathf.Min(1f, alpha + Glint(px, py, radius, 2.1f, 0.55f, 0.22f, size));

                    color.a = alpha * edge;
                    pixels[y * size + x] = color;
                }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
        }

        /// <summary>
        /// Một tia loé: đoạn thẳng NGẮN đặt lệch tâm, mờ dần về hai đầu.
        /// `angle` hướng tia, `offset` khoảng cách từ tâm, `length` độ dài (tỉ lệ bán kính).
        /// </summary>
        private static float Glint(float px, float py, float radius, float angle,
                                   float offset, float length, int size)
        {
            float cx = Mathf.Cos(angle) * radius * offset;
            float cy = Mathf.Sin(angle) * radius * offset;
            float dx = px - cx, dy = py - cy;

            // xoay về hệ của tia: u dọc tia, v ngang tia
            float ca = Mathf.Cos(-angle + Mathf.PI * 0.5f), sa = Mathf.Sin(-angle + Mathf.PI * 0.5f);
            float u = dx * ca - dy * sa;
            float v = dx * sa + dy * ca;

            float halfLength = radius * length;
            float halfWidth = size * 0.018f;
            if (Mathf.Abs(u) > halfLength || Mathf.Abs(v) > halfWidth) return 0f;

            float along = 1f - Mathf.Abs(u) / halfLength;        // mờ dần về hai đầu
            float across = 1f - Mathf.Abs(v) / halfWidth;
            return along * along * across * 0.55f;
        }

        /// <summary>
        /// Vòng gồm `segments` mắt xích rời nhau.
        ///
        /// Mỗi mắt là một cung PHÌNH GIỮA, THÓT HAI ĐẦU — đó là thứ làm nó đọc ra "mắt
        /// xích" chứ không phải "nét đứt". Vẽ trắng, lúc chạy tint sang màu của cặp.
        /// </summary>
        private static Sprite MakeChainRing(int size, int segments)
        {
            Texture2D texture = NewTexture(size, size, mipmap: true);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;

            float ringRadius = half * 0.80f;        // bán kính tâm vòng
            float maxThick = half * 0.150f;         // nửa bề dày ở giữa mắt
            float minThick = half * 0.070f;         // nửa bề dày ở hai đầu mắt
            float fill = 0.74f;                     // phần cung là mắt, còn lại là khe

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = x - half + 0.5f;
                    float py = y - half + 0.5f;
                    float dist = Mathf.Sqrt(px * px + py * py);

                    float angle = Mathf.Atan2(py, px) + Mathf.PI;      // 0..2pi
                    float seg = angle / (2f * Mathf.PI) * segments;
                    float within = seg - Mathf.Floor(seg);             // 0..1 trong một mắt

                    if (within > fill) { pixels[y * size + x] = new Color32(0, 0, 0, 0); continue; }

                    // hình thoi thuôn: dày nhất ở giữa mắt, mảnh dần ra hai đầu
                    float alongLink = within / fill;                   // 0..1 dọc mắt
                    float taper = Mathf.Sin(alongLink * Mathf.PI);     // 0 ở hai đầu, 1 ở giữa
                    float thickness = Mathf.Lerp(minThick, maxThick, taper);

                    float band = thickness - Mathf.Abs(dist - ringRadius);
                    float alpha = Mathf.Clamp01(band / 1.5f + 0.5f);
                    if (alpha <= 0f) { pixels[y * size + x] = new Color32(0, 0, 0, 0); continue; }

                    // lõi sáng hơn viền để mắt xích trông có khối
                    float core = Mathf.Clamp01(1f - Mathf.Abs(dist - ringRadius) / thickness);
                    Color color = Color.Lerp(new Color(0.78f, 0.78f, 0.82f), Color.white, core * core);
                    color.a = alpha;
                    pixels[y * size + x] = color;
                }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
        }

        private static Sprite MakeFuseBadge(int size)
        {
            Color body = Hex("#111827");
            Color rimColor = Hex("#ffffff55");

            Texture2D texture = NewTexture(size, size, mipmap: true);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            float rim = size * 0.055f;                 // ~2px trên badge ~36px

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = x - half + 0.5f;
                    float py = y - half + 0.5f;
                    float dist = Mathf.Sqrt(px * px + py * py);
                    float outer = Mathf.Clamp01(half - dist);
                    if (outer <= 0f) { pixels[y * size + x] = new Color32(0, 0, 0, 0); continue; }

                    float inner = Mathf.Clamp01((half - rim) - dist + 0.5f);
                    Color color = Color.Lerp(rimColor, body, inner);
                    color.a = Mathf.Lerp(rimColor.a, 1f, inner) * outer;
                    pixels[y * size + x] = color;
                }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);;
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
        }

        /// <summary>
        /// `mipmap` BẮT BUỘC bật cho sprite bị VẼ NHỎ HƠN texture.
        ///
        /// Bubble vẽ ở cỡ ~72px trên bàn 7 cột trong khi texture là 160px, tức đang thu
        /// nhỏ. Không có mipmap thì bộ lấy mẫu chỉ đọc một điểm cho mỗi pixel màn hình,
        /// và dải chống răng cưa dày đúng 1 texel ở mép hình tròn bị bỏ sót lúc thì
        /// trúng lúc thì trượt — nhìn ra thành viền lởm chởm, vỡ nham nhở.
        ///
        /// Không dùng cho sprite 9-slice: mipmap làm các dải biên lem vào nhau.
        /// </summary>
        private static Texture2D NewTexture(int width, int height, bool mipmap = false)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, mipmap)
            {
                // Trilinear khi CÓ mipmap. Bilinear chỉ nội suy trong MỘT mức rồi nhảy
                // cứng sang mức kế — chỗ nhảy đó nằm ngay trên mép cong của ô và hiện ra
                // thành viền gãy khúc. Trilinear trộn giữa hai mức nên mép liền mạch.
                filterMode = mipmap ? FilterMode.Trilinear : FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = mipmap ? 4 : 0
            };
        }

        /// <summary>Khoảng cách có dấu tới biên hình chữ nhật bo góc (SDF).</summary>
        private static float RoundedRectDistance(float px, float py, float halfW, float halfH, float radius)
        {
            float qx = Mathf.Abs(px) - (halfW - radius);
            float qy = Mathf.Abs(py) - (halfH - radius);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        /// <summary>
        /// outlineWidth &lt; 0 => tô đặc; &gt; 0 => chỉ vẽ viền dày bằng đó.
        /// sliceBorder &gt; 0 => tạo sprite 9-slice.
        /// </summary>
        private static Sprite MakeRounded(int size, float radius, float outlineWidth, float sliceBorder,
                                          bool sliced, bool verticalFade = false)
        {
            Texture2D texture = NewTexture(size, size);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = x - half + 0.5f;
                    float py = y - half + 0.5f;
                    float d = RoundedRectDistance(px, py, half, half, radius);

                    float alpha = Mathf.Clamp01(0.5f - d);
                    if (outlineWidth > 0f)
                    {
                        float inner = Mathf.Clamp01(0.5f - (d + outlineWidth));
                        alpha = Mathf.Clamp01(alpha - inner);
                    }
                    if (verticalFade)
                        alpha *= Mathf.Lerp(0f, 0.4f, y / (float)(size - 1));   // sáng dần lên trên

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }

            texture.SetPixels32(pixels);
            texture.Apply();

            var rect = new Rect(0, 0, size, size);
            var pivot = new Vector2(0.5f, 0.5f);
            if (!sliced) return Sprite.Create(texture, rect, pivot, PixelsPerUnit);

            var border = new Vector4(sliceBorder, sliceBorder, sliceBorder, sliceBorder);
            return Sprite.Create(texture, rect, pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect, border);
        }

        /// <summary>Ô trắng đặc — dùng cho vùng nhận raycast và các lớp phủ vuông.</summary>
        private static Sprite MakeSolid()
        {
            Texture2D texture = NewTexture(4, 4);
            var pixels = new Color32[16];
            for (int i = 0; i < 16; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        private static Sprite MakeCircle(int size, float ringThickness)
        {
            Texture2D texture = NewTexture(size, size, mipmap: true);
            var pixels = new Color32[size * size];

            // Chừa 2 texel trống quanh mép: mipmap và lọc bilinear đều cần chỗ để trộn.
            // Vẽ sát biên texture thì mức mip nhỏ nhất kéo màu từ mép đối diện sang.
            float radius = size * 0.5f - 2f;
            float inner = radius * (1f - ringThickness * 2f);

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - size * 0.5f + 0.5f;
                    float dy = y - size * 0.5f + 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    // dải chuyển dày 1.5 texel thay vì 1: rộng hơn thì lúc thu nhỏ vẫn
                    // còn đủ mẫu để mép mượt, mà phóng to cũng chưa thấy nhoè
                    float alpha = Mathf.Clamp01((radius - d) / 1.5f + 0.5f);
                    if (ringThickness > 0f)
                        alpha = Mathf.Min(alpha, Mathf.Clamp01((d - inner) / 1.5f + 0.5f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }

            texture.SetPixels32(pixels);
            texture.Apply(true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        /// <summary>
        /// Lớp phủ bubble: chóa trắng ở 32%/28% và dải tối ở đáy trong, hệt hai lớp
        /// của CSS. Màu nằm trong RGB của texture nên phải vẽ với tint trắng.
        /// </summary>
        private static Sprite MakeBubbleSheen(int size)
        {
            Texture2D texture = NewTexture(size, size, mipmap: true);
            var pixels = new Color32[size * size];
            float radius = size * 0.5f - 1f;
            float centre = size * 0.5f;

            // Chóa RỘNG và MỀM, lệch nhẹ lên trên-trái. Chóa nhỏ mà gắt làm quả bóng
            // trông như thuỷ tinh bóng loáng; ở đây muốn cảm giác nhựa mờ, ánh sáng
            // toả từ phía trên.
            float sheenX = size * 0.40f;
            float sheenY = size * 0.74f;                 // texture có y hướng lên
            float sheenRadius = size * 0.72f;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - centre + 0.5f;
                    float dy = y - centre + 0.5f;
                    float inside = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy));
                    if (inside <= 0f) { pixels[y * size + x] = new Color32(0, 0, 0, 0); continue; }

                    float sheenDistance = Mathf.Sqrt((x - sheenX) * (x - sheenX) + (y - sheenY) * (y - sheenY));
                    float sheen = Mathf.Clamp01(1f - sheenDistance / sheenRadius);
                    sheen = sheen * sheen * sheen * 0.34f;               // luỹ thừa 3 -> chuyển mượt

                    // Tối dần ở nửa dưới, đậm nhất sát rìa — cho khối tròn mà không tạo
                    // vệt đen rõ ràng.
                    float depthFromBottom = (dy + radius) / (radius * 0.85f);
                    float shade = Mathf.Clamp01(1f - depthFromBottom);
                    shade = shade * shade * 0.26f;

                    float alpha = sheen + shade;
                    byte rgb = alpha > 0.0001f ? (byte)(255f * (sheen / alpha)) : (byte)0;
                    pixels[y * size + x] = new Color32(rgb, rgb, rgb, (byte)(Mathf.Clamp01(alpha) * inside * 255f));
                }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        /// <summary>falloff nhỏ = đuôi dài và mềm; lớn = gọn và tắt nhanh.</summary>
        private static Sprite MakeSoftGlow(int size, float falloff)
        {
            Texture2D texture = NewTexture(size, size);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half + 0.5f) / half;
                    float dy = (y - half + 0.5f) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - d), falloff);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        private static Sprite MakeSoftLine(int width, int height)
        {
            Texture2D texture = NewTexture(width, height);
            var pixels = new Color32[width * height];
            float half = height * 0.5f;

            for (int y = 0; y < height; y++)
            {
                float t = Mathf.Abs(y - half + 0.5f) / half;
                float alpha = Mathf.Clamp01(1f - t);
                alpha = alpha * alpha;
                var value = new Color32(255, 255, 255, (byte)(alpha * 255f));
                for (int x = 0; x < width; x++) pixels[y * width + x] = value;
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        /// <summary>
        /// Màu nền tại một điểm, theo toạ độ chuẩn hoá của màn (0,0 = góc dưới-trái).
        ///
        /// Tách khỏi MakeBackground để chỗ khác lấy được ĐÚNG màu nền tại một điểm mà
        /// không phải đọc pixel: texture nền sau khi bake ra PNG thì không đọc được
        /// (isReadable tắt), và nội suy từ ảnh 128px còn kém chính xác hơn tính thẳng.
        /// </summary>
        public static Color BackgroundColorAt(float u, float v)
        {
            // tâm ở 50% ngang, -10% dọc (trên đỉnh); ellipse rộng hơn cao
            float dx = (u - 0.5f) / 0.62f;
            float dy = (v - 1.1f) / 0.55f;
            float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
            return Color.Lerp(PuzzlePalette.BackgroundTop, PuzzlePalette.Background, d);
        }

        /// <summary>Nền: radial-gradient(... at 50% -10%, --bg2, --bg).</summary>
        private static Sprite MakeBackground(int size)
        {
            Texture2D texture = NewTexture(size, size);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = BackgroundColorAt(x / (float)(size - 1),
                                                             y / (float)(size - 1));

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        /// <summary>Nét đứt tỉ lệ 0.44/0.30 như stroke-dasharray của CSS.</summary>
        private static Texture2D MakeDash(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat
            };
            int solid = Mathf.RoundToInt(width * 0.44f / (0.44f + 0.30f));
            var pixels = new Color32[width * height];
            for (int x = 0; x < width; x++)
            {
                // làm mềm hai đầu nét cho giống stroke-linecap:round
                float edge = Mathf.Min(x, solid - 1 - x);
                byte alpha = x < solid ? (byte)(Mathf.Clamp01(edge / 1.5f + 0.35f) * 255f) : (byte)0;
                for (int y = 0; y < height; y++) pixels[y * width + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        // ------------------------------------------------------------------
        // Icon vật phẩm
        //
        // Vẽ bằng SDF thay vì dùng ký tự font: ⚒ và ◈ ở cỡ 34px ra hai chấm không
        // đọc được là gì, và một nửa số font hệ thống còn không có chúng.
        // ------------------------------------------------------------------

        private static Sprite hammerIcon, paintIcon, plusMoveIcon;

        public static Sprite HammerIcon =>
            hammerIcon != null ? hammerIcon : (hammerIcon = MakeHammer(160));
        public static Sprite PaintIcon =>
            paintIcon != null ? paintIcon : (paintIcon = MakePaint(160));
        public static Sprite PlusMoveIcon =>
            plusMoveIcon != null ? plusMoveIcon : (plusMoveIcon = MakePlusMove(160));

        /// <summary>
        /// Khoảng cách tới một tam giác LỒI: lấy max của ba khoảng cách tới ba nửa mặt
        /// phẳng. Đúng ở trong và sát cạnh, hơi phóng đại ở xa ngoài — không sao, chỗ
        /// duy nhất cần chính xác là dải khử răng cưa rộng 0.02.
        /// </summary>
        private static float TriangleDistance(float px, float py,
            float ax, float ay, float bx, float by, float cx, float cy)
        {
            float sign = Mathf.Sign((bx - ax) * (cy - ay) - (by - ay) * (cx - ax));
            float e1 = EdgeDistance(px, py, ax, ay, bx, by, sign);
            float e2 = EdgeDistance(px, py, bx, by, cx, cy, sign);
            float e3 = EdgeDistance(px, py, cx, cy, ax, ay, sign);
            return Mathf.Max(e1, Mathf.Max(e2, e3));
        }

        private static float EdgeDistance(float px, float py,
            float ax, float ay, float bx, float by, float sign)
        {
            float ex = bx - ax, ey = by - ay;
            float len = Mathf.Sqrt(ex * ex + ey * ey);
            if (len <= 0f) return 1f;
            // pháp tuyến hướng ra ngoài, chuẩn hoá theo chiều quay của tam giác
            return -sign * ((px - ax) * ey - (py - ay) * ex) / len;
        }

        /// <summary>Khoảng cách tới đoạn thẳng — dùng vẽ cán búa và các nét thẳng.</summary>
        private static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay;
            float wx = px - ax, wy = py - ay;
            float len2 = vx * vx + vy * vy;
            float t = len2 <= 0f ? 0f : Mathf.Clamp01((wx * vx + wy * vy) / len2);
            float dx = wx - vx * t, dy = wy - vy * t;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Búa: cán chéo + đầu búa hình chữ nhật bo góc. Vẽ nghiêng 30° vì búa dựng
        /// thẳng đứng trông giống cái cờ-lê, còn nghiêng thì đọc ra ngay là "đập".
        /// </summary>
        private static Sprite MakeHammer(int size)
        {
            Texture2D texture = NewTexture(size, size, mipmap: true);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;

            Color handle = Hex("#B45309");        // gỗ
            Color head = Hex("#CBD5E1");          // thép
            Color headDark = Hex("#7C8AA0");

            const float angle = 30f * Mathf.Deg2Rad;
            float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = (x - half + 0.5f) / half;      // -1..1
                    float py = (y - half + 0.5f) / half;

                    // xoay về hệ của búa: trục dọc là cán
                    float rx = px * ca + py * sa;
                    float ry = -px * sa + py * ca;

                    Color c = new Color(0, 0, 0, 0);

                    // cán: từ đáy lên gần đầu búa
                    float dHandle = SegmentDistance(rx, ry, 0f, -0.72f, 0f, 0.30f) - 0.085f;
                    if (dHandle < 0.02f)
                        c = Over(new Color(handle.r, handle.g, handle.b,
                                           Mathf.Clamp01(-dHandle / 0.02f)), c);

                    // đầu búa: khối ngang ở trên
                    float dHead = RoundedRectDistance(rx, ry - 0.44f, 0.52f, 0.24f, 0.10f);
                    if (dHead < 0.02f)
                    {
                        // mặt trên sáng, mặt dưới tối — cho ra khối, không phải mảng phẳng
                        float shade = Mathf.InverseLerp(0.20f, 0.68f, ry);
                        Color steel = Color.Lerp(headDark, head, shade);
                        c = Over(new Color(steel.r, steel.g, steel.b,
                                           Mathf.Clamp01(-dHead / 0.02f)), c);
                    }

                    pixels[y * size + x] = c;
                }

            texture.SetPixels32(pixels);
            texture.Apply(true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// Sơn: giọt màu đang rơi, tô bằng chính dải cầu vồng của ô đa sắc — nhìn là
        /// biết nó biến ô thành ô đa sắc, không cần đọc nhãn.
        /// </summary>
        private static Sprite MakePaint(int size)
        {
            Texture2D texture = NewTexture(size, size, mipmap: true);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;

            var stops = new[] { 0f, 0.25f, 0.5f, 0.75f, 1f };
            var colors = new[]
            {
                Hex("#F472B6"), Hex("#FBBF24"), Hex("#34D399"), Hex("#60A5FA"), Hex("#F472B6")
            };

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = (x - half + 0.5f) / half;
                    float py = (y - half + 0.5f) / half;

                    // Giọt = bao lồi của một hình tròn và một đỉnh nhọn. Phần thẳng phải
                    // là TIẾP TUYẾN của hình tròn, không phải một đoạn thẳng tuỳ ý nối
                    // vào: nối tuỳ ý thì tại chỗ giáp bề rộng nhảy bậc (0.456 của đường
                    // tròn so với 0.51 của đoạn thẳng) và hiện ra thành hai cái gờ vai.
                    const float cy = -0.30f;              // tâm phần tròn
                    const float radius = 0.52f;
                    const float apexY = 0.86f;            // đỉnh nhọn

                    float dist = apexY - cy;
                    float sinA = radius / dist;
                    float cosA = Mathf.Sqrt(Mathf.Max(0f, 1f - sinA * sinA));
                    float touchY = cy + radius * sinA;    // chỗ tiếp tuyến chạm đường tròn

                    float d;
                    if (py <= touchY)
                    {
                        d = Mathf.Sqrt(px * px + (py - cy) * (py - cy)) - radius;
                    }
                    else
                    {
                        // bề rộng thu về 0 ĐÚNG tại đỉnh, nên không còn cái gai 1px cũ
                        float halfWidth = (apexY - py) * (sinA / cosA);
                        d = Mathf.Abs(px) - halfWidth;
                    }

                    if (d > 0.02f) { pixels[y * size + x] = new Color32(0, 0, 0, 0); continue; }

                    // cầu vồng quét theo góc, giống ô đa sắc
                    float ang = (Mathf.Atan2(py + 0.2f, px) + Mathf.PI) / (2f * Mathf.PI);
                    Color rainbow = GradientAt(ang, stops, colors);

                    // chóa sáng lệch trái trên cho ra khối cầu
                    float gloss = Mathf.Clamp01(1f - new Vector2(px + 0.20f, py + 0.10f).magnitude / 0.42f);
                    rainbow = Color.Lerp(rainbow, Color.white, gloss * 0.55f);

                    rainbow.a = Mathf.Clamp01(-d / 0.02f);
                    pixels[y * size + x] = rainbow;
                }

            texture.SetPixels32(pixels);
            texture.Apply(true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// +1 lượt: mũi tên vòng lại (như nút chơi lại) kèm dấu cộng — "thêm một lần đi".
        /// </summary>
        private static Sprite MakePlusMove(int size)
        {
            Texture2D texture = NewTexture(size, size, mipmap: true);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;

            Color arc = Hex("#60A5FA");
            Color plus = Hex("#34D399");

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = (x - half + 0.5f) / half;
                    float py = (y - half + 0.5f) / half;
                    Color c = new Color(0, 0, 0, 0);

                    // Cung tròn hở ở góc phải-trên, hai đầu BO TRÒN. Cắt phẳng theo tia
                    // (cách cũ) để lại hai mặt cắt vuông góc, nhìn ra như lỗi render chứ
                    // không ra như một nét vẽ.
                    const float ringR = 0.62f;
                    const float thick = 0.115f;
                    const float gapFrom = 0.15f, gapTo = 1.25f;

                    float dist = Mathf.Sqrt(px * px + py * py);
                    float ang = Mathf.Atan2(py, px);                    // -pi..pi
                    float dArc;
                    if (ang > gapFrom && ang < gapTo)
                    {
                        // trong khe: khoảng cách tới đầu gần nhất -> thành mũ tròn
                        float near = (ang - gapFrom) < (gapTo - ang) ? gapFrom : gapTo;
                        float ex = Mathf.Cos(near) * ringR, ey = Mathf.Sin(near) * ringR;
                        dArc = Mathf.Sqrt((px - ex) * (px - ex) + (py - ey) * (py - ey)) - thick;
                    }
                    else
                    {
                        dArc = Mathf.Abs(dist - ringR) - thick;
                    }
                    if (dArc < 0.02f)
                        c = Over(new Color(arc.r, arc.g, arc.b, Mathf.Clamp01(-dArc / 0.02f)), c);

                    // Mũi tên ở đầu dưới của cung. Không có nó thì hình đọc thành "làm
                    // mới", đúng nghĩa nút Chơi lại ngay bên cạnh — hai nút khác chức
                    // năng mà cùng một hình là lỗi nặng hơn xấu.
                    {
                        float ax = Mathf.Cos(gapFrom) * ringR, ay = Mathf.Sin(gapFrom) * ringR;
                        float tx = -Mathf.Sin(gapFrom), ty = Mathf.Cos(gapFrom);   // tiếp tuyến
                        float nx = Mathf.Cos(gapFrom), ny = Mathf.Sin(gapFrom);    // pháp tuyến
                        const float len = 0.26f, wide = 0.22f;
                        float dTri = TriangleDistance(px, py,
                            ax + tx * len, ay + ty * len,                          // đỉnh nhọn
                            ax - nx * wide, ay - ny * wide,
                            ax + nx * wide, ay + ny * wide);
                        if (dTri < 0.02f)
                            c = Over(new Color(arc.r, arc.g, arc.b, Mathf.Clamp01(-dTri / 0.02f)), c);
                    }

                    // Dấu cộng đẩy ra xa hơn: ở 0.52 nó đè lên đỉnh mũi tên (0.57, 0.39),
                    // hai hình cách nhau 0.13 nên dính thành một cục không đọc ra gì.
                    float cx = px - 0.64f, cy = py - 0.62f;
                    float dPlus = Mathf.Min(
                        RoundedRectDistance(cx, cy, 0.26f, 0.088f, 0.045f),
                        RoundedRectDistance(cx, cy, 0.088f, 0.26f, 0.045f));
                    if (dPlus < 0.02f)
                        c = Over(new Color(plus.r, plus.g, plus.b, Mathf.Clamp01(-dPlus / 0.02f)), c);

                    pixels[y * size + x] = c;
                }

            texture.SetPixels32(pixels);
            texture.Apply(true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    /// <summary>Helper dựng UI bằng code.</summary>
    public static class Ui
    {
        private static Font builtinFont;
        private static Font overrideFont;

        /// <summary>
        /// Font tự chọn. Gán TRƯỚC khi dựng UI vì Text lấy font ngay lúc tạo.
        ///
        /// Font mặc định của Unity là Liberation Sans — chữ vuông, khác hẳn kiểu bo
        /// tròn trong ảnh mẫu. Muốn giống thì phải có file font thật trong project;
        /// không có cách nào sinh ra font bằng code.
        /// </summary>
        public static Font OverrideFont
        {
            get => overrideFont;
            set => overrideFont = value;
        }

        public static Font Font
        {
            get
            {
                if (overrideFont != null) return overrideFont;
                if (builtinFont != null) return builtinFont;
                builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (builtinFont == null) builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return builtinFont;
            }
        }

        /// <summary>
        /// Node đã có sẵn tên này dưới parent, hoặc null.
        ///
        /// Dùng cho những lớp mà UI có thể đến TỪ PREFAB: dựng lại thì thành hai bản chồng
        /// nhau, mà xoá đi dựng lại thì mất đúng thứ tự anh em — tức là mất thứ tự VẼ.
        /// </summary>
        public static RectTransform Reuse(string name, Transform parent)
        {
            Transform found = parent != null ? parent.Find(name) : null;
            return found as RectTransform;
        }

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        public static Image Image(string name, Transform parent, Color color, Sprite sprite = null,
                                 UnityEngine.UI.Image.Type type = UnityEngine.UI.Image.Type.Simple)
        {
            RectTransform rect = Node(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite != null ? sprite : PuzzleSprites.Square;
            image.type = type;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// Panel bo góc có viền — dựng đúng như CSS: một lớp nền + một lớp viền 1px.
        /// Trả về node gốc; hai ảnh là con của nó và căng full.
        /// </summary>
        public static RectTransform Panel(string name, Transform parent, Color fill, Color border, int radius)
        {
            RectTransform root = Node(name, parent);
            Image background = Image("Fill", root, fill, PuzzleSprites.RoundedFill(radius),
                UnityEngine.UI.Image.Type.Sliced);
            Stretch(background.rectTransform, 0, 0, 0, 0);

            if (border.a > 0f)
            {
                Image line = Image("Border", root, border, PuzzleSprites.RoundedOutline(radius),
                    UnityEngine.UI.Image.Type.Sliced);
                Stretch(line.rectTransform, 0, 0, 0, 0);
            }
            return root;
        }

        public static Text Text(string name, Transform parent, string content, int size, Color color,
                                TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            RectTransform rect = Node(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// Nút bo góc có viền, nhãn ở con cuối. `primary` thêm lớp sáng dần từ trên
        /// xuống, thay cho linear-gradient của CSS.
        /// </summary>
        public static Button Button(string name, Transform parent, string label, int fontSize,
                                    Color background, Color labelColor, int radius = PuzzlePalette.RadiusPanel,
                                    bool primary = false, bool showBorder = true)
        {
            RectTransform rect = Node(name, parent);

            // ảnh nhận raycast + là targetGraphic; bo góc luôn ở lớp này
            var image = rect.gameObject.AddComponent<Image>();
            image.color = background;
            image.sprite = PuzzleSprites.RoundedFill(radius);
            image.type = UnityEngine.UI.Image.Type.Sliced;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.16f, 1.16f, 1.16f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.32f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            if (primary)
            {
                Image sheen = Image("Sheen", rect, new Color(1f, 1f, 1f, 0.55f),
                    PuzzleSprites.RoundedTopSheen(radius), UnityEngine.UI.Image.Type.Sliced);
                Stretch(sheen.rectTransform, 0, 0, 0, 0);
            }
            if (showBorder)
            {
                Color border = primary ? PuzzlePalette.AccentTop : PuzzlePalette.Line;
                Image line = Image("Border", rect, border, PuzzleSprites.RoundedOutline(radius),
                    UnityEngine.UI.Image.Type.Sliced);
                Stretch(line.rectTransform, 0, 0, 0, 0);
            }

            Text text = Text(name + "Label", rect, label, fontSize, labelColor);
            Stretch(text.rectTransform, 8, 8, 6, 6);
            return button;
        }

        /// <summary>
        /// Đổi bán kính bo của một nút đã dựng.
        ///
        /// Cần cho bố cục thích ứng: nút co nhỏ hơn 2 lần bán kính thì hai border 9-slice
        /// chồng lên nhau và góc bo hiện ra khuyết. Bán kính phải giảm theo cỡ nút.
        /// </summary>
        public static void SetButtonRadius(Button button, int radius)
        {
            var fill = button.GetComponent<Image>();
            if (fill != null)
            {
                fill.sprite = PuzzleSprites.RoundedFill(radius);
                fill.type = UnityEngine.UI.Image.Type.Sliced;
            }
            Transform border = button.transform.Find("Border");
            if (border != null)
            {
                var line = border.GetComponent<Image>();
                line.sprite = PuzzleSprites.RoundedOutline(radius);
                line.type = UnityEngine.UI.Image.Type.Sliced;
            }
            Transform sheen = button.transform.Find("Sheen");
            if (sheen != null)
            {
                var top = sheen.GetComponent<Image>();
                top.sprite = PuzzleSprites.RoundedTopSheen(radius);
                top.type = UnityEngine.UI.Image.Type.Sliced;
            }
        }

        /// <summary>Bán kính lớn nhất còn an toàn cho một phần tử cỡ này.</summary>
        public static int SafeRadius(float width, float height, int desired)
        {
            float limit = Mathf.Min(width, height) * 0.5f - 2f;
            return Mathf.Clamp(Mathf.FloorToInt(limit), 6, desired);
        }

        /// <summary>Nhãn của nút do Button() dựng — luôn là con cuối cùng.</summary>
        public static Text LabelOf(Button button)
        {
            return button.transform.GetChild(button.transform.childCount - 1).GetComponent<Text>();
        }

        /// <summary>
        /// Xoá con của một node, có hiệu lực NGAY.
        ///
        /// Không dùng Object.Destroy trần: nó chỉ xoá ở cuối frame, nên childCount vẫn
        /// tính các node cũ và lần dựng tiếp theo chồng lên chúng — bàn và thẻ overlay
        /// sẽ cộng dồn qua mỗi lần mở màn. Tách cha ra trước để hết đếm ngay lập tức.
        /// </summary>
        public static void ClearChildren(Transform parent, int keepFirst = 0)
        {
            for (int i = parent.childCount - 1; i >= keepFirst; i--)
            {
                Transform child = parent.GetChild(i);
                child.SetParent(null, false);
                if (Application.isPlaying) Object.Destroy(child.gameObject);
                else Object.DestroyImmediate(child.gameObject);
            }
        }

        /// <summary>
        /// Chiều cao THẬT mà đoạn chữ cần, tính cả phần tự xuống dòng.
        ///
        /// Bắt buộc phải đặt CHIỀU RỘNG và fontSize TRƯỚC khi gọi: preferredHeight phụ
        /// thuộc chiều rộng để biết chữ ngắt dòng ở đâu, đo trước khi có chiều rộng thì
        /// ra số vô nghĩa. ForceUpdateCanvases để rect vừa gán có hiệu lực ngay, không
        /// phải chờ tới cuối frame.
        /// </summary>
        public static float MeasureTextHeight(Text text)
        {
            if (text == null) return 0f;
            Canvas.ForceUpdateCanvases();
            return text.preferredHeight;
        }

        /// <summary>Căng full theo cha, với lề.</summary>
        public static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Neo vào một dải ngang, tính từ đỉnh cha xuống.</summary>
        public static void TopBand(RectTransform rect, float topOffset, float height, float sideMargin = 0)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.offsetMin = new Vector2(sideMargin, -topOffset - height);
            rect.offsetMax = new Vector2(-sideMargin, -topOffset);
        }

        public static void BottomBand(RectTransform rect, float bottomOffset, float height, float sideMargin = 0)
        {
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.offsetMin = new Vector2(sideMargin, bottomOffset);
            rect.offsetMax = new Vector2(-sideMargin, bottomOffset + height);
        }
    }

    /// <summary>
    /// Âm thanh sinh tại runtime: một clip sine, đổi pitch khi phát.
    /// Giữ được chi tiết dễ thương của source gốc — pitch tăng dần theo độ dài chuỗi.
    /// </summary>
    public sealed class PuzzleAudio
    {
        private const float BaseFrequency = 440f;
        private readonly AudioSource source;
        private readonly AudioClip sine;

        public bool Enabled = true;

        public PuzzleAudio(GameObject host)
        {
            this.source = host.AddComponent<AudioSource>();
            this.source.playOnAwake = false;
            this.source.spatialBlend = 0f;

            int sampleRate = 44100;
            int samples = sampleRate / 8;                       // 0.125s
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-t * 22f);           // tắt dần cho đỡ gắt
                data[i] = Mathf.Sin(2f * Mathf.PI * BaseFrequency * t) * envelope;
            }
            this.sine = AudioClip.Create("puzzleSine", samples, 1, sampleRate, false);
            this.sine.SetData(data, 0);
        }

        public void Tone(float frequency, float volume = 0.3f)
        {
            if (!this.Enabled || this.sine == null) return;
            this.source.pitch = Mathf.Clamp(frequency / BaseFrequency, 0.15f, 4f);
            this.source.PlayOneShot(this.sine, volume);
        }

        public void Select(int chainLength) => Tone(392f * Mathf.Pow(1.0595f, chainLength), 0.28f);
        public void Clear(int chainLength)  => Tone(190f + chainLength * 38f, 0.4f);
        public void Fall()                 => Tone(150f, 0.22f);
        public void Bad()                  => Tone(140f, 0.3f);
        public void Undo()                 => Tone(300f, 0.25f);
        public void Blip(int step)         => Tone(280f + step * 55f, 0.2f);
    }
}
