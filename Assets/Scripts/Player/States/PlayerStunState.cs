using SonTinhThuyTinh.Core;
using UnityEngine;

namespace SonTinhThuyTinh.Player.States
{
    // Stunned (Sơn Tinh's rock golems): pushed back, the stagger clip stretched over the whole stun and stars circling the head; no input
    // until it is over. Hits still land meanwhile (unlike the hit reaction, it gives no invulnerability). Started by PlayerController.Stun.
    public sealed class PlayerStunState : IState
    {
        static readonly int HitHash = Animator.StringToHash("Hit");

        readonly PlayerController player;
        float duration;
        float elapsed;
        Vector3 push;
        GameObject stars;

        public PlayerStunState(PlayerController player) => this.player = player;

        // push: horizontal speed (m/s) at the start, fading out in the first 0.5 s (14 m/s: thrown about 3.5 m).
        public void Begin(float seconds, Vector3 pushVelocity)
        {
            duration = Mathf.Max(0.1f, seconds);
            push = Vector3.ProjectOnPlane(pushVelocity, Vector3.up);
        }

        public void Enter()
        {
            elapsed = 0f;
            player.SetSpeed(0f);
            player.SetTrail(false);
            player.IsInvulnerable = false;
            player.InputReader.ClearBuffered();
            var set = player.MoveSet;
            float clip = set != null ? set.hitHeavyDuration * set.hitHeavySpeed : 1f;   // length of the stagger at normal speed
            player.SetAnimSpeed(Mathf.Clamp(clip / duration, 0.35f, 2f));
            player.Animator.CrossFadeInFixedTime(HitHash, 0.04f);
            stars = StunStars.Spawn(player.transform, player.CameraTarget, duration);
        }

        public void Tick(float deltaTime)
        {
            elapsed += deltaTime;
            float fade = Mathf.Clamp01(1f - elapsed / 0.5f);
            player.Move(push * (fade * deltaTime), deltaTime);
            if (elapsed >= duration) player.ChangeState(player.LocomotionState);
        }

        public void Exit()
        {
            player.SetAnimSpeed(1f);
            player.InputReader.ClearBuffered();
            if (stars != null) Object.Destroy(stars);
        }
    }

    // Little yellow stars going round over a stunned head.
    public sealed class StunStars : MonoBehaviour
    {
        Transform owner;
        Transform head;
        Transform[] stars;
        float age;

        public static GameObject Spawn(Transform owner, Transform head, float seconds)
        {
            var go = new GameObject("Stun Stars");
            var s = go.AddComponent<StunStars>();
            s.owner = owner;
            s.head = head;
            s.stars = new Transform[4];
            for (int i = 0; i < s.stars.Length; i++)
            {
                var star = new GameObject("Star");
                star.transform.SetParent(go.transform, false);
                var sr = star.AddComponent<SpriteRenderer>();
                sr.sprite = StarSprite();
                sr.color = i % 2 == 0 ? new Color(1f, 0.93f, 0.35f) : new Color(1f, 1f, 0.75f);
                sr.sortingOrder = 400;
                s.stars[i] = star.transform;
            }
            Destroy(go, seconds + 0.1f);
            return go;
        }

        void LateUpdate()
        {
            if (owner == null) { Destroy(gameObject); return; }
            age += Time.deltaTime;
            Vector3 centre = head != null ? head.position + Vector3.up * 0.55f : owner.position + Vector3.up * 2.3f;
            transform.position = centre;
            Camera cam = Camera.main;
            for (int i = 0; i < stars.Length; i++)
            {
                float a = age * 6f + i * Mathf.PI * 2f / stars.Length;
                stars[i].position = centre + new Vector3(Mathf.Cos(a) * 0.38f, Mathf.Sin(a * 2f) * 0.05f, Mathf.Sin(a) * 0.38f);
                stars[i].localScale = Vector3.one * (0.16f + 0.03f * Mathf.Sin(age * 10f + i));
                if (cam != null) stars[i].rotation = cam.transform.rotation;
            }
        }

        static Sprite star;

        // A five-pointed star, drawn once.
        static Sprite StarSprite()
        {
            if (star != null) return star;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f) / (size * 0.5f);
                    float angle = Mathf.Atan2(p.y, p.x) + Mathf.PI / 2f;
                    float spike = Mathf.Abs(Mathf.Repeat(angle * 5f / (2f * Mathf.PI), 1f) - 0.5f) * 2f;   // 1 at a point, 0 between
                    float edge = Mathf.Lerp(0.42f, 0.98f, spike * spike);
                    float alpha = Mathf.Clamp01((edge - p.magnitude) * 14f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            tex.SetPixels(pixels);
            tex.Apply();
            star = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return star;
        }
    }
}
