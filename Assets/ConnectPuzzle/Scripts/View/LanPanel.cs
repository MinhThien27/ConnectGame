using System.Collections.Generic;
using ConnectPuzzle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Bảng "Đấu cùng Wi-Fi": bắt cặp hai máy trong cùng mạng rồi trao đổi mã bàn và kết
    /// quả qua UDP.
    ///
    /// Phụ thuộc MỘT CHIỀU vào DuelPanel, và chỉ một chiều đó thôi: Wi-Fi là một cách
    /// CHUYỂN mã và kết quả thay cho copy-paste, nó không đổi luật phân định nào. Trạng
    /// thái ván đấu (seed, preset, mã, kết quả của mình) sống một bản duy nhất bên
    /// DuelPanel; lớp này chỉ giữ trạng thái riêng của phiên mạng.
    ///
    /// Dùng [SerializeField] chứ KHÔNG tìm theo tên. Tìm theo tên nghĩa là đổi tên một
    /// node trong Editor sẽ làm code vỡ âm thầm — mà "dễ quản lý trong Editor" chính là
    /// lý do chuyển sang prefab. Đổi lại, prefab sinh ra một loại lỗi mới mà code-dựng-UI
    /// không có: QUÊN GÁN một field. Vì vậy có MissingFields() và bài kiểm chạy nó trên
    /// prefab thật.
    /// </summary>
    public sealed class LanPanel : MonoBehaviour
    {
        // ---- nằm TRONG prefab bảng
        [Header("Khung")]
        [SerializeField] private RectTransform panel;

        [Header("Chữ")]
        [SerializeField] private Text head;
        [SerializeField] private Text note;
        [SerializeField] private Text status;

        [Header("Nút")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button seekButton;
        [SerializeField] private Button closeButton;

        // ---- nằm NGOÀI prefab bảng
        [SerializeField] private Button catcher;
        [SerializeField] private DuelLanLink link;

        private DuelPanel duel;

        private bool active;
        private DuelResult opponent;
        private bool hasOpponent;
        private string opponentName = "";
        private float nextAnnounce;
        private int seed, preset;
        private float contentHeight;

        public RectTransform Panel => this.panel;
        public Text Head => this.head;
        public Text Note => this.note;

        // ---- số đo và trạng thái, cho bài kiểm đọc thay vì chép lại hằng số
        public float ContentHeight => this.contentHeight;
        public float PanelHeight => this.panel == null ? 0f : this.panel.rect.height;
        public bool Active => this.active;
        public string StatusText => this.status == null ? "" : this.status.text;
        public DuelLanLink Link => this.link;

        /// <summary>
        /// Liệt kê MỌI field còn trống. Rỗng nghĩa là prefab gán đủ.
        ///
        /// Trả danh sách thay vì bool: "prefab thiếu tham chiếu" là câu vô dụng khi có bảy
        /// field, còn "thiếu status, seekButton" thì sửa được ngay.
        /// </summary>
        public List<string> MissingFields()
        {
            var missing = new List<string>();
            if (this.panel == null) missing.Add(nameof(this.panel));
            if (this.head == null) missing.Add(nameof(this.head));
            if (this.note == null) missing.Add(nameof(this.note));
            if (this.status == null) missing.Add(nameof(this.status));
            if (this.hostButton == null) missing.Add(nameof(this.hostButton));
            if (this.seekButton == null) missing.Add(nameof(this.seekButton));
            if (this.closeButton == null) missing.Add(nameof(this.closeButton));
            return missing;
        }

        /// <summary>
        /// Nối các field bằng cách tìm theo tên MỘT LẦN, dùng cho lúc dựng prefab.
        ///
        /// Đây là công cụ của editor script, KHÔNG phải đường chạy lúc chơi: lúc chơi thì
        /// mọi thứ đã gán sẵn trong prefab. Gọi ở runtime là quay về tìm-theo-tên, tức là
        /// quay lại đúng thứ vừa bỏ.
        /// </summary>
        public void BindByNameForAuthoring()
        {
            this.panel = GetComponent<RectTransform>();
            this.head = Find<Text>("LanHead");
            this.note = Find<Text>("LanNote");
            this.status = Find<Text>("LanStatus");
            this.hostButton = Find<Button>("LanHost");
            this.seekButton = Find<Button>("LanSeek");
            this.closeButton = Find<Button>("LanClose");
        }

        /// <summary>Nối hai thứ nằm ngoài prefab bảng. Gọi lúc dựng.</summary>
        public void BindOutsideForAuthoring(Button outsideCatcher, DuelLanLink lanLink)
        {
            this.catcher = outsideCatcher;
            this.link = lanLink;
        }

        /// <summary>
        /// Nối bảng đấu và listener. Gọi lại được: mọi nút đều gỡ listener trước.
        ///
        /// Gọi từ DuelPanel.Wire chứ không từ PuzzleGame: quan hệ phụ thuộc là
        /// LanPanel -> DuelPanel, nên chủ của nó là bảng đấu.
        /// </summary>
        public void Wire(DuelPanel owner)
        {
            this.duel = owner;

            // DuelLanLink là MonoBehaviour nên prefab GIỮ được component, nhưng event C#
            // thì không — phải đăng ký lại mỗi lần chạy. Gỡ trước khi gắn để gọi lại
            // không nhân đôi số lần xử lý một lời mời.
            if (this.link != null)
            {
                this.link.OnInvite -= OnInvite;
                this.link.OnOpponentResult -= OnOpponentResult;
                this.link.OnProblem -= OnProblem;
                this.link.OnInvite += OnInvite;
                this.link.OnOpponentResult += OnOpponentResult;
                this.link.OnProblem += OnProblem;
            }

            DuelPanel.Bind(this.catcher, ClosePanel);
            DuelPanel.Bind(this.hostButton, StartHost);
            DuelPanel.Bind(this.seekButton, StartSeek);
            DuelPanel.Bind(this.closeButton, ClosePanel);

            if (this.panel != null)
                this.contentHeight = DuelPanel.MeasureContent(this.panel, "LanCatcher");
        }

        // ==================================================================
        // Mở / đóng
        // ==================================================================

        public void OpenPanel()
        {
            if (this.duel != null) this.duel.ClosePanel();
            this.catcher.gameObject.SetActive(true);
            this.panel.gameObject.SetActive(true);

            // Thứ tự BẮT BUỘC: lớp chặn lên trước, BẢNG lên sau — bảng phải vẽ ĐÈ lên lớp
            // chặn. Đảo hai dòng này là lớp tối phủ lên nội dung và mọi cú chạm đều rơi vào
            // lớp chặn, tức là chạm đâu cũng tắt bảng.
            this.catcher.transform.SetAsLastSibling();
            this.panel.SetAsLastSibling();
            SetStatus("Chưa nối. Một người bấm \"Mở phòng\", người kia bấm \"Tìm phòng\".");
        }

        public void ClosePanel()
        {
            if (this.panel == null) return;
            this.panel.gameObject.SetActive(false);
            if (this.catcher != null) this.catcher.gameObject.SetActive(false);
        }

        private void SetStatus(string message)
        {
            if (this.status != null) this.status.text = message;
        }

        // ==================================================================
        // Bắt cặp
        // ==================================================================

        private void StartHost()
        {
            if (!this.link.Start(DuelLanLink.Role.Host)) return;

            this.seed = DuelChallenge.SeedFrom(
                System.Environment.TickCount ^ Random.Range(0, int.MaxValue));
            this.preset = Random.Range(0, DuelCode.PresetCount);
            this.link.Announce(this.seed, this.preset);
            this.nextAnnounce = Time.unscaledTime + 1f;

            SetStatus("Đang mời… Bảo bạn bấm \"Tìm phòng\".");
            BeginDuel(this.seed, this.preset, "Bạn mở phòng");
        }

        private void StartSeek()
        {
            if (!this.link.Start(DuelLanLink.Role.Guest)) return;
            SetStatus("Đang tìm phòng trên mạng Wi-Fi này…");
        }

        private void OnInvite(int inviteSeed, int invitePreset, string who)
        {
            if (this.active) return;                 // đã vào bàn rồi thì bỏ qua lời mời sau
            SetStatus("Đã nối với " + who + ".");
            BeginDuel(inviteSeed, invitePreset, "Vào bàn của " + who);
        }

        /// <summary>
        /// Đối thủ xong trước KHÔNG kết thúc ván của mình. Đây là chỗ dễ cài nhầm nhất:
        /// phản xạ tự nhiên là "đối thủ xong rồi, dừng lại và so" — mà đúng luật thì phải
        /// chơi hết, vì thắng thua tính theo dọn sạch bàn và số lượt.
        /// </summary>
        private void OnOpponentResult(DuelResult result, string who)
        {
            this.opponent = result;
            this.hasOpponent = true;
            this.opponentName = string.IsNullOrEmpty(who) ? "Đối thủ" : who;

            if (this.duel != null && this.duel.ResultReady)
            {
                CompareWithOpponent();
                return;
            }
            Toast(this.opponentName + " đã xong. Chưa ai thắng cả — cứ chơi hết bàn của bạn.");
        }

        private void OnProblem(string message)
        {
            SetStatus(message);
            Toast(message);
        }

        private void BeginDuel(int newSeed, int newPreset, string toast)
        {
            this.active = true;
            this.hasOpponent = false;
            this.opponentName = "";
            ClosePanel();
            this.duel.StartDuel(newSeed, newPreset);
            Toast(toast + " · bàn " + DuelChallenge.PresetName(newPreset) +
                  ". Ai xong trước thì chờ, không phải thắng luôn.");
        }

        /// <summary>Gửi kết quả đi khi ván đấu Wi-Fi kết thúc, rồi so nếu đã có bên kia.</summary>
        public void PublishResult()
        {
            if (!this.active || this.link == null || this.duel == null) return;
            this.link.SendResult(this.duel.Result);

            // CHỈ gửi và báo. KHÔNG dựng thẻ ở đây: hàm này chạy bên trong CaptureResult,
            // mà Evaluate còn chạy tiếp và dựng thẻ thắng/thua đè lên. Việc dựng thẻ phán
            // quyết để Evaluate tự quyết, qua TryShowVerdict.
            if (!this.hasOpponent)
                Toast("Đã gửi kết quả. Đang chờ " +
                      (this.opponentName == "" ? "đối thủ" : this.opponentName) + " chơi xong…");
        }

        /// <summary>
        /// Dựng thẻ phán quyết nếu đủ hai bên. Trả true khi đã dựng, để Evaluate biết mà
        /// DỪNG, không dựng tiếp thẻ thắng/thua đè lên.
        /// </summary>
        public bool TryShowVerdict()
        {
            if (!this.active || !this.hasOpponent) return false;
            if (this.duel == null || !this.duel.ResultReady) return false;
            CompareWithOpponent();
            return true;
        }

        private void CompareWithOpponent()
        {
            DuelOutcome outcome = DuelVerdict.Compare(this.duel.Result, this.opponent,
                                                      out string reason);
            this.duel.ShowVerdict(this.opponent, outcome, reason);
        }

        /// <summary>Chủ phòng phát lại lời mời đều đặn, để người bấm "Tìm phòng" muộn vẫn thấy.</summary>
        public void Tick()
        {
            if (this.link == null || this.link.CurrentRole != DuelLanLink.Role.Host) return;
            if (this.hasOpponent) return;
            if (Time.unscaledTime < this.nextAnnounce) return;
            this.nextAnnounce = Time.unscaledTime + 1f;
            this.link.Announce(this.seed, this.preset);
        }

        /// <summary>
        /// Toast đi nhờ DuelPanel vì nó mới là bên cầm host. Lớp này không giữ thêm một
        /// tham chiếu nữa cho một việc duy nhất.
        /// </summary>
        private void Toast(string message)
        {
            if (this.duel != null) this.duel.RelayToast(message);
        }

        // ---- lối vào cho kiểm thử: đi qua ĐÚNG các hàm mà nút thật gọi
        public void TestFeedInvite(int s, int p, string who) => OnInvite(s, p, who);
        public void TestFeedResult(DuelResult r, string who) => OnOpponentResult(r, who);
        public void TestStartHost() => StartHost();

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

        /// <summary>Mọi Image trong bảng còn thiếu sprite — bài kiểm đọc để bắt lỗi quên gán.</summary>
        public List<string> ImagesWithoutSprite()
        {
            var missing = new List<string>();
            foreach (Image image in GetComponentsInChildren<Image>(true))
                if (image.sprite == null) missing.Add(image.name);
            return missing;
        }
    }
}
