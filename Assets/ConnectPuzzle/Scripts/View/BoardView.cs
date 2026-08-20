using System.Collections;
using System.Collections.Generic;
using ConnectPuzzle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{

    /// <summary>
    /// Vẽ bàn: các ô, vùng chọn, và hoạt ảnh.
    ///
    /// Vùng chọn KHÔNG vẽ bằng nét nối rời giữa các tâm ô. Nó vẽ viền của HỢP các ô
    /// đang chọn, nên chuỗi hiện ra như một khối liền mạch uốn theo đường đi. Làm được
    /// mà không cần shader bằng bốn lớp chồng nhau:
    ///   1. quầng rộng, màu ô, mờ      (dưới cùng)
    ///   2. quầng lõi, pha trắng, đậm hơn
    ///   3. hợp các hình, nở thêm t, tô TRẮNG ĐỤC
    ///   4. hợp các hình, kích thước gốc, tô MÀU NỀN ĐỤC
    /// Lớp 4 khoét ruột lớp 3, chừa lại đúng một viền trắng dày t bao quanh cả chuỗi.
    /// Bubble vẽ đè lên trên cùng nên vẫn thấy màu ô.
    /// </summary>
    public sealed class BoardView
    {
        /// <summary>Prefab một ô bàn, nạp một lần rồi instantiate theo số ô của màn.</summary>
        public const string CellResourcePath = "UI/Cell";
        private GameObject cellPrefab;

        /// <summary>
        /// Độ mờ của ký hiệu trên ô. Là hằng số vì hoạt ảnh trượt phải trả nó về đúng
        /// giá trị này sau khi mờ dần — hai chỗ ghi số rời nhau thì sẽ lệch nhau.
        /// </summary>
        public const float GlyphAlpha = 0.33f;

        private const float SpringStiffness = 420f;
        private const float SpringDamping = 21f;
        private const float SelectedScale = 1.1f;

        private readonly RectTransform root;
        private readonly RectTransform glowWideLayer;
        private readonly RectTransform glowCoreLayer;
        private readonly RectTransform outlineLayer;
        private readonly RectTransform innerLayer;
        private readonly RectTransform cellLayer;

        // Canvas gốc — dùng để đổi vị trí mảnh khoét sang toạ độ chuẩn hoá của màn.
        // Tra một lần rồi giữ: GetComponentInParent leo cả cây, mà nó chạy cho từng
        // mảnh của từng khung hình suốt lúc kéo.
        private RectTransform canvasRect;

        private readonly List<Image> glowWideParts = new List<Image>();
        private readonly List<Image> glowCoreParts = new List<Image>();
        private readonly List<Image> outlineParts = new List<Image>();
        private readonly List<Image> innerParts = new List<Image>();

        private readonly List<int> chainCells = new List<int>();
        private int chainColor = -1;
        private float pulseTime;
        private int flashIndex = -1;
        private float flashTime = -10f;

        private CellView[] cells;
        private LevelData level;
        private float cellSize;
        private bool dimmed;
        private bool scaleAnimating;

        public float CellSize => this.cellSize;
        public RectTransform Root => this.root;

        /// <summary>
        /// Màu vòng của một cặp liên kết. Lấy từ bảng riêng, KHÔNG dùng bảng màu ô —
        /// trùng màu ô thì người chơi tưởng vòng đang nói về màu chuỗi.
        /// </summary>
        private static readonly Color[] LinkColors =
        {
            new Color32(0xFF, 0xFF, 0xFF, 0xFF),   // trắng
            new Color32(0x9B, 0xFF, 0xE0, 0xFF),   // ngọc
            new Color32(0xFF, 0xD6, 0x8A, 0xFF),   // vàng nhạt
            new Color32(0xD9, 0xB8, 0xFF, 0xFF),   // tím nhạt
            new Color32(0xFF, 0xB8, 0xC8, 0xFF)    // hồng nhạt
        };

        private static Color LinkColor(int linkId, bool dimmed)
        {
            Color c = LinkColors[Mathf.Abs(linkId) % LinkColors.Length];
            return dimmed ? new Color(c.r, c.g, c.b, 0.3f) : c;
        }

        /// <summary>Ô theo chỉ số lưới; null nếu ngoài bàn. Dùng cho kiểm thử hiển thị.</summary>
        public CellView CellAt(int index) =>
            this.cells != null && index >= 0 && index < this.cells.Length ? this.cells[index] : null;

        /// <summary>
        /// Bật thì mọi hoạt ảnh đang chạy nhảy thẳng tới trạng thái cuối.
        /// Dùng khi người chơi chạm giữa lúc hoạt ảnh chạy: model đã ở trạng thái cuối
        /// ngay từ lúc Commit(), nên bắt họ chờ xem hoạt ảnh là mất lượt kéo vô cớ.
        /// </summary>
        public bool FastForward;

        /// <summary>
        /// Dựng khung bàn, hoặc NHẬN LẠI khung đã có sẵn nếu UI đến từ prefab.
        ///
        /// Nhận lại chứ không xoá-dựng-lại: thứ tự anh em CHÍNH LÀ thứ tự vẽ ở đây (quầng
        /// dưới, viền, ruột, ô trên cùng), mà dựng lại thì các lớp này bị đẩy xuống sau
        /// InputArea và ChainPreview — bàn vẫn hiện nhưng chuỗi đang chọn bị che.
        /// </summary>
        public BoardView(RectTransform parent)
        {
            RectTransform existing = Ui.Reuse("Board", parent);
            if (existing != null)
            {
                this.root = existing;
                this.glowWideLayer = Ui.Reuse("SelGlowWide", existing);
                this.glowCoreLayer = Ui.Reuse("SelGlowCore", existing);
                this.outlineLayer = Ui.Reuse("SelOutline", existing);
                this.innerLayer = Ui.Reuse("SelInner", existing);
                this.cellLayer = Ui.Reuse("Cells", existing);
                return;
            }

            this.root = Ui.Node("Board", parent);

            // Thứ tự tạo = thứ tự vẽ. Hai lớp quầng dưới cùng, rồi viền trắng, rồi ruột
            // khoét bụng viền, cuối cùng là các ô.
            this.glowWideLayer = Ui.Node("SelGlowWide", this.root);
            this.glowCoreLayer = Ui.Node("SelGlowCore", this.root);
            this.outlineLayer = Ui.Node("SelOutline", this.root);
            this.innerLayer = Ui.Node("SelInner", this.root);
            this.cellLayer = Ui.Node("Cells", this.root);

            foreach (RectTransform layer in new[] { this.glowWideLayer, this.glowCoreLayer,
                                                    this.outlineLayer,
                                                    this.innerLayer, this.cellLayer })
                Ui.Stretch(layer, 0, 0, 0, 0);
        }

        // ------------------------------------------------------------------
        // Dựng và bố cục
        // ------------------------------------------------------------------

        public void Build(LevelData levelData)
        {
            this.level = levelData;
            ClearChain();
            Ui.ClearChildren(this.cellLayer);

            BoardGeometry geo = levelData.Geometry;
            // Nạp prefab MỘT lần cho cả bàn.
            if (this.cellPrefab == null)
            {
                this.cellPrefab = Resources.Load<GameObject>(CellResourcePath);
                if (this.cellPrefab == null)
                {
                    Debug.LogError("[UI] Thiếu prefab " + CellResourcePath +
                                   ". Chạy menu Connect Puzzle > Dựng lại prefab ô bàn.");
                    return;
                }
            }

            this.cells = new CellView[geo.CellCount];

            for (int i = 0; i < geo.CellCount; i++)
            {
                CellView cell;

                if (geo.Active[i])
                {
                    // Ô thật: instantiate từ prefab.
                    GameObject instance = Object.Instantiate(this.cellPrefab, this.cellLayer, false);
                    instance.name = "Cell" + i;
                    cell = instance.GetComponent<CellView>();

                    if (i == 0)
                    {
                        System.Collections.Generic.List<string> missing = cell.MissingFields();
                        if (missing.Count > 0)
                            Debug.LogError("[UI] Prefab ô bàn chưa gán: " + string.Join(", ", missing));
                    }
                }
                else
                {
                    // Ô TƯỜNG: node rỗng, KHÔNG instantiate prefab.
                    //
                    // Prefab luôn mang đủ 11 lớp con; dùng nó cho ô tường thì bong bóng, chóa
                    // sáng, khe nền đều hiện ra trên một chỗ đáng lẽ trống — mà code phía sau
                    // lại bỏ qua ô tường nên không có ai tắt chúng đi. Node rỗng giữ đúng hành
                    // vi cũ và không tốn gì.
                    RectTransform node = Ui.Node("Cell" + i, this.cellLayer);
                    cell = node.gameObject.AddComponent<CellView>();
                    cell.Root = node;
                    cell.IsWall = true;
                }

                this.cells[i] = cell;
            }
        }

        /// <summary>Ô vuông, bàn co theo không gian còn lại — cùng cách tính với bản HTML.</summary>
        public void Layout(Vector2 availableSize)
        {
            if (this.level == null) return;
            BoardGeometry geo = this.level.Geometry;

            this.cellSize = Mathf.Min(availableSize.x / geo.Columns, availableSize.y / geo.Rows);
            this.cellSize = Mathf.Max(this.cellSize, 12f);
            this.root.sizeDelta = new Vector2(this.cellSize * geo.Columns, this.cellSize * geo.Rows);

            float bubbleInset = this.cellSize * 0.08f;
            float slotInset = this.cellSize * 0.11f;
            float shadowDrop = this.cellSize * 0.045f;
            float glowSpread = this.cellSize * 0.30f;
            float ringOut = this.cellSize * 0.055f;

            for (int i = 0; i < geo.CellCount; i++)
            {
                CellView cell = this.cells[i];
                int x = i % geo.Columns;
                int y = i / geo.Columns;

                cell.Root.anchorMin = cell.Root.anchorMax = new Vector2(0, 1);
                cell.Root.pivot = new Vector2(0.5f, 0.5f);
                cell.Root.sizeDelta = new Vector2(this.cellSize, this.cellSize);
                cell.Root.anchoredPosition = new Vector2((x + 0.5f) * this.cellSize, -(y + 0.5f) * this.cellSize);

                if (cell.IsWall) continue;

                Ui.Stretch(cell.SlotBackground.rectTransform, slotInset, slotInset, slotInset, slotInset);
                Ui.Stretch(cell.BubbleRoot, 0, 0, 0, 0);
                Ui.Stretch(cell.Fill.rectTransform, bubbleInset, bubbleInset, bubbleInset, bubbleInset);
                Ui.Stretch(cell.Sheen.rectTransform, bubbleInset, bubbleInset, bubbleInset, bubbleInset);
                Ui.Stretch(cell.Shadow.rectTransform, bubbleInset * 0.4f, bubbleInset * 0.4f,
                    bubbleInset + shadowDrop, bubbleInset - shadowDrop);
                Ui.Stretch(cell.Glow.rectTransform, -glowSpread, -glowSpread, -glowSpread, -glowSpread);
                Ui.Stretch(cell.Ring.rectTransform, bubbleInset - ringOut, bubbleInset - ringOut,
                    bubbleInset - ringOut, bubbleInset - ringOut);
                Ui.Stretch(cell.Glyph.rectTransform, 0, 0, 0, 0);
                cell.Glyph.fontSize = Mathf.Max(8, Mathf.RoundToInt(this.cellSize * 0.32f));

                Ui.Stretch(cell.Ice.rectTransform, bubbleInset, bubbleInset, bubbleInset, bubbleInset);

                float ringInset = Mathf.Max(0f, bubbleInset - this.cellSize * 0.06f);
                Ui.Stretch(cell.GoalRing.rectTransform, ringInset, ringInset, ringInset, ringInset);
                float badge = this.cellSize * 0.40f;
                foreach (RectTransform r in new[] { cell.FuseBadge.rectTransform, cell.Fuse.rectTransform })
                {
                    r.anchorMin = r.anchorMax = new Vector2(1, 1);
                    r.pivot = new Vector2(0.5f, 0.5f);
                    r.sizeDelta = new Vector2(badge, badge);
                    r.anchoredPosition = new Vector2(-badge * 0.30f, -badge * 0.30f);
                }
                cell.Fuse.fontSize = Mathf.Max(8, Mathf.RoundToInt(badge * 0.62f));
            }

            UpdateSelectionVisuals();
        }

        /// <summary>Vị trí tâm ô trong hệ toạ độ local của bàn (gốc ở góc trên-trái).</summary>
        public Vector2 CellCenter(int index)
        {
            BoardGeometry geo = this.level.Geometry;
            int x = index % geo.Columns;
            int y = index / geo.Columns;
            return new Vector2((x + 0.5f) * this.cellSize, -(y + 0.5f) * this.cellSize);
        }

        /// <summary>Đổi điểm bấm (world) thành chỉ số ô, hoặc -1 nếu ra ngoài bàn.</summary>
        public int CellAtWorldPoint(Vector3 worldPoint) => CellAtWorldPoint(worldPoint, 0f);

        /// <summary>
        /// Như trên, nhưng bỏ qua dải mép ngoài của ô.
        ///
        /// Cần cho việc kéo chéo: đường kéo giữa hai ô chéo nhau đi sát GÓC của hai ô
        /// trực giao bên cạnh. Không có dải chết đó thì hai ô này bị quét vào chuỗi dù
        /// người chơi chỉ lướt qua góc, làm chuỗi chéo đi lệch hẳn ý định.
        /// </summary>
        public int CellAtWorldPoint(Vector3 worldPoint, float edgeInset)
        {
            if (this.level == null) return -1;
            Vector2 local = this.root.InverseTransformPoint(worldPoint);
            BoardGeometry geo = this.level.Geometry;

            float px = local.x + this.root.sizeDelta.x * 0.5f;
            float py = this.root.sizeDelta.y * 0.5f - local.y;

            int x = Mathf.FloorToInt(px / this.cellSize);
            int y = Mathf.FloorToInt(py / this.cellSize);
            if (x < 0 || y < 0 || x >= geo.Columns || y >= geo.Rows) return -1;

            if (edgeInset > 0f)
            {
                float fx = px / this.cellSize - x;
                float fy = py / this.cellSize - y;
                if (fx < edgeInset || fx > 1f - edgeInset) return -1;
                if (fy < edgeInset || fy > 1f - edgeInset) return -1;
            }

            int index = y * geo.Columns + x;
            return geo.Active[index] ? index : -1;
        }

        // ------------------------------------------------------------------
        // Đồng bộ hiển thị
        // ------------------------------------------------------------------

        public void Refresh(PuzzleSession session, bool showSymbols)
        {
            for (int i = 0; i < this.cells.Length; i++)
            {
                CellView cell = this.cells[i];
                if (cell.IsWall) continue;

                int value = session.Board[i];
                bool alive = PuzzleSession.IsAlive(value);
                cell.SetAlive(alive, showSymbols);
                cell.ResetScale();
                cell.BubbleRoot.anchoredPosition = Vector2.zero;

                CellMark mark = session.MarkAt(i);
                if (alive)
                {
                    // Hình dạng do MATERIAL SDF quyết định, không do alpha của texture:
                    // mép được tính theo bề rộng một pixel màn hình nên sắc và mượt ở
                    // mọi cỡ. Texture chỉ còn cung cấp MÀU.
                    bool stoneShape = value == PuzzleSession.Stone;
                    cell.Fill.material = PuzzleSprites.SdfMaterial(
                        stoneShape ? 0.22f : 0.5f, 0f, sheen: !stoneShape && !(mark != null && mark.Kind == CellKind.Wild));

                    if (value == PuzzleSession.Stone)
                    {
                        // Đá: khối VUÔNG VỨC xám, không chóa tròn — hình dáng khác hẳn
                        // bubble nên nhìn một cái là biết nó không bấm được.
                        bool thick = mark != null && mark.Hp >= 2;
                        cell.Fill.sprite = thick ? PuzzleSprites.StoneCracked : PuzzleSprites.StoneTile;
                        cell.Fill.color = this.dimmed ? new Color(1, 1, 1, 0.42f) : Color.white;
                        cell.Glyph.enabled = false;
                        cell.Sheen.enabled = false;
                    }
                    else if (mark != null && mark.Kind == CellKind.Wild)
                    {
                        // Đa sắc: quang phổ quay quanh lõi trắng. Màu nằm trong texture
                        // nên Image phải để trắng, tint vào là hỏng phổ.
                        cell.Fill.sprite = PuzzleSprites.WildDisc;
                        cell.Fill.color = this.dimmed ? new Color(1, 1, 1, 0.42f) : Color.white;
                        cell.Glyph.enabled = false;
                        cell.Sheen.enabled = true;
                        cell.Sheen.color = this.dimmed ? new Color(1, 1, 1, 0.25f) : new Color(1, 1, 1, 0.55f);
                    }
                    else
                    {
                        // Chóa sáng giờ do shader vẽ, nên tắt hẳn lớp Sheen: để cả hai
                        // thì bóng chồng lên nhau và ô sáng bệch ở góc trên-trái.
                        cell.Fill.sprite = PuzzleSprites.Square;
                        cell.Fill.color = Tint(PuzzlePalette.Colors[value]);
                        cell.Glyph.text = PuzzlePalette.Glyphs[value];
                        cell.Sheen.enabled = false;
                    }
                    cell.Shadow.color = new Color(0f, 0f, 0f, this.dimmed ? 0.15f : 0.42f);
                }

                // Ngòi nổ: số đếm ngược. Vòng đích: viền vàng quanh ô.
                bool bomb = alive && mark != null && mark.Kind == CellKind.Bomb;
                cell.Fuse.enabled = bomb;
                cell.FuseBadge.enabled = bomb;
                if (bomb)
                {
                    int left = mark.Fuse - session.MovesUsed;
                    cell.Fuse.text = left.ToString();
                    cell.Fuse.color = Color.white;
                    // badge đỏ khi sắp hết giờ — đổi NỀN chứ không đổi màu chữ, vì chữ
                    // đỏ trên nền đậm khó đọc hơn hẳn chữ trắng trên nền đỏ
                    cell.FuseBadge.color = left <= 2 ? PuzzlePalette.Bad : Color.white;
                }

                // Băng: lớp phủ MỜ lên trên ô, giữ nguyên màu ô bên dưới — người chơi
                // cần đọc được màu đó để tính trước nước sau khi băng tan.
                bool frozen = alive && session.IsFrozen(i);
                cell.Ice.enabled = frozen;
                if (frozen)
                {
                    cell.Ice.sprite = PuzzleSprites.IceOverlay(mark.Hp >= 2);
                    cell.Ice.color = this.dimmed ? new Color(1, 1, 1, 0.4f) : Color.white;
                }

                // Liên kết: vòng quanh ô, MÀU THEO SỐ HIỆU CẶP. Bàn có 3-4 cặp mà tô
                // cùng một màu thì người chơi không biết ô nào ăn theo ô nào — và cơ
                // chế này chỉ có nghĩa khi họ nhìn ra được cặp.
                bool linked = alive && mark != null && mark.Kind == CellKind.Link;
                if (linked)
                {
                    // Vòng XÍCH (đứt quãng thành mắt), khác hẳn vòng LIỀN của ô đích —
                    // phân biệt bằng hình, không chỉ bằng màu.
                    cell.GoalRing.enabled = true;
                    cell.GoalRing.material = null;                    // sprite tự lo hình
                    cell.GoalRing.sprite = PuzzleSprites.ChainRing;
                    cell.GoalRing.color = LinkColor(mark.LinkId, this.dimmed);

                    // Số hiệu cặp: bàn 4 cặp mà chỉ khác màu thì phải nhớ bảng màu, và
                    // người mù màu không đọc được. Có số thì ghép cặp là chuyện nhìn ra
                    // ngay. Ô liên kết không bao giờ mang ngòi nổ nên dùng chung badge.
                    cell.FuseBadge.enabled = true;
                    cell.FuseBadge.color = LinkColor(mark.LinkId, this.dimmed);
                    cell.Fuse.enabled = true;
                    cell.Fuse.text = (mark.LinkId + 1).ToString();
                    // Chữ TRẮNG: thân huy hiệu là đĩa tối #111827 (chỉ có VIỀN mới ăn màu
                    // của cặp), nên chữ tối là chữ tối trên nền tối — huy hiệu hiện ra
                    // như một chấm đen trống không, đúng thứ nhìn thấy trên ảnh chụp.
                    cell.Fuse.color = Color.white;
                    continue;
                }

                bool goal = alive && mark != null && mark.Goal;
                cell.GoalRing.enabled = goal;
                if (goal)
                {
                    // vòng đích cũng vẽ bằng SDF: nó mảnh nên răng cưa lộ rõ nhất
                    cell.GoalRing.material = PuzzleSprites.SdfMaterial(0.5f, 0.055f, sheen: false);
                    cell.GoalRing.sprite = PuzzleSprites.Square;
                    cell.GoalRing.color = new Color(0.98f, 0.75f, 0.14f, this.dimmed ? 0.3f : 1f);
                }
            }
        }

        private Color Tint(Color c)
        {
            return this.dimmed ? new Color(c.r * 0.33f, c.g * 0.33f, c.b * 0.33f, 0.48f) : c;
        }

        /// <summary>Đặt MỤC TIÊU phóng to; lò xo trong Tick sẽ chạy tới đó.</summary>
        public void SetSelected(int index, bool selected)
        {
            CellView cell = this.cells[index];
            if (cell.IsWall || !cell.Fill.enabled) return;
            cell.ScaleTarget = selected ? SelectedScale : 1f;
        }

        // ------------------------------------------------------------------
        // Vùng chọn — viền của HỢP các ô
        // ------------------------------------------------------------------

        public void DrawChain(List<int> selection, int colorIndex)
        {
            // ô vừa được thêm sẽ loé lên rồi tắt dần — cho cảm giác chuỗi "ăn" vào ô
            if (selection.Count > this.chainCells.Count)
            {
                this.flashIndex = selection.Count - 1;
                this.flashTime = this.pulseTime;
            }
            else if (selection.Count < this.chainCells.Count)
            {
                this.flashIndex = -1;
            }

            this.chainCells.Clear();
            this.chainCells.AddRange(selection);
            this.chainColor = colorIndex;
            UpdateSelectionVisuals();
        }

        public void ClearChain()
        {
            this.chainCells.Clear();
            this.chainColor = -1;
            UpdateSelectionVisuals();
        }

        /// <summary>
        /// Chạy lò xo phóng to và vẽ lại vùng chọn. Gọi mỗi frame từ tầng game.
        /// </summary>
        public void TickChain(float deltaTime)
        {
            this.pulseTime += deltaTime;

            if (!this.scaleAnimating && this.cells != null)
            {
                float dt = Mathf.Min(deltaTime, 0.05f);   // chặn bước lớn để lò xo không nổ
                foreach (CellView cell in this.cells)
                {
                    if (cell.IsWall) continue;
                    if (Mathf.Abs(cell.ScaleCurrent - cell.ScaleTarget) < 0.0005f &&
                        Mathf.Abs(cell.ScaleVelocity) < 0.0005f)
                    {
                        cell.ScaleCurrent = cell.ScaleTarget;
                        cell.ScaleVelocity = 0f;
                        cell.BubbleRoot.localScale = Vector3.one * cell.ScaleCurrent;
                        continue;
                    }
                    cell.ScaleVelocity += (cell.ScaleTarget - cell.ScaleCurrent) * SpringStiffness * dt;
                    cell.ScaleVelocity *= Mathf.Exp(-SpringDamping * dt);
                    cell.ScaleCurrent += cell.ScaleVelocity * dt;
                    cell.BubbleRoot.localScale = Vector3.one * cell.ScaleCurrent;
                }
            }

            if (this.chainCells.Count > 0) UpdateSelectionVisuals();
        }

        /// <summary>Số điểm quầng rải trên mỗi đoạn nối, để quầng thành ống liền chứ không gãy.</summary>
        private const int GlowPointsPerLink = 3;

        private void UpdateSelectionVisuals()
        {
            int count = this.chainCells.Count;
            int links = Mathf.Max(0, count - 1);
            EnsureSelectionParts(count + links, count + links * GlowPointsPerLink);

            if (count == 0 || this.level == null)
            {
                DisableFrom(this.glowWideParts, 0);
                DisableFrom(this.glowCoreParts, 0);
                DisableFrom(this.outlineParts, 0);
                DisableFrom(this.innerParts, 0);
                return;
            }

            Color color = this.chainColor >= 0 ? PuzzlePalette.Colors[this.chainColor] : Color.white;
            float radius = this.cellSize * 0.42f;
            float thickness = this.cellSize * 0.085f;

            // Hai lần dựng cùng một hợp hình, chỉ khác độ nở và màu:
            //   viền (nở thêm t, trắng đặc) -> nét chính
            //   ruột (không nở, màu nền)    -> khoét bụng, chừa lại đúng viền
            //
            // Cả hai đều ĐỤC là có lý do. Hợp ở đây ghép từ nhiều hình rời, nên hình
            // tròn và dải nối chồng nhau; nếu tô trong suốt thì alpha cộng dồn ở chỗ
            // chồng, ra mảng đậm nhạt loang lổ chứ không phải viền mềm. Muốn mềm thì
            // phải dùng sprite có biên mềm, và đó là việc của hai lớp quầng.
            BuildUnion(this.outlineParts, radius, thickness, Color.white, false);

            // Lớp khoét lấy màu TỪ GRADIENT chứ không dùng hằng số nền.
            //
            // Nền là gradient: #14182A ở đỉnh, #0F1220 ở giữa và đáy. Khoét bằng hằng số
            // #0F1220 thì ở nửa trên màn hình nó lệch (5,6,10)/255 so với nền thật, và
            // hiện thành một mảng phẳng tối hơn đúng theo hình cái viền — rõ nhất ở hàng
            // ô trên cùng.
            BuildUnion(this.innerParts, radius, 0f, PuzzlePalette.Background, true);

            BuildGlow(color, radius, thickness);
        }

        /// <summary>Hợp của các hình tròn (mỗi ô) và dải nối (mỗi cặp liền nhau).</summary>
        private void BuildUnion(List<Image> parts, float radius, float expand, Color color,
                                bool sampleBackground)
        {
            int count = this.chainCells.Count;
            int part = 0;

            for (int i = 0; i < count; i++)
            {
                int cellIndex = this.chainCells[i];
                float scale = this.cells[cellIndex].IsWall ? 1f : this.cells[cellIndex].ScaleCurrent;
                float r = radius * scale + expand;
                Image circle = parts[part++];
                Place(circle, CellCenter(cellIndex), new Vector2(r * 2f, r * 2f), 0f,
                    color, PuzzleSprites.Circle);
                if (sampleBackground) circle.color = BackgroundAt(circle.rectTransform);
            }

            for (int i = 0; i + 1 < count; i++)
            {
                Vector2 a = CellCenter(this.chainCells[i]);
                Vector2 b = CellCenter(this.chainCells[i + 1]);
                Vector2 delta = b - a;
                float scale = 0.5f * (this.cells[this.chainCells[i]].ScaleCurrent +
                                      this.cells[this.chainCells[i + 1]].ScaleCurrent);
                // Cổ nối gần bằng bán kính quả bóng. Hẹp hơn nữa thì cạnh thẳng của dải
                // cắt vào cung tròn thành góc gãy thấy rõ, nhất là ở đoạn chéo dài hơn.
                float r = radius * scale * 0.90f + expand;
                Image bar = parts[part++];
                Place(bar, a + delta * 0.5f, new Vector2(delta.magnitude, r * 2f),
                    Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, color, PuzzleSprites.Square);
                if (sampleBackground) bar.color = BackgroundAt(bar.rectTransform);
            }

            DisableFrom(parts, part);
        }

        /// <summary>
        /// Màu nền THẬT ở đúng chỗ một mảnh khoét đang đứng.
        ///
        /// Đi qua toạ độ WORLD của chính mảnh đó chứ không tự suy từ toạ độ bàn: chuỗi
        /// neo và pivot từ mảnh leo lên tới canvas dài, mỗi tầng một kiểu; tự suy là
        /// chép ra một bản luật neo thứ hai, để rồi nó trôi khỏi bản thật lúc nào không
        /// hay.
        ///
        /// Hỏng đường nào cũng lùi về màu nền phẳng: lệch màu một chút vẫn hơn ném
        /// exception giữa lúc vẽ từng khung hình.
        /// </summary>
        private Color BackgroundAt(RectTransform part)
        {
            if (this.canvasRect == null)
            {
                Canvas canvas = this.root.GetComponentInParent<Canvas>();
                if (canvas == null) return PuzzlePalette.Background;
                this.canvasRect = (RectTransform)canvas.rootCanvas.transform;
            }

            Vector2 local = this.canvasRect.InverseTransformPoint(part.position);
            Rect area = this.canvasRect.rect;
            return PuzzleSprites.BackgroundColorAt(
                Mathf.InverseLerp(area.xMin, area.xMax, local.x),
                Mathf.InverseLerp(area.yMin, area.yMax, local.y));
        }

        /// <summary>
        /// Quầng sáng: hai lớp chồng nhau, rải điểm dọc theo chuỗi.
        ///
        /// Không dùng dải chữ nhật cho đoạn nối như trước — dải mềm theo chiều dọc
        /// nhưng CẮT VUÔNG hai đầu, để lại cạnh thẳng thấy rõ trong quầng. Rải vài
        /// điểm tròn dọc đoạn thì quầng thành ống liền, không có cạnh nào.
        ///
        /// Lớp lõi pha về phía trắng: uGUI trộn alpha chứ không cộng sáng, nên muốn
        /// chỗ chồng nhau trông "nóng" thì phải tự làm nhạt màu ở lõi.
        /// </summary>
        private void BuildGlow(Color color, float radius, float thickness)
        {
            int count = this.chainCells.Count;
            Color coreColor = Color.Lerp(color, Color.white, 0.45f);
            int part = 0;

            for (int i = 0; i < count; i++)
            {
                int cellIndex = this.chainCells[i];
                float scale = this.cells[cellIndex].IsWall ? 1f : this.cells[cellIndex].ScaleCurrent;
                PlaceGlowPoint(part++, CellCenter(cellIndex), i, count, radius * scale, thickness, color, coreColor);
            }

            for (int i = 0; i + 1 < count; i++)
            {
                Vector2 a = CellCenter(this.chainCells[i]);
                Vector2 b = CellCenter(this.chainCells[i + 1]);
                for (int k = 1; k <= GlowPointsPerLink; k++)
                {
                    float t = k / (float)(GlowPointsPerLink + 1);
                    PlaceGlowPoint(part++, Vector2.Lerp(a, b, t), i + t, count,
                        radius * 0.9f, thickness, color, coreColor);
                }
            }

            DisableFrom(this.glowWideParts, part);
            DisableFrom(this.glowCoreParts, part);
        }

        private void PlaceGlowPoint(int part, Vector2 centre, float chainPosition, int count,
                                    float radius, float thickness, Color color, Color coreColor)
        {
            // sóng sáng chạy dọc chuỗi
            float wave = 0.5f + 0.5f * Mathf.Sin(this.pulseTime * 4.4f - chainPosition * 0.85f);

            // đầu chuỗi (ô vừa nối) sáng hơn phần đuôi
            float headBoost = count > 1
                ? Mathf.Clamp01(1f - (count - 1 - chainPosition) / 2.5f) * 0.22f
                : 0.22f;

            // ô vừa được thêm loé lên rồi tắt dần
            float flash = 0f;
            if (this.flashIndex >= 0 && Mathf.Abs(chainPosition - this.flashIndex) < 0.5f)
                flash = Mathf.Max(0f, 1f - (this.pulseTime - this.flashTime) / 0.4f) * 0.35f;

            float wideAlpha = 0.16f + wave * 0.10f + headBoost * 0.5f + flash * 0.5f;
            float coreAlpha = 0.24f + wave * 0.12f + headBoost + flash;

            float wideSize = (radius + thickness) * 6.4f * (1f + flash * 0.35f);
            float coreSize = (radius + thickness) * 3.1f * (1f + flash * 0.30f);

            Place(this.glowWideParts[part], centre, new Vector2(wideSize, wideSize), 0f,
                new Color(color.r, color.g, color.b, wideAlpha), PuzzleSprites.Bloom);
            Place(this.glowCoreParts[part], centre, new Vector2(coreSize, coreSize), 0f,
                new Color(coreColor.r, coreColor.g, coreColor.b, coreAlpha), PuzzleSprites.Bloom);
        }

        private static void Place(Image image, Vector2 centre, Vector2 size, float angle, Color color, Sprite sprite)
        {
            image.enabled = true;
            image.sprite = sprite;
            image.color = color;
            image.rectTransform.anchoredPosition = centre;
            image.rectTransform.sizeDelta = size;
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private static void DisableFrom(List<Image> parts, int from)
        {
            for (int i = from; i < parts.Count; i++) parts[i].enabled = false;
        }

        private void EnsureSelectionParts(int unionNeeded, int glowNeeded)
        {
            while (this.outlineParts.Count < unionNeeded)
            {
                int index = this.outlineParts.Count;
                this.outlineParts.Add(MakePart(this.outlineLayer, "Outline" + index));
                this.innerParts.Add(MakePart(this.innerLayer, "Inner" + index));
            }
            while (this.glowWideParts.Count < glowNeeded)
            {
                int index = this.glowWideParts.Count;
                this.glowWideParts.Add(MakePart(this.glowWideLayer, "GlowWide" + index));
                this.glowCoreParts.Add(MakePart(this.glowCoreLayer, "GlowCore" + index));
            }
        }

        private static Image MakePart(RectTransform layer, string name)
        {
            Image image = Ui.Image(name, layer, Color.white, PuzzleSprites.Circle);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0, 1);
            image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            image.enabled = false;
            return image;
        }

        // ------------------------------------------------------------------
        // Hoạt ảnh
        // ------------------------------------------------------------------

        /// <summary>Ô nổ so le. Chạy trên màu ĐANG hiện, nên phải gọi TRƯỚC Refresh.</summary>
        public IEnumerator PlayPop(int[] clearedCells)
        {
            this.scaleAnimating = true;
            const float duration = 0.28f;
            const float stagger = 0.026f;
            float total = duration + stagger * clearedCells.Length;
            float elapsed = 0f;

            while (elapsed < total)
            {
                elapsed += Time.deltaTime;
                for (int k = 0; k < clearedCells.Length; k++)
                {
                    CellView cell = this.cells[clearedCells[k]];
                    if (cell.IsWall) continue;
                    float t = Mathf.Clamp01((elapsed - k * stagger) / duration);
                    float scale = t < 0.35f
                        ? Mathf.Lerp(1f, 1.3f, t / 0.35f)
                        : Mathf.Lerp(1.3f, 0.05f, (t - 0.35f) / 0.65f);
                    cell.BubbleRoot.localScale = Vector3.one * scale;
                    if (t >= 1f) cell.SetAlive(false, false);
                }
                if (this.FastForward) break;
                yield return null;
            }

            foreach (int i in clearedCells)
            {
                CellView cell = this.cells[i];
                if (cell.IsWall) continue;
                cell.ResetScale();
                cell.SetAlive(false, false);
            }
            this.scaleAnimating = false;
        }

        /// <summary>
        /// Băng NỨT và băng TAN — hai hoạt ảnh khác hẳn nhau vì chúng nói hai chuyện
        /// khác nhau, và người chơi phải phân biệt được ngay:
        ///
        ///   · NỨT  = "có tiến triển, nhưng chưa xong" — băng giật nảy rồi loé sáng,
        ///            xong vẫn còn đó (đổi sang lớp mỏng hơn).
        ///   · TAN  = "ô này mở khoá rồi, ăn được" — lớp băng phình ra và tan biến,
        ///            để lộ ô màu bên dưới.
        ///
        /// Gọi SAU Refresh: lúc đó Refresh đã đặt đúng sprite/độ dày cho trạng thái MỚI,
        /// nên với ô vừa tan thì lớp băng đã tắt — ta bật lại tạm để chạy hoạt ảnh tan,
        /// nếu không sẽ không có gì để cho tan.
        /// </summary>
        public IEnumerator PlayIce(List<int> cracked, List<int> thawed)
        {
            if ((cracked == null || cracked.Count == 0) && (thawed == null || thawed.Count == 0))
                yield break;

            // Ô vừa tan: bật lại lớp băng ở trạng thái mỏng nhất để nó có cái mà tan.
            if (thawed != null)
                foreach (int i in thawed)
                {
                    CellView cell = this.cells[i];
                    if (cell.IsWall || cell.Ice == null) continue;
                    cell.Ice.enabled = true;
                    cell.Ice.sprite = PuzzleSprites.IceOverlay(false);
                    cell.Ice.color = Color.white;
                    cell.Ice.rectTransform.localScale = Vector3.one;
                }

            const float duration = 0.42f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // NỨT: giật nảy nhanh rồi tắt dần — như bị gõ một nhát.
                if (cracked != null)
                {
                    float shake = Mathf.Sin(t * Mathf.PI * 6f) * (1f - t) * 0.10f;
                    float flash = Mathf.Clamp01(1f - t * 2.2f);
                    foreach (int i in cracked)
                    {
                        CellView cell = this.cells[i];
                        if (cell.IsWall || cell.Ice == null || !cell.Ice.enabled) continue;
                        cell.Ice.rectTransform.localScale = Vector3.one * (1f + shake);
                        // loé trắng: cộng thẳng vào màu, hết loé thì về trắng chuẩn
                        float w = 1f + flash * 0.9f;
                        cell.Ice.color = new Color(w, w, w, 1f);
                    }
                }

                // TAN: phình ra rồi mờ hẳn — cảm giác lớp vỏ bung ra chứ không phải
                // co lại biến mất (co lại đọc thành "ô bị ăn", sai nghĩa).
                if (thawed != null)
                {
                    float grow = 1f + t * 0.42f;
                    float fade = 1f - t * t;
                    foreach (int i in thawed)
                    {
                        CellView cell = this.cells[i];
                        if (cell.IsWall || cell.Ice == null) continue;
                        cell.Ice.rectTransform.localScale = Vector3.one * grow;
                        cell.Ice.color = new Color(1f, 1f, 1f, fade);
                    }
                }

                if (this.FastForward) break;
                yield return null;
            }

            // Trả mọi thứ về đúng trạng thái cuối, không để sót scale/màu dở dang.
            if (cracked != null)
                foreach (int i in cracked)
                {
                    CellView cell = this.cells[i];
                    if (cell.IsWall || cell.Ice == null) continue;
                    cell.Ice.rectTransform.localScale = Vector3.one;
                    cell.Ice.color = Color.white;
                }
            if (thawed != null)
                foreach (int i in thawed)
                {
                    CellView cell = this.cells[i];
                    if (cell.IsWall || cell.Ice == null) continue;
                    cell.Ice.rectTransform.localScale = Vector3.one;
                    cell.Ice.color = Color.white;
                    cell.Ice.enabled = false;         // đã tan thật, tắt hẳn
                }
        }

        /// <summary>
        /// Ô rơi xuống. Gọi SAU Refresh: lúc đó ô đã mang màu mới, ta chỉ dịch nó lên
        /// vị trí cũ rồi cho trượt về 0. Ô từ hàng chờ có bậc cũ nằm ngoài cửa sổ nên
        /// nó xuất phát từ phía trên bàn.
        /// </summary>
        public IEnumerator PlayFalls(List<FallStep> falls)
        {
            if (falls.Count == 0) yield break;
            BoardGeometry geo = this.level.Geometry;

            var moving = new List<KeyValuePair<CellView, float>>();
            foreach (FallStep fall in falls)
            {
                int toRow = geo.Rows - 1 - fall.ToSlotIndex;
                if (toRow < 0 || toRow >= geo.Rows) continue;
                int fromRow = geo.Rows - 1 - fall.FromSlotIndex;
                float offset = (toRow - fromRow) * this.cellSize;
                if (Mathf.Approximately(offset, 0f)) continue;

                CellView cell = this.cells[toRow * geo.Columns + fall.Column];
                if (cell.IsWall) continue;
                cell.BubbleRoot.anchoredPosition = new Vector2(0, offset);
                moving.Add(new KeyValuePair<CellView, float>(cell, offset));
            }
            if (moving.Count == 0) yield break;

            const float duration = 0.32f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // nảy nhẹ khi chạm đáy thay vì dừng khựng
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float bounce = Mathf.Sin(t * Mathf.PI) * 0.06f;
                foreach (var pair in moving)
                    pair.Key.BubbleRoot.anchoredPosition =
                        new Vector2(0, Mathf.Lerp(pair.Value, 0f, eased) - bounce * this.cellSize * Mathf.Sign(pair.Value));
                if (this.FastForward) break;
                yield return null;
            }
            foreach (var pair in moving) pair.Key.BubbleRoot.anchoredPosition = Vector2.zero;
        }

        public IEnumerator PlayHint(int[] cells)
        {
            this.scaleAnimating = true;
            const float duration = 2.1f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float pulse = 1f + Mathf.Sin(elapsed * 9f) * 0.14f;
                foreach (int i in cells)
                {
                    if (this.cells[i].IsWall) continue;
                    this.cells[i].BubbleRoot.localScale = Vector3.one * pulse;
                }
                if (this.FastForward) break;
                yield return null;
            }
            foreach (int i in cells)
                if (!this.cells[i].IsWall) this.cells[i].ResetScale();
            this.scaleAnimating = false;
        }

        private const float SlideBaseSeconds = 0.17f;
        private const float SlideMinSeconds = 0.20f;
        private const float SlideMaxSeconds = 0.60f;
        private const float SlidePopSeconds = 0.32f;

        /// <summary>
        /// Thời lượng trượt của MỘT ô, theo khoảng cách nó phải đi (tính bằng số ô).
        ///
        /// Giãn theo CĂN BẬC HAI, không theo tỉ lệ thẳng: tỉ lệ thẳng thì ô đi 9 ô mất
        /// gấp 9 lần ô đi 1 ô, chờ rất lâu; còn thời lượng cố định thì ô đi xa phải bay
        /// vọt lên vô lý. Căn bậc hai cho cảm giác vật nặng đi xa thì lâu hơn nhưng
        /// không lâu gấp bội.
        /// </summary>
        public static float SlideDuration(float distanceInCells)
        {
            if (distanceInCells <= 0f) return SlideMinSeconds;
            return Mathf.Clamp(SlideBaseSeconds * Mathf.Sqrt(distanceInCells),
                               SlideMinSeconds, SlideMaxSeconds);
        }

        private struct SlideItem
        {
            public CellView Cell;
            public Vector2 From;
            public float Duration;
            public Color FromColor;
            public Color ToColor;
            public bool Recolors;
            public string FromGlyph;
            public string ToGlyph;
        }

        /// <summary>
        /// Ô trượt từ chỗ cũ về chỗ mới sau khi xáo lại dồn ô.
        ///
        /// Gọi SAU Refresh: lúc đó ô đã mang MÀU MỚI và nằm ở CHỖ MỚI, ta chỉ dịch nó
        /// ngược về chỗ cũ rồi cho trượt về 0 — cùng thủ pháp với hoạt ảnh rơi, chỉ khác
        /// là lệch cả hai trục thay vì chỉ trục dọc.
        ///
        /// Mỗi ô có thời lượng RIÊNG theo khoảng cách, nên ô gần settle trước còn ô xa
        /// vẫn đang bay. Cho cả bàn cùng một thời lượng thì thành một nhịp nhảy đồng
        /// loạt, mất hẳn cảm giác các ô dồn lại.
        /// </summary>
        public IEnumerator PlaySlide(List<ShuffleMove> moves, PuzzleSession session)
        {
            if (moves == null || moves.Count == 0)
            {
                yield return PlayShuffle(session);
                yield break;
            }

            this.scaleAnimating = true;
            BoardGeometry geo = this.level.Geometry;
            var items = new List<SlideItem>();
            float total = SlidePopSeconds;

            foreach (ShuffleMove move in moves)
            {
                if (move.ToRow < 0 || move.ToRow >= geo.Rows) continue;      // đích trong hàng chờ
                if (move.ToColumn < 0 || move.ToColumn >= geo.Columns) continue;

                CellView cell = this.cells[move.ToRow * geo.Columns + move.ToColumn];
                if (cell.IsWall || !cell.Fill.enabled) continue;

                // y hướng LÊN trong UI còn hàng tăng xuống dưới, nên trục dọc đảo dấu
                var offset = new Vector2(
                    (move.FromColumn - move.ToColumn) * this.cellSize,
                    (move.ToRow - move.FromRow) * this.cellSize);
                if (offset.sqrMagnitude < 1f) continue;

                float duration = SlideDuration(offset.magnitude / Mathf.Max(1f, this.cellSize));
                cell.BubbleRoot.anchoredPosition = offset;

                bool recolors = move.FromColor >= 0 && move.ToColor >= 0 && move.FromColor != move.ToColor;
                var item = new SlideItem
                {
                    Cell = cell,
                    From = offset,
                    Duration = duration,
                    Recolors = recolors,
                    FromColor = recolors ? Tint(PuzzlePalette.Colors[move.FromColor]) : cell.Fill.color,
                    ToColor = recolors ? Tint(PuzzlePalette.Colors[move.ToColor]) : cell.Fill.color,
                    FromGlyph = recolors ? PuzzlePalette.Glyphs[move.FromColor] : cell.Glyph.text,
                    ToGlyph = recolors ? PuzzlePalette.Glyphs[move.ToColor] : cell.Glyph.text
                };
                // đặt màu và ký hiệu cũ ngay, không thì frame đầu đã nhảy sang cái mới
                if (recolors)
                {
                    cell.Fill.color = item.FromColor;
                    cell.Glyph.text = item.FromGlyph;
                }

                items.Add(item);
                if (duration > total) total = duration;
            }

            if (items.Count == 0) { this.scaleAnimating = false; yield break; }

            // Ô nào không trượt thì nảy nhẹ, để cả bàn trông như vừa được dựng lại chứ
            // không phải chỉ vài ô lẻ tự dịch chỗ.
            for (int i = 0; i < this.cells.Length; i++)
                if (!this.cells[i].IsWall && session.Board[i] >= 0)
                    this.cells[i].BubbleRoot.localScale = Vector3.one * 0.86f;

            float elapsed = 0f;
            while (elapsed < total)
            {
                elapsed += Time.deltaTime;

                foreach (SlideItem item in items)
                {
                    float t = Mathf.Clamp01(elapsed / item.Duration);
                    float eased = 1f - Mathf.Pow(1f - t, 3f);
                    item.Cell.BubbleRoot.anchoredPosition = Vector2.Lerp(item.From, Vector2.zero, eased);

                    // Màu chuyển theo t THÔ, không theo eased: eased xong rất sớm nên màu
                    // sẽ đổi hết ngay đoạn đầu, mắt vẫn thấy như đổi tức thời. Dồn phần
                    // chuyển màu về nửa sau để nó trùng lúc ô sắp đáp.
                    if (!item.Recolors) continue;
                    item.Cell.Fill.color = Color.Lerp(item.FromColor, item.ToColor,
                        Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.35f) / 0.65f)));

                    // Ký hiệu là Text, không trộn được như màu nền — nên cho nó MỜ HẲN
                    // rồi hiện lại, và đổi chữ đúng lúc đang trong suốt để không ai thấy
                    // khoảnh khắc nhảy chữ. Đặt lại text mỗi frame theo t nên không cần
                    // giữ cờ "đã đổi chưa".
                    if (!item.Cell.Glyph.enabled) continue;
                    item.Cell.Glyph.text = t < 0.5f ? item.FromGlyph : item.ToGlyph;
                    float fade = Mathf.SmoothStep(0f, 1f, Mathf.Abs(2f * t - 1f));
                    item.Cell.Glyph.color = new Color(0f, 0f, 0f, GlyphAlpha * fade);
                }

                // nảy về cỡ thường, hơi quá đà một chút cho có sức sống
                float pop = Mathf.Clamp01(elapsed / SlidePopSeconds);
                float scale = Mathf.Lerp(0.86f, 1f, 1f - Mathf.Pow(1f - pop, 3f))
                              + Mathf.Sin(pop * Mathf.PI) * 0.08f;
                for (int i = 0; i < this.cells.Length; i++)
                    if (!this.cells[i].IsWall && session.Board[i] >= 0)
                        this.cells[i].BubbleRoot.localScale = Vector3.one * scale;

                if (this.FastForward) break;
                yield return null;
            }

            // Chốt về trạng thái cuối. Bắt buộc phải ghi lại màu VÀ ký hiệu: vòng lặp ở
            // trên ghi đè cả hai, mà nếu FastForward cắt giữa đường thì màu đang nằm
            // giữa hai màu và ký hiệu đang trong suốt — sẽ kẹt vĩnh viễn như vậy.
            foreach (SlideItem item in items)
            {
                item.Cell.BubbleRoot.anchoredPosition = Vector2.zero;
                if (!item.Recolors) continue;
                item.Cell.Fill.color = item.ToColor;
                item.Cell.Glyph.text = item.ToGlyph;
                item.Cell.Glyph.color = new Color(0f, 0f, 0f, GlyphAlpha);
            }
            for (int i = 0; i < this.cells.Length; i++)
                if (!this.cells[i].IsWall) this.cells[i].ResetScale();
            this.scaleAnimating = false;
        }

        public IEnumerator PlayShuffle(PuzzleSession session)
        {
            this.scaleAnimating = true;
            BoardGeometry geo = this.level.Geometry;
            const float duration = 0.34f;

            for (int i = 0; i < this.cells.Length; i++)
                if (!this.cells[i].IsWall && session.Board[i] >= 0)
                    this.cells[i].BubbleRoot.localScale = Vector3.one * 0.25f;

            float elapsed = 0f;
            while (elapsed < duration + 0.3f)
            {
                elapsed += Time.deltaTime;
                for (int i = 0; i < this.cells.Length; i++)
                {
                    if (this.cells[i].IsWall || session.Board[i] < 0) continue;
                    int x = i % geo.Columns, y = i / geo.Columns;
                    float delay = (x + y) * 0.022f;
                    float t = Mathf.Clamp01((elapsed - delay) / duration);
                    float scale = t <= 0f ? 0.25f : Mathf.Lerp(0.25f, 1f, 1f - Mathf.Pow(1f - t, 3f))
                                                    + Mathf.Sin(t * Mathf.PI) * 0.12f;
                    this.cells[i].BubbleRoot.localScale = Vector3.one * scale;
                }
                if (this.FastForward) break;
                yield return null;
            }
            for (int i = 0; i < this.cells.Length; i++)
                if (!this.cells[i].IsWall) this.cells[i].ResetScale();
            this.scaleAnimating = false;
        }

        // ------------------------------------------------------------------
        // Chẩn đoán thua: mờ cả bàn, chỉ thắp sáng ô là bằng chứng
        // ------------------------------------------------------------------

        public void SetDimmed(PuzzleSession session, bool value, bool showSymbols)
        {
            this.dimmed = value;
            Refresh(session, showSymbols);
        }

        public void SetCulprit(int index, bool value, PuzzleSession session)
        {
            CellView cell = this.cells[index];
            if (cell.IsWall) return;
            int color = session.Board[index];
            if (color < 0) return;

            cell.Fill.color = value ? PuzzlePalette.Colors[color] : Tint(PuzzlePalette.Colors[color]);
            cell.Sheen.color = value ? Color.white : new Color(1, 1, 1, 0.4f);
            cell.Ring.enabled = value;
            cell.Glow.enabled = value;
            if (value)
            {
                cell.Ring.color = PuzzlePalette.Bad;
                Color glow = PuzzlePalette.Bad;
                glow.a = 0.5f;
                cell.Glow.color = glow;
            }
        }

        public void PulseCulprits(List<int> culprits, float time)
        {
            float pulse = 1f + Mathf.Sin(time * 10f) * 0.12f;
            foreach (int i in culprits)
                if (!this.cells[i].IsWall) this.cells[i].BubbleRoot.localScale = Vector3.one * pulse;
        }

        public void ResetScales()
        {
            this.scaleAnimating = false;
            this.FastForward = false;
            foreach (CellView cell in this.cells)
                if (!cell.IsWall) cell.ResetScale();
        }
    }
}
