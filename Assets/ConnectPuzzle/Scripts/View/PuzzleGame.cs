using System.Collections;
using System.Collections.Generic;
using ConnectPuzzle.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Toàn bộ tầng hiển thị + luồng game. Dựng UI bằng code nên chỉ cần thả component
    /// này vào một scene rỗng là chạy — không phải wire prefab nào.
    ///
    /// Mọi luật chơi nằm ở ConnectPuzzle.Core (thuần C#, có bộ test riêng). File này
    /// chỉ lo hiển thị, hoạt ảnh và điều hướng.
    /// </summary>
    [AddComponentMenu("Connect Puzzle/Puzzle Game")]
    public sealed class PuzzleGame : MonoBehaviour, DuelController.IHost, ItemShop.IHost
    {
        private const float DiagnosisMinSeconds = 1.9f;

        [Header("Giao diện")]
        [Tooltip("Kéo một file font (.ttf/.otf) vào đây để thay font mặc định của Unity. " +
                 "Muốn giống ảnh mẫu thì dùng font sans bo tròn — Nunito, Baloo 2, Fredoka " +
                 "hoặc Be Vietnam Pro; ba font này đều có đủ dấu tiếng Việt. " +
                 "Để trống thì dùng Liberation Sans của Unity (chữ vuông).")]
        [SerializeField] private Font uiFont;

        [Header("Vùng an toàn")]
        [Tooltip("Giả lập lề an toàn trong Editor để xem bố cục khi máy có tai thỏ, " +
                 "không cần build ra thiết bị. Trên máy thật luôn dùng Screen.safeArea.")]
        [SerializeField] private bool simulateSafeAreaInEditor = false;
        [SerializeField] private float simulatedInsetTop = 132f;
        [SerializeField] private float simulatedInsetBottom = 68f;
        [SerializeField] private float simulatedInsetLeft = 0f;
        [SerializeField] private float simulatedInsetRight = 0f;

        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private SafeAreaPanel safeArea;
        private Camera uiCamera;
        [SerializeField] private RectTransform menuScreen;
        [SerializeField] private RectTransform gameScreen;
        [SerializeField] private RectTransform boardArea;
        /// <summary>Thẻ kết ván. Component nằm trên chính node Overlay và tự giữ ref của nó.</summary>
        [SerializeField] private OverlayCard card;

        private BoardView board;
        private EffectLayer effects;
        private PuzzleAudio audioPlayer;
        [SerializeField] private BoardPointerInput pointerInput;

        [SerializeField] private Text levelNameText, levelSubText;
        [SerializeField] private Text movesText, movesMaxText, cellsText, scoreText;
        [SerializeField] private Text movesLabel, cellsLabel;
        [SerializeField] private Text parText, queueText, chainPreviewText;
        [SerializeField] private Text[] starTexts;
        [SerializeField] private RectTransform chainPreview;
        [SerializeField] private Button undoButton, shuffleButton, hintButton, restartButton, soundButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Text undoCountText, shuffleCountText;
        [SerializeField] private DiagnosisBanner diagBanner;

        private PuzzleSession session;
        private LevelData level;
        private int levelIndex;
        private bool busy;
        private bool dragging;
        private int displayedScore;
        private Coroutine diagnosisRoutine;
        private Coroutine scoreRoutine;
        private Vector2 lastBoardArea;

        // ==================================================================
        // Dựng UI
        // ==================================================================

        /// <summary>
        /// Tên prefab trong Resources, dùng chung giữa runtime và script editor.
        /// Để ở đây chứ không nằm lẫn trong vùng "đấu seed" — hai thứ này không liên quan
        /// gì tới đấu, chúng chỉ tình cờ được thêm vào lúc đang sửa vùng đó.
        /// </summary>
        public const string ItemPanelResourcePath = "UI/ItemPanel";
        public const string LevelButtonResourcePath = "UI/LevelButton";

        private bool built;

        /// <summary>Bàn đang hiển thị — công khai để kiểm thử toán toạ độ.</summary>
        public BoardView BoardVisual => this.board;

        /// <summary>Node vùng an toàn — công khai để kiểm thử bố cục khi có tai thỏ.</summary>
        public SafeAreaPanel SafeArea => this.safeArea;

        public PuzzleSession CurrentSession => this.session;

        private void Awake()
        {
            BuildAll();
        }

        /// <summary>
        /// Dựng UI bằng code.
        ///
        /// GIAI ĐOẠN CHUYỂN TIẾP: khi UI đến từ prefab gốc thì mọi tham chiếu đã được nối
        /// sẵn, và dựng thêm lần nữa sẽ tạo ra một bộ UI thứ hai chồng lên bộ có sẵn. Kiểm
        /// canvas là đủ để phân biệt: nó là thứ đầu tiên BuildAll tạo ra.
        ///
        /// Hàm này sẽ bị XOÁ ở bước cuối, khi prefab là nguồn duy nhất. Tới lúc đó không
        /// còn hai đường nữa.
        /// </summary>
        public void BuildAll()
        {
            if (this.built) return;

            if (this.canvas != null)
            {
                this.built = true;
                // Vẫn phải chạy: font ảnh hưởng những Text dựng ĐỘNG lúc chơi (thẻ overlay),
                // còn camera là đồ của scene nên prefab không lưu được tham chiếu tới nó —
                // hai thứ này không phải là "dựng UI", nên không sinh bản sao nào.
                if (this.uiFont != null) Ui.OverrideFont = this.uiFont;
                BuildCamera();
                BuildLevelGrid();     // lưới màn là nội dung theo tiến trình, luôn dựng lúc chạy
                WireAll();
                this.audioPlayer = new PuzzleAudio(this.gameObject) { Enabled = PuzzleProgress.Sound };
                return;
            }

            this.built = true;
            // phải đặt TRƯỚC khi dựng: Text lấy font ngay lúc tạo, gán sau không đổi được
            if (this.uiFont != null) Ui.OverrideFont = this.uiFont;
            BuildCamera();
            BuildCanvas();
            BuildMenuScreen();
            BuildGameScreen();
            BuildOverlay();
            BuildToast();
            WireAll();
            this.audioPlayer = new PuzzleAudio(this.gameObject) { Enabled = PuzzleProgress.Sound };
        }


        /// <summary>
        /// Nối HÀNH VI vào cây UI: listener nút, delegate chạm, và những đối tượng C# thuần
        /// không serialize được.
        ///
        /// Tách riêng khỏi việc dựng vì UI có thể đến từ prefab, mà prefab CHỈ lưu được hình
        /// dạng và tham chiếu — AddListener trong code là đăng ký lúc chạy, Unity không ghi
        /// nó vào file. Bỏ qua bước này thì cả bàn phím nút im lặng đúng nghĩa: nhìn vẫn
        /// đẹp, bấm không ra gì.
        ///
        /// Gọi được nhiều lần: mọi nút đều RemoveAllListeners trước.
        /// </summary>
        private void WireAll()
        {
            this.safeArea.SimulateInsets = this.simulateSafeAreaInEditor;
            this.safeArea.SimulatedInsets = new Vector4(
                this.simulatedInsetLeft, this.simulatedInsetTop,
                this.simulatedInsetRight, this.simulatedInsetBottom);
            this.safeArea.Apply();

            // BoardView/EffectLayer là lớp C# thuần, prefab không giữ được. Hai hàm dựng
            // của chúng NHẬN LẠI node có sẵn nên gọi ở đây không sinh bản sao.
            this.board = new BoardView(this.boardArea);
            this.effects = new EffectLayer(this, this.gameScreen);
            this.effects.AttachFlash(this.boardArea);
            if (this.diagBanner != null) this.diagBanner.Wire();

            // DuelLanLink là MonoBehaviour: prefab giữ được component nhưng event C# thì
            // không, nên phải dựng lại controller và đăng ký lại mỗi lần chạy.
            if (this.lan == null) this.lan = gameObject.AddComponent<DuelLanLink>();

            this.pointerInput.Configure(null);                   // ScreenSpaceOverlay -> camera null
            this.pointerInput.PointerDown = OnPointerDown;
            this.pointerInput.PointerDrag = OnPointerDrag;
            this.pointerInput.PointerUp = OnPointerUp;

            // ---- menu
            Bind(this.menuEndlessButton, OpenEndless);
            Bind(this.menuDailyButton, OpenDaily);
            Bind(this.menuDuelButton, () => this.duel.OpenPanel());
            Bind(this.menuSoundButton, ToggleSound);
            Bind(this.menuSymbolButton, ToggleSymbols);
            Bind(this.menuFreeButton, () =>
            {
                PuzzleProgress.FreePlay = !PuzzleProgress.FreePlay;
                RefreshMenu();
                Toast(PuzzleProgress.FreePlay
                    ? "Chơi tự do: vào thẳng màn nào cũng được. Sao và điểm vẫn được ghi."
                    : "Đã tắt chơi tự do — mở màn theo tiến trình như cũ.");
            });
            Bind(this.menuResetButton, () =>
            {
                PuzzleProgress.ResetAll(LevelCatalog.Levels.Length);
                RefreshMenu();
            });

            // ---- màn chơi
            Bind(this.backButton, OnBackFromGame);
            Bind(this.soundButton, ToggleSound);
            Bind(this.undoButton, OnUndo);
            Bind(this.shuffleButton, OnShuffle);
            Bind(this.hintButton, OnHint);
            Bind(this.restartButton, RestartLevel);

            // ---- vật phẩm
            this.items = new ItemShop(this, this.itemButton, this.itemBalanceText,
                                      this.itemPanel, this.itemPanelCatcher,
                                      this.itemRows, this.itemRowCosts, this.itemWalletText);
            this.items.Wire();

            // ---- đấu seed + Wi-Fi: một lớp riêng lo cả ba đường (mã, kết quả, mạng)
            this.duel = new DuelController(this, this.duelPanel, this.duelCatcher, this.duelView,
                                           this.lan, this.lanCatcher, this.lanView);
            this.duel.Wire();
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void Start()
        {
            ShowMenu();
        }

        private void BuildCamera()
        {
            this.uiCamera = Camera.main;
            if (this.uiCamera == null)
            {
                var go = new GameObject("PuzzleCamera");
                go.transform.SetParent(this.transform, false);
                this.uiCamera = go.AddComponent<Camera>();
                this.uiCamera.tag = "MainCamera";
            }
            this.uiCamera.orthographic = true;
            this.uiCamera.clearFlags = CameraClearFlags.SolidColor;
            this.uiCamera.backgroundColor = PuzzlePalette.Background;
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("PuzzleCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(this.transform, false);
            this.canvas = canvasGo.AddComponent<Canvas>();
            this.canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            // PHẢI là MatchWidthOrHeight. Với Expand, Unity lấy min(w/refW, h/refH) và
            // `matchWidthOrHeight` bên dưới bị BỎ QUA hoàn toàn — nên trên màn 640x480
            // hệ số thành 0.25 và chiều rộng logic ra 2560 thay vì 1080, kéo lệch mọi số
            // đo trong file này. Đúng triệu chứng smoke test bắt được.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // Khớp theo CHIỀU RỘNG (0), không phải 0.5: mọi số đo trong file này đều tính
            // trên chiều rộng logic 1080 (lề 30, ba ô HUD chia đều 1080...). Với 0.5 thì
            // chiều rộng logic đổi theo tỉ lệ màn hình và bố cục ngang bị lệch.
            scaler.matchWidthOrHeight = 0f;
            canvasGo.AddComponent<GraphicRaycaster>();

            if (EventSystem.current == null)
            {
                var eventGo = new GameObject("EventSystem", typeof(EventSystem));
                eventGo.transform.SetParent(this.transform, false);
                eventGo.AddComponent<StandaloneInputModule>();
            }

            // Nền TRÀN HẾT màn hình, cố ý nằm ngoài safe area: màu phải phủ cả dưới tai
            // thỏ và vạch home, đúng như viewport-fit=cover của bản HTML.
            Image background = Ui.Image("Background", canvasGo.transform, Color.white,
                PuzzleSprites.BackgroundGradient);
            Ui.Stretch(background.rectTransform, 0, 0, 0, 0);

            // Toàn bộ NỘI DUNG nằm trong vùng an toàn.
            this.contentRoot = Ui.Node("SafeArea", canvasGo.transform);
            Ui.Stretch(this.contentRoot, 0, 0, 0, 0);
            this.safeArea = this.contentRoot.gameObject.AddComponent<SafeAreaPanel>();
        }

        // ------------------------------------------------------------------

        private void BuildMenuScreen()
        {
            this.menuScreen = Ui.Node("MenuScreen", this.contentRoot);
            Ui.Stretch(this.menuScreen, 0, 0, 0, 0);

            Text title = Ui.Text("Title", this.menuScreen, "Connect Puzzle", 78, PuzzlePalette.Foreground,
                TextAnchor.UpperCenter, FontStyle.Bold);
            Ui.TopBand(title.rectTransform, 70, 95, 40);

            Text subtitle = Ui.Text("Subtitle", this.menuScreen,
                "Nối các ô cùng màu. Dọn sạch bàn trong số lượt cho phép", 30,
                PuzzlePalette.Dim, TextAnchor.UpperCenter);
            Ui.TopBand(subtitle.rectTransform, 172, 50, 60);

            // Ví sao. Nằm ĐÈ lên dải tiêu đề chứ không chiếm dải riêng: tiêu đề căn
            // giữa nên hai mép luôn trống, và menu đã phải nhường chỗ cho hai nút chế
            // độ rồi — thêm một dải nữa là lưới màn bị đẩy xuống lần thứ ba.
            //
            // Trước đây số dư này KHÔNG hiện ở đâu cả: người chơi chỉ thấy nút vật phẩm
            // xám đi mà không biết mình có bao nhiêu sao, cũng không biết còn thiếu mấy.
            this.menuWalletText = Ui.Text("Wallet", this.menuScreen, "", 30,
                PuzzlePalette.Star, TextAnchor.UpperRight, FontStyle.Bold);
            this.menuWalletText.supportRichText = true;

            // Nút Vô tận nằm NGAY DƯỚI tiêu đề chứ không lẫn trong footer: nó là một
            // chế độ chơi riêng, không phải một tuỳ chọn.
            this.menuEndlessButton = Ui.Button("MenuEndless", this.menuScreen, "", 32,
                PuzzlePalette.Accent, new Color(0.05f, 0.06f, 0.14f), PuzzlePalette.RadiusPanel, true);

            // Thử thách hôm nay: một bàn dùng chung cho mọi máy, đổi lúc 0h UTC.
            this.menuDailyButton = Ui.Button("MenuDaily", this.menuScreen, "", 32,
                PuzzlePalette.Good, new Color(0.05f, 0.06f, 0.14f), PuzzlePalette.RadiusPanel, true);

            // Đấu seed: cùng một mã ra cùng một bàn trên mọi máy — đã đo trên ARM64
            // thật, không phải giả định (xem BoardFingerprint).
            this.menuDuelButton = Ui.Button("MenuDuel", this.menuScreen, "⚔  Đấu seed bạn bè", 32,
                PuzzlePalette.Foreground, PuzzlePalette.Panel, PuzzlePalette.RadiusPanel, true);

            BuildDuelPanel();
            BuildLanPanel();

            BuildLevelScroll();
            BuildLevelGrid();
            BuildMenuFooter();

            // bố cục ngay một lần để không phần tử nào ở trạng thái cỡ 0
            LayoutMenu();
        }

        // --- các số đo cố định của menu, tính theo chiều rộng logic 1080 ---
        private const int MenuColumns = 5;
        private const float MenuGap = 18f;
        private const float MenuSideMargin = 45f;
        private const float MenuHeaderHeight = 58f;
        private const float MenuWorldSpacing = 26f;
        private const float MenuFooterHeight = 300f;    // ba hàng: 2 nút gạt + chơi tự do + link

        /// <summary>Kết quả tính bố cục menu cho một kích thước khung cụ thể.</summary>
        public struct MenuMetrics
        {
            public int Columns;
            public float ButtonSize;
            public float HeaderTop;      // chiều cao khối tiêu đề
            public float HeaderHeight;   // chiều cao một nhãn thế giới
            public float FooterHeight;
            public int TotalRows;
            public float GridBottom;     // đáy lưới, đo từ đỉnh khung
            public float ContentHeight;  // chiều cao phần cuộn được
        }

        /// <summary>
        /// Tính bố cục menu cho khung cỡ này. Hàm THUẦN nên kiểm được bất biến "lưới
        /// luôn nằm trên footer" trên mọi tỉ lệ màn hình.
        ///
        /// Ba thứ cùng thích ứng, vì chỉ đổi cỡ nút là không đủ: trên canvas logic cao
        /// 810 thì riêng chrome đã 592px, còn 24 nút 6 hàng không cách nào vừa.
        ///  - chrome (tiêu đề, nhãn thế giới, footer) co lại trên màn thấp
        ///  - số CỘT tăng lên để bớt hàng
        ///  - cỡ nút lấy theo cả hai chiều
        /// </summary>
        public static MenuMetrics ComputeMenuMetrics(float width, float height, int[] worldSizes)
        {
            return ComputeMenuMetrics(width, height, worldSizes, -1f);
        }

        /// <summary>
        /// headerTop &lt; 0 nghĩa là ước lượng theo chiều cao khung; truyền số dương khi đã
        /// ĐO được chiều cao chữ thật, để khối tiêu đề không lấn xuống lưới.
        /// </summary>
        public static MenuMetrics ComputeMenuMetrics(float width, float height, int[] worldSizes,
                                                    float headerTop)
        {
            // 0 ở màn thấp, 1 ở màn cao — dùng để co chrome
            float tall = Mathf.Clamp01((height - 900f) / 1000f);

            var best = new MenuMetrics
            {
                HeaderTop = headerTop > 0f ? headerTop : Mathf.Lerp(112f, 250f, tall),
                HeaderHeight = Mathf.Lerp(42f, 58f, tall),
                FooterHeight = Mathf.Lerp(200f, 281f, tall),
                Columns = MenuColumns,
                ButtonSize = 0f
            };

            // Lưới màn giờ NẰM TRONG VÙNG CUỘN, nên cỡ nút không còn phải ép cho vừa
            // chiều cao — với 70 màn thì ép kiểu cũ ra nút 40px mà vẫn tràn. Cỡ nút chỉ
            // lấy theo chiều RỘNG, phần cao bao nhiêu thì cuộn bấy nhiêu.
            //
            // Số cột vẫn tăng trên màn hẹp-cao để một màn hình thấy được nhiều màn hơn,
            // nhưng không được nhỏ hơn ngưỡng bấm được.
            for (int columns = MenuColumns; columns <= 8; columns++)
            {
                int rows = 0;
                foreach (int count in worldSizes) rows += (count + columns - 1) / columns;
                if (rows == 0) continue;

                float size = (width - MenuSideMargin * 2f - MenuGap * (columns - 1)) / columns;
                if (size < 88f && columns > MenuColumns) continue;   // nhỏ quá thì thôi

                if (best.ButtonSize > 0f && size <= best.ButtonSize) continue;
                best.ButtonSize = size;
                best.Columns = columns;
                best.TotalRows = rows;
            }
            if (best.ButtonSize <= 0f)
            {
                best.Columns = MenuColumns;
                best.ButtonSize = Mathf.Max(40f,
                    (width - MenuSideMargin * 2f - MenuGap * (MenuColumns - 1)) / MenuColumns);
                best.TotalRows = 0;
                foreach (int count in worldSizes) best.TotalRows += (count + MenuColumns - 1) / MenuColumns;
            }

            // Chiều cao NỘI DUNG cuộn — đo từ đầu lưới, không tính khối tiêu đề.
            best.ContentHeight = worldSizes.Length * best.HeaderHeight
                               + (worldSizes.Length - 1) * MenuWorldSpacing
                               + best.TotalRows * (best.ButtonSize + MenuGap);

            best.GridBottom = best.HeaderTop + best.ContentHeight;
            return best;
        }

        /// <summary>Số màn của từng thế giới, theo thứ tự xuất hiện.</summary>
        public static int[] WorldSizes()
        {
            var sizes = new List<int>();
            int lastWorld = -1;
            foreach (LevelConfig cfg in LevelCatalog.Levels)
            {
                if (cfg.World != lastWorld) { sizes.Add(0); lastWorld = cfg.World; }
                sizes[sizes.Count - 1]++;
            }
            return sizes.ToArray();
        }

        private sealed class WorldHeader
        {
            public int World;
            public Text Label;
        }

        private readonly List<WorldHeader> worldHeaders = new List<WorldHeader>();

        [SerializeField] private RectTransform levelViewport, levelContent;
        [SerializeField] private ScrollRect levelScroll;

        /// <summary>
        /// Vùng cuộn cho danh sách màn.
        ///
        /// Bắt buộc phải có từ khi bảng màn lên 70: trên màn 4:3 lưới cao 1509 mà chỗ
        /// trống chỉ tới 1255 — không cỡ nút nào cứu được, vì ép nhỏ nữa thì nút tụt
        /// dưới ngưỡng bấm được mà vẫn tràn.
        ///
        /// Dùng RectMask2D chứ không Mask: Mask cần thêm một Image làm khuôn và tốn một
        /// lượt stencil, còn ở đây chỉ cần cắt theo hình chữ nhật.
        /// </summary>
        private void BuildLevelScroll()
        {
            this.levelViewport = Ui.Node("LevelViewport", this.menuScreen);
            this.levelViewport.gameObject.AddComponent<RectMask2D>();

            this.levelContent = Ui.Node("LevelContent", this.levelViewport);
            this.levelContent.anchorMin = new Vector2(0, 1);
            this.levelContent.anchorMax = new Vector2(1, 1);
            this.levelContent.pivot = new Vector2(0.5f, 1);

            this.levelScroll = this.levelViewport.gameObject.AddComponent<ScrollRect>();
            this.levelScroll.content = this.levelContent;
            this.levelScroll.viewport = this.levelViewport;
            this.levelScroll.horizontal = false;
            this.levelScroll.vertical = true;
            this.levelScroll.movementType = ScrollRect.MovementType.Elastic;
            this.levelScroll.elasticity = 0.08f;
            this.levelScroll.inertia = true;
            this.levelScroll.decelerationRate = 0.12f;
            this.levelScroll.scrollSensitivity = 34f;
        }

        /// <summary>Prefab nút chọn màn, nạp một lần rồi instantiate 90 bản.</summary>
        [SerializeField] private GameObject levelButtonPrefab;

        private void BuildLevelGrid()
        {
            // Nạp MỘT lần. Resources.Load trong vòng lặp 90 vòng là 90 lần tra bảng asset,
            // và nếu thiếu thì báo lỗi 90 lần thay vì một lần.
            this.levelButtonPrefab = Resources.Load<GameObject>(LevelButtonResourcePath);
            if (this.levelButtonPrefab == null)
            {
                Debug.LogError("[UI] Thiếu prefab " + LevelButtonResourcePath +
                               ". Chạy menu Connect Puzzle > Dựng lại prefab nút chọn màn.");
                return;
            }

            int lastWorld = -1;

            // Gọi lại được: danh sách phải sạch trước, không thì mỗi lần gọi lại nhân đôi
            // số mục và RefreshMenu ghi hai lần lên cùng một nút.
            this.worldHeaders.Clear();
            this.levelButtons.Clear();

            for (int i = 0; i < LevelCatalog.Levels.Length; i++)
            {
                LevelConfig cfg = LevelCatalog.Levels[i];

                if (cfg.World != lastWorld)
                {
                    lastWorld = cfg.World;
                    RectTransform reusedHeader = Ui.Reuse("World" + cfg.World, this.levelContent);
                    Text header = reusedHeader != null
                        ? reusedHeader.GetComponent<Text>()
                        : Ui.Text("World" + cfg.World, this.levelContent,
                            LevelCatalog.WorldName(cfg.World).ToUpperInvariant(), 27, PuzzlePalette.Dim,
                            TextAnchor.MiddleLeft, FontStyle.Bold);
                    this.worldHeaders.Add(new WorldHeader { World = cfg.World, Label = header });
                }

                int captured = i;

                // Dùng lại nút có sẵn nếu UI đến từ prefab, không thì instantiate.
                // Tên "Level{N}" là hợp đồng: bài kiểm và deep-link tìm nút theo tên đó,
                // nên nó cũng chính là khoá để nhận lại.
                RectTransform reused = Ui.Reuse("Level" + (i + 1), this.levelContent);
                GameObject instance = reused != null
                    ? reused.gameObject
                    : Instantiate(this.levelButtonPrefab, this.levelContent, false);
                instance.name = "Level" + (i + 1);

                var view = instance.GetComponent<LevelButtonView>();
                if (view == null)
                {
                    Debug.LogError("[UI] Prefab nút chọn màn thiếu LevelButtonView.");
                    continue;
                }
                if (i == 0)
                {
                    System.Collections.Generic.List<string> missing = view.MissingFields();
                    if (missing.Count > 0)
                        Debug.LogError("[UI] Prefab nút chọn màn chưa gán: " +
                                       string.Join(", ", missing));
                }

                Button button = view.Button;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnLevelPicked(captured));

                Text label = view.Label;
                Text stars = view.Stars;
                view.SetGravity(cfg.Gravity);
                Text badge = cfg.Gravity ? view.GravityBadge.GetComponent<Text>() : null;

                this.levelButtons.Add(new LevelButton
                {
                    Index = i, Button = button, Label = label, Stars = stars, Badge = badge
                });
            }
        }

        /// <summary>
        /// Đặt lại vị trí toàn bộ menu theo kích thước hiện có. Gọi khi mở menu và khi
        /// khung đổi (quay máy, đổi lề an toàn).
        /// </summary>
        private void LayoutMenu()
        {
            // Trước lượt layout đầu tiên của Canvas, rect còn bằng 0. Dùng độ phân giải
            // tham chiếu thay vì bỏ qua — bỏ qua thì nút giữ nguyên cỡ 0x0 và biến mất.
            Vector2 real = this.menuScreen.rect.size;
            bool canMeasure = real.x >= 1f && real.y >= 1f;
            Vector2 size = canMeasure ? real : new Vector2(1080f, 1920f);

            Text title = this.menuScreen.Find("Title").GetComponent<Text>();
            Text subtitle = this.menuScreen.Find("Subtitle").GetComponent<Text>();

            // --- Lượt 1: chốt cỡ chữ và chiều rộng, rồi ĐO chiều cao thật.
            // Trước đây khối tiêu đề lấy theo tỉ lệ chiều cao khung, nên khi phụ đề tự
            // xuống 2 dòng nó lấn xuống nhãn thế giới — đúng lỗi thấy trên ảnh chụp.
            MenuMetrics guess = ComputeMenuMetrics(size.x, size.y, WorldSizes());
            const float titleTop = 44f;
            const float titleGap = 23f;
            const float headerBottomPad = 22f;
            const float titleMargin = 40f;
            const float subtitleMargin = 60f;

            title.fontSize = Mathf.RoundToInt(Mathf.Clamp(guess.HeaderTop * 0.34f, 40f, 78f));
            subtitle.fontSize = Mathf.RoundToInt(Mathf.Clamp(guess.HeaderTop * 0.15f, 20f, 34f));
            Ui.TopBand(title.rectTransform, titleTop, guess.HeaderTop * 0.42f, titleMargin);
            Ui.TopBand(subtitle.rectTransform, titleTop + 10f, guess.HeaderTop * 0.32f, subtitleMargin);

            // Chỉ ĐO khi rect đã có kích thước thật. Rect rộng 0 thì mỗi chữ tự xuống một
            // dòng, preferredHeight ra số vô nghĩa và khối tiêu đề đẩy hết lưới ra ngoài.
            float titleHeight = canMeasure ? Ui.MeasureTextHeight(title) : guess.HeaderTop * 0.42f;
            float subtitleHeight = canMeasure ? Ui.MeasureTextHeight(subtitle) : guess.HeaderTop * 0.32f;

            // --- Lượt 2: bố cục lại với chiều cao ĐÃ ĐO.
            // Nút Vô tận chen giữa khối tiêu đề và lưới màn nên nó phải được cộng vào
            // headerTop TRƯỚC khi tính bố cục, chứ không phải đặt xen vào sau — đặt sau
            // thì lưới vẫn nghĩ mình bắt đầu ở chỗ cũ và nút đè lên nhãn thế giới.
            // Hai nút chế độ xếp CHỒNG chứ không cạnh nhau: nhãn của chúng là câu có số
            // ("kỷ lục 12345", "chuỗi 7 ngày"), nửa bề ngang không đủ chỗ và chữ sẽ bị cắt
            // đúng ở phần mang thông tin.
            const float endlessHeight = 92f;
            const float endlessGap = 18f;
            const float dailyHeight = 92f;
            const float dailyGap = 14f;
            const float duelHeight = 78f;
            const float duelGap = 12f;
            float modeBlock = duelHeight + duelGap + dailyHeight + dailyGap + endlessHeight + endlessGap;
            float titleBlock = titleTop + titleHeight + titleGap + subtitleHeight + headerBottomPad;

            float headerTop = canMeasure
                ? titleBlock + modeBlock
                : -1f;                                   // âm = để ComputeMenuMetrics tự ước lượng
            MenuMetrics m = ComputeMenuMetrics(size.x, size.y, WorldSizes(), headerTop);

            Ui.TopBand(title.rectTransform, titleTop, titleHeight, titleMargin);
            Ui.TopBand(subtitle.rectTransform, titleTop + titleHeight + titleGap, subtitleHeight,
                       subtitleMargin);

            if (this.menuWalletText != null)
            {
                // Neo vào ĐÚNG dải tiêu đề, lề phải hẹp hơn lề của tiêu đề để không đè
                // lên chữ khi tên game dài ra.
                Ui.TopBand(this.menuWalletText.rectTransform, titleTop + 8f,
                           Mathf.Max(34f, titleHeight * 0.45f), 34f);
            }

            float endlessTop = Mathf.Max(titleBlock + dailyHeight + dailyGap,
                                         m.HeaderTop - endlessHeight - endlessGap);
            float dailyTop = Mathf.Max(titleBlock + duelHeight + duelGap,
                                       endlessTop - dailyGap - dailyHeight);
            float duelTop = Mathf.Max(titleBlock, dailyTop - duelGap - duelHeight);

            if (this.menuDuelButton != null)
            {
                Ui.TopBand(this.menuDuelButton.GetComponent<RectTransform>(),
                           duelTop, duelHeight, 60f);
                Ui.SetButtonRadius(this.menuDuelButton,
                    Ui.SafeRadius(size.x - 120f, duelHeight, PuzzlePalette.RadiusPanel));
                Ui.LabelOf(this.menuDuelButton).fontSize =
                    Mathf.RoundToInt(Mathf.Clamp(duelHeight * 0.36f, 22f, 32f));
            }

            if (this.menuDailyButton != null)
            {
                Ui.TopBand(this.menuDailyButton.GetComponent<RectTransform>(),
                           dailyTop, dailyHeight, 60f);
                Ui.SetButtonRadius(this.menuDailyButton,
                    Ui.SafeRadius(size.x - 120f, dailyHeight, PuzzlePalette.RadiusPanel));
                Ui.LabelOf(this.menuDailyButton).fontSize =
                    Mathf.RoundToInt(Mathf.Clamp(dailyHeight * 0.35f, 22f, 34f));
            }

            if (this.menuEndlessButton != null)
            {
                // đặt ngay trên lưới, đo ngược từ mốc lưới lên cho khớp mọi tỉ lệ màn hình
                Ui.TopBand(this.menuEndlessButton.GetComponent<RectTransform>(),
                           endlessTop, endlessHeight, 60f);
                Ui.SetButtonRadius(this.menuEndlessButton,
                    Ui.SafeRadius(size.x - 120f, endlessHeight, PuzzlePalette.RadiusPanel));
                Ui.LabelOf(this.menuEndlessButton).fontSize =
                    Mathf.RoundToInt(Mathf.Clamp(endlessHeight * 0.35f, 22f, 34f));
            }

            int columns = m.Columns;
            float buttonSize = m.ButtonSize;
            float gridWidth = columns * buttonSize + (columns - 1) * MenuGap;
            float left = (size.x - gridWidth) * 0.5f;      // căn giữa, không dính lề trái

            // Bán kính bo phải giảm theo cỡ nút, không thì nút nhỏ hơn 2 lần bán kính
            // sẽ có hai border 9-slice chồng nhau và góc bo hiện ra khuyết.
            int radius = Ui.SafeRadius(buttonSize, buttonSize, PuzzlePalette.RadiusSmall);

            // Vùng cuộn chiếm đúng khoảng giữa khối tiêu đề và footer.
            float viewportTop = m.HeaderTop;
            float viewportHeight = Mathf.Max(160f, size.y - viewportTop - m.FooterHeight);
            Ui.TopBand(this.levelViewport, viewportTop, viewportHeight, 0f);
            this.levelContent.sizeDelta = new Vector2(0, m.ContentHeight);
            this.levelContent.anchoredPosition = new Vector2(
                0, Mathf.Clamp(this.levelContent.anchoredPosition.y, 0f,
                               Mathf.Max(0f, m.ContentHeight - viewportHeight)));

            // Bên trong vùng cuộn, y đo từ ĐẦU nội dung chứ không từ đỉnh màn hình.
            float y = 0f;
            int lastWorld = -1;
            int headerIndex = -1;

            for (int i = 0; i < LevelCatalog.Levels.Length; i++)
            {
                LevelConfig cfg = LevelCatalog.Levels[i];

                if (cfg.World != lastWorld)
                {
                    if (lastWorld != -1) y += MenuWorldSpacing;
                    lastWorld = cfg.World;
                    headerIndex++;
                    Text header = this.worldHeaders[headerIndex].Label;
                    Ui.TopBand(header.rectTransform, y, m.HeaderHeight, left);
                    header.fontSize = Mathf.RoundToInt(Mathf.Clamp(m.HeaderHeight * 0.47f, 18f, 27f));
                    y += m.HeaderHeight;
                }

                int indexInWorld = CountBefore(i, cfg.World);
                int row = indexInWorld / columns;
                int column = indexInWorld % columns;

                LevelButton entry = this.levelButtons[i];
                RectTransform rect = entry.Button.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(buttonSize, buttonSize);
                rect.anchoredPosition = new Vector2(
                    left + column * (buttonSize + MenuGap),
                    -(y + row * (buttonSize + MenuGap)));
                Ui.SetButtonRadius(entry.Button, radius);

                entry.Label.fontSize = Mathf.RoundToInt(Mathf.Max(14f, buttonSize * 0.24f));
                Ui.Stretch(entry.Label.rectTransform, 0, 0, buttonSize * 0.07f, buttonSize * 0.3f);
                entry.Stars.fontSize = Mathf.RoundToInt(Mathf.Max(9f, buttonSize * 0.12f));
                entry.Stars.rectTransform.offsetMin = new Vector2(0, buttonSize * 0.08f);
                entry.Stars.rectTransform.offsetMax = new Vector2(0, buttonSize * 0.24f);
                if (entry.Badge != null)
                    entry.Badge.fontSize = Mathf.RoundToInt(Mathf.Max(10f, buttonSize * 0.13f));

                if (i + 1 >= LevelCatalog.Levels.Length || LevelCatalog.Levels[i + 1].World != cfg.World)
                    y += (row + 1) * (buttonSize + MenuGap);
            }

            LayoutMenuFooter(size, m);
        }

        /// <summary>Footer co theo chiều cao khung, và bán kính bo giảm theo cỡ nút.</summary>
        private void LayoutMenuFooter(Vector2 size, MenuMetrics m)
        {
            // Footer giờ có BA hàng: hai nút gạt, nút chơi tự do, rồi link xoá tiến độ.
            float toggleHeight = Mathf.Clamp(m.FooterHeight * 0.28f, 58f, 84f);
            float linkHeight = Mathf.Clamp(m.FooterHeight * 0.22f, 46f, 64f);
            float toggleWidth = Mathf.Min(320f, (size.x - 60f) * 0.5f - 8f);
            float wideWidth = Mathf.Min(654f, size.x - 60f);

            float rowTop = m.FooterHeight - toggleHeight - 18f;
            float rowFree = Mathf.Max(linkHeight + 24f, rowTop - toggleHeight - 12f);
            float rowLink = 18f;

            PlaceBottomRow(this.menuSoundButton.GetComponent<RectTransform>(),
                rowTop, 0, 2, toggleWidth, toggleHeight);
            PlaceBottomRow(this.menuSymbolButton.GetComponent<RectTransform>(),
                rowTop, 1, 2, toggleWidth, toggleHeight);
            PlaceBottomRow(this.menuFreeButton.GetComponent<RectTransform>(),
                rowFree, 0, 1, wideWidth, toggleHeight);
            PlaceBottomRow(this.menuResetButton.GetComponent<RectTransform>(),
                rowLink, 0, 1, toggleWidth, linkHeight);

            Ui.SetButtonRadius(this.menuFreeButton,
                Ui.SafeRadius(wideWidth, toggleHeight, PuzzlePalette.RadiusPanel));
            Ui.LabelOf(this.menuFreeButton).fontSize =
                Mathf.RoundToInt(Mathf.Clamp(toggleHeight * 0.34f, 20f, 30f));

            int toggleRadius = Ui.SafeRadius(toggleWidth, toggleHeight, PuzzlePalette.RadiusPanel);
            Ui.SetButtonRadius(this.menuSoundButton, toggleRadius);
            Ui.SetButtonRadius(this.menuSymbolButton, toggleRadius);
            Ui.SetButtonRadius(this.menuResetButton,
                Ui.SafeRadius(toggleWidth, linkHeight, PuzzlePalette.RadiusChip));

            int fontSize = Mathf.RoundToInt(Mathf.Clamp(toggleHeight * 0.34f, 20f, 30f));
            Ui.LabelOf(this.menuSoundButton).fontSize = fontSize;
            Ui.LabelOf(this.menuSymbolButton).fontSize = fontSize;
            Ui.LabelOf(this.menuResetButton).fontSize = Mathf.RoundToInt(Mathf.Clamp(linkHeight * 0.36f, 18f, 26f));
        }

        private sealed class LevelButton
        {
            public int Index;
            public Button Button;
            public Text Label;
            public Text Stars;
            public Text Badge;
        }

        private readonly List<LevelButton> levelButtons = new List<LevelButton>();
        [SerializeField] private Button menuSoundButton, menuSymbolButton, menuResetButton, menuFreeButton, menuEndlessButton;
        [SerializeField] private Button menuDailyButton;
        [SerializeField] private Text menuWalletText;

        /// <summary>Khoá ngày của ván thử thách đang chơi; 0 nếu không phải thử thách.</summary>
        private int dailyKey;

        private static int CountBefore(int index, int world)
        {
            int n = 0;
            for (int i = 0; i < index; i++) if (LevelCatalog.Levels[i].World == world) n++;
            return n;
        }

        private void BuildMenuFooter()
        {
            // Cao 88 chứ không 78: bán kính 38 cần phần tử cao >= 76, sát quá thì làm
            // tròn số dưới pixel là góc bo bị khuyết.
            // Footer neo vào ĐÁY, không chạy theo lưới màn. Neo theo lưới thì trên máy
            // cao nó dính lên giữa và chừa một dải trống lớn phía dưới.
            this.menuSoundButton = Ui.Button("MenuSound", this.menuScreen, "", 30,
                PuzzlePalette.Panel, PuzzlePalette.Dim);
            PlaceBottomRow(this.menuSoundButton.GetComponent<RectTransform>(), 128, 0, 2, 320, 88);

            this.menuSymbolButton = Ui.Button("MenuSymbols", this.menuScreen, "", 30,
                PuzzlePalette.Panel, PuzzlePalette.Dim);
            PlaceBottomRow(this.menuSymbolButton.GetComponent<RectTransform>(), 128, 1, 2, 320, 88);

            this.menuFreeButton = Ui.Button("MenuFree", this.menuScreen, "", 28,
                PuzzlePalette.Panel, PuzzlePalette.Dim);
            PlaceBottomRow(this.menuFreeButton.GetComponent<RectTransform>(), 226, 0, 1, 654, 84);

            // nút dạng link: nền trong suốt, không viền, bo nhỏ vì nó thấp
            this.menuResetButton = Ui.Button("MenuReset", this.menuScreen, "Xoá tiến độ", 26,
                new Color(0, 0, 0, 0), PuzzlePalette.Dim, 24, false, false);
            PlaceBottomRow(this.menuResetButton.GetComponent<RectTransform>(), 40, 0, 1, 320, 72);
        }

        private static void PlaceRow(RectTransform rect, float top, int slot, int slotCount, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(RowOffsetX(slot, slotCount, width), -top);
        }

        /// <summary>Như PlaceRow nhưng đo từ ĐÁY lên.</summary>
        private static void PlaceBottomRow(RectTransform rect, float bottom, int slot, int slotCount,
                                           float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(RowOffsetX(slot, slotCount, width), bottom);
        }

        private static float RowOffsetX(int slot, int slotCount, float width)
        {
            float totalWidth = slotCount * width + (slotCount - 1) * 16f;
            return -totalWidth * 0.5f + width * 0.5f + slot * (width + 16f);
        }

        // ------------------------------------------------------------------

        private void BuildGameScreen()
        {
            this.gameScreen = Ui.Node("GameScreen", this.contentRoot);
            Ui.Stretch(this.gameScreen, 0, 0, 0, 0);

            // ---- thanh trên
            this.backButton = Ui.Button("Back", this.gameScreen, "←", 42, PuzzlePalette.Panel, PuzzlePalette.Foreground);
            RectTransform backRect = this.backButton.GetComponent<RectTransform>();
            backRect.anchorMin = backRect.anchorMax = new Vector2(0, 1);
            backRect.pivot = new Vector2(0, 1);
            backRect.sizeDelta = new Vector2(92, 84);
            backRect.anchoredPosition = new Vector2(30, -26);

            this.levelNameText = Ui.Text("LevelName", this.gameScreen, "", 38, PuzzlePalette.Foreground,
                TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.TopBand(this.levelNameText.rectTransform, 26, 44, 140);
            this.levelSubText = Ui.Text("LevelSub", this.gameScreen, "", 26, PuzzlePalette.Dim, TextAnchor.UpperLeft);
            Ui.TopBand(this.levelSubText.rectTransform, 68, 38, 140);

            this.soundButton = Ui.Button("Sound", this.gameScreen, "", 34, PuzzlePalette.Panel, PuzzlePalette.Foreground);
            RectTransform soundRect = this.soundButton.GetComponent<RectTransform>();
            soundRect.anchorMin = soundRect.anchorMax = new Vector2(1, 1);
            soundRect.pivot = new Vector2(1, 1);
            soundRect.sizeDelta = new Vector2(92, 84);
            soundRect.anchoredPosition = new Vector2(-30, -26);

            // ---- HUD ba ô
            BuildStat("Moves", 0, "LƯỢT CÒN", out this.movesText, out this.movesMaxText, out this.movesLabel);
            BuildStat("Cells", 1, "Ô CÒN LẠI", out this.cellsText, out _, out this.cellsLabel);
            BuildStat("Score", 2, "ĐIỂM", out this.scoreText, out _);

            // ---- dải phụ: sao / par / hàng chờ
            this.starTexts = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                this.starTexts[i] = Ui.Text("Star" + i, this.gameScreen, "★", 30, PuzzlePalette.Star,
                    TextAnchor.MiddleLeft);
                RectTransform rect = this.starTexts[i].rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
                rect.sizeDelta = new Vector2(40, 42);
                rect.anchoredPosition = new Vector2(34 + i * 34, -276);
            }

            this.parText = Ui.Text("Par", this.gameScreen, "", 26, PuzzlePalette.Dim, TextAnchor.MiddleLeft);
            Ui.TopBand(this.parText.rectTransform, 274, 44, 150);
            this.queueText = Ui.Text("Queue", this.gameScreen, "", 26, PuzzlePalette.Accent, TextAnchor.MiddleRight);
            Ui.TopBand(this.queueText.rectTransform, 274, 44, 34);

            // ---- vùng bàn
            this.boardArea = Ui.Node("BoardArea", this.gameScreen);
            this.boardArea.anchorMin = new Vector2(0, 0);
            this.boardArea.anchorMax = new Vector2(1, 1);
            this.boardArea.offsetMin = new Vector2(24, 230);   // ApplyLayout chỉnh lại theo hàng vật phẩm
            this.boardArea.offsetMax = new Vector2(-24, -336);

            this.board = new BoardView(this.boardArea);
            this.effects = new EffectLayer(this, this.gameScreen);
            this.effects.AttachFlash(this.boardArea);

            // vùng nhận kéo, phủ hết khu bàn
            Image inputArea = Ui.Image("InputArea", this.boardArea, new Color(0, 0, 0, 0), PuzzleSprites.Square);
            Ui.Stretch(inputArea.rectTransform, 0, 0, 0, 0);
            inputArea.raycastTarget = true;
            this.pointerInput = inputArea.gameObject.AddComponent<BoardPointerInput>();

            // ---- chip xem trước điểm
            // chip xem trước điểm: bo mạnh cho gần dạng viên thuốc của HTML.
            // Cao 64 nên phải dùng RadiusChip (28) — bán kính 38 sẽ làm border 9-slice
            // hai cạnh chồng lên nhau và góc bị khuyết.
            this.chainPreview = Ui.Panel("ChainPreview", this.boardArea,
                new Color(0.04f, 0.05f, 0.11f, 0.94f), PuzzlePalette.Line, PuzzlePalette.RadiusChip);
            this.chainPreview.sizeDelta = new Vector2(250, 64);
            this.chainPreviewText = Ui.Text("Label", this.chainPreview, "", 30, PuzzlePalette.Foreground,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Stretch(this.chainPreviewText.rectTransform, 0, 0, 0, 0);
            this.chainPreview.gameObject.SetActive(false);

            // ---- banner chẩn đoán
            RectTransform diag = Ui.Node("DiagBanner", this.gameScreen);
            diag.anchorMin = diag.anchorMax = new Vector2(0.5f, 1);
            diag.pivot = new Vector2(0.5f, 1);
            diag.sizeDelta = new Vector2(900, 150);
            diag.anchoredPosition = new Vector2(0, -348);
            Image diagBg = Ui.Image("Bg", diag, PuzzlePalette.DiagPanel,
                PuzzleSprites.RoundedFill(PuzzlePalette.RadiusPanel), Image.Type.Sliced);
            Ui.Stretch(diagBg.rectTransform, 0, 0, 0, 0);
            Image diagBorder = Ui.Image("Border", diag, PuzzlePalette.DiagBorder,
                PuzzleSprites.RoundedOutline(PuzzlePalette.RadiusPanel), Image.Type.Sliced);
            Ui.Stretch(diagBorder.rectTransform, 0, 0, 0, 0);
            Text diagTitle = Ui.Text("Title", diag, "", 38, PuzzlePalette.Bad,
                TextAnchor.UpperCenter, FontStyle.Bold);
            Ui.Stretch(diagTitle.rectTransform, 20, 20, 14, 90);
            Text diagHint = Ui.Text("Hint", diag, "", 27, new Color(0.79f, 0.8f, 0.92f),
                TextAnchor.UpperCenter);
            Ui.Stretch(diagHint.rectTransform, 20, 20, 60, 30);
            Text tapHint = Ui.Text("Tap", diag, "CHẠM ĐỂ TIẾP TỤC", 20,
                new Color(0.42f, 0.44f, 0.6f), TextAnchor.LowerCenter);
            Ui.Stretch(tapHint.rectTransform, 20, 20, 110, 10);
            diag.gameObject.SetActive(false);

            // ---- bắt chạm để bỏ qua chẩn đoán
            // Bắt chạm phủ CẢ màn hình, không chỉ vùng an toàn: chạm vào dải tai thỏ
            // cũng phải bỏ qua được bước chẩn đoán. Đặt sau safe area nên nằm trên nội
            // dung, và trước Overlay nên không che thẻ thắng/thua.
            Button skip = Ui.Button("SkipCatcher", this.canvas.transform, "", 1,
                new Color(0, 0, 0, 0), Color.clear, PuzzlePalette.RadiusPanel, false, false);
            Ui.Stretch(skip.GetComponent<RectTransform>(), 0, 0, 0, 0);
            // vùng bắt chạm trong suốt: dùng ô đặc, không cần 9-slice
            Image catcherImage = skip.GetComponent<Image>();
            catcherImage.sprite = PuzzleSprites.Square;
            catcherImage.type = Image.Type.Simple;
            skip.gameObject.SetActive(false);

            // Component sống TRÊN node DiagBanner. Lớp bắt chạm là ANH EM chứ không
            // phải con — nó phủ cả màn hình — nhưng tham chiếu thì không đòi quan hệ
            // cha con, nên banner vẫn tự giữ được.
            this.diagBanner = diag.gameObject.AddComponent<DiagnosisBanner>();
            this.diagBanner.BindForAuthoring(diagTitle, diagHint, skip);

            // ---- bốn nút điều khiển
            this.undoButton = BuildControl("Undo", 0, "↶", "Hoàn tác", out this.undoCountText);
            this.shuffleButton = BuildControl("Shuffle", 1, "⇄", "Xáo lại", out this.shuffleCountText);
            this.hintButton = BuildControl("Hint", 2, "?", "Gợi ý", out _);
            this.restartButton = BuildControl("Restart", 4, "↻", "Chơi lại", out _);

            BuildItemControls();
        }

        private void BuildStat(string name, int slot, string caption, out Text value, out Text suffix)
        {
            BuildStat(name, slot, caption, out value, out suffix, out _);
        }

        /// <summary>
        /// `caption` trả ra ngoài vì hai ô đầu ĐỔI NHÃN theo chế độ: "Lượt còn" thành
        /// "Nước đã đi" ở vô tận, "Ô còn lại" thành "Ô đích còn" ở màn mục tiêu.
        /// </summary>
        private void BuildStat(string name, int slot, string caption, out Text value, out Text suffix,
                               out Text captionOut)
        {
            RectTransform panel = Ui.Panel("Stat" + name, this.gameScreen,
                PuzzlePalette.Panel, PuzzlePalette.Line, PuzzlePalette.RadiusPanel);

            const float margin = 30f;
            const float gap = 14f;
            float width = (1080f - margin * 2f - gap * 2f) / 3f;
            panel.anchorMin = panel.anchorMax = new Vector2(0, 1);
            panel.pivot = new Vector2(0, 1);
            panel.sizeDelta = new Vector2(width, 126);
            panel.anchoredPosition = new Vector2(margin + slot * (width + gap), -132);

            Text captionText = Ui.Text("Caption", panel, caption, 22, PuzzlePalette.Dim, TextAnchor.UpperLeft);
            Ui.Stretch(captionText.rectTransform, 20, 12, 12, 78);
            captionOut = captionText;

            value = Ui.Text("Value", panel, "0", 50, PuzzlePalette.Foreground, TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Stretch(value.rectTransform, 20, 12, 42, 8);

            suffix = Ui.Text("Suffix", panel, "", 28, PuzzlePalette.Dim, TextAnchor.LowerRight);
            Ui.Stretch(suffix.rectTransform, 12, 16, 48, 14);
        }

        private Button BuildControl(string name, int slot, string icon, string label, out Text counter)
        {
            // NĂM ô, không phải bốn: nút Vật phẩm nhập luôn vào hàng này thay vì có hàng
            // riêng — hàng riêng ăn 132px mà chỉ để chứa ba nút hiếm khi bấm.
            const float margin = 30f;
            const float gap = 14f;
            float width = (1080f - margin * 2f - gap * 4f) / 5f;

            Button button = Ui.Button("Ctl" + name, this.gameScreen, "", 1, PuzzlePalette.Panel, PuzzlePalette.Foreground);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(0, 0);
            rect.sizeDelta = new Vector2(width, 136);
            rect.anchoredPosition = new Vector2(margin + slot * (width + gap), 46);

            // Nhãn tự động của Ui.Button để rỗng và giữ nguyên: Object.Destroy bị hoãn
            // tới cuối frame, xoá ở đây sẽ làm lệch chỉ số con của các lần GetChild sau.
            Text iconText = Ui.Text("Icon", rect, icon, 42, PuzzlePalette.Foreground, TextAnchor.MiddleCenter);
            Ui.Stretch(iconText.rectTransform, 4, 4, 16, 54);
            Text labelText = Ui.Text("Label", rect, label, 24, PuzzlePalette.Foreground, TextAnchor.MiddleCenter,
                FontStyle.Bold);
            Ui.Stretch(labelText.rectTransform, 4, 4, 84, 12);

            counter = Ui.Text("Count", rect, "", 22, new Color(0.05f, 0.06f, 0.14f), TextAnchor.MiddleCenter,
                FontStyle.Bold);
            RectTransform badge = counter.rectTransform;
            badge.anchorMin = badge.anchorMax = new Vector2(1, 1);
            badge.pivot = new Vector2(1, 1);
            badge.sizeDelta = new Vector2(42, 42);
            badge.anchoredPosition = new Vector2(-8, -8);
            Image badgeBg = Ui.Image("BadgeBg", rect, PuzzlePalette.Accent, PuzzleSprites.Circle);
            badgeBg.rectTransform.anchorMin = badgeBg.rectTransform.anchorMax = new Vector2(1, 1);
            badgeBg.rectTransform.pivot = new Vector2(1, 1);
            badgeBg.rectTransform.sizeDelta = new Vector2(42, 42);
            badgeBg.rectTransform.anchoredPosition = new Vector2(-8, -8);
            badge.SetAsLastSibling();

            return button;
        }

        private void BuildOverlay()
        {
            RectTransform root = Ui.Node("Overlay", this.canvas.transform);
            Ui.Stretch(root, 0, 0, 0, 0);
            Image shade = Ui.Image("Shade", root, new Color(0.03f, 0.04f, 0.08f, 0.86f));
            Ui.Stretch(shade.rectTransform, 0, 0, 0, 0);
            shade.raycastTarget = true;

            RectTransform cardRect = Ui.Panel("Card", root,
                PuzzlePalette.Panel, PuzzlePalette.Line, PuzzlePalette.RadiusCard);
            cardRect.sizeDelta = new Vector2(760, 620);

            // Component sống TRÊN node Overlay và tự giữ ref tới khung thẻ.
            this.card = root.gameObject.AddComponent<OverlayCard>();
            this.card.BindByNameForAuthoring();
            root.gameObject.SetActive(false);
        }

        // ==================================================================
        // Điều hướng
        // ==================================================================

        /// <summary>
        /// Nút ← ở thanh trên. Gọi thẳng ShowMenu trước đây bỏ sót vô tận: điểm đang cao
        /// mà thoát ra giữa ván (không đợi bí) thì không đi qua Evaluate() — nơi DUY
        /// NHẤT lưu kỷ lục — nên điểm mất trắng không báo gì cả.
        /// </summary>
        private void OnBackFromGame()
        {
            TryRecordEndlessScore();

            // Chủ động rời màn hình khi ván vô tận VẪN CÒN SỐNG: lưu lại để tiếp tục,
            // không bắt chơi lại từ đầu. HasMove() phòng ca hiếm ← lọt qua lúc bàn đã
            // bí — lưu một bàn chết thì lần sau mở lên chỉ thấy một bàn không đi được.
            if (this.level != null && this.level.Endless)
            {
                if (this.session != null && this.session.HasMove())
                    PuzzleProgress.SaveEndlessState(this.session);
                else
                    PuzzleProgress.ClearEndlessState();
            }

            ShowMenu();
        }

        /// <summary>
        /// Lưu kỷ lục vô tận nếu ván đang chơi là vô tận và có điểm. Gọi ở MỌI lối ra
        /// khỏi một ván vô tận đang chạy (không riêng lúc bí), vì đó là lúc điểm có giá
        /// trị nhất và dễ mất nhất — người chơi thấy điểm cao rồi bấm thoát ngay.
        /// An toàn gọi nhiều lần: RecordEndless chỉ ghi khi điểm mới cao hơn.
        /// </summary>
        private void TryRecordEndlessScore()
        {
            if (this.level == null || !this.level.Endless || this.session == null) return;
            PuzzleProgress.RecordEndless(this.session.Score);
        }

        public void ShowMenu()
        {
            CancelDiagnosis();
            this.card.Hide();
            this.gameScreen.gameObject.SetActive(false);
            this.menuScreen.gameObject.SetActive(true);
            RefreshMenu();
        }

        private void RefreshMenu()
        {
            LayoutMenu();
            foreach (LevelButton entry in this.levelButtons)
            {
                bool unlocked = PuzzleProgress.IsUnlocked(entry.Index);
                int stars = PuzzleProgress.Stars(entry.Index);

                // KHÔNG dùng interactable = false: nút tắt thì bấm vào không có gì xảy
                // ra cả, không phân biệt được với game hỏng. Để nút sống và cho nó nói.
                entry.Button.interactable = true;

                // Không dùng 🔒: font mặc định của Unity không có glyph emoji nên nút
                // khoá hiện ra TRỐNG TRƠN. Số màn mờ đi vừa đọc được vừa không phụ
                // thuộc font.
                entry.Label.text = (entry.Index + 1).ToString();
                entry.Label.color = unlocked ? PuzzlePalette.Foreground : new Color(0.35f, 0.38f, 0.55f, 0.55f);

                // Sao đầy và sao rỗng phải KHÁC MÀU. Trước đây cả hai tô cùng màu vàng
                // nên màn 0 sao trông y như màn 3 sao — không đọc được tiến độ.
                if (!unlocked) entry.Stars.text = "";
                else entry.Stars.text =
                    "<color=#FBBF24>" + new string('★', stars) + "</color>" +
                    "<color=#3B4270>" + new string('★', 3 - stars) + "</color>";

                Image bg = entry.Button.GetComponent<Image>();
                bg.color = stars > 0 ? PuzzlePalette.PanelLight : PuzzlePalette.Panel;
            }
            UpdateToggleLabels();
        }

        /// <summary>
        /// Nút màn chưa mở PHẢI phản hồi. Trước đây nó chỉ bị `interactable = false` nên
        /// bấm vào là im lặng hoàn toàn — không phân biệt được với "game hỏng".
        /// </summary>
        private void OnLevelPicked(int index)
        {
            if (!PuzzleProgress.IsUnlocked(index))
            {
                this.audioPlayer.Tone(180f, 0.2f);
                Toast("Màn " + (index + 1) + " chưa mở. Qua màn trước để mở, hoặc bật Chơi tự do ở cuối menu.");
                return;
            }
            OpenLevel(index);
        }

        /// <summary>
        /// Câu giới thiệu cho cơ chế LẦN ĐẦU gặp, tìm TỰ ĐỘNG theo bảng màn.
        /// Ghi số màn cứng thì mỗi lần thêm/bớt màn là các mốc lệch hết mà không ai báo —
        /// người chơi gặp đá ở màn 31 mà lời giải thích nhảy ra ở màn 25.
        /// </summary>
        private static readonly (System.Func<LevelConfig, bool> Match, string Message)[] IntroRules =
        {
            (c => c.Stones > 0, "Đá không nối được. Nó chỉ vỡ khi bạn ăn một chuỗi NGAY CẠNH nó."),
            (c => c.Stones > 0 && c.StoneHp >= 2, "Đá dày cần bị va 2 lần mới vỡ."),
            (c => c.Wilds > 0, "Ô đa sắc ✦ ghép được với mọi màu — nhưng mỗi chuỗi chỉ được dùng 1 ô."),
            (c => c.Bombs > 0, "Ô có số là ngòi nổ: hết số mà chưa ăn tới là thua ngay."),
            (c => c.Goals > 0, "Màn này chỉ cần dọn các ô có vòng vàng, không cần sạch bàn."),
            (c => c.Ices > 0, "Ô phủ băng chưa chọn được. Ăn một chuỗi NGAY CẠNH cho tan băng, rồi mới ăn nó."),
            (c => c.Ices > 0 && c.IceHp >= 2, "Băng dày cần làm tan 2 lần mới ăn được ô bên dưới."),
            (c => c.Links > 0, "◇ Hai ô cùng vòng màu bị TRÓI nhau: ăn một ô thì ô kia vỡ theo, dù ở xa.")
        };

        private readonly HashSet<int> introShown = new HashSet<int>();

        private void ShowIntroFor(int index)
        {
            for (int r = 0; r < IntroRules.Length; r++)
            {
                if (this.introShown.Contains(r)) continue;

                int first = -1;
                for (int i = 0; i < LevelCatalog.Levels.Length; i++)
                    if (IntroRules[r].Match(LevelCatalog.Levels[i])) { first = i; break; }

                if (first != index) continue;
                this.introShown.Add(r);
                Toast(IntroRules[r].Message);
            }
        }

        /// <summary>Mở một màn theo chỉ số. Công khai để deep-link và để kiểm thử.</summary>
        public void OpenLevel(int index)
        {
            OpenLevelData(index, LevelBuilder.Build(LevelCatalog.Levels[index]));
            ShowIntroFor(index);
        }

        /// <summary>Mở chế độ vô tận. Dùng chung toàn bộ đường chơi, chỉ khác dữ liệu màn.</summary>
        public void OpenEndless()
        {
            OpenLevelData(-1, EndlessLevel.Build());

            // Nạp lại ván trước nếu có. PHẢI làm ngay đây, sau khi OpenLevelData đã vẽ
            // bàn mới lên màn — không thì bàn mới hiện ra một khung hình trước khi bị
            // thay bằng bàn cũ, và displayedScore/HUD vẫn còn số 0 của ván mới.
            if (PuzzleProgress.TryLoadEndlessState(this.session))
            {
                this.displayedScore = this.session.Score;
                this.scoreText.text = this.session.Score.ToString();
                this.board.Refresh(this.session, PuzzleProgress.Symbols);
                RedrawSelection();
                UpdateHud();
                Toast("∞ Đã tiếp tục ván trước — điểm " + this.session.Score + ".");
            }
            else
            {
                Toast("∞ Vô tận: ô rớt xuống mãi, không giới hạn lượt. Chuỗi " +
                      EndlessRules.ComboMinChain + "+ ô liên tiếp để nhân điểm. Hết nước đi là xong ván.");
            }
        }

        /// <summary>
        /// Mở thử thách của hôm nay. Bàn sinh từ NGÀY UTC nên mọi máy cùng một bàn.
        /// </summary>
        public void OpenDaily()
        {
            this.dailyKey = DailyChallenge.TodayKey();
            OpenLevelData(DailyIndex, DailyChallenge.BuildFor(this.dailyKey));

            int streak = PuzzleProgress.DailyStreakLive(this.dailyKey);
            if (PuzzleProgress.DailyWon(this.dailyKey))
                Toast("✦ Hôm nay bạn đã xong rồi — chơi lại để phá điểm " +
                      PuzzleProgress.DailyBest(this.dailyKey) + ".");
            else
                Toast("✦ Thử thách hôm nay: bàn này giống nhau trên mọi máy, đổi lúc 0h UTC." +
                      (streak > 0 ? " Chuỗi hiện tại " + streak + " ngày." : ""));
        }

        /// <summary>Chỉ số giả cho thử thách, để không đụng ô lưu tiến độ của màn thật.</summary>
        private const int DailyIndex = -2;

        /// <summary>Ván đang chơi có phải thử thách hằng ngày không.</summary>
        private bool IsDaily => this.levelIndex == DailyIndex;

        private void OpenLevelData(int index, LevelData data)
        {
            // Mở bất cứ thứ gì KHÔNG phải thử thách thì bỏ khoá ngày, không thì kết quả
            // của màn thường sẽ được ghi vào bản ghi của ngày hôm nay.
            if (index != DailyIndex) this.dailyKey = 0;

            this.levelIndex = index;
            this.level = data;
            this.session = new PuzzleSession(this.level);

            this.menuScreen.gameObject.SetActive(false);
            this.gameScreen.gameObject.SetActive(true);
            this.card.Hide();

            this.board.Build(this.level);
            this.lastBoardArea = Vector2.zero;
            ApplyLayout(force: true);
            ResetRun();
        }

        private void RestartLevel()
        {
            CancelDiagnosis();
            // Chơi lại/Chơi ván mới cũng là một lối THOÁT khỏi ván hiện tại — bấm nút
            // này giữa lúc đang chơi vô tận (không phải từ thẻ đã bí) trước đây xoá điểm
            // mà chưa từng so với kỷ lục.
            TryRecordEndlessScore();

            // "Chơi lại" nghĩa là BỎ ván này, không phải rời đi để quay lại — nên phải
            // XOÁ save đang có (nếu có), không thì lần sau mở Vô tận từ menu lại tiếp
            // tục đúng ván vừa chủ động bỏ, ngược với điều người chơi vừa chọn.
            if (this.level != null && this.level.Endless) PuzzleProgress.ClearEndlessState();

            this.session.Restart();
            ResetRun();
        }

        private void ResetRun()
        {
            this.busy = false;
            this.items.ClearPending();
            this.dragging = false;
            this.displayedScore = 0;

            LevelConfig cfg = this.level.Config;
            this.levelNameText.text = this.level.Endless ? "∞ Vô tận"
                : IsDuel ? "⚔ Đấu " + this.duel.Code
                : IsDaily ? "✦ Thử thách hôm nay"
                : ((this.levelIndex + 1) + ". " + cfg.Name);
            this.levelSubText.text =
                this.level.Endless ? "kỷ lục " + PuzzleProgress.EndlessBest + " · ô rớt xuống mãi"
                : IsDuel ? this.duel.PresetLabel + " · " + this.level.TotalCells + " ô"
                : IsDaily ? DailyBadge()
                : this.level.GoalMode
                    ? this.level.GoalTotal + " ô đích · " + this.level.TotalCells + " ô" +
                      (this.level.Gravity ? " · gravity" : "")
                : this.level.Gravity
                    ? this.level.TotalCells + " ô · " + this.level.VisibleCells + " ô hiện · gravity"
                    : this.level.TotalCells + " ô · " + cfg.Colors + " màu · bàn tĩnh";

            // Luật chuỗi phải hiện trên HUD, không thì người chơi tưởng game hỏng khi
            // ô thứ N+1 không chịu nối vào.
            string rule = "chuỗi " + this.level.MinChain +
                (this.level.MaxChain == int.MaxValue ? "+ ô" : "-" + this.level.MaxChain + " ô");
            this.parText.text = this.level.Endless
                ? rule + " · " + EndlessRules.ColorsFor(this.session.Score) + " màu"
                : rule + " · tối ưu " + this.level.Par;

            this.board.SetDimmed(this.session, false, PuzzleProgress.Symbols);
            this.board.ClearChain();
            this.board.ResetScales();
            this.chainPreview.gameObject.SetActive(false);
            this.diagBanner.Hide();
            this.card.Hide();
            UpdateHud();
        }

        // ==================================================================
        // Bố cục theo kích thước màn hình
        // ==================================================================

        private void Update()
        {
            // Menu cũng phải bố cục lại khi khung đổi (quay máy, lề an toàn thay đổi),
            // không thì lưới màn đè lên footer trên tỉ lệ màn hình khác.
            if (this.menuScreen.gameObject.activeSelf)
            {
                Vector2 menuSize = this.menuScreen.rect.size;
                if ((menuSize - this.lastMenuArea).sqrMagnitude > 1f)
                {
                    this.lastMenuArea = menuSize;
                    LayoutMenu();
                }
                return;
            }

            if (!this.gameScreen.gameObject.activeSelf) return;
            ApplyLayout(force: false);
            this.board.TickChain(Time.deltaTime);   // nét đứt chạy, như stroke-dashoffset
            this.duel.Tick();
        }

        private Vector2 lastMenuArea;

        /// <summary>
        /// Đáy vùng bàn. Bảng vật phẩm là lớp NỔI nên nó không ăn chỗ của bàn.
        ///
        /// Thuộc về BỐ CỤC chứ không thuộc cửa hàng — trước đây nó nằm lẫn trong vùng
        /// vật phẩm chỉ vì được thêm vào lúc đang sửa vùng đó.
        /// </summary>
        private float BoardBottomInset => 230f;

        private void ApplyLayout(bool force)
        {
            // Đáy vùng bàn phụ thuộc CHẾ ĐỘ (có hàng vật phẩm hay không) nên phải đặt
            // trước khi đọc kích thước — đọc trước thì bàn dùng số đo của chế độ trước đó.
            float inset = this.BoardBottomInset;
            if (!Mathf.Approximately(this.boardArea.offsetMin.y, inset))
            {
                this.boardArea.offsetMin = new Vector2(24, inset);
                LayoutRebuilder.ForceRebuildLayoutImmediate(this.boardArea);
                force = true;
            }

            Vector2 size = this.boardArea.rect.size;
            if (!force && (size - this.lastBoardArea).sqrMagnitude < 1f) return;
            this.lastBoardArea = size;
            this.board.Layout(size);
            this.board.Root.anchoredPosition = Vector2.zero;
            if (this.session != null)
            {
                this.board.Refresh(this.session, PuzzleProgress.Symbols);
                RedrawSelection();
            }
        }

        // ==================================================================
        // Nhập liệu
        // ==================================================================

        /// <summary>Dải mép ô bị bỏ qua khi kéo, xem BoardView.CellAtWorldPoint.</summary>
        private const float CellEdgeInset = 0.10f;

        private Vector3 lastSampleWorld;
        private int lastSampledCell = -1;

        private void OnPointerDown(Vector3 worldPoint)
        {
            if (this.session == null) return;
            this.lastSampleWorld = worldPoint;
            this.lastSampledCell = -1;

            // Đang chạy hoạt ảnh: model đã ở trạng thái cuối rồi, chỉ có hình là chưa
            // xong. Tua nhanh cho hết và KHÔNG nuốt cú kéo — OnDrag ngay frame sau sẽ
            // bắt đầu chuỗi. Trước đây cả cú kéo bị bỏ, ở màn gravity là mất liên tục.
            if (this.busy) { this.board.FastForward = true; return; }

            // Đang ngắm vật phẩm: cú chạm này là chọn MỤC TIÊU, không phải bắt đầu chuỗi.
            if (this.items.Aiming)
            {
                int target = this.board.CellAtWorldPoint(worldPoint, CellEdgeInset);
                if (target >= 0) this.items.Apply(this.items.Pending, target);
                return;
            }

            BeginChain(worldPoint);
        }

        private bool BeginChain(Vector3 worldPoint)
        {
            int cell = this.board.CellAtWorldPoint(worldPoint, CellEdgeInset);
            if (cell < 0 || this.session.Board[cell] < 0) return false;

            ClearSelectionVisuals();
            this.session.ClearSelection();
            this.dragging = true;
            this.lastSampleWorld = worldPoint;
            this.lastSampledCell = cell;
            Extend(cell);
            return true;
        }

        private void OnPointerDrag(Vector3 worldPoint)
        {
            if (this.session == null) return;
            if (this.busy) { this.lastSampleWorld = worldPoint; this.board.FastForward = true; return; }

            // Chuỗi chưa bắt đầu: hoặc cú nhấn rơi vào lúc bận, hoặc nhấn trúng ô trống.
            // Bắt đầu ngay tại đây thay vì bỏ cả cú kéo.
            if (!this.dragging)
            {
                if (!BeginChain(worldPoint)) this.lastSampleWorld = worldPoint;
                return;
            }

            SampleAlong(this.lastSampleWorld, worldPoint);
            this.lastSampleWorld = worldPoint;
        }

        /// <summary>
        /// Quét mọi ô nằm trên ĐOẠN từ điểm lấy mẫu trước tới điểm hiện tại.
        ///
        /// Chỉ đọc vị trí con trỏ mỗi frame là không đủ: kéo nhanh thì hai frame liên
        /// tiếp cách nhau vài ô, ô ở giữa không bao giờ được xét. Luật đòi ô mới phải
        /// kề ô cuối, nên chuỗi đứng im hẳn — đúng triệu chứng kéo mà không nối được,
        /// và càng rõ với đường chéo dài.
        /// </summary>
        private void SampleAlong(Vector3 from, Vector3 to)
        {
            float worldCell = this.board.CellSize * this.board.Root.lossyScale.x;
            float step = Mathf.Max(1f, worldCell * 0.25f);
            float distance = Vector3.Distance(from, to);
            int steps = Mathf.Clamp(Mathf.CeilToInt(distance / step), 1, 128);

            for (int i = 1; i <= steps; i++)
            {
                Vector3 point = Vector3.Lerp(from, to, i / (float)steps);
                int cell = this.board.CellAtWorldPoint(point, CellEdgeInset);
                if (cell < 0 || cell == this.lastSampledCell) continue;
                this.lastSampledCell = cell;
                Extend(cell);
            }
        }

        private void OnPointerUp()
        {
            this.lastSampledCell = -1;
            if (!this.dragging) return;
            this.dragging = false;
            StartCoroutine(CommitRoutine());
        }

        private void Extend(int cell)
        {
            // Phải nhớ ô cuối TRƯỚC khi gọi: khi lùi lại, hàm bỏ ô CUỐI chứ không bỏ ô
            // vừa lùi về. Tắt sáng nhầm `cell` sẽ làm ô đang chọn mất viền còn ô vừa bị
            // bỏ thì vẫn sáng.
            int lastBefore = this.session.Selection.Count > 0
                ? this.session.Selection[this.session.Selection.Count - 1]
                : -1;

            SelectionChange change = this.session.TryExtendSelection(cell);
            if (change == SelectionChange.None) return;

            if (change == SelectionChange.Added)
            {
                this.board.SetSelected(cell, true);
                this.audioPlayer.Select(this.session.Selection.Count);
                Vector2 center = this.board.CellCenter(cell);
                this.effects.Burst(BoardToEffect(center), PuzzlePalette.Colors[this.session.SelectionColor], 3,
                    this.board.CellSize * 0.4f);
            }
            else if (lastBefore >= 0)
            {
                this.board.SetSelected(lastBefore, false);
            }
            RedrawSelection();
        }

        private void RedrawSelection()
        {
            List<int> selection = this.session.Selection;
            this.board.DrawChain(selection, this.session.SelectionColor);

            if (selection.Count >= 1)
            {
                Vector2 head = this.board.CellCenter(selection[selection.Count - 1]);
                this.chainPreview.gameObject.SetActive(true);
                this.chainPreview.anchoredPosition = BoardToArea(head) + new Vector2(0, this.board.CellSize * 0.95f);

                int min = this.level.MinChain;
                int max = this.level.MaxChain;

                if (selection.Count < min)
                {
                    // Nói rõ còn thiếu bao nhiêu, không thì người chơi thả tay rồi
                    // chẳng thấy gì xảy ra mà không hiểu vì sao.
                    this.chainPreviewText.text = "cần " + min + " ô";
                    this.chainPreviewText.color = PuzzlePalette.Dim;
                }
                else if (max != int.MaxValue && selection.Count >= max)
                {
                    this.chainPreviewText.text = "tối đa " + max + " ô  +" + PuzzleSession.ChainScore(selection.Count);
                    this.chainPreviewText.color = PuzzlePalette.Star;
                }
                else
                {
                    this.chainPreviewText.text = selection.Count + " ô  +" + PuzzleSession.ChainScore(selection.Count);
                    this.chainPreviewText.color = PuzzlePalette.Foreground;
                }
            }
            else
            {
                this.chainPreview.gameObject.SetActive(false);
            }
        }

        private void ClearSelectionVisuals()
        {
            foreach (int cell in this.session.Selection) this.board.SetSelected(cell, false);
            this.board.ClearChain();
            this.chainPreview.gameObject.SetActive(false);
        }

        /// <summary>Toạ độ local của bàn -> local của vùng bàn (nơi đặt chip/hiệu ứng).</summary>
        private Vector2 BoardToArea(Vector2 boardLocal)
        {
            return boardLocal + this.board.Root.anchoredPosition
                   - new Vector2(this.board.Root.sizeDelta.x * 0.5f, -this.board.Root.sizeDelta.y * 0.5f);
        }

        private Vector2 BoardToEffect(Vector2 boardLocal)
        {
            Vector3 world = this.board.Root.TransformPoint(boardLocal
                - new Vector2(this.board.Root.sizeDelta.x * 0.5f, -this.board.Root.sizeDelta.y * 0.5f));
            return this.gameScreen.InverseTransformPoint(world);
        }

        // ==================================================================
        // Một nước đi
        // ==================================================================

        private IEnumerator CommitRoutine()
        {
            if (this.session.Selection.Count < this.level.MinChain)
            {
                if (this.session.Selection.Count > 0) this.audioPlayer.Bad();
                ClearSelectionVisuals();
                this.session.ClearSelection();
                yield break;
            }

            this.busy = true;
            this.board.FastForward = false;
            int chainLength = this.session.Selection.Count;
            var clearedCentres = new List<Vector2>(chainLength);
            foreach (int cell in this.session.Selection) clearedCentres.Add(BoardToEffect(this.board.CellCenter(cell)));

            foreach (int cell in this.session.Selection) this.board.SetSelected(cell, false);
            this.board.ClearChain();
            this.chainPreview.gameObject.SetActive(false);

            MoveResult result = this.session.Commit();
            Color color = PuzzlePalette.Colors[result.Color];

            // hiệu ứng phá ô
            foreach (Vector2 centre in clearedCentres)
                this.effects.Burst(centre, color, chainLength >= 6 ? 9 : 6, this.board.CellSize * 0.8f);

            Vector2 middle = Vector2.zero;
            foreach (Vector2 centre in clearedCentres) middle += centre;
            middle /= clearedCentres.Count;

            this.effects.FloatText(middle, "+" + result.Gained, Color.white,
                chainLength >= 8 ? 74 : (chainLength >= 5 ? 62 : 52), 110f, 0.06f);
            string praise = Praise(chainLength);
            if (praise.Length > 0)
                this.effects.FloatText(middle + new Vector2(0, 70), praise, color,
                    chainLength >= 10 ? 48 : 40, 90f, 0.22f);

            this.effects.Flash(chainLength >= 8 ? 0.3f : (chainLength >= 5 ? 0.18f : 0.09f));
            if (chainLength >= 6) this.effects.Shake(this.board.Root, this.board.CellSize * 0.09f);
            this.audioPlayer.Clear(chainLength);

            AnimateScore(result.ScoreBefore, this.session.Score);
            UpdateHud();

            // Ô vỡ THEO liên kết nổ CÙNG LÚC với chuỗi, không phải sau: hai chuyện đó
            // là một nước đi, tách ra thì người chơi tưởng mình vừa đi hai nước.
            if (result.LinkedBroken.Count > 0)
            {
                this.audioPlayer.Tone(300f, 0.18f);
                foreach (int cell in result.LinkedBroken)
                    this.effects.Burst(this.board.CellCenter(cell), PuzzlePalette.Foreground,
                                       7, this.board.CellSize * 0.5f);
                StartCoroutine(this.board.PlayPop(result.LinkedBroken.ToArray()));
            }

            yield return this.board.PlayPop(result.ClearedCells);
            this.board.Refresh(this.session, PuzzleProgress.Symbols);

            if (result.Falls.Count > 0)
            {
                this.audioPlayer.Fall();
                yield return this.board.PlayFalls(result.Falls);
            }

            // Băng chạy SAU Refresh: Refresh đã đặt đúng độ dày cho trạng thái mới, nên
            // ô vừa nứt hiện lớp mỏng hơn ngay khi hoạt ảnh giật nảy chạy — người chơi
            // thấy được là "đã bớt một lớp" chứ không chỉ thấy nó rung.
            if (result.CrackedIce.Count > 0 || result.ThawedIce.Count > 0)
            {
                // hai âm khác nhau: nứt là tiếng gõ đanh, tan là tiếng vỡ cao hơn
                if (result.ThawedIce.Count > 0) this.audioPlayer.Tone(880f, 0.22f);
                else this.audioPlayer.Tone(520f, 0.12f);
                yield return this.board.PlayIce(result.CrackedIce, result.ThawedIce);
            }

            UpdateHud();
            this.busy = false;
            Evaluate();
        }

        private static string Praise(int chainLength)
        {
            if (chainLength >= 10) return "SIÊU PHẨM!";
            if (chainLength >= 8) return "ĐỈNH!";
            if (chainLength >= 6) return "TUYỆT!";
            if (chainLength >= 5) return "Hay!";
            return "";
        }

        /// <summary>
        /// Lối vào cho kiểm thử: chạy ĐÚNG nhánh kết ván mà lượt chơi thật chạy. Gọi
        /// thẳng PuzzleProgress trong bài kiểm sẽ kiểm nhầm — nó bỏ qua chính chỗ dễ
        /// sai nhất, là việc Evaluate chọn nhánh ghi nào.
        /// </summary>
        public void DebugEvaluate() { Evaluate(); }

        private void Evaluate()
        {
            // Vô tận không có "thắng"; ván chỉ dừng khi bàn hết nước đi thật sự.
            if (this.level.Endless)
            {
                if (this.session.HasMove()) return;
                bool newRecord = PuzzleProgress.RecordEndless(this.session.Score);

                // Ván đã kết thúc thật — không còn gì để "tiếp tục" nữa. Xoá save cũ
                // (có thể còn sót từ một lần bấm ← trước đó trong CÙNG ván) để nó không
                // sống lại sai chỗ ở lần mở Vô tận kế tiếp.
                PuzzleProgress.ClearEndlessState();

                this.audioPlayer.Tone(180f, 0.35f);
                this.busy = true;
                this.diagnosisRoutine = StartCoroutine(DiagnoseRoutine(new LossReason
                {
                    Kind = LossKind.Deadlock,
                    Title = "Hết nước đi",
                    Detail = "Không còn hai ô nào ghép được với nhau.",
                    Hint = "Không ô nào còn ô ghép được bên cạnh",
                    EvidenceGroups = { this.session.AliveVisibleCells().ToArray() }
                }, newRecord));
                return;
            }

            if (this.session.IsWon())
            {
                this.duel.CaptureResult();
                if (this.duel.TryShowLanVerdict()) return;

                int stars = this.session.StarsEarned();

                // Huy hiệu kỹ thuật: chỉ ở chiến dịch. Thử thách hằng ngày không ghi
                // huy hiệu vì huy hiệu đẻ ra sao, mà sao mua được vật phẩm — vòng đó
                // sẽ phá đúng cái công bằng mà thử thách dựa vào.
                this.medalJustEarned = !IsDaily && !IsDuel && this.session.MedalEarned &&
                                       PuzzleProgress.RecordMedal(this.levelIndex);

                // Ván đấu KHÔNG ghi vào tiến độ chiến dịch. Không chặn thì chỉ số -3 tạo
                // ra khoá "connectPuzzle.stars.-3", và tệ hơn: đánh bại bạn bè lại đẻ ra
                // sao tiêu được, tức là cày mã dễ để mua vật phẩm.
                bool record = IsDuel
                    ? false
                    : IsDaily
                        ? PuzzleProgress.RecordDaily(this.dailyKey, stars, this.session.Score, true)
                        : PuzzleProgress.Record(this.levelIndex, stars, this.session.Score);
                Celebrate();
                ShowWinCard(stars, record);
                return;
            }

            // Ngòi nổ xét TRƯỚC hết lượt: nó là lý do cụ thể hơn, và người chơi đang
            // nhìn con số đếm ngược nên đó là cái họ theo dõi.
            LossReason reason = LossAnalyzer.BombsBlown(this.session)
                ?? (this.session.MovesUsed >= this.level.MaxMoves
                    ? LossAnalyzer.OutOfMoves(this.session)
                    : this.session.Analyze());

            if (reason == null) return;

            this.duel.CaptureResult();
            if (this.duel.TryShowLanVerdict()) return;

            // Thua vẫn ghi điểm của ngày (không cộng chuỗi): người chơi thử vài lần trong
            // ngày thì con số trên menu phải là lần tốt nhất, không phải chỉ lần thắng.
            if (IsDaily) PuzzleProgress.RecordDaily(this.dailyKey, 0, this.session.Score, false);

            this.audioPlayer.Tone(180f, 0.35f);
            this.busy = true;
            this.diagnosisRoutine = StartCoroutine(DiagnoseRoutine(reason));
        }

        // ==================================================================
        // Chẩn đoán thua — chỉ vào chỗ sai trước khi bật panel
        // ==================================================================

        private IEnumerator DiagnoseRoutine(LossReason reason, bool endlessRecord = false)
        {
            this.endlessRecord = endlessRecord;
            this.board.SetDimmed(this.session, true, PuzzleProgress.Symbols);
            this.diagBanner.Show(reason.Title, reason.Hint);
            this.effects.Shake(this.board.Root, this.board.CellSize * 0.07f);

            var lit = new List<int>();
            float step = reason.EvidenceGroups.Count > 1
                ? Mathf.Clamp(1.5f / reason.EvidenceGroups.Count, 0.17f, 0.42f)
                : 0f;

            float elapsed = 0f;
            int nextGroup = 0;
            float nextAt = 0.42f;

            while (!this.diagBanner.SkipRequested &&
                   (elapsed < DiagnosisMinSeconds || nextGroup < reason.EvidenceGroups.Count))
            {
                elapsed += Time.deltaTime;

                if (nextGroup < reason.EvidenceGroups.Count && elapsed >= nextAt)
                {
                    int[] group = reason.EvidenceGroups[nextGroup];
                    foreach (int cell in group)
                    {
                        this.board.SetCulprit(cell, true, this.session);
                        lit.Add(cell);
                    }
                    if (reason.EvidenceGroups.Count > 1 && group.Length > 0)
                        this.effects.FloatText(BoardToEffect(this.board.CellCenter(group[0])),
                            (nextGroup + 1).ToString(), Color.white, 46, 50f);
                    this.audioPlayer.Blip(nextGroup);
                    nextGroup++;
                    nextAt = elapsed + step;
                }

                this.board.PulseCulprits(lit, elapsed);
                yield return null;
            }

            FinishDiagnosis();
            if (this.level.Endless) ShowEndlessCard(this.endlessRecord);
            else ShowLoseCard(reason);
        }

        private bool endlessRecord;

        /// <summary>Ván này vừa lấy được huy hiệu kỹ thuật LẦN ĐẦU (để thẻ thắng khoe).</summary>
        private bool medalJustEarned;

        private void FinishDiagnosis()
        {
            this.diagnosisRoutine = null;
            this.diagBanner.Hide();
            this.board.ResetScales();
            if (this.session != null) this.board.SetDimmed(this.session, false, PuzzleProgress.Symbols);
            this.busy = false;
        }

        /// <summary>
        /// Huỷ chẩn đoán đang chạy mà KHÔNG bật panel. Bắt buộc phải có: nếu người
        /// chơi bấm Chơi lại hoặc về menu giữa lúc chẩn đoán mà coroutine vẫn sống,
        /// nó sẽ bật panel thua lên trên bàn mới.
        /// </summary>
        private void CancelDiagnosis()
        {
            if (this.diagnosisRoutine == null) return;
            StopCoroutine(this.diagnosisRoutine);
            FinishDiagnosis();
        }

        // ==================================================================
        // HUD
        // ==================================================================

        private void UpdateHud()
        {
            this.items.RefreshBar();
            if (this.level.Endless)
            {
                // Vô tận không có lượt để đếm ngược, nên ô đó chuyển thành số nước ĐÃ đi,
                // và ô "ô còn lại" thành hệ số combo — hai thứ duy nhất còn nghĩa ở đây.
                this.movesLabel.text = "Nước đã đi";
                this.movesText.text = this.session.MovesUsed.ToString();
                this.movesMaxText.text = "";
                this.movesText.color = PuzzlePalette.Foreground;

                this.cellsLabel.text = "Combo";
                this.cellsText.text = this.session.Combo > 0
                    ? "x" + this.session.EndlessMultiplier.ToString("0.##")
                    : "—";

                foreach (Text star in this.starTexts)
                    star.color = new Color(PuzzlePalette.Star.r, PuzzlePalette.Star.g, PuzzlePalette.Star.b, 0f);

                this.parText.text = EndlessRules.ColorsFor(this.session.Score) + " màu · kỷ lục " +
                                    PuzzleProgress.EndlessBest;
                this.queueText.text = "";
                this.undoCountText.text = "0";
                this.undoButton.interactable = false;
                this.shuffleCountText.text = this.session.ShufflesLeft.ToString();
                this.shuffleButton.interactable = this.session.ShufflesLeft > 0;
                UpdateToggleLabels();
                return;
            }

            this.movesLabel.text = "Lượt còn";
            this.movesText.text = this.session.MovesLeft.ToString();
            this.movesMaxText.text = "/" + this.level.MaxMoves;
            this.movesText.color = this.session.MovesLeft <= 2 ? PuzzlePalette.Bad : PuzzlePalette.Foreground;

            // Màn mục tiêu đếm ô ĐÍCH: người chơi cần biết còn cách thắng bao xa, mà ở
            // đó phần bàn thừa không liên quan.
            this.cellsLabel.text = this.level.GoalMode ? "Ô đích còn" : "Ô còn lại";
            this.cellsText.text = (this.level.GoalMode ? this.session.GoalsLeft : this.session.TotalLeft()).ToString();

            for (int i = 0; i < 3; i++)
            {
                int threshold = i == 0 ? this.level.Par : (i == 1 ? this.level.TwoStarMoves : this.level.MaxMoves);
                bool on = this.session.MovesUsed <= threshold;
                this.starTexts[i].color = on
                    ? PuzzlePalette.Star
                    : new Color(PuzzlePalette.Star.r, PuzzlePalette.Star.g, PuzzlePalette.Star.b, 0.2f);
            }

            // Tiến độ huy hiệu phải hiện TRONG lúc chơi, không phải chỉ ở thẻ kết ván:
            // biết mình còn thiếu mấy chuỗi đầy là thứ đổi được cách đi nước tiếp theo,
            // biết sau khi xong ván thì chỉ còn là lời trách.
            if (this.level.MedalChains > 0 && !IsDaily)
                this.queueText.text = "◆ " + this.session.FullChains + "/" + this.level.MedalChains;
            else
                this.queueText.text = this.level.Gravity ? "▼ hàng chờ " + this.session.QueueLeft() : "";

            this.undoCountText.text = this.session.UndosLeft.ToString();
            this.undoButton.interactable = this.session.CanUndo;
            this.shuffleCountText.text = this.session.ShufflesLeft.ToString();
            this.shuffleButton.interactable = this.session.CanShuffle;
            UpdateToggleLabels();
        }

        private void AnimateScore(int from, int to)
        {
            if (this.scoreRoutine != null) StopCoroutine(this.scoreRoutine);
            this.scoreRoutine = StartCoroutine(ScoreRoutine(from, to));
        }

        private IEnumerator ScoreRoutine(int from, int to)
        {
            const float duration = 0.42f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                this.displayedScore = Mathf.RoundToInt(Mathf.Lerp(from, to, 1f - Mathf.Pow(1f - t, 3f)));
                this.scoreText.text = this.displayedScore.ToString();
                yield return null;
            }
            this.displayedScore = to;
            this.scoreText.text = to.ToString();
            this.scoreRoutine = null;
        }

        // ==================================================================
        // Toast — câu nhắn ngắn, tự tắt
        // ==================================================================

        [SerializeField] private ToastView toast;

        /// <summary>
        /// Dựng trên CANVAS chứ không trên màn hình game: toast phải hiện được cả ở menu
        /// (bấm màn khoá, bật/tắt chơi tự do) lẫn trong ván.
        /// </summary>
        private void BuildToast()
        {
            RectTransform root = Ui.Panel("Toast", this.canvas.transform,
                PuzzlePalette.DiagPanel, PuzzlePalette.Line, PuzzlePalette.RadiusPanel);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0);
            root.pivot = new Vector2(0.5f, 0);
            root.sizeDelta = new Vector2(940, 120);
            root.anchoredPosition = new Vector2(0, 220);

            Text label = Ui.Text("Text", root, "", 28,
                PuzzlePalette.Foreground, TextAnchor.MiddleCenter);
            Ui.Stretch(label.rectTransform, 26, 26, 12, 12);

            this.toast = root.gameObject.AddComponent<ToastView>();
            this.toast.BindByNameForAuthoring();
            root.gameObject.SetActive(false);
        }

        /// <summary>
        /// Giữ lại làm cửa vào cho hơn 40 chỗ gọi trong lớp này, thay vì sửa hết
        /// thành this.toast.Show(...). Bản thân toast do ToastView lo.
        /// </summary>
        private void Toast(string message)
        {
            if (this.toast != null) this.toast.Show(message);
        }

        private void UpdateToggleLabels()
        {
            // KHÔNG dùng emoji: font mặc định của Unity chỉ có BMP nên 🔊 hiện ra ô
            // trống. Nút nhỏ trong game dùng ♪ và đổi MÀU để phân biệt bật/tắt; nút ở
            // menu dùng chữ, rõ hơn icon.
            bool on = PuzzleProgress.Sound;
            Text gameLabel = Ui.LabelOf(this.soundButton);
            gameLabel.text = "♪";
            gameLabel.color = on ? PuzzlePalette.Foreground : new Color(0.4f, 0.43f, 0.6f, 0.7f);

            Ui.LabelOf(this.menuSoundButton).text = "Âm thanh: " + (on ? "Bật" : "Tắt");
            Ui.LabelOf(this.menuSymbolButton).text = (PuzzleProgress.Symbols ? "◆" : "○") + " Ký hiệu";

            if (this.menuFreeButton != null)
            {
                bool free = PuzzleProgress.FreePlay;
                Ui.LabelOf(this.menuFreeButton).text = "Chơi tự do: " + (free ? "Bật" : "Tắt");
                this.menuFreeButton.GetComponent<Image>().color =
                    free ? PuzzlePalette.PanelLight : PuzzlePalette.Panel;
            }
            if (this.menuEndlessButton != null)
                Ui.LabelOf(this.menuEndlessButton).text = "∞  Vô tận — kỷ lục " + PuzzleProgress.EndlessBest;

            RefreshWallet();

            if (this.menuDailyButton != null)
            {
                int today = DailyChallenge.TodayKey();
                int streak = PuzzleProgress.DailyStreakLive(today);
                string tail = PuzzleProgress.DailyWon(today)
                    ? "đã xong ✓" + (streak > 0 ? "  chuỗi " + streak : "")
                    : streak > 0 ? "chuỗi " + streak + " ngày" : "bàn mới mỗi ngày";
                Ui.LabelOf(this.menuDailyButton).text = "✦  Thử thách hôm nay — " + tail;
            }
        }

        private void ToggleSound()
        {
            PuzzleProgress.Sound = !PuzzleProgress.Sound;
            this.audioPlayer.Enabled = PuzzleProgress.Sound;
            UpdateToggleLabels();
        }

        private void ToggleSymbols()
        {
            PuzzleProgress.Symbols = !PuzzleProgress.Symbols;
            UpdateToggleLabels();
            if (this.session != null && this.gameScreen.gameObject.activeSelf)
                this.board.Refresh(this.session, PuzzleProgress.Symbols);
        }

        // ==================================================================
        // Hoàn tác / gợi ý / xáo lại
        // ==================================================================

        /// <summary>Lối vào kiểm thử: đi qua ĐÚNG nút Hoàn tác, vì phần hoàn sao nằm ở đó.</summary>
        public void DebugUndo() { OnUndo(); }

        private void OnUndo()
        {
            if (this.busy) return;
            PuzzleSession.UndoResult outcome = this.session.Undo();
            if (outcome != PuzzleSession.UndoResult.Ok) { this.audioPlayer.Bad(); return; }

            this.board.SetDimmed(this.session, false, PuzzleProgress.Symbols);
            this.board.ClearChain();
            this.chainPreview.gameObject.SetActive(false);
            this.displayedScore = this.session.Score;
            this.scoreText.text = this.session.Score.ToString();
            // Hoàn tác một bước ĐÃ DÙNG vật phẩm thì phải trả sao lại. Không trả thì
            // hoàn tác biến thành hình phạt, và người chơi học cách không bao giờ bấm nó.
            PuzzleSession.ItemKind undone = this.session.LastUndoneItem;
            if (undone != PuzzleSession.ItemKind.None)
            {
                PuzzleProgress.RefundStars(PuzzleSession.ItemCost(undone));
                Toast("Đã hoàn ★" + PuzzleSession.ItemCost(undone) + ".");
            }

            this.audioPlayer.Undo();
            UpdateHud();
        }

        private void OnHint()
        {
            if (this.busy) return;
            int[] cells = this.session.FindHint();
            if (cells == null || cells.Length == 0) { this.audioPlayer.Bad(); return; }
            StartCoroutine(this.board.PlayHint(cells));
        }

        private void OnShuffle()
        {
            if (this.busy) return;
            StartCoroutine(ShuffleRoutine());
        }

        private IEnumerator ShuffleRoutine()
        {
            // Vô tận không có lời giải để dựng lại — chỉ cần gieo ra một bàn còn đi được.
            if (this.level.Endless)
            {
                if (this.session.ShufflesLeft <= 0 || !this.session.ReshuffleEndless())
                {
                    this.audioPlayer.Bad();
                    yield break;
                }
                this.board.Refresh(this.session, PuzzleProgress.Symbols);
                UpdateHud();
                this.audioPlayer.Tone(420f, 0.18f);
                Toast("Đã xáo lại bàn — mạch combo về 0.");
                yield break;
            }

            int movesLeft = this.session.MovesLeft;
            int cellsLeft = this.session.TotalLeft();
            ShufflePlan plan = this.session.PlanShuffle();

            // Không xáo ra một bàn vẫn thua — nói thẳng, không tiêu quota.
            if (plan == null)
            {
                this.audioPlayer.Bad();
                UpdateHud();
                LossReason reason = this.session.MovesUsed >= this.level.MaxMoves
                    ? LossAnalyzer.OutOfMoves(this.session)
                    : this.session.Analyze();
                if (reason != null)
                {
                    ShowLoseCard(new LossReason
                    {
                        Kind = reason.Kind,
                        Title = "Xáo cũng không cứu được",
                        Detail = "Còn " + cellsLeft + " ô mà chỉ còn " + movesLeft +
                                 " lượt — không có cách xáo nào đủ để dọn sạch."
                    });
                }
                yield break;
            }

            this.busy = true;
            // ApplyShuffle trả về ô nào trượt từ đâu tới đâu; Refresh phải chạy trước để
            // ô đã ở chỗ mới với màu mới, rồi PlaySlide mới dịch ngược về chỗ cũ.
            List<ShuffleMove> moves = this.session.ApplyShuffle(plan);
            this.board.SetDimmed(this.session, false, PuzzleProgress.Symbols);
            this.board.ClearChain();
            this.chainPreview.gameObject.SetActive(false);
            this.card.Hide();
            UpdateHud();

            for (int i = 0; i < 4; i++) this.audioPlayer.Tone(392f * Mathf.Pow(1.26f, i), 0.2f);
            yield return this.board.PlaySlide(moves, this.session);

            this.effects.FloatText(Vector2.zero,
                "Lời giải mới cần " + plan.RequiredMoves + " lượt", PuzzlePalette.Accent, 40, 90f);
            this.busy = false;
        }

        // ==================================================================
        // Thẻ thắng / thua
        // ==================================================================

        private void ShowWinCard(int stars, bool record)
        {
            // ĐẾM nút trước rồi mới mở thẻ: khung phải biết nó cần cao bao nhiêu
            bool last = this.levelIndex >= LevelCatalog.Levels.Length - 1;
            this.card.Begin(IsDuel ? 4 : 1 + (last ? 0 : 1) + (stars < 3 ? 1 : 0));

            Text title = Ui.Text("Title", this.card.Root, "Qua màn!",
                60, PuzzlePalette.Foreground, TextAnchor.UpperCenter, FontStyle.Bold);

            Text starRow = Ui.Text("Stars", this.card.Root,
                "<color=#FBBF24>" + new string('★', stars) + "</color>" +
                "<color=#3B4270>" + new string('★', 3 - stars) + "</color>",
                78, Color.white, TextAnchor.UpperCenter);
            starRow.supportRichText = true;

            string best = record ? "   ★ kỷ lục mới" : "";

            // Dòng huy hiệu chỉ hiện ở màn CÓ huy hiệu. Ở màn không có, im lặng còn hơn
            // báo "0/0" — người chơi sẽ đi tìm một thứ không tồn tại.
            string medal = "";
            if (this.level.MedalChains > 0)
            {
                medal = this.medalJustEarned
                    ? "\n<color=#34D399>◆ Huy hiệu kỹ thuật!  +★" + PuzzleProgress.MedalBonus + "</color>"
                    : this.session.MedalEarned
                        ? "\n◆ Huy hiệu kỹ thuật (đã có)"
                        : "\n◆ Kỹ thuật " + this.session.FullChains + "/" + this.level.MedalChains +
                          " chuỗi " + this.level.MaxChain + " ô";
            }

            Text detail = Ui.Text("Detail", this.card.Root,
                "Dùng " + this.session.MovesUsed + "/" + this.level.MaxMoves + " lượt · tối ưu " + this.level.Par +
                "\nĐiểm " + this.session.Score + best + medal,
                30, PuzzlePalette.Dim, TextAnchor.UpperCenter);
            detail.supportRichText = true;

            this.card.Header(new[] { title, starRow, detail }, new[] { 60, 78, 30 });

            int slot = 0;
            if (IsDuel)
            {
                this.card.AddButton("Sao chép kết quả", slot++, true, this.duel.CopyResult);
                this.card.AddButton("Dán kết quả đối thủ", slot++, false, this.duel.PasteOpponentResult);
                this.card.AddButton("Chơi lại", slot++, false, RestartLevel);
                this.card.AddButton("Danh sách màn", slot, false, ShowMenu);
                return;
            }
            if (!last) this.card.AddButton("Màn tiếp theo →", slot++, true, () => OpenLevel(this.levelIndex + 1));
            if (stars < 3) this.card.AddButton("Thử lại để lấy 3★", slot++, false, RestartLevel);
            this.card.AddButton("Danh sách màn", slot, false, ShowMenu);
        }

        private void ShowLoseCard(LossReason reason)
        {
            // ĐẾM nút trước rồi mới mở thẻ
            bool canShuffle = this.session.CanShuffle;
            bool canUndo = this.session.CanUndo;
            // Ván đấu: bỏ phao (xáo/hoàn tác) và thay bằng hai nút chia sẻ kết quả — thua
            // rồi vẫn phải so được, vì luật phân định tính cả ca "cả hai đều bí".
            this.card.Begin(IsDuel ? 4 : 2 + (canShuffle ? 1 : 0) + (canUndo ? 1 : 0));

            Text title = Ui.Text("Title", this.card.Root, reason.Title,
                56, PuzzlePalette.Bad, TextAnchor.UpperCenter, FontStyle.Bold);

            Text detail = Ui.Text("Detail", this.card.Root, reason.Detail,
                30, PuzzlePalette.Dim, TextAnchor.UpperCenter);

            this.card.Header(new[] { title, detail }, new[] { 56, 30 });

            int slot = 0;
            if (IsDuel)
            {
                this.card.AddButton("Sao chép kết quả", slot++, true, this.duel.CopyResult);
                this.card.AddButton("Dán kết quả đối thủ", slot++, false, this.duel.PasteOpponentResult);
                this.card.AddButton("↻ Chơi lại bàn này", slot++, false, RestartLevel);
                this.card.AddButton("Danh sách màn", slot, false, ShowMenu);
                return;
            }
            if (canShuffle)
                this.card.AddButton("⇄ Xáo lại màn (còn " + this.session.ShufflesLeft + ")", slot++, slot == 1, OnShuffleFromCard);
            if (canUndo)
                this.card.AddButton("↶ Hoàn tác (còn " + this.session.UndosLeft + ")", slot++, slot == 1, OnUndoFromCard);
            this.card.AddButton("↻ Chơi lại màn", slot++, slot == 1, RestartLevel);
            this.card.AddButton("Danh sách màn", slot, false, ShowMenu);
        }

        /// <summary>Thẻ kết ván vô tận: chỉ có điểm, kỷ lục và chơi lại.</summary>
        private void ShowEndlessCard(bool record)
        {
            this.card.Begin(2);

            Text title = Ui.Text("Title", this.card.Root, "Hết nước đi",
                56, PuzzlePalette.Foreground, TextAnchor.UpperCenter, FontStyle.Bold);

            Text score = Ui.Text("Score", this.card.Root, this.session.Score.ToString(),
                78, PuzzlePalette.Accent, TextAnchor.UpperCenter, FontStyle.Bold);

            Text detail = Ui.Text("Detail", this.card.Root,
                this.session.MovesUsed + " nước · " +
                (record ? "★ kỷ lục mới" : "kỷ lục " + PuzzleProgress.EndlessBest),
                30, PuzzlePalette.Dim, TextAnchor.UpperCenter);

            this.card.Header(new[] { title, score, detail }, new[] { 56, 78, 30 });

            this.card.AddButton("↻ Chơi ván mới", 0, true, RestartLevel);
            this.card.AddButton("Danh sách màn", 1, false, ShowMenu);

            if (record)
            {
                Celebrate();
            }
        }

        /// <summary>
        /// Lối vào cho kiểm thử: dựng thẳng thẻ thua với lý do cho trước rồi trả về khung
        /// thẻ, để bài kiểm tra đo được bố cục THẬT thay vì tính lại theo hằng số.
        /// </summary>
        public RectTransform DebugShowLoseCard(LossReason reason)
        {
            ShowLoseCard(reason);
            return this.card.Root;
        }

        private void OnShuffleFromCard()
        {
            this.card.Hide();
            OnShuffle();
        }

        private void OnUndoFromCard()
        {
            this.card.Hide();
            OnUndo();
        }

        /// <summary>Dòng phụ của thử thách: hôm nay đã xong chưa, chuỗi bao nhiêu ngày.</summary>
        private string DailyBadge()
        {
            int streak = PuzzleProgress.DailyStreakLive(this.dailyKey);
            string s = this.level.TotalCells + " ô · " + this.level.Config.Colors + " màu";
            if (PuzzleProgress.DailyWon(this.dailyKey))
                s += " · đã xong ✓ (" + PuzzleProgress.DailyBest(this.dailyKey) + " điểm)";
            if (streak > 0) s += " · chuỗi " + streak;
            return s;
        }


        // ==================================================================
        // Vật phẩm — bảng và nút; luật mua/dùng nằm trong ItemShop
        // ==================================================================

        [SerializeField] private Button itemButton;                 // nút mở bảng, nằm trong hàng điều khiển
        [SerializeField] private Text itemBalanceText;              // huy hiệu ★ ở góc nút
        [SerializeField] private RectTransform itemPanel;           // bảng chọn, ẩn mặc định
        [SerializeField] private Button itemPanelCatcher;           // chạm ra ngoài để đóng
        [SerializeField] private Button[] itemRows;
        [SerializeField] private Text[] itemRowCosts;
        [SerializeField] private Text itemWalletText;               // dòng số dư trong bảng
        [SerializeField] private ItemPanelView itemView;

        /// <summary>Cửa hàng vật phẩm. Lớp C# thuần nên dựng lại trong WireAll.</summary>
        private ItemShop items;

        /// <summary>
        /// Thử thách hằng ngày và ván đấu KHÔNG cho dùng vật phẩm, dù Core vẫn cho:
        /// điểm của mọi máy chỉ so được với nhau khi ai cũng chơi đúng một bàn với đúng
        /// một bộ luật.
        /// </summary>
        private bool ItemsUsable =>
            this.session != null && this.session.ItemsAllowed && !IsDaily && !IsDuel;

        private void BuildItemControls()
        {
            this.itemButton = BuildControl("Items", 3, "", "Vật phẩm", out this.itemBalanceText);

            // Nút này dùng ICON VẼ thay vì ký tự: chỗ chữ của BuildControl để rỗng rồi
            // đặt ảnh đè lên, nên không phải sửa BuildControl cho riêng một nút.
            Transform iconSlot = this.itemButton.transform.Find("Icon");
            Image icon = Ui.Image("IconArt", (RectTransform)iconSlot.parent,
                                  Color.white, PuzzleSprites.HammerIcon);
            RectTransform ir = icon.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 1f);
            ir.pivot = new Vector2(0.5f, 1f);
            ir.sizeDelta = new Vector2(46, 46);
            ir.anchoredPosition = new Vector2(0, -18);

            BuildItemPanel();
        }

        /// <summary>
        /// Nạp bảng vật phẩm TỪ PREFAB. Lớp chặn vẫn dựng bằng code, xem BuildDuelPanel.
        ///
        /// Bảng dựng sẵn rồi ẩn đi, không dựng lại mỗi lần mở: dựng lại nghĩa là Destroy
        /// các con cũ, mà Destroy bị hoãn tới cuối frame — mở nhanh hai lần là có hai bộ
        /// nút chồng lên nhau.
        /// </summary>
        private void BuildItemPanel()
        {
            this.itemPanelCatcher = Ui.Button("ItemCatcher", this.gameScreen, "", 1,
                new Color(0.03f, 0.04f, 0.08f, 0.72f), Color.clear, PuzzlePalette.RadiusPanel, false, false);
            Ui.Stretch(this.itemPanelCatcher.GetComponent<RectTransform>(), 0, 0, 0, 0);
            Image catcher = this.itemPanelCatcher.GetComponent<Image>();
            catcher.sprite = PuzzleSprites.Square;
            catcher.type = Image.Type.Simple;
            this.itemPanelCatcher.gameObject.SetActive(false);

            var prefab = Resources.Load<GameObject>(ItemPanelResourcePath);
            if (prefab == null)
            {
                Debug.LogError("[UI] Thiếu prefab " + ItemPanelResourcePath +
                               ". Chạy menu Connect Puzzle > Prefab > Cắt bảng vật phẩm.");
                return;
            }

            GameObject instance = Instantiate(prefab, this.gameScreen, false);
            instance.name = "ItemPanel";
            this.itemView = instance.GetComponent<ItemPanelView>();
            if (this.itemView == null)
            {
                Debug.LogError("[UI] Prefab bảng vật phẩm thiếu ItemPanelView.");
                return;
            }

            System.Collections.Generic.List<string> missing = this.itemView.MissingFields();
            if (missing.Count > 0)
                Debug.LogError("[UI] Prefab bảng vật phẩm chưa gán: " + string.Join(", ", missing));

            this.itemPanel = (RectTransform)instance.transform;
            this.itemWalletText = this.itemView.Wallet;
            this.itemRows = this.itemView.Rows;
            this.itemRowCosts = this.itemView.Costs;
            this.itemPanel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Chạy hoạt ảnh của một lần dùng vật phẩm rồi đánh giá lại ván.
        ///
        /// Ở lại PuzzleGame chứ không vào ItemShop: nó động tới busy, bàn, HUD và Evaluate
        /// — đó là luồng ván chơi, không phải việc của cửa hàng.
        /// </summary>
        private IEnumerator PlayItemEffect(MoveResult effect)
        {
            this.busy = true;
            this.items.RefreshBar();

            this.board.SetDimmed(this.session, false, PuzzleProgress.Symbols);
            if (effect != null && (effect.CrackedIce.Count > 0 || effect.ThawedIce.Count > 0))
                yield return this.board.PlayIce(effect.CrackedIce, effect.ThawedIce);

            this.board.Refresh(this.session, PuzzleProgress.Symbols);
            this.busy = false;
            UpdateHud();

            // Đập ô có thể mở lại thế cờ đang bí, hoặc +1 lượt có thể gỡ án hết lượt —
            // nên phải đánh giá lại, không thì thẻ thua vẫn còn treo sau khi đã cứu.
            Evaluate();
        }

        /// <summary>
        /// Số sao TIÊU ĐƯỢC (đã trừ phần đã tiêu), kèm số huy hiệu. Hiện cả hai vì chúng
        /// là hai nguồn khác nhau: sao đến từ đi ít lượt, huy hiệu đến từ chuỗi dài —
        /// thấy cả hai thì mới biết nên cày cái nào để có thêm tiền.
        ///
        /// Ở lại đây chứ không vào ItemShop vì nó vẽ CẢ ví trên menu, mà menu thì không
        /// liên quan gì tới cửa hàng.
        /// </summary>
        private void RefreshWallet()
        {
            int levels = LevelCatalog.Levels.Length;
            int balance = PuzzleProgress.StarsBalance(levels);
            int medals = PuzzleProgress.MedalCount(levels);
            string text = "<color=#FBBF24>★ " + balance + "</color>";
            if (medals > 0) text += "  <color=#34D399>◆ " + medals + "</color>";

            if (this.menuWalletText != null) this.menuWalletText.text = text;
            if (this.items != null) this.items.ShowWallet(text, balance);
        }

        // ---- những gì cửa hàng cần từ màn chơi. Cài TƯỜNG MINH: đây là hợp đồng với
        //      ItemShop, không phải API của PuzzleGame.
        PuzzleSession ItemShop.IHost.Session => this.session;
        bool ItemShop.IHost.Busy => this.busy;
        bool ItemShop.IHost.ItemsUsable => ItemsUsable;
        void ItemShop.IHost.Toast(string message) => Toast(message);
        void ItemShop.IHost.BadSound() => this.audioPlayer.Bad();
        void ItemShop.IHost.Tone(float hertz, float seconds) => this.audioPlayer.Tone(hertz, seconds);
        void ItemShop.IHost.PlayItemEffect(MoveResult effect) => StartCoroutine(PlayItemEffect(effect));
        void ItemShop.IHost.RefreshWallet() => RefreshWallet();

        // ==================================================================
        // Đấu — bảng, nút, và số đo; luật nằm trong DuelController
        // ==================================================================

        [SerializeField] private Button menuDuelButton;
        [SerializeField] private RectTransform duelPanel;
        [SerializeField] private Button duelCatcher;
        [SerializeField] private Text duelCodeText;
        [SerializeField] private InputField duelInput;
        [SerializeField] private Text duelStatusText;
        [SerializeField] private DuelPanelView duelView;

        [SerializeField] private DuelLanLink lan;
        [SerializeField] private RectTransform lanPanel;
        [SerializeField] private Button lanCatcher;
        [SerializeField] private Text lanStatusText;
        [SerializeField] private Button lanHostButton, lanSeekButton;
        [SerializeField] private LanPanelView lanView;

        /// <summary>
        /// Toàn bộ luật của chế độ đấu. Là lớp C# thuần nên dựng lại trong WireAll.
        ///
        /// Các tham chiếu UI ở TRÊN vẫn nằm lại PuzzleGame chứ không chuyển sang
        /// DuelController: chúng là [SerializeField] và đã được lưu vào PuzzleRoot.prefab.
        /// Dời sang một component khác là làm gãy hết rồi phải nối tay lại.
        /// </summary>
        private DuelController duel;

        private const int DuelIndex = -3;
        private bool IsDuel => this.levelIndex == DuelIndex;

        /// <summary>Chiều cao bảng đấu, phải đủ chứa đúng chuỗi phần tử xếp trong prefab.</summary>
        private const float DuelPanelHeight = 980f;

        /// <summary>Tên prefab bảng đấu trong Resources.</summary>
        public const string DuelPanelResourcePath = "UI/DuelPanel";

        /// <summary>Tên prefab bảng Wi-Fi. Dùng chung giữa runtime và script editor.</summary>
        public const string LanPanelResourcePath = "UI/LanPanel";

        // ---- số đo, cho bài kiểm đọc thay vì chép lại hằng số
        public float DuelContentHeight => this.duel == null ? 0f : this.duel.ContentHeight;
        public float DuelPanelSize => DuelPanelHeight;
        public float LanPanelSize => this.duel == null ? 0f : this.duel.LanPanelHeight;
        public float LanContentHeight => this.duel == null ? 0f : this.duel.LanContentHeight;

        /// <summary>
        /// Nạp bảng đấu TỪ PREFAB. Lớp chặn vẫn dựng bằng code và vẫn là EM RUỘT của bảng,
        /// không phải con — đưa nó vào prefab thành con chính là thứ đã gây lỗi "chạm đâu
        /// cũng tắt bảng" ở bảng Wi-Fi.
        /// </summary>
        private void BuildDuelPanel()
        {
            this.duelCatcher = Ui.Button("DuelCatcher", this.menuScreen, "", 1,
                new Color(0.03f, 0.04f, 0.08f, 0.78f), Color.clear, PuzzlePalette.RadiusPanel, false, false);
            Ui.Stretch(this.duelCatcher.GetComponent<RectTransform>(), 0, 0, 0, 0);
            Image catcherImage = this.duelCatcher.GetComponent<Image>();
            catcherImage.sprite = PuzzleSprites.Square;
            catcherImage.type = Image.Type.Simple;
            this.duelCatcher.gameObject.SetActive(false);

            var prefab = Resources.Load<GameObject>(DuelPanelResourcePath);
            if (prefab == null)
            {
                Debug.LogError("[UI] Thiếu prefab " + DuelPanelResourcePath +
                               ". Chạy menu Connect Puzzle > Prefab > Cắt bảng đấu seed.");
                return;
            }

            GameObject instance = Instantiate(prefab, this.menuScreen, false);
            instance.name = "DuelPanel";
            this.duelView = instance.GetComponent<DuelPanelView>();
            if (this.duelView == null)
            {
                Debug.LogError("[UI] Prefab bảng đấu thiếu DuelPanelView.");
                return;
            }

            System.Collections.Generic.List<string> missing = this.duelView.MissingFields();
            if (missing.Count > 0)
                Debug.LogError("[UI] Prefab bảng đấu chưa gán: " + string.Join(", ", missing));

            this.duelPanel = (RectTransform)instance.transform;
            this.duelCodeText = this.duelView.Code;
            this.duelInput = this.duelView.Input;
            this.duelStatusText = this.duelView.Status;
            this.duelPanel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Nạp bảng Wi-Fi TỪ PREFAB thay vì dựng bằng code.
        ///
        /// Không có nhánh dự phòng "thiếu prefab thì dựng bằng code". Có nhánh đó thì bài
        /// kiểm sẽ chạy nhánh dự phòng còn game chạy nhánh prefab, và ta có test xanh trên
        /// một sản phẩm hỏng — kiểu thất bại tệ nhất. Thiếu prefab thì phải NỔ ngay.
        /// </summary>
        private void BuildLanPanel()
        {
            var prefab = Resources.Load<GameObject>(LanPanelResourcePath);
            if (prefab == null)
            {
                Debug.LogError("[UI] Thiếu prefab " + LanPanelResourcePath +
                               ". Chạy menu Connect Puzzle > Dựng lại prefab bảng Wi-Fi.");
                return;
            }

            GameObject instance = Instantiate(prefab, this.menuScreen, false);
            instance.name = "LanPanel";
            this.lanView = instance.GetComponent<LanPanelView>();
            if (this.lanView == null)
            {
                Debug.LogError("[UI] Prefab bảng Wi-Fi thiếu component LanPanelView.");
                return;
            }

            System.Collections.Generic.List<string> missing = this.lanView.MissingFields();
            if (missing.Count > 0)
                Debug.LogError("[UI] Prefab bảng Wi-Fi chưa gán: " + string.Join(", ", missing));

            // Lớp chặn dựng bằng CODE, là em ruột của bảng — giống bảng đấu và bảng vật
            // phẩm. Không đưa vào prefab: xem chú thích ở DuelPanelView.
            this.lanCatcher = Ui.Button("LanCatcher", this.menuScreen, "", 1,
                new Color(0.03f, 0.04f, 0.08f, 0.78f), Color.clear, PuzzlePalette.RadiusPanel, false, false);
            Ui.Stretch(this.lanCatcher.GetComponent<RectTransform>(), 0, 0, 0, 0);
            Image lanCatcherImage = this.lanCatcher.GetComponent<Image>();
            lanCatcherImage.sprite = PuzzleSprites.Square;
            lanCatcherImage.type = Image.Type.Simple;
            this.lanCatcher.gameObject.SetActive(false);

            this.lanPanel = this.lanView.Panel;
            this.lanStatusText = this.lanView.Status;
            this.lanHostButton = this.lanView.HostButton;
            this.lanSeekButton = this.lanView.SeekButton;
            this.lanPanel.gameObject.SetActive(false);
        }

        // ---- lối vào cho kiểm thử: đi qua ĐÚNG các hàm mà nút thật gọi
        public void DebugStartDuel(int seed, int preset) { this.duel.Start(seed, preset); }
        public void DebugCopyDuelResult() { this.duel.CopyResult(); }
        public void DebugPasteOpponentResult() { this.duel.PasteOpponentResult(); }
        public void DebugForceDuelEnd() { this.duel.CaptureResult(); }
        public DuelLanLink DebugLan => this.duel.Link;
        public void DebugOpenLanPanel() { this.duel.OpenLanPanel(); }
        public void DebugStartLanHost() { this.duel.TestStartLanHost(); }
        public bool DebugLanActive => this.duel.LanActive;
        public string DebugLanStatus => this.duel.LanStatus;
        public void DebugFeedLanInvite(int seed, int preset, string who)
            => this.duel.TestFeedLanInvite(seed, preset, who);
        public void DebugFeedLanResult(DuelResult r, string who)
            => this.duel.TestFeedLanResult(r, who);

        // ---- những gì chế độ đấu cần từ màn chơi.
        //
        // Cài TƯỜNG MINH (explicit) chứ không để public: đây là hợp đồng với DuelController,
        // không phải API của PuzzleGame. Để public thì mọi chỗ khác cũng gọi được, và cái
        // ranh giới vừa dựng lên sẽ mòn đi trong vài tuần.
        PuzzleSession DuelController.IHost.Session => this.session;
        bool DuelController.IHost.OnDuelBoard => IsDuel;
        OverlayCard DuelController.IHost.Card => this.card;

        string DuelController.IHost.Clipboard
        {
            get { return Clipboard; }
            set { Clipboard = value; }
        }

        void DuelController.IHost.Toast(string message) => Toast(message);
        void DuelController.IHost.BadSound() => this.audioPlayer.Bad();
        void DuelController.IHost.Tone(float hertz, float seconds) => this.audioPlayer.Tone(hertz, seconds);
        void DuelController.IHost.Celebrate() => Celebrate();
        void DuelController.IHost.OpenDuelBoard(LevelData board) => OpenLevelData(DuelIndex, board);
        void DuelController.IHost.RestartLevel() => RestartLevel();
        void DuelController.IHost.ShowMenu() => ShowMenu();

        /// <summary>
        /// Pháo giấy + chuỗi bốn nốt. Dùng cho cả thắng màn, hết ván vô tận, và thắng một
        /// ván đấu — ba chỗ này trước đây chép lại đúng hai dòng giống hệt nhau.
        /// </summary>
        private void Celebrate()
        {
            this.effects.Confetti(Vector2.zero, this.board.Root.sizeDelta.x * 0.5f);
            for (int i = 0; i < 4; i++) this.audioPlayer.Tone(523f * Mathf.Pow(1.26f, i), 0.3f);
        }

        // ==================================================================
        // Clipboard
        // ==================================================================

        /// <summary>
        /// Đọc/ghi clipboard qua một lớp trung gian thay vì gọi thẳng GUIUtility.
        ///
        /// Lý do là một lỗi ĐO ĐƯỢC, không phải sở thích kiến trúc: clipboard của Windows
        /// là tài nguyên DÙNG CHUNG và độc quyền. Khi Unity Editor mở cùng lúc với rig kiểm
        /// thử, lệnh ghi của rig hỏng im lặng và 8 phép kiểm về chia sẻ kết quả cùng đỏ —
        /// tái hiện được 2/2 lần, không phải chập chờn.
        ///
        /// Bài kiểm thay hai hàm này bằng một chuỗi trong bộ nhớ. Đổi lại, bài kiểm KHÔNG
        /// còn chứng minh clipboard thật hoạt động — phần đó chỉ có bấm Play trên máy mới
        /// biết. Nhưng nó vẫn chạy trọn vẹn phần logic thật: moi mã, so kết quả, phán quyết.
        /// </summary>
        public static System.Func<string> ClipboardRead = () => GUIUtility.systemCopyBuffer;
        public static System.Action<string> ClipboardWrite = v => GUIUtility.systemCopyBuffer = v;

        private static string Clipboard
        {
            get { return ClipboardRead == null ? "" : (ClipboardRead() ?? ""); }
            set { if (ClipboardWrite != null) ClipboardWrite(value); }
        }

        /// <summary>
        /// Tham chiếu scene còn trống. Rỗng nghĩa là prefab gốc nối đủ.
        ///
        /// Chỉ liệt kê những thứ mà THIẾU LÀ HỎNG NGAY, không liệt kê hết 52 field: mục
        /// đích là câu báo lỗi đọc được, không phải một danh sách dài không ai đọc.
        /// </summary>
        public System.Collections.Generic.List<string> MissingSceneRefs()
        {
            var missing = new System.Collections.Generic.List<string>();
            if (this.canvas == null) missing.Add(nameof(this.canvas));
            if (this.contentRoot == null) missing.Add(nameof(this.contentRoot));
            if (this.menuScreen == null) missing.Add(nameof(this.menuScreen));
            if (this.gameScreen == null) missing.Add(nameof(this.gameScreen));
            if (this.boardArea == null) missing.Add(nameof(this.boardArea));
            if (this.card == null) missing.Add(nameof(this.card));
            if (this.diagBanner == null) missing.Add(nameof(this.diagBanner));
            if (this.levelViewport == null) missing.Add(nameof(this.levelViewport));
            if (this.levelContent == null) missing.Add(nameof(this.levelContent));
            if (this.toast == null) missing.Add(nameof(this.toast));
            if (this.scoreText == null) missing.Add(nameof(this.scoreText));
            if (this.movesText == null) missing.Add(nameof(this.movesText));
            if (this.undoButton == null) missing.Add(nameof(this.undoButton));
            if (this.duelPanel == null) missing.Add(nameof(this.duelPanel));
            if (this.itemPanel == null) missing.Add(nameof(this.itemPanel));
            if (this.lanPanel == null) missing.Add(nameof(this.lanPanel));
            return missing;
        }
    }
}
