using System.Collections.Generic;
using ConnectPuzzle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Màn menu: tiêu đề, ví sao, ba nút chế độ, lưới 90 màn cuộn được, và bốn nút gạt
    /// ở chân màn.
    ///
    /// Đặt trên chính node MenuScreen và tự giữ 12 tham chiếu của nó. Trước đây cả 12
    /// nằm trên PuzzleGame, cùng với ~560 dòng bố cục và dựng lưới — đó là khối lớn
    /// nhất khiến PuzzleGame phải biết từng cái nút trong game.
    ///
    /// Bố cục ở đây KHÔNG phải hằng số mà là công thức: ba thứ cùng thích ứng theo khung
    /// (chrome co lại, số cột tăng, cỡ nút lấy theo chiều rộng), vì chỉ đổi cỡ nút là
    /// không đủ — trên canvas logic cao 810 thì riêng chrome đã 592px.
    ///
    /// Những gì cần từ ván chơi đi qua IHost, và danh sách đó CỐ Ý ngắn.
    /// </summary>
    public sealed class MenuScreen : MonoBehaviour
    {
        /// <summary>
        /// Phần ván chơi mà menu cần tới. Toàn là "người chơi vừa bấm cái này" — menu
        /// không tự quyết định gì về ván.
        /// </summary>
        public interface IHost
        {
            /// <summary>Bấm một nút màn. Menu KHÔNG tự kiểm mở khoá — đó là luật, không phải hiển thị.</summary>
            void PickLevel(int index);

            void OpenEndless();
            void OpenDaily();
            void OpenDuel();
            void ToggleSound();
            void ToggleSymbols();
            void ToggleFreePlay();
            void ResetProgress();

            /// <summary>
            /// Bấm nhãn thế giới — mở thẻ của thế giới đó (hướng dẫn cơ chế, leo tháp).
            /// Menu không biết trên thẻ có gì; nó chỉ nói "người chơi vừa bấm nhãn này".
            /// </summary>
            void OpenWorldCard(int world);
        }

        /// <summary>Tên prefab nút chọn màn trong Resources.</summary>
        public const string LevelButtonResource = "UI/LevelButton";


        private IHost host;
        private RectTransform rect;
        private Vector2 lastArea;

        /// <summary>Khung màn menu.</summary>
        public RectTransform Rect =>
            this.rect != null ? this.rect : (this.rect = (RectTransform)this.transform);

        /// <summary>Nút mở bảng đấu — bảng đấu cần nó để tự nối cú bấm.</summary>
        public Button DuelButton => this.menuDuelButton;

        public List<string> MissingFields()
        {
            var missing = new List<string>();
            if (this.menuWalletText == null) missing.Add(nameof(this.menuWalletText));
            if (this.menuEndlessButton == null) missing.Add(nameof(this.menuEndlessButton));
            if (this.menuDailyButton == null) missing.Add(nameof(this.menuDailyButton));
            if (this.menuDuelButton == null) missing.Add(nameof(this.menuDuelButton));
            if (this.menuSoundButton == null) missing.Add(nameof(this.menuSoundButton));
            if (this.menuSymbolButton == null) missing.Add(nameof(this.menuSymbolButton));
            if (this.menuFreeButton == null) missing.Add(nameof(this.menuFreeButton));
            if (this.menuResetButton == null) missing.Add(nameof(this.menuResetButton));
            if (this.levelViewport == null) missing.Add(nameof(this.levelViewport));
            if (this.levelContent == null) missing.Add(nameof(this.levelContent));
            return missing;
        }

        public void BindForAuthoring(Text wallet, Button endless, Button daily, Button duel)
        {
            this.menuWalletText = wallet;
            this.menuEndlessButton = endless;
            this.menuDailyButton = daily;
            this.menuDuelButton = duel;
        }

        /// <summary>
        /// Nối host và listener. Gọi lại được: mọi nút đều gỡ listener trước.
        ///
        /// Tách khỏi Awake vì bài kiểm dựng UI ở edit mode, nơi Awake không chạy.
        /// </summary>
        public void Wire(IHost owner)
        {
            this.host = owner;
            Bind(this.menuEndlessButton, owner.OpenEndless);
            Bind(this.menuDailyButton, owner.OpenDaily);
            Bind(this.menuDuelButton, owner.OpenDuel);
            Bind(this.menuSoundButton, owner.ToggleSound);
            Bind(this.menuSymbolButton, owner.ToggleSymbols);
            Bind(this.menuFreeButton, owner.ToggleFreePlay);
            Bind(this.menuResetButton, owner.ResetProgress);
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        /// <summary>Vẽ ví sao. Chuỗi do PuzzleGame dựng để menu và bảng vật phẩm nói giống nhau.</summary>
        public void ShowWallet(string richText)
        {
            if (this.menuWalletText != null) this.menuWalletText.text = richText;
        }

        /// <summary>
        /// Bố cục lại khi khung đổi (quay máy, đổi lề an toàn).
        ///
        /// So kích thước trước khi làm gì: LayoutMenu đo chữ thật nên nó không rẻ, chạy
        /// mỗi khung hình là phí.
        /// </summary>
        public void Tick()
        {
            Vector2 size = Rect.rect.size;
            if ((size - this.lastArea).sqrMagnitude <= 1f) return;
            this.lastArea = size;
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
        /// <summary>
        /// Dựng phần bên trong menu: vùng cuộn, lưới 90 màn, và chân màn.
        ///
        /// Tiêu đề và ba nút chế độ do PuzzleGame dựng rồi truyền vào, vì chúng phải
        /// có TRƯỚC khi bảng đấu dựng (bảng đấu cần nút mở của nó).
        /// </summary>
        public void BuildContents()
        {
            BuildLevelScroll();
            BuildGrid();
            BuildMenuFooter();
            LayoutMenu();   // bố cục ngay một lần để không phần tử nào ở trạng thái cỡ 0
        }

        private void BuildLevelScroll()
        {
            this.levelViewport = Ui.Node("LevelViewport", Rect);
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

        /// <summary>Dựng 90 nút màn từ prefab. Gọi lại được — dùng lại nút cũ theo tên.</summary>
        public void BuildGrid()
        {
            // Nạp MỘT lần. Resources.Load trong vòng lặp 90 vòng là 90 lần tra bảng asset,
            // và nếu thiếu thì báo lỗi 90 lần thay vì một lần.
            this.levelButtonPrefab = Resources.Load<GameObject>(LevelButtonResource);
            if (this.levelButtonPrefab == null)
            {
                Debug.LogError("[UI] Thiếu prefab " + LevelButtonResource +
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
                    MakeWorldHeaderTappable(header, cfg.World);
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
                button.onClick.AddListener(() => this.host.PickLevel(captured));

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
            Vector2 real = Rect.rect.size;
            bool canMeasure = real.x >= 1f && real.y >= 1f;
            Vector2 size = canMeasure ? real : new Vector2(1080f, 1920f);

            Text title = Rect.Find("Title").GetComponent<Text>();
            Text subtitle = Rect.Find("Subtitle").GetComponent<Text>();

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

        /// <summary>
        /// Nút mở bảng đấu. Ở lại đây vì LayoutMenu xếp nó cùng hai nút chế độ kia;
        /// hành vi của nó thì DuelPanel giữ. Sẽ theo MenuScreen ở bước sau.
        /// </summary>
        [SerializeField] private Button menuDuelButton;
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
            this.menuSoundButton = Ui.Button("MenuSound", Rect, "", 30,
                PuzzlePalette.Panel, PuzzlePalette.Dim);
            PlaceBottomRow(this.menuSoundButton.GetComponent<RectTransform>(), 128, 0, 2, 320, 88);

            this.menuSymbolButton = Ui.Button("MenuSymbols", Rect, "", 30,
                PuzzlePalette.Panel, PuzzlePalette.Dim);
            PlaceBottomRow(this.menuSymbolButton.GetComponent<RectTransform>(), 128, 1, 2, 320, 88);

            this.menuFreeButton = Ui.Button("MenuFree", Rect, "", 28,
                PuzzlePalette.Panel, PuzzlePalette.Dim);
            PlaceBottomRow(this.menuFreeButton.GetComponent<RectTransform>(), 226, 0, 1, 654, 84);

            // nút dạng link: nền trong suốt, không viền, bo nhỏ vì nó thấp
            this.menuResetButton = Ui.Button("MenuReset", Rect, "Xoá tiến độ", 26,
                new Color(0, 0, 0, 0), PuzzlePalette.Dim, 24, false, false);
            PlaceBottomRow(this.menuResetButton.GetComponent<RectTransform>(), 40, 0, 1, 320, 72);
        }

        /// <summary>
        /// Nhãn thế giới thành chỗ bấm được để XEM LẠI bài hướng dẫn.
        ///
        /// Cần có vì bài hướng dẫn chỉ tự hiện MỘT lần: bỏ qua nó rồi thì không còn đường
        /// nào quay lại, và người chơi gặp một bàn đầy đá mà không ai giải thích đá là gì.
        /// Đặt lên nhãn thế giới chứ không thêm nút mới, vì nhãn đã nằm ngay trên đúng
        /// nhóm màn mà nó nói về — và thêm nút là phải sửa prefab.
        ///
        /// Chữ được gán ở CẢ HAI nhánh (nhận lại từ prefab hay tự dựng): nhánh nhận lại
        /// trước đây giữ nguyên chữ của prefab, nên dấu "(?)" sẽ có hoặc không tuỳ node
        /// đến từ đâu.
        /// </summary>
        private void MakeWorldHeaderTappable(Text header, int world)
        {
            if (header == null) return;

            header.text = LevelCatalog.WorldName(world).ToUpperInvariant() + "   (?)";

            // Text do Ui.Text dựng có raycastTarget = false, và node từ prefab thì tuỳ
            // prefab — bật tường minh, không có nó thì nút nhận không nổi cú chạm.
            header.raycastTarget = true;

            // BuildGrid gọi lại được, nên không thêm Button lần thứ hai.
            var button = header.GetComponent<Button>();
            if (button == null)
            {
                button = header.gameObject.AddComponent<Button>();
                button.targetGraphic = header;
            }

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            int captured = world;
            button.onClick.RemoveAllListeners();

            // Đọc this.host lúc BẤM, không lúc dựng: BuildGrid chạy TRƯỚC Wire (lưới màn
            // dựng theo tiến trình nên nó phải xong trước), nên lúc này host vẫn là null.
            button.onClick.AddListener(() => this.host?.OpenWorldCard(captured));
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

        /// <summary>Vẽ lại trạng thái mở khoá và số sao của 90 nút màn, rồi bố cục lại.</summary>
        public void Refresh()
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
        }

        /// <summary>
        /// Nhãn của bốn nút gạt + hai nút chế độ. Chúng là CÂU có số ("kỷ lục 12345",
        /// "chuỗi 7 ngày") nên phải vẽ lại mỗi khi tiến độ đổi.
        ///
        /// KHÔNG dùng emoji: font mặc định của Unity chỉ có BMP nên 🔊 hiện ra ô trống.
        /// </summary>
        public void RefreshLabels()
        {
            // Nút này đi qua BA trạng thái (tắt / tiếng / tiếng + rung) nên nhãn không còn
            // suy ra được từ một bool. Xem PuzzleProgress.FeedbackLabel.
            Ui.LabelOf(this.menuSoundButton).text = PuzzleProgress.FeedbackLabel();
            this.menuSoundButton.GetComponent<Image>().color =
                PuzzleProgress.Sound || PuzzleProgress.Haptics
                    ? PuzzlePalette.PanelLight : PuzzlePalette.Panel;

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
    }
}
