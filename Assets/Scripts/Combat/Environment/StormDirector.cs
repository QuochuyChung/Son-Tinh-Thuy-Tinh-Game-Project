using Unity.Cinemachine;
using UnityEngine;
using SonTinhThuyTinh.Combat.Boss;
using SonTinhThuyTinh.Player;

namespace SonTinhThuyTinh.Combat.Environment
{
    [DisallowMultipleComponent]
    public sealed class StormDirector : MonoBehaviour
    {
        [Header("Khe sam (chi P3)")]
        [SerializeField] private float strikeInterval = 4f;
        [SerializeField] private float telegraphSeconds = 0.8f;
        [SerializeField] private float strikeRadius = 2.6f;
        [SerializeField] private float strikeDamage = 18f;
        [SerializeField] private float strikeStunSeconds = 0.75f;
        [SerializeField] private float strikeTrauma = 0.65f;
        [SerializeField] private float strikeOffsetRadius = 1.5f;
        [SerializeField] private float arenaClamp = 12f;

        [Header("Mua (P2 nhe -> P3 lien tuc)")]
        [SerializeField] private float rainRateP2 = 45f;
        [SerializeField] private float rainRateP3 = 150f;
        [SerializeField] private float rainRampPerSecond = 40f;

        [Header("Screen shake")]
        [SerializeField] private float ambientTrauma = 0.25f;
        [SerializeField] private float ambientInterval = 2f;
        [SerializeField] private float traumaDecayPerSecond = 1f;

        private BattleFlow flow;
        private BossHealth boss;
        private PlayerHealth player;

        private ParticleSystem rain;
        private float rainRate;

        private bool wasFiring;
        private float nextStrikeAt;
        private float nextAmbientAt;
        private bool telegraphing;
        private float telegraphEndsAt;
        private Vector3 telegraphPos;
        private GameObject telegraphRing;

        private float trauma;
        private CinemachineBasicMultiChannelPerlin perlin;
        private NoiseSettings noiseProfile;

        private Material rainMat;
        private Material ringMat;
        private Material flashMat;

        private void Start()
        {
            flow = BattleFlow.Instance;
            boss = BossHealth.Instance;
            player = FindFirstObjectByType<PlayerHealth>();
            BuildRain();
        }

        private void Update()
        {
            if (flow == null) flow = BattleFlow.Instance;
            if (boss == null) boss = BossHealth.Instance;
            if (player == null) player = FindFirstObjectByType<PlayerHealth>();

            float dt = Time.deltaTime;
            bool fighting = flow != null && flow.State == BattleState.Fighting;
            int seg = boss != null ? boss.CurrentSegment : 1;
            bool p2 = fighting && seg >= 2;
            bool p3 = fighting && seg >= 3;

            UpdateRain(p2, seg, dt);

            if (p3)
            {
                if (!wasFiring)
                {
                    wasFiring = true;
                    nextStrikeAt = Time.time + strikeInterval - telegraphSeconds;
                    nextAmbientAt = Time.time + 1f;
                }

                if (!telegraphing && Time.time >= nextStrikeAt) BeginTelegraph();

                if (telegraphing)
                {
                    if (telegraphRing != null)
                    {
                        float t = 1f - Mathf.Clamp01((telegraphEndsAt - Time.time) / telegraphSeconds);
                        float r = Mathf.Lerp(strikeRadius * 0.4f, strikeRadius, t);
                        telegraphRing.transform.localScale = new Vector3(r * 2f, 0.02f, r * 2f);
                    }
                    if (Time.time >= telegraphEndsAt) Strike();
                }

                if (Time.time >= nextAmbientAt)
                {
                    AddTrauma(ambientTrauma);
                    nextAmbientAt = Time.time + ambientInterval;
                }
            }
            else if (wasFiring)
            {
                wasFiring = false;
                if (telegraphRing != null) Destroy(telegraphRing);
                telegraphRing = null;
                telegraphing = false;
            }

            UpdateShake(dt);
        }

        private void UpdateRain(bool on, int seg, float dt)
        {
            float target = 0f;
            if (on) target = seg >= 3 ? rainRateP3 : rainRateP2;
            rainRate = Mathf.MoveTowards(rainRate, target, rainRampPerSecond * dt);
            if (rain == null) return;

            var em = rain.emission;
            em.rateOverTime = rainRate;
            if (rainRate > 0.05f)
            {
                if (!rain.isPlaying) rain.Play();
            }
            else if (rain.particleCount == 0 && rain.isPlaying)
            {
                rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void BeginTelegraph()
        {
            Transform anchor = null;
            if (player != null && boss != null)
                anchor = Random.value < 0.5f ? player.transform : boss.transform;
            else if (player != null) anchor = player.transform;
            else if (boss != null) anchor = boss.transform;
            if (anchor == null)
            {
                nextStrikeAt = Time.time + strikeInterval - telegraphSeconds;
                return;
            }

            Vector2 off = Random.insideUnitCircle * strikeOffsetRadius;
            telegraphPos = new Vector3(
                Mathf.Clamp(anchor.position.x + off.x, -arenaClamp, arenaClamp),
                anchor.position.y,
                Mathf.Clamp(anchor.position.z + off.y, -arenaClamp, arenaClamp));

            telegraphRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Collider col = telegraphRing.GetComponent<Collider>();
            if (col != null) Destroy(col);
            telegraphRing.name = "KheSam_Telegraph";
            telegraphRing.transform.SetParent(transform, false);
            telegraphRing.transform.position = telegraphPos;
            telegraphRing.transform.localScale = new Vector3(strikeRadius * 0.8f, 0.02f, strikeRadius * 0.8f);

            if (ringMat == null)
            {
                ringMat = new Material(Shader.Find("HDRP/Unlit"));
                if (ringMat.HasProperty("_BaseColor"))
                    ringMat.SetColor("_BaseColor", new Color(1f, 0.4f, 0.05f));
                if (ringMat.HasProperty("_Color"))
                    ringMat.SetColor("_Color", new Color(1f, 0.4f, 0.05f));
            }
            Renderer r = telegraphRing.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = ringMat;

            telegraphing = true;
            telegraphEndsAt = Time.time + telegraphSeconds;
        }

        private void Strike()
        {
            var hit = new AttackHit
            {
                damage = strikeDamage,
                isHeavy = false,
                causesKnockdown = false,
                stunSeconds = strikeStunSeconds,
                origin = telegraphPos + Vector3.up * 0.5f,
                direction = Vector3.up,
                source = null
            };
            int hits = AttackResolver.Area(in hit, strikeRadius);
            Debug.Log($"[Storm] KheSam hits={hits} @({telegraphPos.x:F1},{telegraphPos.z:F1}) t={Time.time:F3}");
            AddTrauma(strikeTrauma);

            if (telegraphRing != null)
            {
                Renderer r = telegraphRing.GetComponent<Renderer>();
                if (r != null)
                {
                    if (flashMat == null)
                    {
                        flashMat = new Material(ringMat);
                        if (flashMat.HasProperty("_BaseColor"))
                            flashMat.SetColor("_BaseColor", Color.yellow);
                        if (flashMat.HasProperty("_Color"))
                            flashMat.SetColor("_Color", Color.yellow);
                    }
                    r.sharedMaterial = flashMat;
                }
                Destroy(telegraphRing, 0.12f);
                telegraphRing = null;
            }

            telegraphing = false;
            nextStrikeAt = Time.time + strikeInterval - telegraphSeconds;
        }

        private void AddTrauma(float amount)
        {
            trauma = Mathf.Clamp01(trauma + amount);
            EnsurePerlin();
        }

        private void UpdateShake(float dt)
        {
            trauma = Mathf.Max(0f, trauma - traumaDecayPerSecond * dt);
            if (perlin != null)
                perlin.AmplitudeGain = trauma * trauma;
        }

        private void EnsurePerlin()
        {
            if (perlin != null && perlin.isActiveAndEnabled) return;
            perlin = null;

            CinemachineCamera best = null;
            int bestPriority = int.MinValue;
            CinemachineCamera[] cams =
                FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < cams.Length; i++)
            {
                if (!cams[i].isActiveAndEnabled) continue;
                int p = cams[i].Priority.Value;
                if (p > bestPriority)
                {
                    bestPriority = p;
                    best = cams[i];
                }
            }
            if (best == null) return;

            perlin = best.GetComponent<CinemachineBasicMultiChannelPerlin>();
            if (perlin == null) perlin = best.gameObject.AddComponent<CinemachineBasicMultiChannelPerlin>();
            if (noiseProfile == null) noiseProfile = BuildNoiseProfile();
            perlin.NoiseProfile = noiseProfile;
            perlin.AmplitudeGain = 0f;
            perlin.FrequencyGain = 1f;
        }

        private static NoiseSettings BuildNoiseProfile()
        {
            var profile = ScriptableObject.CreateInstance<NoiseSettings>();
            profile.PositionNoise = new[]
            {
                new NoiseSettings.TransformNoiseParams
                {
                    X = new NoiseSettings.NoiseParams { Frequency = 1.1f, Amplitude = 1f },
                    Y = new NoiseSettings.NoiseParams { Frequency = 1.3f, Amplitude = 1f },
                    Z = new NoiseSettings.NoiseParams { Frequency = 0.9f, Amplitude = 1f }
                }
            };
            profile.OrientationNoise = new[]
            {
                new NoiseSettings.TransformNoiseParams
                {
                    X = new NoiseSettings.NoiseParams { Frequency = 0.8f, Amplitude = 2f },
                    Y = new NoiseSettings.NoiseParams { Frequency = 1f, Amplitude = 2f },
                    Z = new NoiseSettings.NoiseParams { Frequency = 0.7f, Amplitude = 2f }
                }
            };
            return profile;
        }

        private void BuildRain()
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, 14f, 0f);

            rain = go.AddComponent<ParticleSystem>();
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = rain.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 2f;
            main.startLifetime = 1.5f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;

            var shape = rain.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(28f, 2f, 28f);

            var vel = rain.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.y = -14f;

            var em = rain.emission;
            em.rateOverTime = 0f;

            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Stretch;
            pr.velocityScale = 0.05f;
            pr.lengthScale = 1.5f;
            rainMat = new Material(Shader.Find("HDRP/Unlit"));
            if (rainMat.HasProperty("_BaseColor"))
                rainMat.SetColor("_BaseColor", new Color(0.55f, 0.65f, 0.9f));
            if (rainMat.HasProperty("_Color"))
                rainMat.SetColor("_Color", new Color(0.55f, 0.65f, 0.9f));
            pr.sharedMaterial = rainMat;
        }

        private void OnDestroy()
        {
            if (perlin != null) perlin.AmplitudeGain = 0f;
            if (rainMat != null) Destroy(rainMat);
            if (ringMat != null) Destroy(ringMat);
            if (flashMat != null) Destroy(flashMat);
            if (noiseProfile != null) Destroy(noiseProfile);
        }

        public string Status =>
            $"seg={(boss != null ? boss.CurrentSegment : -1)} " +
            $"state={(flow != null ? flow.State.ToString() : "-")} " +
            $"rainRate={rainRate:F0} " +
            $"particles={(rain != null ? rain.particleCount : 0)} " +
            $"trauma={trauma:F2} telegraph={telegraphing}";
    }
}
