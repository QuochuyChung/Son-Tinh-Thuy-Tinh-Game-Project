using System.Collections.Generic;
using UnityEngine;

namespace SonTinhThuyTinh.Characters
{
    [CreateAssetMenu(menuName = "Son Tinh Thuy Tinh/Character Roster", fileName = "CharacterRoster")]
    public class CharacterRoster : ScriptableObject
    {
        [SerializeField] CharacterDefinition[] characters;

        public IReadOnlyList<CharacterDefinition> Characters => characters;

        public CharacterDefinition Get(CharacterId id)
        {
            foreach (CharacterDefinition character in characters)
                if (character.Id == id) return character;

            Debug.LogError($"{name} has no character with id {id}.", this);
            return null;
        }
    }
}
