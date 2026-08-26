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
    public sealed class PuzzleGame : MonoBehaviour, DuelPanel.IHost, ItemPanel.IHost, MenuScreen.IHost
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
        /// <summary>Khung vùng bàn. Component nằm trên chính node BoardArea.</summary>
        [SerializeField] private BoardArea boardArea;
        /// <summary>Thẻ kết ván. Component nằm trên chính node Overlay và tự giữ ref của nó.</summary>
        [SerializeField] private OverlayCard card;

        private BoardView board;
        private EffectLayer effects;
        private PuzzleAudio audioPlayer;

        /// <summary>Bảng số liệu đầu màn chơi. Component nằm trên GameScreen.</summary>
        [SerializeField] private GameHud hud;

        /// <summary>Hàng nút dưới màn chơi + nút quay lại và nút âm thanh.</summary>
        [SerializeField] private ControlBar controls;

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
        /// Nối UI đã có sẵn từ prefab gốc vào ván chơi.
        ///
        /// KHÔNG dựng gì cả — prefab là nguồn duy nhất. Trước đây hàm này có hai đường:
        /// một đường dựng toàn bộ UI bằng code, một đường dùng prefab. Giữ cả hai nghĩa
        /// là giữ hai bản mô tả cùng một giao diện, và chúng trôi khỏi nhau lặng lẽ.
        ///
        /// Tên giữ nguyên BuildAll vì bài kiểm và scene gọi nó; đổi tên là việc riêng.
        /// </summary>
        public void BuildAll()
        {
            if (this.built) return;
            this.built = true;

            if (this.canvas == null)
            {
                Debug.LogError("[UI] Chưa nối canvas. PuzzleGame phải chạy trên một bản " +
                               "instantiate của Resources/UI/PuzzleRoot.prefab.");
                return;
            }

            // Font ảnh hưởng những Text dựng ĐỘNG lúc chơi (thẻ overlay), còn camera là
            // đồ của scene nên prefab không lưu được tham chiếu tới nó.
            if (this.uiFont != null) Ui.OverrideFont = this.uiFont;
            BuildCamera();

            // Lưới màn dựng theo TIẾN TRÌNH nên phải làm lúc chạy, không nằm sẵn trong prefab.
            if (this.menu != null) this.menu.BuildGrid();

            WireAll();
            this.audioPlayer = new PuzzleAudio(this.gameObject) { Enabled = PuzzleProgress.Sound };
            Haptics.Enabled = PuzzleProgress.Haptics;
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
            this.board = new BoardView(this.boardArea.Rect);
            this.effects = new EffectLayer(this, this.gameScreen);
            this.effects.AttachFlash(this.boardArea.Rect);
            if (this.diagBanner != null) this.diagBanner.Wire();


            this.boardArea.Wire(OnPointerDown, OnPointerDrag, OnPointerUp);

            // ---- menu: màn menu tự nối mọi nút của nó
            if (this.menu != null) this.menu.Wire(this);

            // ---- màn chơi
            this.controls.Wire(OnUndo, OnShuffle, OnHint, RestartLevel, ToggleSound, OnBackFromGame);

            // ---- vật phẩm: bảng tự giữ ref của mình, chỉ cần đưa nó host
            if (this.items != null) this.items.Wire(this);

            // ---- đấu seed + Wi-Fi: bảng đấu tự lo, và nó tự nối bảng Wi-Fi của nó
            if (this.duel != null) this.duel.Wire(this);
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

        // ------------------------------------------------------------------


        // ==================================================================
        // Menu — bảng nằm ở MenuScreen; đây chỉ giữ tham chiếu và bắc cầu
        // ==================================================================

        /// <summary>
        /// Màn menu. Component nằm trên chính node MenuScreen và tự giữ 12 tham chiếu
        /// của nó, cùng toàn bộ bố cục và việc dựng lưới 90 màn.
        /// </summary>
        [SerializeField] private MenuScreen menu;

        /// <summary>Khoá ngày của thử thách đang chơi; 0 nếu không phải ván thử thách.</summary>
        private int dailyKey;

        /// <summary>Vẽ lại trạng thái các nút màn và nhãn các nút gạt.</summary>
        private void RefreshMenu()
        {
            this.menu.Refresh();
            UpdateToggleLabels();
        }

        /// <summary>
        /// Nhãn của những thứ ĐỔI THEO TIẾN ĐỘ, ở cả hai màn.
        ///
        /// Gom về một chỗ vì chúng phải đổi cùng lúc: bật âm thanh ở menu mà nút ♪ trong
        /// ván còn xám là hai màn hình nói hai điều khác nhau về cùng một trạng thái.
        /// </summary>
        private void UpdateToggleLabels()
        {
            if (this.controls != null) this.controls.SetSoundOn(PuzzleProgress.Sound);
            if (this.menu != null) this.menu.RefreshLabels();
            RefreshWallet();
        }

        // ---- những gì menu cần từ ván chơi. Cài TƯỜNG MINH: đây là hợp đồng với
        //      MenuScreen, không phải API của PuzzleGame.
        void MenuScreen.IHost.PickLevel(int index) => OnLevelPicked(index);
        void MenuScreen.IHost.OpenEndless() => OpenEndless();
        void MenuScreen.IHost.OpenDaily() => OpenDaily();
        void MenuScreen.IHost.OpenDuel() { if (this.duel != null) this.duel.OpenPanel(); }
        void MenuScreen.IHost.ToggleSound() => CycleFeedback();
        void MenuScreen.IHost.ToggleSymbols() => ToggleSymbols();

        void MenuScreen.IHost.OpenWorldCard(int world) => ShowWorldCard(world);

        /// <summary>
        /// Thẻ của một thế giới, mở bằng cách bấm nhãn thế giới ở menu.
        ///
        /// Hai lối vào dồn vào một thẻ vì menu hết chỗ: chân menu đã bốn nút, ba nút chế độ
        /// nằm trong prefab, và thêm nút là sửa prefab rồi chốt lại ảnh chụp bố cục. Nhãn
        /// thế giới thì đã nằm ngay trên đúng nhóm màn nó nói về — chỗ tự nhiên nhất để
        /// treo cả hai. Thẻ nằm TRÊN menu nên đây cũng là đường xem trước cơ chế và thử
        /// leo tháp ở thế giới còn đang khoá.
        /// </summary>
        private void ShowWorldCard(int world)
        {
            TutorialLesson lesson = TutorialLessons.For(world);
            bool towerOk = GauntletRun.AvailableFor(world);

            this.card.Begin(1 + (lesson != null ? 1 : 0) + (towerOk ? 1 : 0));

            Text title = Ui.Text("Title", this.card.Root, LevelCatalog.WorldName(world),
                46, PuzzlePalette.Foreground, TextAnchor.UpperCenter, FontStyle.Bold);

            var body = new System.Text.StringBuilder();
            if (lesson != null) body.Append(lesson.Title);

            int bestDone = PuzzleProgress.TowerBest(world);
            if (bestDone > 0)
            {
                if (body.Length > 0) body.Append('\n');
                body.Append("▲ Tháp: tốt nhất ").Append(bestDone).Append(" màn");
                int left = PuzzleProgress.TowerBestLeft(world);
                if (left > 0) body.Append(", còn dư ").Append(left).Append(" lượt");
            }
            else if (towerOk)
            {
                if (body.Length > 0) body.Append('\n');
                body.Append("▲ Tháp: chưa leo lần nào");
            }

            Text detail = Ui.Text("Detail", this.card.Root, body.ToString(),
                29, PuzzlePalette.Dim, TextAnchor.UpperCenter);

            this.card.Header(new[] { title, detail }, new[] { 46, 29 });

            int slot = 0;
            if (lesson != null)
                this.card.AddButton("Hướng dẫn cơ chế", slot++, slot == 1,
                    () => OpenTutorial(lesson, "Đóng", null));
            if (towerOk)
                this.card.AddButton("▲ Leo tháp", slot++, slot == 1, () => StartTower(world));
            this.card.AddButton("Đóng", slot, false, this.card.Hide);
        }

        void MenuScreen.IHost.ToggleFreePlay()
        {
            PuzzleProgress.FreePlay = !PuzzleProgress.FreePlay;
            RefreshMenu();
            Toast(PuzzleProgress.FreePlay
                ? "Chơi tự do: vào thẳng màn nào cũng được. Sao và điểm vẫn được ghi."
                : "Đã tắt chơi tự do — mở màn theo tiến trình như cũ.");
        }

        void MenuScreen.IHost.ResetProgress()
        {
            PuzzleProgress.ResetAll(LevelCatalog.Levels.Length);
            RefreshMenu();
        }

        private void OnLevelPicked(int index)
        {
            if (!PuzzleProgress.IsUnlocked(index))
            {
                this.audioPlayer.Tone(180f, 0.2f);
                Haptics.Reject();
                Toast("Màn " + (index + 1) + " chưa mở. Qua màn trước để mở, hoặc bật Chơi tự do ở cuối menu.");
                return;
            }
            OpenLevel(index);
        }

        /// <summary>
        /// Nút ♪ trong ván: tắt/bật TIẾNG, không đụng tới rung.
        ///
        /// Cố ý khác nút ở chân menu. Nút ♪ là nút tắt tiếng gấp — bấm giữa ván vì chỗ
        /// đang ngồi cần im lặng — mà chỗ cần im lặng thì rung lại đúng là thứ muốn giữ.
        /// </summary>
        private void ToggleSound()
        {
            PuzzleProgress.Sound = !PuzzleProgress.Sound;
            this.audioPlayer.Enabled = PuzzleProgress.Sound;
            UpdateToggleLabels();
        }

        /// <summary>Nút phản hồi ở chân menu: tắt hết → tiếng → tiếng + rung.</summary>
        private void CycleFeedback()
        {
            PuzzleProgress.CycleFeedback();
            this.audioPlayer.Enabled = PuzzleProgress.Sound;
            Haptics.Enabled = PuzzleProgress.Haptics;
            UpdateToggleLabels();

            // Nghe/cảm được ngay cái mình vừa bật. Không có phản hồi tại chỗ thì nút này
            // là nút duy nhất ở chân menu bấm vào mà không có gì xảy ra ngoài đổi chữ.
            if (PuzzleProgress.Sound) this.audioPlayer.Tone(PuzzleAudio.Note(3), 0.24f);
            if (PuzzleProgress.Haptics) Haptics.Medium();
        }

        private void ToggleSymbols()
        {
            PuzzleProgress.Symbols = !PuzzleProgress.Symbols;
            UpdateToggleLabels();
            if (this.session != null && this.gameScreen.gameObject.activeSelf)
                this.board.Refresh(this.session, PuzzleProgress.Symbols);
        }

        // ------------------------------------------------------------------

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
            CloseTutorial();
            EndTower();          // rời màn hình giữa chặng là bỏ chặng
            this.card.Hide();
            this.gameScreen.gameObject.SetActive(false);
            this.menuScreen.gameObject.SetActive(true);
            RefreshMenu();
        }


        /// <summary>
        /// Nút màn chưa mở PHẢI phản hồi. Trước đây nó chỉ bị `interactable = false` nên
        /// bấm vào là im lặng hoàn toàn — không phân biệt được với "game hỏng".
        /// </summary>
        /// <summary>
        /// Câu nhắc ngắn cho BIẾN THỂ của một cơ chế, hiện ở màn đầu tiên có nó.
        ///
        /// Bảng này từng chứa cả các cơ chế CHÍNH (đá, đa sắc, ngòi, đích, băng, trói).
        /// Chúng đã chuyển sang thẻ hướng dẫn có hình (TutorialLessons), nên giữ lại ở đây
        /// là nói hai lần cùng một điều — người chơi vừa đọc thẻ xong thì một toast nhảy
        /// ra lặp lại đúng câu đó. Còn lại đúng những gì thẻ KHÔNG dạy: biến thể "dày",
        /// vốn chỉ là một con số khác trên một luật đã biết, nên một dòng chữ là đủ.
        ///
        /// Tìm TỰ ĐỘNG theo bảng màn. Ghi số màn cứng thì mỗi lần thêm/bớt màn là các mốc
        /// lệch hết mà không ai báo.
        ///
        /// THỨ TỰ LÀ HỢP ĐỒNG: chỉ số trong bảng chính là khoá lưu "đã hiện" ở
        /// PuzzleProgress, nên chèn dòng mới vào GIỮA sẽ làm câu nhắc cũ hiện lại một lần.
        /// Thêm vào CUỐI.
        /// </summary>
        private static readonly (System.Func<LevelConfig, bool> Match, string Message)[] IntroRules =
        {
            (c => c.Stones > 0 && c.StoneHp >= 2, "Đá dày cần bị va 2 lần mới vỡ."),
            (c => c.Ices > 0 && c.IceHp >= 2, "Băng dày cần làm tan 2 lần mới ăn được ô bên dưới.")
        };

        private void ShowIntroFor(int index)
        {
            for (int r = 0; r < IntroRules.Length; r++)
            {
                if (PuzzleProgress.IntroSeen(r)) continue;

                int first = -1;
                for (int i = 0; i < LevelCatalog.Levels.Length; i++)
                    if (IntroRules[r].Match(LevelCatalog.Levels[i])) { first = i; break; }

                if (first != index) continue;
                PuzzleProgress.MarkIntroSeen(r);
                Toast(IntroRules[r].Message);
            }
        }

        // ==================================================================
        // Leo tháp
        // ==================================================================

        /// <summary>Chặng đang chạy, null nếu không leo tháp.</summary>
        private GauntletRun tower;

        private bool IsTower => this.tower != null;

        /// <summary>
        /// Bắt đầu một chặng. Dựng cả 5 màn ngay (GauntletRun.Start làm việc đó) vì ngân
        /// sách là tổng par của cả chặng.
        /// </summary>
        private void StartTower(int world)
        {
            GauntletRun run = GauntletRun.Start(world);
            if (run == null)
            {
                Toast("Thế giới này không mở được chặng leo tháp.");
                return;
            }
            this.tower = run;
            OpenTowerLevel();
            Toast("Leo tháp " + run.Levels.Length + " màn · " + run.Budget +
                  " lượt cho cả chặng (chơi rời được " + run.SeparateBudget() + ").");
        }

        /// <summary>
        /// Mở màn hiện tại của chặng.
        ///
        /// Ghi MaxMoves vào LevelData TRƯỚC khi OpenLevelData dựng session: hàm dựng của
        /// PuzzleSession nhớ lại MaxMoves làm ngân sách gốc, nên sửa sau đó thì nút Chơi
        /// lại sẽ trả về con số cũ.
        /// </summary>
        private void OpenTowerLevel()
        {
            LevelData level = this.tower.CurrentLevel;
            if (level == null) return;
            level.MaxMoves = this.tower.BudgetForCurrentLevel();
            OpenLevelData(this.tower.CurrentLevelIndex, level);
        }

        private void EndTower()
        {
            this.tower = null;
        }

        /// <summary>Thẻ tổng kết chặng — hiện cho cả khi xong và khi hỏng.</summary>
        private void ShowTowerCard()
        {
            GauntletRun run = this.tower;
            bool cleared = run.Cleared;
            bool best = PuzzleProgress.RecordTower(run.World, run.Done, cleared ? run.Budget : 0);

            this.card.Begin(2);

            Text title = Ui.Text("Title", this.card.Root,
                cleared ? "Lên đỉnh tháp!" : "Chặng dừng ở màn " + (run.Done + 1),
                56, cleared ? PuzzlePalette.Foreground : PuzzlePalette.Bad,
                TextAnchor.UpperCenter, FontStyle.Bold);

            var body = new System.Text.StringBuilder();
            body.Append(LevelCatalog.WorldName(run.World)).Append('\n')
                .Append("Qua ").Append(run.Done).Append('/').Append(run.Levels.Length)
                .Append(" màn · ").Append(run.Score).Append(" điểm");
            if (cleared) body.Append("\nCòn dư ").Append(run.Budget).Append(" lượt");
            if (best) body.Append("\n<color=#34D399>★ Thành tích mới</color>");
            else
            {
                int bestDone = PuzzleProgress.TowerBest(run.World);
                if (bestDone > 0)
                    body.Append("\nTốt nhất: ").Append(bestDone).Append('/')
                        .Append(run.Levels.Length).Append(" màn");
            }

            Text detail = Ui.Text("Detail", this.card.Root, body.ToString(),
                30, PuzzlePalette.Dim, TextAnchor.UpperCenter);
            detail.supportRichText = true;

            this.card.Header(new[] { title, detail }, new[] { 56, 30 });

            int world = run.World;
            this.card.AddButton("↻ Leo lại từ đầu", 0, true, () => { EndTower(); StartTower(world); });
            this.card.AddButton("Danh sách màn", 1, false, () => { EndTower(); ShowMenu(); });
        }

        /// <summary>
        /// Thẻ giữa chặng: vừa qua một màn, còn bấy nhiêu lượt cho phần còn lại.
        ///
        /// Con số "còn N lượt" là thứ duy nhất người chơi cần ở đây, và nó phải to: cả chế
        /// độ này là quyết định tiêu lượt, mà quyết định đó được đưa ra ở đúng thời điểm
        /// đọc thẻ này.
        /// </summary>
        private void ShowTowerStepCard(int movesUsed)
        {
            GauntletRun run = this.tower;
            this.card.Begin(2);

            Text title = Ui.Text("Title", this.card.Root,
                "Qua màn " + run.Done + "/" + run.Levels.Length,
                56, PuzzlePalette.Foreground, TextAnchor.UpperCenter, FontStyle.Bold);

            Text budget = Ui.Text("Budget", this.card.Root,
                "còn " + run.Budget + " lượt",
                72, run.Budget <= 6 ? PuzzlePalette.Bad : PuzzlePalette.Star,
                TextAnchor.UpperCenter, FontStyle.Bold);

            Text detail = Ui.Text("Detail", this.card.Root,
                "Màn này tốn " + movesUsed + " lượt · " + run.Score + " điểm" +
                "\ncho " + (run.Levels.Length - run.Done) + " màn còn lại",
                30, PuzzlePalette.Dim, TextAnchor.UpperCenter);

            this.card.Header(new[] { title, budget, detail }, new[] { 56, 72, 30 });

            this.card.AddButton("Màn tiếp theo →", 0, true, OpenTowerLevel);
            this.card.AddButton("Bỏ chặng", 1, false, () => { EndTower(); ShowMenu(); });
        }

        // ==================================================================
        // Bài hướng dẫn cơ chế
        // ==================================================================

        /// <summary>
        /// Thẻ hướng dẫn. Dựng chậm (lúc cần lần đầu) vì phần lớn phiên chơi không mở nó,
        /// và nó ngồi trên chính OverlayCard của thẻ cuối ván nên không tốn node riêng.
        /// </summary>
        private TutorialCard tutorial;
        private Coroutine tutorialRoutine;

        /// <summary>
        /// Mở bài hướng dẫn của thế giới chứa màn này, nếu chưa từng xem. Trả true nếu
        /// đã mở — bên gọi dùng nó để HOÃN các câu nhắc khác lại sau khi thẻ đóng, không
        /// thì toast chạy phía sau một thẻ đang che kín và coi như không ai đọc.
        ///
        /// Bám theo THẾ GIỚI của màn đang mở chứ không theo "màn đầu của thế giới": bật
        /// Chơi tự do là vào thẳng màn 45 được, và ở đó vẫn cần biết ô đa sắc là gì.
        /// </summary>
        private bool MaybeShowTutorial(int index, System.Action after)
        {
            if (index < 0 || index >= LevelCatalog.Levels.Length) return false;   // vô tận/thử thách/đấu

            int world = LevelCatalog.Levels[index].World;
            if (PuzzleProgress.TutorialSeen(world)) return false;

            TutorialLesson lesson = TutorialLessons.For(world);
            if (lesson == null) return false;

            PuzzleProgress.MarkTutorialSeen(world);
            OpenTutorial(lesson, "Bắt đầu", after);
            return true;
        }

        /// <summary>
        /// Mở thẻ hướng dẫn cho một bài cụ thể. Dùng cho cả lần đầu tự hiện và lần bấm
        /// nhãn thế giới ở menu để xem lại.
        /// </summary>
        private void OpenTutorial(TutorialLesson lesson, string buttonLabel, System.Action after)
        {
            if (this.tutorialRoutine != null) StopCoroutine(this.tutorialRoutine);
            this.tutorialRoutine = StartCoroutine(TutorialRoutine(lesson, buttonLabel, after));
        }

        /// <summary>
        /// Chờ ĐÚNG một khung hình trước khi dựng thẻ.
        ///
        /// OverlayCard.Begin chốt chiều rộng thẻ theo rect của lớp phủ, mà rect đó chỉ có
        /// số thật sau khi Canvas đã bố cục xong. Dựng ngay trong cùng khung hình với
        /// OpenLevelData thì thẻ nhận chiều rộng dự phòng 420 trên mọi máy, và mọi phép đo
        /// chữ bên trong đều sai theo.
        /// </summary>
        private IEnumerator TutorialRoutine(TutorialLesson lesson, string buttonLabel,
                                            System.Action after)
        {
            yield return null;

            if (this.tutorial == null) this.tutorial = new TutorialCard(this.card);

            // busy chặn cú chạm xuống bàn thật phía sau lớp phủ.
            this.busy = true;

            yield return this.tutorial.Show(lesson, buttonLabel, () =>
            {
                this.busy = false;
                this.tutorialRoutine = null;
                after?.Invoke();
            });
        }

        private void CloseTutorial()
        {
            if (this.tutorialRoutine != null) { StopCoroutine(this.tutorialRoutine); this.tutorialRoutine = null; }
            if (this.tutorial != null && this.tutorial.Visible)
            {
                this.tutorial.Close();
                this.busy = false;
            }
        }

        /// <summary>Mở một màn theo chỉ số. Công khai để deep-link và để kiểm thử.</summary>
        public void OpenLevel(int index)
        {
            OpenLevelData(index, LevelBuilder.Build(LevelCatalog.Levels[index]));

            // Thẻ hướng dẫn đi TRƯỚC câu nhắc: nếu thẻ mở, câu nhắc chờ tới lúc thẻ đóng.
            if (!MaybeShowTutorial(index, () => ShowIntroFor(index))) ShowIntroFor(index);
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
                this.hud.SetScore(this.session.Score);
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
            // Leo tháp không cho chơi lại MỘT màn. Chơi lại đặt lại ngân sách về đúng con
            // số lúc vào màn, tức là thử được vô hạn lần ở cùng ngân sách — chỉ cần lặp
            // tới khi đi đúng par là cả chặng thành chuyện chắc chắn xong. Muốn thử lại
            // thì leo lại từ đầu, và nút đó nằm trên thẻ tổng kết.
            if (IsTower)
            {
                Toast("Leo tháp không chơi lại một màn — đó là cái giá của ngân sách chung.");
                return;
            }

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
            if (this.hud != null) this.hud.SetScore(0);

            LevelConfig cfg = this.level.Config;
            string hudTitle = this.level.Endless ? "∞ Vô tận"
                : IsDuel ? "⚔ Đấu " + this.duel.Code
                : IsDaily ? "✦ Thử thách hôm nay"
                : IsTower ? "▲ Tháp " + (this.tower.Done + 1) + "/" + this.tower.Levels.Length +
                            " · " + cfg.Name
                : ((this.levelIndex + 1) + ". " + cfg.Name);
            string hudSub =
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
            this.hud.SetTitle(hudTitle, hudSub);
            // Màn chính xác phải NÓI RA là nó khít. "tối ưu 8" bên cạnh "8 lượt" trông y
            // như mọi màn khác, và người chơi chỉ phát hiện ra mình không có lượt dư vào
            // lúc hết lượt — tức là lúc đã quá muộn để chơi khác đi.
            this.hud.SetRuleLine(this.level.Endless
                ? rule + " · " + EndlessRules.ColorsFor(this.session.Score) + " màu"
                : this.level.Exact
                    ? rule + " · đúng " + this.level.Par + " lượt, không dư"
                    : rule + " · tối ưu " + this.level.Par);

            this.board.SetDimmed(this.session, false, PuzzleProgress.Symbols);
            this.board.ClearChain();
            this.board.ResetScales();
            this.boardArea.HidePreview();
            this.diagBanner.Hide();
            this.card.Hide();
            UpdateHud();
        }

        // ==================================================================
        // Bố cục theo kích thước màn hình
        // ==================================================================

        private void Update() => TickFrame();

        /// <summary>
        /// Một nhịp khung hình. Tách khỏi Update để bài kiểm gọi được.
        ///
        /// Update KHÔNG chạy ở edit mode, nên khi nó còn là thân của Update thì THỨ TỰ
        /// các việc trong đây là thứ không kiểm được — mà thứ tự chính là chỗ đã sai:
        /// nhánh menu return sớm, và phiên Wi-Fi đặt sau nó nên không bao giờ chạy khi
        /// người chơi đứng ở menu chờ tìm phòng.
        /// </summary>
        public void TickFrame()
        {
            // Phiên Wi-Fi nhắc lại TRƯỚC mọi nhánh màn hình. Khách bấm "Tìm phòng" rồi
            // ĐỨNG YÊN Ở MENU chờ, nên đặt sau nhánh menu là gói TÌM không bao giờ được
            // phát lại — đúng lỗi "cùng Wi-Fi mà không thấy nhau".
            if (Lan != null) Lan.Tick();

            // Thẻ hướng dẫn nhắc TRƯỚC mọi nhánh màn hình: nó mở được từ cả menu (bấm nhãn
            // thế giới) lẫn trong ván, mà nhánh menu phía dưới return sớm — đặt sau nó thì
            // hoạt ảnh đứng im đúng ở chỗ hay được mở nhất.
            if (this.tutorial != null && this.tutorial.Visible) this.tutorial.Tick(Time.deltaTime);

            // Menu cũng phải bố cục lại khi khung đổi (quay máy, lề an toàn thay đổi),
            // không thì lưới màn đè lên footer trên tỉ lệ màn hình khác.
            if (this.menuScreen.gameObject.activeSelf) { this.menu.Tick(); return; }

            if (!this.gameScreen.gameObject.activeSelf) return;
            ApplyLayout(force: false);
            this.board.TickChain(Time.deltaTime);   // nét đứt chạy, như stroke-dashoffset
            TickOpponentLine();
        }

        /// <summary>Dòng tiến độ đối thủ đang hiện, để biết khi nào nó thật sự đổi.</summary>
        private string shownOpponentLine = "";

        /// <summary>
        /// Vẽ lại HUD khi tiến độ đối thủ đổi.
        ///
        /// Phải nằm trong nhịp khung hình chứ không nằm trong UpdateHud: gói tin tới từ
        /// MẠNG, vào lúc chẳng liên quan gì tới lượt của mình. Chỉ dựa vào UpdateHud thì
        /// dòng đó đứng im suốt lúc mình đang ngồi nghĩ — đúng lúc nó đáng đọc nhất, vì
        /// biết đối thủ vừa vượt lên là thứ đổi được nước mình sắp đi.
        ///
        /// SO CHUỖI trước khi vẽ: Refresh ghi cả chục Text, chạy mỗi khung hình là phí, mà
        /// tiến độ thì chỉ đổi vài lần trong cả ván.
        /// </summary>
        private void TickOpponentLine()
        {
            string line = Lan == null ? "" : Lan.OpponentLine;
            if (line == this.shownOpponentLine) return;
            this.shownOpponentLine = line;
            UpdateHud();
        }


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
            RectTransform areaRect = this.boardArea.Rect;
            if (!Mathf.Approximately(areaRect.offsetMin.y, inset))
            {
                areaRect.offsetMin = new Vector2(24, inset);
                LayoutRebuilder.ForceRebuildLayoutImmediate(areaRect);
                force = true;
            }

            Vector2 size = areaRect.rect.size;
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
                Haptics.Tick();
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
                this.boardArea.ShowPreview(
                    BoardToArea(head) + new Vector2(0, this.board.CellSize * 0.95f),
                    selection.Count, this.level.MinChain, this.level.MaxChain);
            }
            else
            {
                this.boardArea.HidePreview();
            }
        }

        private void ClearSelectionVisuals()
        {
            foreach (int cell in this.session.Selection) this.board.SetSelected(cell, false);
            this.board.ClearChain();
            this.boardArea.HidePreview();
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
                if (this.session.Selection.Count > 0) { this.audioPlayer.Bad(); Haptics.Reject(); }
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
            this.boardArea.HidePreview();

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

            // Cùng ba ngưỡng mà rung màn hình và cỡ chữ điểm đang dùng (5 / 6 / 8), để ba
            // kênh phản hồi nói CÙNG một điều về cùng một nước đi.
            if (chainLength >= 8) Haptics.Strong();
            else if (chainLength >= 5) Haptics.Medium();
            else Haptics.Light();

            this.hud.AnimateScore(result.ScoreBefore, this.session.Score);
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
                Haptics.Light();
                yield return this.board.PlayIce(result.CrackedIce, result.ThawedIce);
            }

            UpdateHud();
            ReportLanProgress();
            this.busy = false;
            Evaluate();
        }

        /// <summary>
        /// Báo cho đối thủ biết mình đang ở đâu. Gọi sau mỗi thay đổi làm đổi bộ số
        /// (lượt / ô còn / điểm) — nước đi và hoàn tác.
        ///
        /// Chỉ gửi khi đang có ván đấu Wi-Fi: ở màn thường thì không có ai nghe, mà mở
        /// socket ra phát vào mạng LAN của người ta là chuyện không nên làm khi không cần.
        /// </summary>
        private void ReportLanProgress()
        {
            if (!IsDuel || this.duel == null) return;
            if (Lan == null || !Lan.Active || Lan.Link == null) return;
            Lan.Link.SendProgress(this.duel.SnapshotProgress());
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
                Haptics.Strong();
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
                if ((Lan != null && Lan.TryShowVerdict())) return;

                // Leo tháp đi đường riêng và KHÔNG ghi gì vào tiến độ chiến dịch: chặng
                // chơi lại được vô hạn, nên ghi sao ở đây là mở đường cày sao bằng cách
                // chạy lại màn dễ nhất của chặng.
                if (IsTower)
                {
                    int used = this.session.MovesUsed;
                    this.tower.Complete(used, this.session.Score);
                    Celebrate();
                    if (this.tower.Cleared) ShowTowerCard();
                    else ShowTowerStepCard(used);
                    return;
                }

                int stars = this.session.StarsEarned();

                // Huy hiệu kỹ thuật: chỉ ở chiến dịch. Thử thách hằng ngày không ghi
                // huy hiệu vì huy hiệu đẻ ra sao, mà sao mua được vật phẩm — vòng đó
                // sẽ phá đúng cái công bằng mà thử thách dựa vào.
                this.medalJustEarned = !IsDaily && !IsDuel && !IsTower && this.session.MedalEarned &&
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
            if ((Lan != null && Lan.TryShowVerdict())) return;

            // Thua vẫn ghi điểm của ngày (không cộng chuỗi): người chơi thử vài lần trong
            // ngày thì con số trên menu phải là lần tốt nhất, không phải chỉ lần thắng.
            if (IsDaily) PuzzleProgress.RecordDaily(this.dailyKey, 0, this.session.Score, false);

            // Hỏng chặng ghi NGAY tại đây, trước khi hoạt ảnh chẩn đoán chạy: thẻ tổng kết
            // hiện ra ở cuối hoạt ảnh đó và nó đọc trạng thái đã hỏng.
            if (IsTower) this.tower.Fail(this.session.Score);

            this.audioPlayer.Tone(180f, 0.35f);
            Haptics.Strong();
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
            this.hud.SetOpponentLine(Lan == null ? "" : Lan.OpponentLine);
            this.hud.Refresh(this.session, this.level, IsDaily);
            this.controls.Refresh(this.session, this.level.Endless);
            UpdateToggleLabels();
        }


        // ==================================================================
        // Toast — câu nhắn ngắn, tự tắt
        // ==================================================================

        [SerializeField] private ToastView toast;

        /// <summary>
        /// Giữ lại làm cửa vào cho hơn 40 chỗ gọi trong lớp này, thay vì sửa hết
        /// thành this.toast.Show(...). Bản thân toast do ToastView lo.
        /// </summary>
        private void Toast(string message)
        {
            if (this.toast != null) this.toast.Show(message);
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
            if (outcome != PuzzleSession.UndoResult.Ok)
            {
                this.audioPlayer.Bad();
                Haptics.Reject();
                return;
            }

            this.board.SetDimmed(this.session, false, PuzzleProgress.Symbols);
            this.board.ClearChain();
            this.boardArea.HidePreview();
            this.hud.SetScore(this.session.Score);
            // Hoàn tác một bước ĐÃ DÙNG vật phẩm thì phải trả sao lại. Không trả thì
            // hoàn tác biến thành hình phạt, và người chơi học cách không bao giờ bấm nó.
            PuzzleSession.ItemKind undone = this.session.LastUndoneItem;
            if (undone != PuzzleSession.ItemKind.None)
            {
                PuzzleProgress.RefundStars(PuzzleSession.ItemCost(undone));
                Toast("Đã hoàn ★" + PuzzleSession.ItemCost(undone) + ".");
            }

            this.audioPlayer.Undo();
            Haptics.Light();
            UpdateHud();
            ReportLanProgress();
        }

        private void OnHint()
        {
            if (this.busy) return;
            int[] cells = this.session.FindHint();
            if (cells == null || cells.Length == 0)
            {
                this.audioPlayer.Bad();
                Haptics.Reject();
                return;
            }
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
                    Haptics.Reject();
                    yield break;
                }
                this.board.Refresh(this.session, PuzzleProgress.Symbols);
                UpdateHud();
                this.audioPlayer.Tone(420f, 0.18f);
                Haptics.Medium();
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
                Haptics.Reject();
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
            this.boardArea.HidePreview();
            this.card.Hide();
            UpdateHud();

            this.audioPlayer.Fanfare(2);
            Haptics.Medium();
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
            this.card.Begin(IsDuel ? 4
                          : IsDaily ? 2 + (stars < 3 ? 1 : 0)
                          : 1 + (last ? 0 : 1) + (stars < 3 ? 1 : 0));

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
            if (IsTower) { ShowTowerCard(); return; }   // Evaluate đã đi đường riêng; chốt hai lần cho chắc
            if (IsDaily)
            {
                // KHÔNG có "Màn tiếp theo →" ở đây, và đó là một lỗi được sửa chứ không
                // phải một lựa chọn: chỉ số của thử thách là -2, nên `last` ra false và
                // nhánh chung phía dưới dựng nút gọi OpenLevel(-1) — tức
                // LevelCatalog.Levels[-1], ném ra ngoài mảng. Thắng thử thách rồi bấm nút
                // sáng nhất trên thẻ là gặp exception.
                this.card.AddButton("Chia sẻ kết quả", slot++, true, CopyDailyShare);
                if (stars < 3) this.card.AddButton("Thử lại để lấy 3★", slot++, false, RestartLevel);
                this.card.AddButton("Danh sách màn", slot, false, ShowMenu);
                return;
            }
            if (!last) this.card.AddButton("Màn tiếp theo →", slot++, true, () => OpenLevel(this.levelIndex + 1));
            if (stars < 3) this.card.AddButton("Thử lại để lấy 3★", slot++, false, RestartLevel);
            this.card.AddButton("Danh sách màn", slot, false, ShowMenu);
        }

        /// <summary>
        /// Sao chép kết quả thử thách hôm nay ra clipboard.
        ///
        /// Đọc chuỗi ngày SAU khi Evaluate đã ghi ván (RecordDaily chạy trước ShowWinCard),
        /// nên con số đã tính cả hôm nay.
        /// </summary>
        private void CopyDailyShare()
        {
            string text = DailyShare.Build(
                this.dailyKey,
                this.session.IsWon(),
                this.session.StarsEarned(),
                this.session.MovesUsed,
                this.level.Par,
                this.session.Score,
                PuzzleProgress.DailyStreakLive(this.dailyKey),
                this.session.ChainLog,
                this.level.MinChain,
                this.level.MaxChain);

            Clipboard = text;
            Toast("Đã sao chép kết quả — dán vào Zalo, Messenger hay chỗ nào cũng được.");
        }

        private void ShowLoseCard(LossReason reason)
        {
            // ĐẾM nút trước rồi mới mở thẻ
            // Leo tháp không có phao: hết lượt là hỏng chặng, không xáo cũng không hoàn tác
            // ngược lại một nước đã tiêu. Cho phao ở đây là cho thử lại vô hạn trên cùng
            // một ngân sách, và cả chặng mất nghĩa.
            if (IsTower) { ShowTowerCard(); return; }

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

        /// <summary>
        /// Bảng vật phẩm. Component nằm trên gốc ItemPanel.prefab và tự giữ cả tham
        /// chiếu bên trong bảng lẫn nút mở + lớp chặn nằm ngoài.
        /// </summary>
        [SerializeField] private ItemPanel items;

        /// <summary>
        /// Thử thách hằng ngày và ván đấu KHÔNG cho dùng vật phẩm, dù Core vẫn cho:
        /// điểm của mọi máy chỉ so được với nhau khi ai cũng chơi đúng một bàn với đúng
        /// một bộ luật.
        /// </summary>
        private bool ItemsUsable =>
            this.session != null && this.session.ItemsAllowed && !IsDaily && !IsDuel && !IsTower;

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

            if (this.menu != null) this.menu.ShowWallet(text);
            if (this.items != null) this.items.ShowWallet(text, balance);
        }

        // ---- những gì cửa hàng cần từ màn chơi. Cài TƯỜNG MINH: đây là hợp đồng với
        //      ItemShop, không phải API của PuzzleGame.
        PuzzleSession ItemPanel.IHost.Session => this.session;
        bool ItemPanel.IHost.Busy => this.busy;
        bool ItemPanel.IHost.ItemsUsable => ItemsUsable;
        void ItemPanel.IHost.Toast(string message) => Toast(message);
        void ItemPanel.IHost.BadSound() { this.audioPlayer.Bad(); Haptics.Reject(); }
        void ItemPanel.IHost.Tone(float hertz, float seconds) => this.audioPlayer.Tone(hertz, seconds);
        void ItemPanel.IHost.PlayItemEffect(MoveResult effect) => StartCoroutine(PlayItemEffect(effect));
        void ItemPanel.IHost.RefreshWallet() => RefreshWallet();

        // ==================================================================
        // Đấu — hai bảng tự lo phần của mình; đây chỉ giữ tham chiếu và bắc cầu
        // ==================================================================

        /// <summary>
        /// Bảng đấu seed. Component nằm trên gốc DuelPanel.prefab và tự giữ cả tham
        /// chiếu bên trong bảng lẫn nút mở + lớp chặn + bảng Wi-Fi nằm ngoài.
        ///
        /// Bảng Wi-Fi KHÔNG có tham chiếu riêng ở đây: nó phụ thuộc một chiều vào bảng
        /// đấu, nên chủ của nó là bảng đấu chứ không phải PuzzleGame.
        /// </summary>
        [SerializeField] private DuelPanel duel;

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
        public float LanPanelSize => Lan == null ? 0f : Lan.PanelHeight;
        public float LanContentHeight => Lan == null ? 0f : Lan.ContentHeight;

        private LanPanel Lan => this.duel == null ? null : this.duel.Lan;

        // ---- lối vào cho kiểm thử: đi qua ĐÚNG các hàm mà nút thật gọi
        public void DebugStartDuel(int seed, int preset) { this.duel.StartDuel(seed, preset); }
        public void DebugCopyDuelResult() { this.duel.CopyResult(); }
        public void DebugPasteOpponentResult() { this.duel.PasteOpponentResult(); }
        public void DebugForceDuelEnd() { this.duel.CaptureResult(); }
        public DuelLanLink DebugLan => Lan == null ? null : Lan.Link;
        public void DebugOpenLanPanel() { if (Lan != null) Lan.OpenPanel(); }
        public void DebugStartLanHost() { if (Lan != null) Lan.TestStartHost(); }
        public bool DebugLanActive => Lan != null && Lan.Active;

        /// <summary>Số nhịp phiên Wi-Fi đã nhận. Bài kiểm dùng để canh THỨ TỰ trong TickFrame.</summary>
        public int DebugLanTicks => Lan == null ? -1 : Lan.TickCount;

        /// <summary>Vẽ lại dòng trạng thái Wi-Fi ngay. Bài kiểm dùng để đo chữ dài nhất.</summary>
        public void DebugLanForceStatus() { if (Lan != null) Lan.TestRefreshStatus(); }
        public string DebugLanStatus => Lan == null ? "" : Lan.StatusText;
        public void DebugFeedLanInvite(int seed, int preset, string who)
        {
            if (Lan != null) Lan.TestFeedInvite(seed, preset, who);
        }

        public void DebugFeedLanResult(DuelResult r, string who)
        {
            if (Lan != null) Lan.TestFeedResult(r, who);
        }

        // ---- những gì chế độ đấu cần từ màn chơi.
        //
        // Cài TƯỜNG MINH (explicit) chứ không để public: đây là hợp đồng với DuelController,
        // không phải API của PuzzleGame. Để public thì mọi chỗ khác cũng gọi được, và cái
        // ranh giới vừa dựng lên sẽ mòn đi trong vài tuần.
        PuzzleSession DuelPanel.IHost.Session => this.session;
        bool DuelPanel.IHost.OnDuelBoard => IsDuel;
        OverlayCard DuelPanel.IHost.Card => this.card;

        string DuelPanel.IHost.Clipboard
        {
            get { return Clipboard; }
            set { Clipboard = value; }
        }

        void DuelPanel.IHost.Toast(string message) => Toast(message);
        void DuelPanel.IHost.BadSound() { this.audioPlayer.Bad(); Haptics.Reject(); }
        void DuelPanel.IHost.Tone(float hertz, float seconds) => this.audioPlayer.Tone(hertz, seconds);
        void DuelPanel.IHost.Celebrate() => Celebrate();
        void DuelPanel.IHost.OpenDuelBoard(LevelData board) => OpenLevelData(DuelIndex, board);
        void DuelPanel.IHost.RestartLevel() => RestartLevel();
        void DuelPanel.IHost.ShowMenu() => ShowMenu();

        /// <summary>
        /// Pháo giấy + chuỗi bốn nốt + nhịp rung đôi. Dùng cho cả thắng màn, hết ván vô
        /// tận, và thắng một ván đấu — ba chỗ này trước đây chép lại đúng hai dòng giống
        /// hệt nhau.
        /// </summary>
        private void Celebrate()
        {
            this.effects.Confetti(Vector2.zero, this.board.Root.sizeDelta.x * 0.5f);
            this.audioPlayer.Fanfare(5);
            Haptics.Success();
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
            if (this.menu == null) missing.Add(nameof(this.menu));
            if (this.toast == null) missing.Add(nameof(this.toast));
            if (this.hud == null) missing.Add(nameof(this.hud));
            if (this.controls == null) missing.Add(nameof(this.controls));
            if (this.duel == null) missing.Add(nameof(this.duel));
            if (this.items == null) missing.Add(nameof(this.items));
            if (Lan == null) missing.Add("duel.Lan");
            return missing;
        }
    }
}
