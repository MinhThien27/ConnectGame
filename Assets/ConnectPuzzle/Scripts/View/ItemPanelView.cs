using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Tham chiếu tới bảng chọn vật phẩm trong prefab.
    ///
    /// Ba dòng vật phẩm là MẢNG chứ không ba field riêng: thứ tự phải khớp
    /// PuzzleGame.ItemOrder, và một mảng nói rõ điều đó hơn ba field rời rạc.
    /// </summary>
    /// <remarks>Lớp chặn ItemCatcher ở ngoài prefab — xem chú thích ở DuelPanelView.</remarks>
    public sealed class ItemPanelView : MonoBehaviour
    {
        [SerializeField] private Text head;
        [SerializeField] private Text wallet;
        [SerializeField] private Button[] rows;
        [SerializeField] private Text[] costs;

        public Text Head => this.head;
        public Text Wallet => this.wallet;
        public Button[] Rows => this.rows;
        public Text[] Costs => this.costs;

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

        /// <summary>Thứ tự PHẢI khớp PuzzleGame.ItemOrder — bài kiểm chốt điều này.</summary>
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
