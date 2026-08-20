using ConnectPuzzle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Toàn bộ chế độ "đấu": sinh/nhập mã bàn, chia sẻ kết quả để tự so, và bắt cặp
    /// qua Wi-Fi cùng mạng.
    ///
    /// Ba thứ đó ở chung một lớp vì chúng là MỘT tính năng nhìn từ người chơi: cùng một
    /// bàn, cùng một luật phân định (dọn sạch > ít lượt > điểm), cùng một thẻ phán quyết.
    /// Wi-Fi chỉ là một cách CHUYỂN mã và kết quả thay cho copy-paste — nó không đổi luật
    /// nào cả, nên tách nó ra lớp riêng sẽ phải nhân đôi trạng thái ván đấu.
    ///
    /// Là lớp C# thuần, không phải MonoBehaviour — giống BoardView, EffectLayer,
    /// OverlayCard. Có lý do cụ thể chứ không phải cho đồng bộ: các tham chiếu UI đang là
    /// [SerializeField] trên PuzzleGame và đã được lưu vào PuzzleRoot.prefab. Chuyển
    /// chúng sang một MonoBehaviour mới là làm gãy hết tham chiếu trong prefab và phải
    /// nối tay lại — đổi lấy đúng con số 0 lợi ích.
    ///
    /// Những gì cần từ màn chơi đi qua IHost. Danh sách đó CỐ Ý ngắn: nó chính là thước
    /// đo xem việc tách có sạch không.
    /// </summary>
    public sealed class DuelController
    {
        /// <summary>
        /// Phần màn chơi mà chế độ đấu cần tới.
        ///
        /// Gói theo VIỆC chứ không theo đối tượng: Celebrate() thay cho (effects, board,
        /// audioPlayer), OpenDuelBoard() thay cho (OpenLevelData, DuelIndex). Nhờ vậy
        /// lớp này không cầm tham chiếu tới thứ nó không có việc gì phải biết.
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

        private readonly IHost host;

        private readonly RectTransform panel;
        private readonly Button catcher;
        private readonly DuelPanelView view;

        private readonly DuelLanLink lan;
        private readonly Button lanCatcher;
        private readonly LanPanelView lanView;

        /// <summary>Mã của ván đấu đang chơi; rỗng nếu không phải ván đấu.</summary>
        private string code = "";
        private int seed, preset;

        /// <summary>
        /// Kết quả của CHÍNH ván đấu vừa xong. Chốt một lần lúc ván kết thúc thay vì đọc
        /// lại từ session mỗi lúc cần: người chơi có thể bấm Hoàn tác hay Xáo lại từ thẻ
        /// thua, và nếu đọc lại thì con số gửi cho bạn bè sẽ khác con số đã hiện ra.
        /// </summary>
        private DuelResult myResult;
        private bool myResultReady;

        private bool lanActive;
        private DuelResult lanOpponent;
        private bool lanHasOpponent;
        private string lanOpponentName = "";
        private float lanNextAnnounce;
        private int lanSeed, lanPreset;

        private float contentHeight;
        private float lanContentHeight;

        public DuelController(IHost host,
                              RectTransform panel, Button catcher, DuelPanelView view,
                              DuelLanLink lan, Button lanCatcher, LanPanelView lanView)
        {
            this.host = host;
            this.panel = panel;
            this.catcher = catcher;
            this.view = view;
            this.lan = lan;
            this.lanCatcher = lanCatcher;
            this.lanView = lanView;
        }

        private Text CodeText => this.view == null ? null : this.view.Code;
        private InputField Input => this.view == null ? null : this.view.Input;
        private Text StatusText => this.view == null ? null : this.view.Status;
        private RectTransform LanPanel => this.lanView == null ? null : this.lanView.Panel;
        private Text LanStatusText => this.lanView == null ? null : this.lanView.Status;

        // ---- số đo, cho bài kiểm đọc thay vì chép lại hằng số
        public float ContentHeight => this.contentHeight;
        public float LanContentHeight => this.lanContentHeight;
        public float LanPanelHeight => LanPanel == null ? 0f : LanPanel.rect.height;
        public bool LanActive => this.lanActive;
        public string LanStatus => LanStatusText == null ? "" : LanStatusText.text;
        public DuelLanLink Link => this.lan;

        /// <summary>Mã của ván đấu đang chơi, để màn chơi đặt tên bàn.</summary>
        public string Code => this.code;

        /// <summary>Tên kiểu bàn của ván đấu đang chơi.</summary>
        public string PresetLabel => DuelChallenge.PresetName(this.preset);

        // ==================================================================
        // Nối hành vi
        // ==================================================================

        /// <summary>
        /// Nối listener và đo lại số đo bảng. Gọi lại được: mọi nút đều gỡ listener trước.
        ///
        /// Tách khỏi hàm dựng vì prefab chỉ lưu được HÌNH DẠNG — AddListener là đăng ký
        /// lúc chạy, Unity không ghi nó vào file.
        /// </summary>
        public void Wire()
        {
            Bind(this.catcher, ClosePanel);
            if (this.view != null)
            {
                Bind(this.view.Reroll, Reroll);
                Bind(this.view.Copy, CopyCode);
                Bind(this.view.PlayMine, () => Start(this.seed, this.preset));
                Bind(this.view.Paste, PasteCode);
                Bind(this.view.PlayTheirs, PlayTyped);
                Bind(this.view.LanOpen, OpenLanPanel);
            }
            if (Input != null)
            {
                Input.onValueChanged.RemoveAllListeners();
                Input.onValueChanged.AddListener(_ => RefreshStatus());
            }

            // DuelLanLink là MonoBehaviour nên prefab GIỮ được component, nhưng event C#
            // thì không — phải đăng ký lại mỗi lần chạy. Gỡ trước khi gắn để gọi lại
            // không nhân đôi số lần xử lý một lời mời.
            if (this.lan != null)
            {
                this.lan.OnInvite -= OnLanInvite;
                this.lan.OnOpponentResult -= OnLanOpponentResult;
                this.lan.OnProblem -= OnLanProblem;
                this.lan.OnInvite += OnLanInvite;
                this.lan.OnOpponentResult += OnLanOpponentResult;
                this.lan.OnProblem += OnLanProblem;
            }

            Bind(this.lanCatcher, CloseLanPanel);
            if (this.lanView != null)
            {
                Bind(this.lanView.HostButton, StartLanHost);
                Bind(this.lanView.SeekButton, StartLanSeek);
                Bind(this.lanView.CloseButton, CloseLanPanel);
            }

            if (this.panel != null) this.contentHeight = MeasureContent(this.panel, null);
            if (LanPanel != null) this.lanContentHeight = MeasureContent(LanPanel, "LanCatcher");
            if (CodeText != null) Reroll();
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        /// <summary>
        /// Đo chiều cao nội dung TỪ CÂY THẬT thay vì cộng dồn lúc dựng.
        ///
        /// Bố cục nằm trong prefab và bạn sửa được, nên con số này phải ĐO lại từ những
        /// gì có thật — giữ tổng cộng dồn cũ thì sửa prefab xong bài kiểm "không tràn
        /// khung" vẫn dùng số cũ và mất hết ý nghĩa.
        ///
        /// Trước đây là HAI hàm gần y hệt nhau, khác đúng một tên trong danh sách bỏ qua.
        /// </summary>
        private static float MeasureContent(RectTransform panel, string alsoSkip)
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
        // Bảng đấu seed
        // ==================================================================

        public void OpenPanel()
        {
            this.catcher.gameObject.SetActive(true);
            this.panel.gameObject.SetActive(true);
            this.catcher.transform.SetAsLastSibling();
            this.panel.SetAsLastSibling();
            RefreshStatus();
        }

        public void ClosePanel()
        {
            this.panel.gameObject.SetActive(false);
            this.catcher.gameObject.SetActive(false);
        }

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
            CodeText.text = DuelCode.Encode(this.seed, this.preset);
        }

        private void CopyCode()
        {
            string text = CodeText.text;
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
            Text status = StatusText;
            if (status == null) return;
            string raw = Input != null ? Input.text : "";

            if (string.IsNullOrEmpty(raw)) { status.text = ""; return; }

            switch (DuelCode.TryDecode(raw, out int s, out int p, out int version))
            {
                case DuelCode.DecodeResult.Ok:
                    if (version != DuelCode.Version)
                    {
                        status.color = PuzzlePalette.Star;
                        status.text = "Mã này thuộc luật bản " + version +
                            ", bạn đang ở bản " + DuelCode.Version +
                            " — hai máy sẽ ra bàn KHÁC nhau. Cần cập nhật cho khớp.";
                    }
                    else
                    {
                        status.color = PuzzlePalette.Good;
                        status.text = "Hợp lệ · bàn " + DuelChallenge.PresetName(p);
                    }
                    break;
                case DuelCode.DecodeResult.BadChecksum:
                    status.color = PuzzlePalette.Bad;
                    status.text = "Sai mã — có ký tự gõ nhầm. Kiểm tra lại giúp mình.";
                    break;
                case DuelCode.DecodeResult.BadChar:
                    status.color = PuzzlePalette.Bad;
                    status.text = "Mã chỉ gồm chữ và số.";
                    break;
                default:
                    status.color = PuzzlePalette.Dim;
                    status.text = "Mã gồm 8 ký tự, ví dụ K7M2-QX9F.";
                    break;
            }
        }

        private void PlayTyped()
        {
            string raw = Input != null ? Input.text : "";
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
            Start(s, p);
        }

        public void Start(int seed, int preset)
        {
            ClosePanel();

            // XOÁ kết quả của ván đấu trước. Không xoá thì vào bàn mới vẫn còn cờ "đã có
            // kết quả", và người chơi bấm "Dán kết quả đối thủ" ngay khi chưa đánh nước
            // nào sẽ được so bằng thành tích của VÁN CŨ — bài kiểm bắt đúng chỗ này.
            this.myResultReady = false;
            this.myResult = default(DuelResult);
            this.seed = seed;
            this.preset = preset;
            this.code = DuelCode.Encode(seed, preset);
            this.host.OpenDuelBoard(DuelChallenge.Build(seed, preset));
            this.host.Toast("⚔ Mã " + this.code + " · bàn " + DuelChallenge.PresetName(preset) +
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

            Input.text = found;
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

        public void CaptureResult()
        {
            if (!this.host.OnDuelBoard) return;
            this.myResult = DuelVerdict.From(this.host.Session, this.seed, this.preset);
            this.myResultReady = true;
            PublishLanResult();
        }

        /// <summary>Dòng để dán vào chat: câu người đọc hiểu, kèm mã máy đọc.</summary>
        private string ResultLine()
        {
            DuelResult r = this.myResultReady
                ? this.myResult
                : DuelVerdict.From(this.host.Session, this.seed, this.preset);

            return "Connect Puzzle · bàn " + this.code + "\n" +
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
        /// Moi mã KẾT QUẢ trước, không moi mã BÀN: mã bàn 8 ký tự nằm lọt trong mã kết
        /// quả 10 ký tự, nên nếu thử mã bàn trước thì có lúc nó rút ra một mã bàn hợp lệ
        /// từ giữa mã kết quả và ta đi mở một bàn khác thay vì so điểm.
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
            ShowVerdictCard(theirs, outcome, reason);
        }

        private void ShowVerdictCard(DuelResult theirs, DuelOutcome outcome, string reason)
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

        // ==================================================================
        // Đấu cùng Wi-Fi
        // ==================================================================

        public void OpenLanPanel()
        {
            ClosePanel();
            this.lanCatcher.gameObject.SetActive(true);
            LanPanel.gameObject.SetActive(true);

            // Thứ tự BẮT BUỘC: lớp chặn lên trước, BẢNG lên sau — bảng phải vẽ ĐÈ lên lớp
            // chặn. Đảo hai dòng này là lớp tối phủ lên nội dung và mọi cú chạm đều rơi vào
            // lớp chặn, tức là chạm đâu cũng tắt bảng.
            this.lanCatcher.transform.SetAsLastSibling();
            LanPanel.SetAsLastSibling();
            SetLanStatus("Chưa nối. Một người bấm \"Mở phòng\", người kia bấm \"Tìm phòng\".");
        }

        public void CloseLanPanel()
        {
            if (LanPanel == null) return;
            LanPanel.gameObject.SetActive(false);
            if (this.lanCatcher != null) this.lanCatcher.gameObject.SetActive(false);
        }

        private void SetLanStatus(string message)
        {
            if (LanStatusText != null) LanStatusText.text = message;
        }

        private void StartLanHost()
        {
            if (!this.lan.Start(DuelLanLink.Role.Host)) return;

            this.lanSeed = DuelChallenge.SeedFrom(
                System.Environment.TickCount ^ Random.Range(0, int.MaxValue));
            this.lanPreset = Random.Range(0, DuelCode.PresetCount);
            this.lan.Announce(this.lanSeed, this.lanPreset);
            this.lanNextAnnounce = Time.unscaledTime + 1f;

            SetLanStatus("Đang mời… Bảo bạn bấm \"Tìm phòng\".");
            BeginLanDuel(this.lanSeed, this.lanPreset, "Bạn mở phòng");
        }

        private void StartLanSeek()
        {
            if (!this.lan.Start(DuelLanLink.Role.Guest)) return;
            SetLanStatus("Đang tìm phòng trên mạng Wi-Fi này…");
        }

        private void OnLanInvite(int seed, int preset, string who)
        {
            if (this.lanActive) return;              // đã vào bàn rồi thì bỏ qua lời mời sau
            SetLanStatus("Đã nối với " + who + ".");
            BeginLanDuel(seed, preset, "Vào bàn của " + who);
        }

        /// <summary>
        /// Đối thủ xong trước KHÔNG kết thúc ván của mình. Đây là chỗ dễ cài nhầm nhất:
        /// phản xạ tự nhiên là "đối thủ xong rồi, dừng lại và so" — mà đúng luật thì phải
        /// chơi hết, vì thắng thua tính theo dọn sạch bàn và số lượt.
        /// </summary>
        private void OnLanOpponentResult(DuelResult result, string who)
        {
            this.lanOpponent = result;
            this.lanHasOpponent = true;
            this.lanOpponentName = string.IsNullOrEmpty(who) ? "Đối thủ" : who;

            if (this.myResultReady)
            {
                CompareWithLanOpponent();
                return;
            }
            this.host.Toast(this.lanOpponentName +
                            " đã xong. Chưa ai thắng cả — cứ chơi hết bàn của bạn.");
        }

        private void OnLanProblem(string message)
        {
            SetLanStatus(message);
            this.host.Toast(message);
        }

        private void BeginLanDuel(int seed, int preset, string toast)
        {
            this.lanActive = true;
            this.lanHasOpponent = false;
            this.lanOpponentName = "";
            CloseLanPanel();
            Start(seed, preset);
            this.host.Toast(toast + " · bàn " + DuelChallenge.PresetName(preset) +
                  ". Ai xong trước thì chờ, không phải thắng luôn.");
        }

        /// <summary>Gửi kết quả đi khi ván đấu Wi-Fi kết thúc, rồi so nếu đã có bên kia.</summary>
        private void PublishLanResult()
        {
            if (!this.lanActive || this.lan == null) return;
            this.lan.SendResult(this.myResult);

            // CHỈ gửi và báo. KHÔNG dựng thẻ ở đây: hàm này chạy bên trong CaptureResult,
            // mà Evaluate còn chạy tiếp và dựng thẻ thắng/thua đè lên. Việc dựng thẻ phán
            // quyết để Evaluate tự quyết, qua TryShowLanVerdict.
            if (!this.lanHasOpponent)
                this.host.Toast("Đã gửi kết quả. Đang chờ " +
                      (this.lanOpponentName == "" ? "đối thủ" : this.lanOpponentName) + " chơi xong…");
        }

        /// <summary>
        /// Dựng thẻ phán quyết nếu đủ hai bên. Trả true khi đã dựng, để Evaluate biết mà
        /// DỪNG, không dựng tiếp thẻ thắng/thua đè lên.
        /// </summary>
        public bool TryShowLanVerdict()
        {
            if (!this.lanActive || !this.lanHasOpponent || !this.myResultReady) return false;
            CompareWithLanOpponent();
            return true;
        }

        private void CompareWithLanOpponent()
        {
            DuelOutcome outcome = DuelVerdict.Compare(this.myResult, this.lanOpponent,
                                                      out string reason);
            ShowVerdictCard(this.lanOpponent, outcome, reason);
        }

        /// <summary>Chủ phòng phát lại lời mời đều đặn, để người bấm "Tìm phòng" muộn vẫn thấy.</summary>
        public void Tick()
        {
            if (this.lan == null || this.lan.CurrentRole != DuelLanLink.Role.Host) return;
            if (this.lanHasOpponent) return;
            if (Time.unscaledTime < this.lanNextAnnounce) return;
            this.lanNextAnnounce = Time.unscaledTime + 1f;
            this.lan.Announce(this.lanSeed, this.lanPreset);
        }

        // ---- lối vào cho kiểm thử: đi qua ĐÚNG các hàm mà nút thật gọi
        public void TestFeedLanInvite(int seed, int preset, string who) => OnLanInvite(seed, preset, who);
        public void TestFeedLanResult(DuelResult r, string who) => OnLanOpponentResult(r, who);
        public void TestStartLanHost() => StartLanHost();
    }
}
