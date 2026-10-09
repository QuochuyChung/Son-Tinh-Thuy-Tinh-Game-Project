using System.Collections;
using SonTinhThuyTinh.CameraSystem;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SonTinhThuyTinh.Combat.Boss
{
    public sealed class FinisherPrompt : MonoBehaviour
    {
        enum Phase
        {
            Idle,
            Kneel,
            Prompt,
            Cinematic,
            Done
        }

        [Tooltip("Giây boss quỳ (placeholder scale) trước khi hiện prompt kết liễu.")]
        [SerializeField, Min(0f)] float kneelSeconds = 4f;

        [Tooltip("Giây cinematic thắng (camera quay quanh boss + banner).")]
        [SerializeField, Min(0f)] float cinematicSeconds = 3f;

        [Tooltip("Tỉ lệ scale Y khi quỳ — placeholder cho clip quỳ 1 gối.")]
        [SerializeField, Range(0.1f, 1f)] float kneelSquash = 0.65f;

        [Tooltip("Độ tối tối đa của lớp fade khi cinematic.")]
        [SerializeField, Range(0f, 1f)] float cinematicFadeAlpha = 0.55f;

        [Tooltip("Độ/ngập camera quay quanh boss trong cinematic.")]
        [SerializeField] float orbitDegreesPerSecond = 30f;

        public static FinisherPrompt Instance { get; private set; }

        public bool IsRunning => phase != Phase.Idle && phase != Phase.Done;

        Phase phase = Phase.Idle;
        Transform boss;
        Vector3 baseScale;
        bool kneelHold;
        bool confirmed;
        float cinematicElapsed;
        GameObject camGo;
        CinemachineCamera finisherCam;
        CinemachineOrbitalFollow orbit;
        ThirdPersonCameraInput playerCamInput;
        GUIStyle titleStyle;
        GUIStyle hintStyle;
        GUIStyle bannerStyle;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (camGo != null) Destroy(camGo);
        }

        void Start()
        {
            BossHealth bossHealth = BossHealth.Instance;
            if (bossHealth == null)
            {
                Debug.LogError("[Finisher] Thiếu BossHealth — tắt FinisherPrompt");
                enabled = false;
                return;
            }
            boss = bossHealth.transform;
            baseScale = boss.localScale;
            playerCamInput = FindFirstObjectByType<ThirdPersonCameraInput>();
            bossHealth.Exhausted += OnExhausted;
        }

        void LateUpdate()
        {
            if (!kneelHold || boss == null) return;
            boss.localScale = new Vector3(baseScale.x, baseScale.y * kneelSquash, baseScale.z);
        }

        void OnGUI()
        {
            if (phase == Phase.Prompt) DrawPrompt();
            else if (phase == Phase.Cinematic) DrawCinematic();
        }

        void OnExhausted()
        {
            if (phase != Phase.Idle) return;
            if (boss == null) return;
            Debug.Log("[Finisher] Boss cạn kiệt — bắt đầu nghi thức kết liễu");
            StartCoroutine(RunFinisher());
        }

        public void ConfirmFinisher()
        {
            if (phase == Phase.Prompt) confirmed = true;
        }

        IEnumerator RunFinisher()
        {
            phase = Phase.Kneel;
            kneelHold = true;
            yield return new WaitForSeconds(kneelSeconds);

            phase = Phase.Prompt;
            if (playerCamInput != null) playerCamInput.enabled = false;
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            Debug.Log("[Finisher] Hiện prompt kết liễu");

            confirmed = false;
            while (!confirmed)
            {
                Keyboard kb = Keyboard.current;
                if (kb != null && (kb.nKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                    confirmed = true;
                yield return null;
            }

            Debug.Log("[Finisher] Xác nhận kết liễu — chạy cinematic thắng");
            phase = Phase.Cinematic;
            cinematicElapsed = 0f;
            SpawnCinematicCamera();
            Cursor.visible = false;

            while (cinematicElapsed < cinematicSeconds)
            {
                cinematicElapsed += Time.deltaTime;
                if (orbit != null)
                    orbit.HorizontalAxis.Value = Mathf.Repeat(orbit.HorizontalAxis.Value + (orbitDegreesPerSecond * Time.deltaTime) + 180f, 360f) - 180f;
                yield return null;
            }

            DespawnCinematicCamera();
            phase = Phase.Done;
            if (playerCamInput != null) playerCamInput.enabled = true;
            Debug.Log("[Finisher] Cinematic xong — kết trận Won");
            if (BattleFlow.Instance != null)
                BattleFlow.Instance.CompleteFinisher("cinematic kết liễu — " + boss.name + " cạn kiệt");
        }

        void SpawnCinematicCamera()
        {
            if (camGo != null || boss == null) return;
            camGo = new GameObject("FinisherCamera");
            finisherCam = camGo.AddComponent<CinemachineCamera>();
            orbit = camGo.AddComponent<CinemachineOrbitalFollow>();
            camGo.AddComponent<CinemachineRotationComposer>();
            finisherCam.Priority.Value = 100;
            finisherCam.Follow = boss;
            finisherCam.LookAt = boss;
            Debug.Log("[Finisher] Tạo cinematic camera (priority 100)");
        }

        void DespawnCinematicCamera()
        {
            if (camGo != null) Destroy(camGo);
            camGo = null;
            finisherCam = null;
            orbit = null;
        }

        void DrawPrompt()
        {
            EnsureStyles();
            float w = Mathf.Min(540f, Screen.width - 40f);
            Rect area = new Rect((Screen.width - w) * 0.5f, Screen.height - 210f, w, 160f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Space(8f);
            GUILayout.Label(boss != null ? boss.name + " cạn kiệt — quỳ một gối, không còn sức phản kháng." : "Boss cạn kiệt.", titleStyle);
            GUILayout.Space(8f);
            if (GUILayout.Button("Kết liễu  (N / Enter / Space / Click)", GUILayout.Height(42f)))
                ConfirmFinisher();
            GUILayout.Space(6f);
            GUILayout.Label("Cinematic thắng chạy ngay sau khi kết liễu.", hintStyle);
            GUILayout.EndArea();
        }

        void DrawCinematic()
        {
            EnsureStyles();
            float dim = cinematicSeconds > 0f
                ? Mathf.Lerp(0f, cinematicFadeAlpha, Mathf.Clamp01(cinematicElapsed / cinematicSeconds))
                : cinematicFadeAlpha;
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, dim);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.blackTexture);
            GUI.color = prev;

            float w = Mathf.Min(900f, Screen.width - 40f);
            Rect area = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.62f, w, 130f);
            GUILayout.BeginArea(area);
            GUILayout.Label("THẮNG", bannerStyle);
            GUILayout.Label((boss != null ? boss.name : "Boss") + " cạn kiệt · quân thua rút lui, mất Mị Nương", titleStyle);
            GUILayout.EndArea();
        }

        void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 46,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }
    }
}
