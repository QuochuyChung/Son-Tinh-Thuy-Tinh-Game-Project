using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    public enum ArenaBossType
    {
        ThuyTinh,   // Boss is Water Lord -> Phase 2: Water level rises, rain & waves intensify
        SonTinh     // Boss is Mountain Lord -> Phase 2: Giant rock pillars emerge from the ground
    }

    // Controls the Valley of the End final arena:
    // - Holds player and boss spawn anchors
    // - Handles Phase 2 environmental shifts (Rising Water vs Erupting Rock Pillars)
    public class FinalBattleArenaManager : MonoBehaviour
    {
        [Header("Spawn Points")]
        [SerializeField] Transform playerSpawn;
        [SerializeField] Transform bossSpawn;

        [Header("Phase 2 - Water Rising (vs Thủy Tinh)")]
        [SerializeField] Transform waterTransform;
        [SerializeField] float waterRiseHeight = 1.2f;
        [SerializeField] float waterRiseDuration = 3.5f;
        [SerializeField] GameObject waterStormFx;

        [Header("Phase 2 - Earth Eruption (vs Sơn Tinh)")]
        [SerializeField] List<Transform> rockPillars = new();
        [SerializeField] float rockEruptHeight = 4.0f;
        [SerializeField] float rockEruptDuration = 2.5f;
        [SerializeField] GameObject earthQuakeFx;

        [Header("Status")]
        [SerializeField] bool isPhase2Active;
        [SerializeField] ArenaBossType activeBossType = ArenaBossType.ThuyTinh;

        public Transform PlayerSpawn => playerSpawn;
        public Transform BossSpawn => bossSpawn;
        public bool IsPhase2Active => isPhase2Active;

        Vector3 initialWaterPos;
        readonly List<Vector3> initialPillarPos = new();
        Coroutine activeTransition;

        void Awake()
        {
            if (waterTransform != null)
                initialWaterPos = waterTransform.localPosition;

            if (rockPillars != null)
            {
                foreach (var pillar in rockPillars)
                {
                    if (pillar != null)
                        initialPillarPos.Add(pillar.localPosition);
                }
            }
        }

        public void TriggerPhase2(ArenaBossType bossType)
        {
            if (isPhase2Active) return;
            isPhase2Active = true;
            activeBossType = bossType;

            if (activeTransition != null)
                StopCoroutine(activeTransition);

            if (bossType == ArenaBossType.ThuyTinh)
                activeTransition = StartCoroutine(CoWaterRising());
            else
                activeTransition = StartCoroutine(CoRockEruption());
        }

        IEnumerator CoWaterRising()
        {
            if (waterStormFx != null) waterStormFx.SetActive(true);
            if (waterTransform == null) yield break;

            Vector3 start = waterTransform.localPosition;
            Vector3 target = initialWaterPos + Vector3.up * waterRiseHeight;
            float elapsed = 0f;

            while (elapsed < waterRiseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / waterRiseDuration);
                waterTransform.localPosition = Vector3.Lerp(start, target, t);
                yield return null;
            }

            waterTransform.localPosition = target;
        }

        IEnumerator CoRockEruption()
        {
            if (earthQuakeFx != null) earthQuakeFx.SetActive(true);
            if (rockPillars == null || rockPillars.Count == 0) yield break;

            float elapsed = 0f;
            while (elapsed < rockEruptDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / rockEruptDuration);

                for (int i = 0; i < rockPillars.Count; i++)
                {
                    if (rockPillars[i] == null) continue;
                    // Slightly stagger each pillar for dramatic earthquake effect
                    float delay = (i % 3) * 0.15f;
                    float delayedT = Mathf.Clamp01((elapsed - delay) / (rockEruptDuration - delay));
                    float eased = Mathf.SmoothStep(0f, 1f, delayedT);
                    rockPillars[i].localPosition = initialPillarPos[i] + Vector3.up * (rockEruptHeight * eased);
                }
                yield return null;
            }

            for (int i = 0; i < rockPillars.Count; i++)
            {
                if (rockPillars[i] != null)
                    rockPillars[i].localPosition = initialPillarPos[i] + Vector3.up * rockEruptHeight;
            }
        }

        [ContextMenu("Test Phase 2: Thủy Tinh (Nước dâng)")]
        public void TestPhase2Water()
        {
            TriggerPhase2(ArenaBossType.ThuyTinh);
        }

        [ContextMenu("Test Phase 2: Sơn Tinh (Cột đá trồi)")]
        public void TestPhase2Earth()
        {
            TriggerPhase2(ArenaBossType.SonTinh);
        }

        [ContextMenu("Reset Phase")]
        public void ResetPhase()
        {
            isPhase2Active = false;
            if (activeTransition != null) StopCoroutine(activeTransition);

            if (waterTransform != null)
                waterTransform.localPosition = initialWaterPos;
            if (waterStormFx != null)
                waterStormFx.SetActive(false);

            for (int i = 0; i < rockPillars.Count; i++)
            {
                if (rockPillars[i] != null && i < initialPillarPos.Count)
                    rockPillars[i].localPosition = initialPillarPos[i];
            }
            if (earthQuakeFx != null)
                earthQuakeFx.SetActive(false);
        }
    }
}
