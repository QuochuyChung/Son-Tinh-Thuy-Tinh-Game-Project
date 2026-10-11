using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Audio
{
    // The sounds of a character (added to every PlayerController by AudioManager): footsteps by distance walked (grass, stone or wet ground
    // by the map), jump / landing, dodge, slide, climb, and the swing of every attack and spell. A rival that BossAI drives makes the same
    // sounds, 3D at its position; the player's own are flat in the head.
    [DisallowMultipleComponent]
    public class PlayerAudio : MonoBehaviour
    {
        PlayerController pc;
        BossAI ai;
        Vector3 last;
        float stride, airTime;
        bool wasGrounded = true, thuyTinh;
        string lastState;

        void Awake()
        {
            pc = GetComponent<PlayerController>();
            ai = GetComponent<BossAI>();
            thuyTinh = name.Contains("ThuyTinh");
            last = transform.position;
            if (pc != null) pc.AttackStarted += OnAttack;
        }

        void OnDestroy()
        {
            if (pc != null) pc.AttackStarted -= OnAttack;
        }

        void Emit(Sfx id, float volume = 1f)
        {
            if (ai != null) GameAudio.Play(id, transform.position, volume);
            else GameAudio.Play(id, volume);
        }

        void OnAttack(AttackData data, int comboIndex)
        {
            if (data is SpellData)
            {
                int index = SpellIndex(data);
                Emit((Sfx)((int)(thuyTinh ? Sfx.WaterSpell0 : Sfx.EarthSpell0) + index));
                Emit(thuyTinh ? Sfx.ThuyShoutSpell : Sfx.SonShoutSpell, 0.9f);   // the battle cry
                GameAudio.Combat();
                return;
            }
            Emit(data.heavy ? Sfx.SwingHeavy : Sfx.SwingLight, data.heavy ? 1f : 0.85f);
            if (data.heavy) Emit(thuyTinh ? Sfx.ThuyShoutHeavy : Sfx.SonShoutHeavy, 0.9f);
        }

        int SpellIndex(AttackData data)
        {
            SpellData[] spells = pc.MoveSet != null ? pc.MoveSet.spells : null;
            if (spells != null)
                for (int i = 0; i < spells.Length && i < 3; i++)
                    if (spells[i] == data) return i;
            return 0;
        }

        void Update()
        {
            if (pc == null || pc.Health == null) return;
            Vector3 now = transform.position;
            Vector3 d = now - last;
            last = now;
            if (pc.Health.IsDead) return;

            float flat = new Vector2(d.x, d.z).magnitude;
            bool grounded = pc.IsGrounded;
            if (flat > 3f) { stride = 0f; wasGrounded = grounded; return; }   // teleported (a respawn, a cutscene mark)

            string state = pc.CurrentStateName;
            if (grounded)
            {
                if (!wasGrounded && airTime > 0.3f) Emit(Sfx.Land);
                airTime = 0f;
                if (state == "PlayerLocomotionState" && pc.CurrentSpeed > 0.8f)
                {
                    stride += flat;
                    float length = pc.IsSprinting ? 2.5f : 1.9f;
                    if (stride >= length) { stride -= length; Emit(Footstep(), 0.7f); }
                }
            }
            else airTime += Time.deltaTime;

            if (state != lastState)
            {
                switch (state)
                {
                    case "PlayerDodgeState": Emit(Sfx.Dodge); break;
                    case "PlayerSlideState": Emit(Sfx.Slide); break;
                    case "PlayerJumpState": Emit(Sfx.Jump); break;
                    case "PlayerClimbState": Emit(Sfx.Climb); break;
                }
                lastState = state;
            }
            wasGrounded = grounded;
        }

        // the ground: wet on the marsh, stone on the palace plateau and in the last arena, grass elsewhere
        Sfx Footstep()
        {
            switch (gameObject.scene.name)
            {
                case "Map_ThuyTinh": return Sfx.FootstepWet;
                case "Map_HungVuong": case "Map_FinalBattle": case "Cutscene_PhanXu": return Sfx.FootstepStone;
                default: return Sfx.FootstepGrass;
            }
        }
    }
}
