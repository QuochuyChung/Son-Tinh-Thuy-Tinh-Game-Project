using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // One sính lễ (voi chín ngà, gà chín cựa, ngựa chín hồng mao). Both characters' branches use the same assets.
    [CreateAssetMenu(menuName = "Son Tinh Thuy Tinh/Gift Item", fileName = "Gift_")]
    public class GiftItem : ScriptableObject
    {
        [SerializeField] string displayName;
        [TextArea(2, 4)]
        [SerializeField] string description;
        [SerializeField] Sprite icon;
        [Tooltip("Optional banner text when the gift is received, {0} = display name. Empty: the default \"Đã có ...\".")]
        [SerializeField] string receivedMessage;

        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public string ReceivedMessage => receivedMessage;
    }
}
