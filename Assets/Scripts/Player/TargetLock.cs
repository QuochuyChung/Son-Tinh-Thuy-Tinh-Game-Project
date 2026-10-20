using System.Collections.Generic;
using SonTinhThuyTinh.Combat;
using UnityEngine;

namespace SonTinhThuyTinh.Player
{
    // Target lock (` key): only in a fight, i.e. while an enemy (anything with Health but the player) is alive within Range. The first press
    // locks on to the enemy nearest the middle of the screen, a second press lets go. When the locked enemy dies (or gets far away) the lock
    // jumps to the nearest one left, and turns itself off when there is none. A blue dot floats over the locked enemy. Attacks and spells turn
    // the character towards it (PlayerController.FaceAim), so they go where they should; moving and the camera stay free.
    // Added by PlayerController at start; does nothing while an AI drives the character.
    public sealed class TargetLock : MonoBehaviour
    {
        public const float Range = 30f;
        const float LoseRange = 40f;
        const float ScanInterval = 0.25f;

        PlayerController player;
        Health target;
        float targetTop;
        float nextScanAt;
        readonly List<Health> candidates = new();
        Transform marker;
        SpriteRenderer markerSprite;

        public Health Target => target;

        void Awake() => player = GetComponent<PlayerController>();

        void OnDisable() => Release();

        void OnDestroy()
        {
            if (marker != null) Destroy(marker.gameObject);
        }

        void Update()
        {
            PlayerInputReader input = player.InputReader;
            if (input == null || input.AiActive || player.Health == null || player.Health.IsDead)
            {
                Release();
                return;
            }

            if (Time.time >= nextScanAt) Scan();

            if (input.ConsumeLockOn())
            {
                if (target != null) Release();
                else Lock(PickNearestToView());
            }

            // the locked one died, vanished or is far away: on to the nearest one left (none: the lock turns off)
            if (target != null && !Valid(target, LoseRange))
            {
                Scan();
                Lock(PickNearest());
            }
        }

        void LateUpdate()
        {
            if (target == null)
            {
                if (marker != null && marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
                return;
            }
            EnsureMarker();
            if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
            float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 7f);
            marker.position = target.transform.position + Vector3.up * (targetTop + 0.45f + 0.05f * Mathf.Sin(Time.time * 3f));
            marker.localScale = Vector3.one * (0.4f * pulse);
            Camera cam = Camera.main;
            if (cam != null) marker.rotation = cam.transform.rotation;
        }

        public void Release() => Lock(null);

        void Lock(Health next)
        {
            target = next;
            if (target != null) targetTop = TopOf(target);
        }

        // Everything with Health that is alive, not the player and within Range.
        void Scan()
        {
            nextScanAt = Time.time + ScanInterval;
            candidates.Clear();
            foreach (Health h in FindObjectsByType<Health>(FindObjectsSortMode.None))
                if (Valid(h, Range)) candidates.Add(h);
        }

        bool Valid(Health h, float range)
        {
            if (h == null || h == player.Health || h.IsDead || !h.isActiveAndEnabled) return false;
            if (h.transform.IsChildOf(transform)) return false;
            return (h.transform.position - transform.position).sqrMagnitude <= range * range;
        }

        // The press: the one closest to where the camera looks, nearer ones a little preferred.
        Health PickNearestToView()
        {
            Camera cam = Camera.main;
            Vector3 view = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : transform.forward;
            Health best = null;
            float bestScore = float.MaxValue;
            foreach (Health h in candidates)
            {
                if (!Valid(h, Range)) continue;
                Vector3 to = h.transform.position - transform.position;
                to.y = 0f;
                float angle = to.sqrMagnitude > 0.01f ? Vector3.Angle(view, to) : 0f;
                float score = angle / 90f + to.magnitude / Range;
                if (score < bestScore) { bestScore = score; best = h; }
            }
            return best;
        }

        Health PickNearest()
        {
            Health best = null;
            float bestDistance = float.MaxValue;
            foreach (Health h in candidates)
            {
                if (!Valid(h, Range)) continue;
                float d = (h.transform.position - transform.position).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = h; }
            }
            return best;
        }

        // Height of the top of the enemy above its pivot (from its collider, else its meshes).
        static float TopOf(Health h)
        {
            Transform root = h.transform;
            var cc = h.GetComponentInParent<CharacterController>();
            if (cc != null) return cc.bounds.max.y - root.position.y;
            bool any = false;
            Bounds b = default;
            foreach (Renderer r in h.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (any) return b.max.y - root.position.y;
            var col = h.GetComponentInChildren<Collider>();
            return col != null ? col.bounds.max.y - root.position.y : 2f;
        }

        void EnsureMarker()
        {
            if (marker != null) return;
            var go = new GameObject("Target Lock Marker");
            marker = go.transform;
            markerSprite = go.AddComponent<SpriteRenderer>();
            markerSprite.sprite = DotSprite();
            markerSprite.sortingOrder = 500;
        }

        static Sprite dot;

        // A blue dot with a white rim and a soft glow, drawn once.
        static Sprite DotSprite()
        {
            if (dot != null) return dot;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var core = new Color(0.15f, 0.55f, 1f, 1f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                    Color c;
                    if (r < 0.48f) c = Color.Lerp(new Color(0.55f, 0.85f, 1f, 1f), core, r / 0.48f);
                    else if (r < 0.62f) c = Color.white;
                    else c = new Color(0.2f, 0.6f, 1f, Mathf.Clamp01((1f - r) / 0.38f) * 0.55f);
                    pixels[y * size + x] = c;
                }
            tex.SetPixels(pixels);
            tex.Apply();
            dot = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return dot;
        }
    }
}
