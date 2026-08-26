using System.Collections.Generic;
using ConnectPuzzle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Bảng "Đấu seed bạn bè": sinh/nhập mã bàn, và chia sẻ kết quả để hai bên tự so.
    ///
    /// Đặt trên gốc DuelPanel.prefab và tự giữ tham chiếu của mình. Gộp từ DuelPanelView
    /// (chỉ giữ ref) và nửa "mã + kết quả" của DuelController (chỉ giữ hàm).
    ///
    /// Wi-Fi nằm ở LanPanel, và nó phụ thuộc MỘT CHIỀU vào lớp này: Wi-Fi chỉ là một cách
    /// CHUYỂN mã và kết quả thay cho copy-paste, nó không đổi luật phân định nào cả. Nên
    /// trạng thái ván đấu (seed, preset, mã, kết quả của mình) sống ở đây, một bản duy
    /// nhất, và LanPanel mượn qua Start()/Result/ShowVerdict().
    ///
    /// KHÔNG gồm lớp chặn (DuelCatcher) trong prefab: nó là em ruột của bảng, dựng bằng
    /// code. Nó là hình chữ nhật trong suốt phủ màn hình — không có gì để sửa trong
    /// Editor — còn đưa nó thành CON của bảng chính là thứ đã gây lỗi "chạm đâu cũng tắt
    /// bảng" ở bảng Wi-Fi.
    /// </summary>
    public sealed class DuelPanel : MonoBehaviour
    {
        /// <summary>
        /// Phần màn chơi mà chế độ đấu cần tới.
        ///
        /// Gói theo VIỆC chứ không theo đối tượng: Celebrate() thay cho (effects, board,
        /// audioPlayer), OpenDuelBoard() thay cho (OpenLevelData, DuelIndex). Nhờ vậy lớp
        /// này không cầm tham chiếu tới thứ nó không có việc gì phải biết.
        /// </summary>
        public interface IHost
        {
            PuzzleSession Session { get; }

            /// <summary>Ván đang chơi có phải ván đấu không.</summary>
            bool OnDuelBoard { get; }

            OverlayCard Card { get; }
            string Clipboard { get; set; }

            void Toast(string message);
            void BadSound();
            void Tone(float hertz, float seconds);

            /// <summary>Pháo giấy + chuỗi nốt thắng. Dùng chung với thẻ vô tận.</summary>
            void Celebrate();

            void OpenDuelBoard(LevelData board);
            void RestartLevel();
            void ShowMenu();
        }

        // ---- nằm TRONG prefab bảng
        [SerializeField] private Text code;
        [SerializeField] private Button reroll;
        [SerializeField] private Button copy;
        [SerializeField] private Button playMine;
        [SerializeField] private InputField input;
        [SerializeField] private Text status;
        [SerializeField] private Button paste;
        [SerializeField] private Button playTheirs;
        [SerializeField] private Button lanOpen;

        // ---- nằm NGOÀI prefab bảng: nút mở ở menu, lớp chặn là em ruột, và bảng Wi-Fi
        [SerializeField] private Button openButton;
        [SerializeField] private Button catcher;
        [SerializeField] private LanPanel lan;

        private IHost host;

        /// <summary>Mã của ván đấu đang chơi; rỗng nếu không phải ván đấu.</summary>
        private string boardCode = "";
        private int seed, preset;

        /// <summary>
        /// Kết quả của CHÍNH ván đấu vừa xong. Chốt một lần lúc ván kết thúc thay vì đọc
        /// lại từ session mỗi lúc cần: người chơi có thể bấm Hoàn tác hay Xáo lại từ thẻ
        /// thua, và nếu đọc lại thì con số gửi cho bạn bè sẽ khác con số đã hiện ra.
        /// </summary>
        private DuelResult myResult;
        private bool myResultReady;

        private float contentHeight;

        // ---- LanPanel đọc, không ai khác
        public DuelResult Result => this.myResult;
        public bool ResultReady => this.myResultReady;

        /// <summary>Mã của ván đấu đang chơi, để màn chơi đặt tên bàn.</summary>
        public string Code => this.boardCode;

        /// <summary>Tên kiểu bàn của ván đấu đang chơi.</summary>
        public string PresetLabel => DuelChallenge.PresetName(this.preset);

        /// <summary>Chiều cao nội dung đã xếp — bài kiểm đọc thay vì chép lại hằng số.</summary>
        public float ContentHeight => this.contentHeight;

        public LanPanel Lan => this.lan;

        public List<string> MissingFields()
        {
            var missing = new List<string>();
            if (this.code == null) missing.Add(nameof(this.code));
            if (this.reroll == null) missing.Add(nameof(this.reroll));
            if (this.copy == null) missing.Add(nameof(this.copy));
            if (this.playMine == null) missing.Add(nameof(this.playMine));
            if (this.input == null) missing.Add(nameof(this.input));
            if (this.status == null) missing.Add(nameof(this.status));
            if (this.paste == null) missing.Add(nameof(this.paste));
            if (this.playTheirs == null) missing.Add(nameof(this.playTheirs));
            if (this.lanOpen == null) missing.Add(nameof(this.lanOpen));
            return missing;
        }

        public void BindByNameForAuthoring()
        {
            this.code = Find<Text>("DuelCode");
            this.reroll = Find<Button>("DuelReroll");
            this.copy = Find<Button>("DuelCopy");
            this.playMine = Find<Button>("DuelPlayMine");
            this.input = Find<InputField>("DuelFieldBg");
            this.status = Find<Text>("DuelStatus");
            this.paste = Find<Button>("DuelPaste");
            this.playTheirs = Find<Button>("DuelPlayTheirs");
            this.lanOpen = Find<Button>("DuelLanOpen");
        }

        /// <summary>Nối ba thứ nằm ngoài prefab bảng. Gọi lúc dựng.</summary>
        public void BindOutsideForAuthoring(Button open, Button outsideCatcher, LanPanel lanPanel)
        {
            this.openButton = open;
            this.catcher = outsideCatcher;
            this.lan = lanPanel;
        }

        /// <summary>
        /// Nối host và listener. Gọi lại được: mọi nút đều gỡ listener trước.
        ///
        /// Tách khỏi Awake vì bài kiểm dựng UI ở edit mode, nơi Awake không chạy — nối ở
        /// đó thì rig kiểm một bảng câm mà vẫn báo xanh.
        /// </summary>
        public void Wire(IHost owner)
        {
            this.host = owner;

            Bind(this.openButton, OpenPanel);
            Bind(this.catcher, ClosePanel);
            Bind(this.reroll, Reroll);
            Bind(this.copy, CopyCode);
            Bind(this.playMine, () => StartDuel(this.seed, this.preset));
            Bind(this.paste, PasteCode);
            Bind(this.playTheirs, PlayTyped);
            Bind(this.lanOpen, OpenLanPanel);

            if (this.input != null)
            {
                this.input.onValueChanged.RemoveAllListeners();
                this.input.onValueChanged.AddListener(_ => RefreshStatus());
            }

            this.contentHeight = MeasureContent((RectTransform)transform, null);
            if (this.lan != null) this.lan.Wire(this);
            if (this.code != null) Reroll();
        }

        internal static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        /// <summary>
        /// Đo chiều cao nội dung TỪ CÂY THẬT thay vì cộng dồn lúc dựng.
        ///
        /// Bố cục nằm trong prefab và bạn sửa được, nên con số này phải ĐO lại từ những gì
        /// có thật — giữ tổng cộng dồn cũ thì sửa prefab xong bài kiểm "không tràn khung"
        /// vẫn dùng số cũ và mất hết ý nghĩa.
        ///
        /// Trước đây là HAI hàm gần y hệt nhau, khác đúng một tên trong danh sách bỏ qua.
        /// </summary>
        internal static float MeasureContent(RectTransform panel, string alsoSkip)
        {
            float top = panel.rect.height * 0.5f;
            float lowest = 0f;
            foreach (RectTransform child in panel)
            {
                if (child.name == "Fill" || child.name == "Border") continue;
                if (alsoSkip != null && child.name == alsoSkip) continue;
                float bottomFromTop = top - child.localPosition.y + child.rect.height;
                if (bottomFromTop > lowest) lowest = bottomFromTop;
            }
            return lowest;
        }

        // ==================================================================
        // Mở / đóng
        // ==================================================================

        public void OpenPanel()
        {
            this.catcher.gameObject.SetActive(true);
            gameObject.SetActive(true);
            this.catcher.transform.SetAsLastSibling();
            transform.SetAsLastSibling();
            RefreshStatus();
        }

        public void ClosePanel()
        {
            gameObject.SetActive(false);
            if (this.catcher != null) this.catcher.gameObject.SetActive(false);
        }

        private void OpenLanPanel()
        {
            if (this.lan != null) this.lan.OpenPanel();
        }

        // ==================================================================
        // Mã bàn
        // ==================================================================

        /// <summary>
        /// Sinh mã mới. Mầm lấy từ đồng hồ VÀ số ngẫu nhiên: chỉ lấy đồng hồ thì hai máy
        /// bấm cùng lúc ra cùng mã, mà "mã của tôi" trùng "mã của bạn" là chuyện khó hiểu
        /// nhất có thể xảy ra ở màn hình này.
        /// </summary>
        private void Reroll()
        {
            int entropy = System.Environment.TickCount ^ Random.Range(0, int.MaxValue);
            this.seed = DuelChallenge.SeedFrom(entropy);
            this.preset = Random.Range(0, DuelCode.PresetCount);
            this.code.text = DuelCode.Encode(this.seed, this.preset);
        }

        private void CopyCode()
        {
            string text = this.code.text;
            this.host.Clipboard =
                "Đấu Connect Puzzle với tôi! Mã: " + text +
                " (" + DuelChallenge.PresetName(this.preset) + ")";
            this.host.Toast("Đã sao chép mã " + text + " — dán vào tin nhắn cho bạn.");
        }

        /// <summary>
        /// Nói ngay lúc GÕ mã đúng hay sai, không đợi bấm nút. Đợi tới lúc bấm thì người
        /// chơi phải đoán xem mình gõ sai chỗ nào.
        /// </summary>
        private void RefreshStatus()
        {
            if (this.status == null) return;
            string raw = this.input != null ? this.input.text : "";

            if (string.IsNullOrEmpty(raw)) { this.status.text = ""; return; }

            switch (DuelCode.TryDecode(raw, out int s, out int p, out int version))
            {
                case DuelCode.DecodeResult.Ok:
                    if (version != DuelCode.Version)
                    {
                        this.status.color = PuzzlePalette.Star;
                        this.status.text = "Mã này thuộc luật bản " + version +
                            ", bạn đang ở bản " + DuelCode.Version +
                            " — hai máy sẽ ra bàn KHÁC nhau. Cần cập nhật cho khớp.";
                    }
                    else
                    {
                        this.status.color = PuzzlePalette.Good;
                        this.status.text = "Hợp lệ · bàn " + DuelChallenge.PresetName(p);
                    }
                    break;
                case DuelCode.DecodeResult.BadChecksum:
                    this.status.color = PuzzlePalette.Bad;
                    this.status.text = "Sai mã — có ký tự gõ nhầm. Kiểm tra lại giúp mình.";
                    break;
                case DuelCode.DecodeResult.BadChar:
                    this.status.color = PuzzlePalette.Bad;
                    this.status.text = "Mã chỉ gồm chữ và số.";
                    break;
                default:
                    this.status.color = PuzzlePalette.Dim;
                    this.status.text = "Mã gồm 8 ký tự, ví dụ K7M2-QX9F.";
                    break;
            }
        }

        private void PlayTyped()
        {
            string raw = this.input != null ? this.input.text : "";
            DuelCode.DecodeResult outcome = DuelCode.TryDecode(raw, out int s, out int p, out int version);

            if (outcome != DuelCode.DecodeResult.Ok)
            {
                // Bấm nút mà thất bại thì phải nói TO. Trước đây chỉ có một tiếng bíp và
                // một dòng chữ nhỏ nằm lẫn giữa hai cái nút — người chơi bấm, không thấy
                // gì xảy ra, và kết luận là nút hỏng.
                this.host.BadSound();
                RefreshStatus();
                this.host.Toast(FailureMessage(outcome, raw));
                return;
            }
            if (version != DuelCode.Version)
            {
                // KHÔNG cho chơi. Cho chơi thì hai người so điểm trên hai bàn khác nhau mà
                // không ai biết — im lặng sai, đúng thứ tệ nhất ở tính năng này.
                this.host.BadSound();
                RefreshStatus();
                this.host.Toast("Mã này thuộc luật bản " + version + ", máy bạn đang ở bản " +
                      DuelCode.Version + ". Hai bên sẽ ra bàn KHÁC nhau nên mình không cho " +
                      "chơi — cần cập nhật cho khớp phiên bản.");
                return;
            }
            StartDuel(s, p);
        }

        public void StartDuel(int newSeed, int newPreset)
        {
            ClosePanel();

            // XOÁ kết quả của ván đấu trước. Không xoá thì vào bàn mới vẫn còn cờ "đã có
            // kết quả", và người chơi bấm "Dán kết quả đối thủ" ngay khi chưa đánh nước
            // nào sẽ được so bằng thành tích của VÁN CŨ — bài kiểm bắt đúng chỗ này.
            this.myResultReady = false;
            this.myResult = default(DuelResult);
            this.seed = newSeed;
            this.preset = newPreset;
            this.boardCode = DuelCode.Encode(newSeed, newPreset);
            this.host.OpenDuelBoard(DuelChallenge.Build(newSeed, newPreset));
            this.host.Toast("⚔ Mã " + this.boardCode + " · bàn " + DuelChallenge.PresetName(newPreset) +
                  " — bạn của bạn sẽ gặp đúng bàn này.");
        }

        /// <summary>
        /// Dán mã từ clipboard. Moi mã ra khỏi CẢ CÂU chứ không đòi clipboard đúng bằng
        /// 8 ký tự — người ta copy nguyên tin nhắn bạn gửi, kèm lời mời và tên bàn.
        /// </summary>
        private void PasteCode()
        {
            string clip = this.host.Clipboard;
            string found = DuelCode.ExtractFrom(clip);

            if (found == null)
            {
                this.host.BadSound();
                this.host.Toast(string.IsNullOrEmpty(clip)
                    ? "Chưa có gì trong clipboard — copy mã bạn gửi rồi bấm lại."
                    : "Không thấy mã hợp lệ trong đoạn vừa dán.");
                return;
            }

            this.input.text = found;
            RefreshStatus();
            this.host.Tone(660f, 0.1f);
        }

        /// <summary>
        /// Câu báo lỗi NÓI RA VIỆC PHẢI LÀM, không chỉ nói "sai". "Mã không hợp lệ" đúng
        /// mà vô dụng: người chơi không biết mình gõ thiếu, gõ nhầm, hay dán sai thứ.
        /// </summary>
        private static string FailureMessage(DuelCode.DecodeResult outcome, string raw)
        {
            int digits = 0;
            if (raw != null)
                foreach (char c in raw)
                    if (char.IsLetterOrDigit(c)) digits++;

            switch (outcome)
            {
                case DuelCode.DecodeResult.BadChecksum:
                    return "Mã này không có thật — chắc có một ký tự gõ nhầm. " +
                           "Đối chiếu lại từng ký tự với mã bạn gửi nhé.";
                case DuelCode.DecodeResult.BadChar:
                    return "Mã chỉ gồm chữ và số. Bạn vừa gõ một ký tự không nằm trong mã.";
                default:
                    if (digits == 0) return "Chưa nhập gì cả. Dán mã bạn gửi hoặc gõ 8 ký tự.";
                    return digits < 8
                        ? "Mã cần 8 ký tự, bạn mới nhập " + digits + ". Ví dụ: K7M2-QX9F."
                        : "Mã dài quá — đúng 8 ký tự thôi, bạn đang có " + digits + ".";
            }
        }

        // ==================================================================
        // Chia sẻ kết quả và phân định
        // ==================================================================

        /// <summary>
        /// Ảnh chụp trạng thái ĐANG chơi, để báo tiến độ. Cùng hàm dựng với kết quả cuối
        /// (DuelVerdict.From) nên hai bên chắc chắn nói về cùng một bàn và cùng bộ số.
        /// </summary>
        public DuelResult SnapshotProgress()
        {
            return DuelVerdict.From(this.host.Session, this.seed, this.preset);
        }

        /// <summary>Dấu bàn đang chơi — để lọc gói tiến độ đến từ một bàn khác.</summary>
        public int CurrentBoardTag => DuelResult.TagOf(this.seed, this.preset, DuelCode.Version);

        public void CaptureResult()
        {
            if (!this.host.OnDuelBoard) return;
            this.myResult = DuelVerdict.From(this.host.Session, this.seed, this.preset);
            this.myResultReady = true;
            if (this.lan != null) this.lan.PublishResult();
        }

        /// <summary>Dòng để dán vào chat: câu người đọc hiểu, kèm mã máy đọc.</summary>
        private string ResultLine()
        {
            DuelResult r = this.myResultReady
                ? this.myResult
                : DuelVerdict.From(this.host.Session, this.seed, this.preset);

            return "Connect Puzzle · bàn " + this.boardCode + "\n" +
                   (r.Won ? "Mình dọn sạch trong " + r.MovesUsed + " lượt"
                          : "Mình bí, còn " + r.CellsLeft + " ô") +
                   " · " + r.Score + " điểm\n" +
                   "Kết quả: " + DuelResultCode.Encode(r);
        }

        public void CopyResult()
        {
            this.host.Clipboard = ResultLine();
            this.host.Toast("Đã sao chép kết quả — dán cho bạn để so. Rồi bấm \"Dán kết quả đối thủ\".");
        }

        /// <summary>
        /// Dán kết quả đối thủ rồi phân định ngay.
        ///
        /// Moi mã KẾT QUẢ trước, không moi mã BÀN: mã bàn 8 ký tự nằm lọt trong mã kết quả
        /// 10 ký tự, nên nếu thử mã bàn trước thì có lúc nó rút ra một mã bàn hợp lệ từ
        /// giữa mã kết quả và ta đi mở một bàn khác thay vì so điểm.
        /// </summary>
        public void PasteOpponentResult()
        {
            string clip = this.host.Clipboard;
            string found = DuelResultCode.ExtractFrom(clip);

            if (found == null)
            {
                this.host.BadSound();
                this.host.Toast(string.IsNullOrEmpty(clip)
                    ? "Chưa có gì trong clipboard — nhờ bạn gửi kết quả rồi copy vào."
                    : "Không thấy mã kết quả trong đoạn vừa dán. Bạn cần copy CẢ dòng " +
                      "\"Kết quả: …\" mà đối thủ gửi.");
                return;
            }

            DuelResultCode.TryDecode(found, out DuelResult theirs);

            if (!this.myResultReady)
            {
                this.host.BadSound();
                this.host.Toast("Bạn chơi xong bàn này đã, rồi mới so được.");
                return;
            }

            DuelOutcome outcome = DuelVerdict.Compare(this.myResult, theirs, out string reason);
            ShowVerdict(theirs, outcome, reason);
        }

        /// <summary>Chuyển toast hộ LanPanel — chỉ bên này cầm host.</summary>
        public void RelayToast(string message) => this.host.Toast(message);

        /// <summary>Công khai vì LanPanel cũng dựng đúng tấm thẻ này khi so qua mạng.</summary>
        public void ShowVerdict(DuelResult theirs, DuelOutcome outcome, string reason)
        {
            OverlayCard card = this.host.Card;
            card.Begin(3);

            string headline;
            Color headColour;
            switch (outcome)
            {
                case DuelOutcome.Win:  headline = "Bạn thắng!"; headColour = PuzzlePalette.Good; break;
                case DuelOutcome.Lose: headline = "Bạn thua";   headColour = PuzzlePalette.Bad; break;
                case DuelOutcome.Draw: headline = "Hoà";        headColour = PuzzlePalette.Star; break;
                default:               headline = "Khác bàn";   headColour = PuzzlePalette.Star; break;
            }

            Text title = Ui.Text("Title", card.Root, headline,
                56, headColour, TextAnchor.UpperCenter, FontStyle.Bold);

            string table = outcome == DuelOutcome.DifferentBoard
                ? reason
                : "BẠN   " + Describe(this.myResult) + "\n" +
                  "ĐỐI THỦ   " + Describe(theirs) + "\n\n" + reason;

            Text detail = Ui.Text("Detail", card.Root, table,
                28, PuzzlePalette.Dim, TextAnchor.UpperCenter);

            card.Header(new[] { title, detail }, new[] { 56, 28 });

            card.AddButton("Sao chép kết quả của mình", 0, true, CopyResult);
            card.AddButton("Chơi lại bàn này", 1, false, this.host.RestartLevel);
            card.AddButton("Danh sách màn", 2, false, this.host.ShowMenu);

            if (outcome == DuelOutcome.Win) this.host.Celebrate();
        }

        private static string Describe(DuelResult r)
        {
            return (r.Won ? "sạch bàn · " + r.MovesUsed + " lượt" : "bí · còn " + r.CellsLeft + " ô") +
                   " · " + r.Score + " điểm";
        }

        // ---- lối vào cho kiểm thử: đi qua ĐÚNG các hàm mà nút thật gọi
        public void TestPasteOpponentResult() => PasteOpponentResult();

        private T Find<T>(string name) where T : Component
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                {
                    T found = t.GetComponent<T>();
                    if (found != null) return found;
                }
            return null;
        }
    }
}
