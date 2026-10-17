using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Environment;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // The lighthouse lamp of Map_ThuyTinh and what it changes (docs/ke-hoach-thuy-tinh-su-kien.md):
    //  - `lamp` (E at the foot of the tower) works once the water ghosts are beaten; the conversation, then SeaQuest.LightLamp().
    //  - Until the lamp is lit the sea is thick with mist (fog) and a wall of mist (`mist`) shuts the entrance of Shark Bay; lit, the
    //    beacon blazes, the fog thins and the mist wall is gone.
    public class SeaLampBeacon : MonoBehaviour
    {
        [SerializeField] Interactable lamp;
        [SerializeField] Light beacon;
        [SerializeField] GameObject mist;
        [SerializeField] Transform mistCentre;
        [SerializeField] DialogueRunner runner;
        [SerializeField] DialogueSequence lighting;
        [Tooltip("Fog density before / after the lamp, as a multiple of the density the scene was authored with (per-scene environment pass).")]
        [SerializeField] float fogDarkScale = 1.3f;
        [SerializeField] float fogClearScale = 0.7f;
        [SerializeField] float beaconDim = 0.6f;
        [SerializeField] float beaconBright = 30f;

        PlayerController player;
        float nextWarningAt;
        float baseFog;
        FogController weather;      // the scene's own fog swell (zuyzz's Weather_Fog), when there is one: it writes the density every frame, so we steer it, not the density
        float weatherAmount = 1f;

        void Start()
        {
            weather = FindFirstObjectByType<FogController>(FindObjectsInactive.Include);
            baseFog = RenderSettings.fogDensity;   // what the scene authored; the lamp moves the fog around it (when there is no FogController)
            lamp.Interacted.AddListener(LightLamp);
            Apply(1f);
        }

        void Update()
        {
            lamp.enabled = SeaQuest.GhostsCleared && !SeaQuest.LampLit && !QuestDialogue.IsPlaying;
            Apply(Time.deltaTime * 0.5f);

            if (SeaQuest.LampLit || mist == null || !mist.activeSelf) return;
            if (player == null || !player.isActiveAndEnabled)
            {
                player = null;
                foreach (PlayerController c in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                    if (c.isActiveAndEnabled) { player = c; break; }
                if (player == null) return;
            }
            Vector3 d = player.transform.position - mistCentre.position; d.y = 0f;
            if (d.magnitude < 9f && Time.time >= nextWarningAt)
            {
                nextWarningAt = Time.time + 8f;
                GiftTracker.Announce("Sương dày chắn lối vào Vịnh Mập\nHải đăng đang tắt, hãy thắp đèn trước");
            }
        }

        // eases the fog, the beacon and the mist wall towards what the quest says
        void Apply(float step)
        {
            bool lit = SeaQuest.LampLit;
            if (weather != null)
            {
                // the swell reaches its peak while the lamp is out (thick) and is pinned at the calm value once it is lit (thin)
                weatherAmount = Mathf.MoveTowards(weatherAmount, lit ? 0f : 1f, step * 0.5f);
                weather.SetAmount(weatherAmount);
            }
            else RenderSettings.fogDensity = Mathf.MoveTowards(RenderSettings.fogDensity, baseFog * (lit ? fogClearScale : fogDarkScale), step * 0.004f);
            if (beacon != null) beacon.intensity = Mathf.MoveTowards(beacon.intensity, lit ? beaconBright : beaconDim, step * 40f);
            if (mist != null && mist.activeSelf == lit) mist.SetActive(!lit);
        }

        void LightLamp()
        {
            if (!SeaQuest.GhostsCleared || SeaQuest.LampLit) return;
            QuestDialogue.Play(runner, lighting, () =>
            {
                SeaQuest.LightLamp();
                GiftTracker.Announce("Hải đăng đã sáng!\nSương đang tan, lối vào Vịnh Mập đã mở");
            });
        }
    }
}
