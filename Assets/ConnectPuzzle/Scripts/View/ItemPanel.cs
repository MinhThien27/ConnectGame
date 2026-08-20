using System.Collections.Generic;
using ConnectPuzzle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Bảng vật phẩm dùng một lần: mua bằng sao rồi dùng NGAY, không có kho đồ.
    ///
    /// Bỏ kho đồ đi vì kho đòi thêm màn hình quản lý, thêm chỗ lưu, mà không thêm quyết
    /// định nào cho người chơi — quyết định thật chỉ có một: "dùng bây giờ, hay để dành
    /// sao?". Luật giá và luật dùng nằm trong PuzzleSession; lớp này lo phần TIÊU TIỀN
    /// và phần NGẮM: chọn món trong bảng rồi chạm vào ô muốn tác động.
    ///
    /// Sao chỉ bị trừ khi món THẬT SỰ dùng được lên ô đó. Tiêu ngay lúc chọn trong bảng
    /// thì chạm nhầm vào ô trống là mất sao mà chẳng được gì.
    ///
    /// Đặt trên CHÍNH node bảng (gốc của ItemPanel.prefab). Gộp từ hai lớp cũ:
    /// ItemPanelView (chỉ giữ tham chiếu) và ItemShop (chỉ giữ hàm) — hai nửa của cùng
    /// một thứ, mà tách ra thì PuzzleGame phải làm trung gian truyền 8 tham chiếu.
    /// </summary>
    public sealed class ItemPanel : MonoBehaviour
    {
        /// <summary>
        /// Phần màn chơi mà bảng vật phẩm cần tới.
        ///
        /// PlayItemEffect nằm ở host chứ không ở đây vì nó là coroutine chạy hoạt ảnh rồi
        /// gọi lại Evaluate — đó là luồng ván chơi, không phải việc của cửa hàng.
        /// </summary>
        public interface IHost
        {
            PuzzleSession Session { get; }

            /// <summary>Đang chạy hoạt ảnh; lúc này mọi thao tác vật phẩm phải im.</summary>
            bool Busy { get; }

            /// <summary>
            /// Ván này có cho dùng vật phẩm không. Thử thách hằng ngày và ván đấu thì
            /// KHÔNG, dù Core vẫn cho: điểm của mọi máy chỉ so được với nhau khi ai cũng
            /// chơi đúng một bàn với đúng một bộ luật.
            /// </summary>
            bool ItemsUsable { get; }

            void Toast(string message);
            void BadSound();
            void Tone(float hertz, float seconds);
            void PlayItemEffect(MoveResult effect);

            /// <summary>Vẽ lại ví ở CẢ menu lẫn bảng này.</summary>
            void RefreshWallet();
        }

        /// <summary>
        /// Thứ tự ba dòng trong bảng. Prefab PHẢI xếp đúng thứ tự này — bài kiểm chốt
        /// điều đó, vì lệch thứ tự thì bấm "Búa" ra "Sơn" mà giao diện trông vẫn đúng
        /// hoàn toàn.
        /// </summary>
        public static readonly PuzzleSession.ItemKind[] Order =
        {
            PuzzleSession.ItemKind.Hammer,
            PuzzleSession.ItemKind.Paint,
            PuzzleSession.ItemKind.ExtraMove
        };

        // ---- nằm TRONG prefab bảng, nối ngay trong prefab đó
        [SerializeField] private Text head;
        [SerializeField] private Text wallet;
        [SerializeField] private Button[] rows;
        [SerializeField] private Text[] costs;

        // ---- nằm NGOÀI prefab bảng: nút mở nằm trong hàng điều khiển, lớp chặn là em
        //      ruột của bảng. Tham chiếu không đòi quan hệ cha con nên bảng vẫn tự giữ
        //      được, và PuzzleGame không phải biết tới chúng nữa.
        [SerializeField] private Button openButton;
        [SerializeField] private Text balanceText;
        [SerializeField] private Button catcher;

        private IHost host;

        /// <summary>Vật phẩm đang chờ chọn ô. None = không ở chế độ ngắm.</summary>
        private PuzzleSession.ItemKind pending = PuzzleSession.ItemKind.None;

        public Text Head => this.head;

        /// <summary>Món đang cầm; cú chạm kế tiếp lên bàn sẽ dùng nó.</summary>
        public PuzzleSession.ItemKind Pending => this.pending;

        public bool Aiming => this.pending != PuzzleSession.ItemKind.None;

        /// <summary>Bỏ ngắm. Gọi khi vào ván mới hoặc khi mở bảng.</summary>
        public void ClearPending() { this.pending = PuzzleSession.ItemKind.None; }

        public List<string> MissingFields()
        {
            var missing = new List<string>();
            if (this.head == null) missing.Add(nameof(this.head));
            if (this.wallet == null) missing.Add(nameof(this.wallet));
            if (this.rows == null || this.rows.Length != 3) missing.Add("rows(3)");
            else for (int i = 0; i < 3; i++) if (this.rows[i] == null) missing.Add("rows[" + i + "]");
            if (this.costs == null || this.costs.Length != 3) missing.Add("costs(3)");
            else for (int i = 0; i < 3; i++) if (this.costs[i] == null) missing.Add("costs[" + i + "]");
            return missing;
        }

        /// <summary>Thứ tự PHẢI khớp Order — bài kiểm chốt điều này.</summary>
        public void BindByNameForAuthoring()
        {
            this.head = Find<Text>("ItemHead");
            this.wallet = Find<Text>("ItemWallet");

            string[] kinds = { "Hammer", "Paint", "ExtraMove" };
            this.rows = new Button[kinds.Length];
            this.costs = new Text[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                this.rows[i] = Find<Button>("ItemRow" + kinds[i]);
                Transform row = this.rows[i] == null ? null : this.rows[i].transform;
                this.costs[i] = row == null ? null : FindIn<Text>(row, "Cost");
            }
        }

        /// <summary>
        /// Nối ba thứ nằm NGOÀI prefab bảng. Gọi lúc dựng, vì chúng không tìm được bằng
        /// transform.Find từ bảng.
        /// </summary>
        public void BindOutsideForAuthoring(Button open, Text balance, Button outsideCatcher)
        {
            this.openButton = open;
            this.balanceText = balance;
            this.catcher = outsideCatcher;
        }

        /// <summary>
        /// Nối host và listener. Gọi lại được: mọi nút đều gỡ listener trước.
        ///
        /// Tách khỏi Awake vì bài kiểm dựng UI ở edit mode, nơi Awake không chạy.
        /// </summary>
        public void Wire(IHost owner)
        {
            this.host = owner;

            Bind(this.openButton, TogglePanel);
            Bind(this.catcher, ClosePanel);
            if (this.rows == null) return;

            for (int i = 0; i < Order.Length && i < this.rows.Length; i++)
            {
                PuzzleSession.ItemKind kind = Order[i];
                Bind(this.rows[i], () => Choose(kind));
            }
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        // ==================================================================
        // Mở / đóng
        // ==================================================================

        public void TogglePanel()
        {
            if (gameObject.activeSelf) { ClosePanel(); return; }
            if (this.host.Busy || !this.host.ItemsUsable) return;

            // Mở bảng cũng là bỏ ngắm: đang cầm búa mà mở bảng ra thì cú chạm kế tiếp là
            // chạm vào bảng, không phải chạm vào bàn.
            ClearPending();
            RefreshBar();
            if (this.catcher != null) this.catcher.gameObject.SetActive(true);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void ClosePanel()
        {
            gameObject.SetActive(false);
            if (this.catcher != null) this.catcher.gameObject.SetActive(false);
        }

        /// <summary>Cập nhật nút mở bảng và ba dòng trong bảng theo số sao hiện có.</summary>
        public void RefreshBar()
        {
            if (this.openButton == null) return;

            bool show = this.host.ItemsUsable;
            this.openButton.gameObject.SetActive(show);
            if (!show) { ClosePanel(); return; }

            int balance = PuzzleProgress.StarsBalance(LevelCatalog.Levels.Length);
            this.balanceText.text = balance.ToString();
            this.openButton.interactable = !this.host.Busy;

            // Nút sáng lên khi đang cầm một món, để biết cú chạm tới sẽ làm gì.
            this.openButton.GetComponent<Image>().color =
                Aiming ? PuzzlePalette.PanelLight : PuzzlePalette.Panel;

            for (int i = 0; i < Order.Length && i < this.rows.Length; i++)
            {
                int cost = PuzzleSession.ItemCost(Order[i]);
                bool ok = balance >= cost;
                this.rows[i].interactable = ok;
                this.costs[i].color = ok ? PuzzlePalette.Star : PuzzlePalette.Dim;
            }
            this.host.RefreshWallet();
        }

        /// <summary>Vẽ dòng số dư. Chuỗi do host dựng để menu và bảng nói giống nhau.</summary>
        public void ShowWallet(string richText, int balance)
        {
            if (this.wallet == null) return;
            this.wallet.text = balance > 0 ? richText : "<color=#7C819E>Hết sao</color>";
        }

        // ==================================================================
        // Mua và dùng
        // ==================================================================

        private void Choose(PuzzleSession.ItemKind kind)
        {
            ClosePanel();
            if (this.host.Busy || !this.host.ItemsUsable) return;

            int cost = PuzzleSession.ItemCost(kind);
            if (PuzzleProgress.StarsBalance(LevelCatalog.Levels.Length) < cost)
            {
                this.host.BadSound();
                this.host.Toast("Cần ★" + cost + " — thắng thêm màn hoặc săn huy hiệu ◆.");
                return;
            }

            // "+1 lượt" không cần ngắm ô nào cả, dùng thẳng.
            if (kind == PuzzleSession.ItemKind.ExtraMove) { Apply(kind, -1); return; }

            this.pending = kind;
            RefreshBar();
            this.host.Toast(kind == PuzzleSession.ItemKind.Hammer
                ? "Chạm vào ô muốn đập. Đá và băng chỉ mất 1 lớp. Bấm lại nút để bỏ."
                : "Chạm vào ô muốn biến thành đa sắc. Bấm lại nút để bỏ.");
        }

        /// <summary>
        /// Tiêu sao rồi mới dùng, và CHỈ tiêu khi dùng được thật. Tiêu ngay lúc chọn
        /// trong bảng thì chạm nhầm vào ô trống là mất sao mà chẳng được gì.
        /// </summary>
        public void Apply(PuzzleSession.ItemKind kind, int cell)
        {
            PuzzleSession session = this.host.Session;

            if (!session.CanUseItem(kind, cell))
            {
                this.host.BadSound();
                this.host.Toast(kind == PuzzleSession.ItemKind.Paint
                    ? "Sơn chỉ dùng được lên ô màu thường."
                    : "Không dùng được lên ô đó.");
                return;
            }

            int cost = PuzzleSession.ItemCost(kind);
            if (!PuzzleProgress.SpendStars(cost, LevelCatalog.Levels.Length))
            {
                this.host.BadSound();
                return;
            }

            if (session.UseItem(kind, cell, out MoveResult effect) != PuzzleSession.ItemUse.Ok)
            {
                PuzzleProgress.RefundStars(cost);   // không xảy ra, nhưng mất sao thì không tha thứ được
                this.host.BadSound();
                return;
            }

            ClearPending();
            this.host.Tone(660f, 0.12f);

            if (kind == PuzzleSession.ItemKind.ExtraMove)
                this.host.Toast("+1 lượt — còn " + session.MovesLeft + " lượt.");

            this.host.PlayItemEffect(effect);
        }

        // ==================================================================

        private T FindIn<T>(Transform root, string name) where T : Component
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                {
                    T found = t.GetComponent<T>();
                    if (found != null) return found;
                }
            return null;
        }

        private T Find<T>(string name) where T : Component => FindIn<T>(transform, name);
    }
}
