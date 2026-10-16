using SonTinhThuyTinh.Dialogue;
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
        [SerializeField] float fogDark = 0.0054f;
        [SerializeField] float fogClear = 0.0030f;
        [SerializeField] float beaconDim = 0.6f;
        [SerializeField] float beaconBright = 30f;

        PlayerController player;
        float nextWarningAt;

        void Start()
        {
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
            RenderSettings.fogDensity = Mathf.MoveTowards(RenderSettings.fogDensity, lit ? fogClear : fogDark, step * 0.004f);
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
