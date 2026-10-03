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
        [Tooltip("Three short lines shown as the skill rows on the character select screen. Placeholders taken from the legend until the combat moves exist.")]
        [SerializeField] string[] signatureMoves;
        [Tooltip("Face shown in the gameplay HUD medallion. Rendered from the in-game model (Assets/Art/UI/Portraits).")]
        [SerializeField] Sprite portrait;
        [SerializeField] PlayerController playerPrefab;
        [Tooltip("Scene loaded after picking this character: their own gift (sính lễ) branch map.")]
        [SerializeField] string giftBranchScene = SceneNames.Sandbox;

        public CharacterId Id => id;
        public string DisplayName => displayName;
        public string Epithet => epithet;
        public string Description => description;
        public System.Collections.Generic.IReadOnlyList<string> SignatureMoves => signatureMoves;
        public Sprite Portrait => portrait;
        public PlayerController PlayerPrefab => playerPrefab;
        public string GiftBranchScene => giftBranchScene;
    }
}
