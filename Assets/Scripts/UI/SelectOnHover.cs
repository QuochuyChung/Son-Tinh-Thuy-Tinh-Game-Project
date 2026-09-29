using UnityEngine;
using UnityEngine.EventSystems;

namespace SonTinhThuyTinh.UI
{
    // Mouse hover moves the UI selection, so mouse and keyboard/gamepad always agree on what is highlighted.
    public class SelectOnHover : MonoBehaviour, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData) => EventSystem.current.SetSelectedGameObject(gameObject);
    }
}
