using UnityEngine;
using UnityEngine.EventSystems;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Nhận kéo chuỗi qua EventSystem thay vì đọc Input trực tiếp — chạy được với cả
    /// input backend cũ và mới, không phụ thuộc cấu hình project.
    ///
    /// Ở FILE RIÊNG đúng tên lớp, bắt buộc: Unity chỉ nối được MonoBehaviour với script
    /// khi tên file khớp tên lớp. Nằm chung trong EffectLayer.cs thì prefab lưu ra với
    /// m_Script: {fileID: 0} — ô "Script" trống, và vùng nhận chạm của bàn chết câm.
    /// </summary>
    public sealed class BoardPointerInput : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public System.Action<Vector3> PointerDown;
        public System.Action<Vector3> PointerDrag;
        public System.Action PointerUp;

        private Camera uiCamera;

        public void Configure(Camera camera) { this.uiCamera = camera; }

        private Vector3 ToWorld(PointerEventData data)
        {
            var rect = (RectTransform)this.transform;
            RectTransformUtility.ScreenPointToWorldPointInRectangle(rect, data.position, this.uiCamera, out Vector3 world);
            return world;
        }

        public void OnPointerDown(PointerEventData data) => this.PointerDown?.Invoke(ToWorld(data));
        public void OnDrag(PointerEventData data) => this.PointerDrag?.Invoke(ToWorld(data));
        public void OnPointerUp(PointerEventData data) => this.PointerUp?.Invoke();
    }
}
