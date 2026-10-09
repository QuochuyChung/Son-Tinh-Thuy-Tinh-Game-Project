using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // One collectible offered as a sính lễ. Most gifts are shared by both branches;
    // Thủy Tinh's first stop uses Mập 9 Vây instead of Ngựa chín hồng mao.
    [CreateAssetMenu(menuName = "Son Tinh Thuy Tinh/Gift Item", fileName = "Gift_")]
    public class GiftItem : ScriptableObject
    {
        [SerializeField] string displayName;
        [TextArea(2, 4)]
        [SerializeField] string description;
        [SerializeField] Sprite icon;

        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
    }
}
