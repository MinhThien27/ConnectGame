using ConnectPuzzle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Vật phẩm dùng một lần: mua bằng sao rồi dùng NGAY, không có kho đồ.
    ///
    /// Bỏ kho đồ đi vì kho đòi thêm màn hình quản lý, thêm chỗ lưu, mà không thêm quyết
    /// định nào cho người chơi — quyết định thật chỉ có một: "dùng bây giờ, hay để dành
    /// sao?". Luật giá và luật dùng nằm trong PuzzleSession; lớp này lo phần TIÊU TIỀN
    /// và phần NGẮM: chọn món trong bảng rồi chạm vào ô muốn tác động.
    ///
    /// Sao chỉ bị trừ khi món THẬT SỰ dùng được lên ô đó. Tiêu ngay lúc chọn trong bảng
    /// thì chạm nhầm vào ô trống là mất sao mà chẳng được gì.
    ///
    /// Là lớp C# thuần như OverlayCard và DuelController: các tham chiếu UI vẫn là
    /// [SerializeField] trên PuzzleGame và đã nằm trong PuzzleRoot.prefab, dời sang một
    /// MonoBehaviour mới là gãy hết rồi phải nối tay lại.
    /// </summary>
    public sealed class ItemShop
    {
        /// <summary>
        /// Phần màn chơi mà cửa hàng cần tới.
        ///
        /// PlayEffect nằm ở host chứ không ở đây vì nó là một coroutine chạy hoạt ảnh rồi
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

            /// <summary>Chạy hoạt ảnh của một lần dùng vật phẩm, rồi đánh giá lại ván.</summary>
            void PlayItemEffect(MoveResult effect);

            /// <summary>Vẽ lại ví ở CẢ menu lẫn bảng vật phẩm.</summary>
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

        private readonly IHost host;

        private readonly Button button;
        private readonly Text balanceText;
        private readonly RectTransform panel;
        private readonly Button catcher;
        private readonly Button[] rows;
        private readonly Text[] rowCosts;
        private readonly Text walletText;

        /// <summary>Vật phẩm đang chờ chọn ô. None = không ở chế độ ngắm.</summary>
        private PuzzleSession.ItemKind pending = PuzzleSession.ItemKind.None;

        public ItemShop(IHost host, Button button, Text balanceText,
                        RectTransform panel, Button catcher,
                        Button[] rows, Text[] rowCosts, Text walletText)
        {
            this.host = host;
            this.button = button;
            this.balanceText = balanceText;
            this.panel = panel;
            this.catcher = catcher;
            this.rows = rows;
            this.rowCosts = rowCosts;
            this.walletText = walletText;
        }

        /// <summary>Món đang cầm; cú chạm kế tiếp lên bàn sẽ dùng nó.</summary>
        public PuzzleSession.ItemKind Pending => this.pending;

        public bool Aiming => this.pending != PuzzleSession.ItemKind.None;

        /// <summary>Bỏ ngắm. Gọi khi vào ván mới hoặc khi mở bảng.</summary>
        public void ClearPending() { this.pending = PuzzleSession.ItemKind.None; }

        /// <summary>
        /// Nối listener. Gọi lại được: mọi nút đều gỡ listener trước. Tách khỏi hàm dựng
        /// vì prefab chỉ lưu được hình dạng, không lưu được AddListener.
        /// </summary>
        public void Wire()
        {
            Bind(this.button, TogglePanel);
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
        // Bảng chọn
        // ==================================================================

        public void TogglePanel()
        {
            if (this.panel.gameObject.activeSelf) { ClosePanel(); return; }
            if (this.host.Busy || !this.host.ItemsUsable) return;

            // Mở bảng cũng là bỏ ngắm: đang cầm búa mà mở bảng ra thì cú chạm kế tiếp là
            // chạm vào bảng, không phải chạm vào bàn.
            ClearPending();
            RefreshBar();
            this.catcher.gameObject.SetActive(true);
            this.panel.gameObject.SetActive(true);
            this.panel.SetAsLastSibling();
        }

        public void ClosePanel()
        {
            if (this.panel == null) return;
            this.panel.gameObject.SetActive(false);
            if (this.catcher != null) this.catcher.gameObject.SetActive(false);
        }

        /// <summary>Cập nhật nút mở bảng và ba dòng trong bảng theo số sao hiện có.</summary>
        public void RefreshBar()
        {
            if (this.button == null) return;

            bool show = this.host.ItemsUsable;
            this.button.gameObject.SetActive(show);
            if (!show) { ClosePanel(); return; }

            int balance = PuzzleProgress.StarsBalance(LevelCatalog.Levels.Length);
            this.balanceText.text = balance.ToString();
            this.button.interactable = !this.host.Busy;

            // Nút sáng lên khi đang cầm một món, để biết cú chạm tới sẽ làm gì.
            this.button.GetComponent<Image>().color =
                Aiming ? PuzzlePalette.PanelLight : PuzzlePalette.Panel;

            for (int i = 0; i < Order.Length && i < this.rows.Length; i++)
            {
                int cost = PuzzleSession.ItemCost(Order[i]);
                bool ok = balance >= cost;
                this.rows[i].interactable = ok;
                this.rowCosts[i].color = ok ? PuzzlePalette.Star : PuzzlePalette.Dim;
            }
            this.host.RefreshWallet();
        }

        /// <summary>Vẽ dòng số dư trong bảng. Chuỗi do host dựng để menu và bảng nói giống nhau.</summary>
        public void ShowWallet(string richText, int balance)
        {
            if (this.walletText == null) return;
            this.walletText.text = balance > 0 ? richText : "<color=#7C819E>Hết sao</color>";
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
    }
}
