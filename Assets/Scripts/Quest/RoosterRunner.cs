using System;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // Gà Chín Cựa after the thieves drop it (docs/ke-hoach-ga-chin-cua.md): it wanders the stone ring every which way — a new random spot
    // every second or so (Walk, its legs played at the speed it actually moves), stops to peck now and then (Eat). When the player comes
    // near it runs off zig-zagging, flapping as it runs (Fly: the Walk clip is a slow strut, a real rooster flaps when it runs), and when the
    // player gets close it flaps away a few metres, never leaving the ring. Each flap tires it: once it is spent it runs slower and pecks
    // longer, so a chase takes about 7 s. Standing next to it: "Nhấn E để bắt gà" (Interactable), E catches it: the gift and its banner.
    // While Held (in the leader's arms) it only flaps (Fly).
    [RequireComponent(typeof(Interactable))]
    public class RoosterRunner : MonoBehaviour
    {
        public enum Mode { Idle, Held, Free, Caught }

        [SerializeField] Animator animator;
        [SerializeField] GiftItem gift;
        [SerializeField] Transform arenaCenter;
        [SerializeField] float arenaRadius = 8.5f;
        [SerializeField] float walkSpeed = 0.28f;
        [Tooltip("Ground speed of the Walk clip played at normal speed (measured on the clip: ~0.29 m per 3 s cycle at this size).")]
        [SerializeField] float walkClipSpeed = 0.11f;
        [Tooltip("Running away from the player (spent: 60% of it).")]
        [SerializeField] float runSpeed = 3.2f;
        [SerializeField] float flySpeed = 5.5f;
        [Tooltip("Closer than this, it runs away.")]
        [SerializeField] float scareDistance = 4.5f;
        [Tooltip("Closer than this, it flaps away (if it has a flap left).")]
        [SerializeField] float fleeDistance = 2.4f;
        [Tooltip("Flaps before it is spent.")]
        [SerializeField] int stamina = 4;
        [Tooltip("Seconds to get one flap back.")]
        [SerializeField] float staminaRegen = 6f;

        Interactable interactable;
        Mode mode = Mode.Idle;
        PlayerController player;
        Vector3 target;
        float nextDecision, eatUntil, flyUntil, nextFlee, regenAt, nextZig;
        Vector3 flyDir, runDir, velocity;   // velocity: what it really moves at, eased towards what it wants (no instant turns or starts)
        Vector3 worldScale = Vector3.one;
        int left;
        string state;

        public Mode Current => mode;
        public event Action Caught;

        void Awake()
        {
            interactable = GetComponent<Interactable>();
            interactable.Prompt = "Nhấn E để bắt gà";
            interactable.Interacted.AddListener(Catch);
            interactable.enabled = false;
            worldScale = transform.lossyScale;
        }

        // the same size wherever it is parented (the leader's bones are scaled)
        void KeepSize()
        {
            Vector3 p = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(worldScale.x / p.x, worldScale.y / p.y, worldScale.z / p.z);
        }

        public void SetIdle(string clip = "Eat")
        {
            mode = Mode.Idle; interactable.enabled = false;
            Play(clip);
        }

        // in the leader's arms: parented to his chest socket, struggling
        public void Hold(Transform socket)
        {
            mode = Mode.Held; interactable.enabled = false;
            transform.SetParent(socket, false);
            transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity;
            KeepSize();
            transform.localScale *= 0.7f;   // smaller in his arms, so its flapping wings do not hide him
            Play("Fly");
        }

        // dropped: free in the ring
        public void Release(Transform parent)
        {
            Vector3 p = transform.position;
            transform.SetParent(parent, true);
            KeepSize();
            transform.position = Grounded(p);
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            mode = Mode.Free; interactable.enabled = true;
            left = stamina; regenAt = Time.time + staminaRegen;
            // first thing: a panicked flap away from the leader
            flyDir = Quaternion.Euler(0f, UnityEngine.Random.Range(-60f, 60f), 0f) * transform.forward;
            flyUntil = Time.time + 0.6f;
            Play("Fly");
        }

        void Update()
        {
            if (mode != Mode.Free) return;
            FindPlayer();
            float now = Time.time;
            if (left < stamina && now >= regenAt) { left++; regenAt = now + staminaRegen; }

            // flapping away
            if (now < flyUntil)
            {
                Move(flyDir * flySpeed, 0.35f * Mathf.Sin((flyUntil - now) / 0.7f * Mathf.PI));
                return;
            }

            Vector3 away = player != null ? Flat(transform.position - player.transform.position) : Vector3.zero;
            // the player gets close: flap off (if it still can)
            if (player != null && left > 0 && now >= nextFlee && away.magnitude < fleeDistance)
            {
                flyDir = Escape(away, true);
                flyUntil = now + UnityEngine.Random.Range(0.6f, 0.9f);
                nextFlee = now + (left > 1 ? 0.6f : 2f);
                left--;
                regenAt = now + staminaRegen;
                eatUntil = 0f;
                Play("Fly");
                return;
            }
            // the player is near: run away, zig-zagging (a new swerve every ~0.4 s)
            if (player != null && away.magnitude < scareDistance)
            {
                if (now >= nextZig || runDir == Vector3.zero)
                {
                    Vector3 escape = Escape(away, false);
                    runDir = Quaternion.Euler(0f, UnityEngine.Random.Range(-45f, 45f), 0f) * escape;
                    if (Vector3.Dot(runDir, away.normalized) < 0.1f) runDir = escape;   // a swerve never turns back towards the player
                    nextZig = now + UnityEngine.Random.Range(0.3f, 0.6f);
                }
                eatUntil = 0f;
                Play("Fly", 1.3f);   // flapping as it runs
                Move(runDir * runSpeed * (left == 0 ? 0.6f : 1f), 0f);
                return;
            }

            // pecking
            if (now < eatUntil) { velocity = Vector3.zero; Play("Eat"); return; }

            // a new spot every so often, anywhere in the ring (zig-zags, never circles)
            if (now >= nextDecision || Flat(target - transform.position).magnitude < 0.3f)
            {
                if (UnityEngine.Random.value < (left == 0 ? 0.55f : 0.25f))
                {
                    eatUntil = now + UnityEngine.Random.Range(1f, left == 0 ? 3.5f : 2f);
                    nextDecision = eatUntil;
                    Play("Eat");
                    return;
                }
                Vector2 r = UnityEngine.Random.insideUnitCircle * arenaRadius;
                target = arenaCenter.position + new Vector3(r.x, 0f, r.y);
                nextDecision = now + UnityEngine.Random.Range(1.2f, 2.8f);
            }
            Vector3 d = Flat(target - transform.position);
            float speed = walkSpeed * (left == 0 ? 0.6f : 1f);
            Move(d.normalized * speed, 0f);
            Play("Walk", Mathf.Clamp(velocity.magnitude / walkClipSpeed, 0.8f, 3f));   // the legs keep pace with the ground (eased speed)
        }

        // which way to get away from the player without being pinned against the stones: near the edge it runs along them, and a flap
        // there darts sideways past the player, back towards the middle of the ring
        Vector3 Escape(Vector3 away, bool flap)
        {
            Vector3 dir = away.sqrMagnitude > 0.0001f ? away.normalized : transform.forward;
            Vector3 fromCentre = Flat(transform.position - arenaCenter.position);
            float edge = arenaRadius - fromCentre.magnitude;
            if (fromCentre.sqrMagnitude < 0.0001f || edge > (flap ? 3f : 2.5f))
                return flap ? Quaternion.Euler(0f, UnityEngine.Random.Range(-70f, 70f), 0f) * dir : dir;
            Vector3 outward = fromCentre.normalized;
            Vector3 along = Vector3.Cross(Vector3.up, outward);
            float sign = Vector3.Dot(dir, along) >= 0f ? 1f : -1f;
            if (Mathf.Abs(Vector3.Dot(dir, along)) < 0.25f) sign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            if (flap)
            {
                // dart sideways past the player: across their line of approach, on the side that leads back into the ring
                Vector3 across = Vector3.Cross(Vector3.up, dir);
                if (Vector3.Dot(across, outward) > 0f) across = -across;
                return (across * 0.9f + dir * 0.35f - outward * 0.2f).normalized;
            }
            Vector3 slide = dir - Mathf.Max(0f, Vector3.Dot(dir, outward)) * outward;  // drop the part that runs into the stones
            if (slide.magnitude < 0.5f) slide = along * sign;                             // cornered: along the stones
            return slide.normalized;
        }

        void Move(Vector3 wanted, float lift)
        {
            // ease into the wanted speed and direction: a strut speeds up and turns gently, a scare reacts fast
            float accel = wanted.magnitude > walkSpeed * 1.5f ? 18f : 2.5f;
            velocity = Vector3.MoveTowards(velocity, wanted, accel * Time.deltaTime);
            Vector3 next = transform.position + velocity * Time.deltaTime;
            Vector3 off = Flat(next - arenaCenter.position);
            if (off.magnitude > arenaRadius)
            {
                next = arenaCenter.position + off.normalized * arenaRadius;
                flyDir = AlongEdge(flyDir, off.normalized);   // slide along the stones (a bounce sent it back into the player's arms)
                runDir = AlongEdge(runDir, off.normalized);
            }
            next = Grounded(next) + Vector3.up * lift;
            if (velocity.sqrMagnitude > 0.0025f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(Flat(velocity)),
                                                              (velocity.magnitude > walkSpeed * 1.5f ? 540f : 160f) * Time.deltaTime);
            transform.position = next;
        }

        static Vector3 AlongEdge(Vector3 v, Vector3 outward)
        {
            Vector3 t = v - Mathf.Max(0f, Vector3.Dot(v, outward)) * outward;
            return t.sqrMagnitude > 0.04f ? t.normalized : Vector3.Cross(Vector3.up, outward);
        }

        void Catch()
        {
            if (mode != Mode.Free) return;
            mode = Mode.Caught;
            interactable.enabled = false;
            GiftTracker.Collect(gift);   // "Bạn đã nhận được Gà chín cựa"
            Caught?.Invoke();
            gameObject.SetActive(false);
        }

        void Play(string s, float speed = 1f)
        {
            if (animator == null) return;
            animator.speed = speed;
            if (state == s) return;
            state = s;
            animator.CrossFadeInFixedTime(s, 0.15f);
        }

        void FindPlayer()
        {
            if (player != null && player.isActiveAndEnabled) return;
            player = null;
            foreach (PlayerController c in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (c.isActiveAndEnabled) { player = c; break; }
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        static Vector3 Grounded(Vector3 p)
        {
            Terrain t = Terrain.activeTerrain;
            if (t != null) p.y = t.SampleHeight(p) + t.transform.position.y;
            return p;
        }
    }
}
