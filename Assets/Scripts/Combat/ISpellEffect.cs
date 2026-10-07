using SonTinhThuyTinh.Player;

namespace SonTinhThuyTinh.Combat
{
    // Implemented by the scripts on spell effect prefabs; called right after the prefab is spawned by the caster.
    public interface ISpellEffect
    {
        void Init(PlayerController caster, SpellData spell);
    }
}
