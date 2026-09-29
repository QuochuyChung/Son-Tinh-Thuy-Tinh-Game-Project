using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Characters
{
    [CreateAssetMenu(menuName = "Son Tinh Thuy Tinh/Character Definition", fileName = "Character_")]
    public class CharacterDefinition : ScriptableObject
    {
        [SerializeField] CharacterId id;
        [SerializeField] string displayName;
        [SerializeField] string epithet;
        [TextArea(3, 6)]
        [SerializeField] string description;
        [SerializeField] PlayerController playerPrefab;
        [Tooltip("Scene loaded after picking this character: their own gift (sính lễ) branch map.")]
        [SerializeField] string giftBranchScene = SceneNames.Sandbox;

        public CharacterId Id => id;
        public string DisplayName => displayName;
        public string Epithet => epithet;
        public string Description => description;
        public PlayerController PlayerPrefab => playerPrefab;
        public string GiftBranchScene => giftBranchScene;
    }
}
