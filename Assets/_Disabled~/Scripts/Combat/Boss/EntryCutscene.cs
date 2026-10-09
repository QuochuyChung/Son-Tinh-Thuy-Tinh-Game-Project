using System.Collections;
using SonTinhThuyTinh.CameraSystem;
using SonTinhThuyTinh.Player;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SonTinhThuyTinh.Combat.Boss
{
    [DisallowMultipleComponent]
    public sealed class EntryCutscene : MonoBehaviour
    {
        public static EntryCutscene Instance { get; private set; }

        public static bool IntroActive { get; private set; }

        [Header("Tham chiếu")]
        [Tooltip("Người chơi (Player_ThuyTinh) — đặt trong Arena_Final.")]
        [SerializeField] Transform player;
        [Tooltip("Boss Sơn Tinh — clone từ Sandbox_Combat.")]
        [SerializeField] Transform boss;
        [Tooltip("Điểm phán xử (Platform của Dien_PhanXuat).")]
        [SerializeField] Transform phanXuatPoint;
        [Tooltip("Spawn_ThuyTinh — vị trí kết thúc của người chơi.")]
        [SerializeField] Transform playerSpawn;
        [Tooltip("Spawn_SonTinh — vị trí kết thúc của boss.")]
        [SerializeField] Transform bossSpawn;
        [SerializeField] PlayerInputReader playerInput;
        [SerializeField] ThirdPersonCameraInput cameraInput;

        [Header("Nhịp [tune]")]
        [SerializeField, Min(0f)] float phanXuatSeconds = 3f;
        [SerializeField, Min(0f)] float walkSeconds = 4f;
        [SerializeField, Min(0.5f)] float walkDistance = 4f;

        [Header("Camera phán xử [tune]")]
        [SerializeField] Vector3 phanXuatCamOffset = new Vector3(0f, 1.8f, 6f);
        [SerializeField] Vector3 phanXuatLookOffset = new Vector3(0f, 0.6f, 0f);

        [Header("Camera toàn cảnh [tune]")]
        [SerializeField] Vector3 walkCamPos = new Vector3(15f, 6.5f, 0f);
        [SerializeField] Vector3 walkLookPos = new Vector3(0f, 0.5f, 0f);

        [Header("Chữ [tune]")]
        [SerializeField] string phanXuatCaption =
            "HÙNG VƯƠNG PHÁN XỬ — bên nào thắng, giữ đất, giữ nước.";
        [SerializeField] string skipHint = "Nhấn Space / N để bỏ qua";

        GameObject camGo;
        CinemachineCamera introCam;
        GUIStyle captionStyle;
        GUIStyle hintStyle;
        bool showCaption;
        bool running;

        public bool IsRunning => running;

        void Awake()
        {
            Instance = this;
            IntroActive = true;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            IntroActive = false;
            if (camGo != null) Destroy(camGo);
        }

        void Start()
        {
            if (player == null || boss == null || phanXuatPoint == null ||
                playerSpawn == null || bossSpawn == null)
            {
                Debug.LogError("[Entry] thiếu tham chiếu (player/boss/phanXuat/spawns) — bỏ qua cutscene");
                IntroActive = false;
                enabled = false;
                return;
            }

            if (playerInput != null) playerInput.enabled = false;
            if (cameraInput != null) cameraInput.enabled = false;
            running = true;
            Debug.Log("[Entry] cắt cảnh phán xử bắt đầu");
            StartCoroutine(RunIntro());
        }

        void Update()
        {
            if (!running) return;
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.nKey.wasPressedThisFrame))
                Skip();
        }

        void OnGUI()
        {
            if (!showCaption) return;
            EnsureStyles();

            float w = Mathf.Min(900f, Screen.width - 60f);
            GUILayout.BeginArea(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.72f, w, 96f));
            GUILayout.Label(phanXuatCaption, captionStyle);
            GUILayout.Space(6f);
            GUILayout.Label(skipHint, hintStyle);
            GUILayout.EndArea();
        }

        public void Skip()
        {
            if (!running) return;
            StopAllCoroutines();
            SnapToSpawns();
            EndIntro();
        }

        void SnapToSpawns()
        {
            Vector3 pEnd = GroundedEnd(playerSpawn.position, 0f);
            Vector3 bEnd = GroundedEnd(bossSpawn.position, 1f);
            player.position = pEnd;
            boss.position = bEnd;
        }

        IEnumerator RunIntro()
        {
            PlaceActors();
            Vector3 pEnd = GroundedEnd(playerSpawn.position, 0f);
            Vector3 bEnd = GroundedEnd(bossSpawn.position, 1f);

            SpawnShot(phanXuatPoint.position + phanXuatCamOffset,
                phanXuatPoint.position + phanXuatLookOffset);
            showCaption = true;
            yield return new WaitForSeconds(phanXuatSeconds);
            showCaption = false;

            SpawnShot(walkCamPos, walkLookPos);
            Vector3 pStart = player.position;
            Vector3 bStart = boss.position;
            float t = 0f;
            while (t < walkSeconds)
            {
                t += Time.deltaTime;
                float k = walkSeconds <= 0f
                    ? 1f
                    : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / walkSeconds));
                player.position = Vector3.Lerp(pStart, pEnd, k);
                boss.position = Vector3.Lerp(bStart, bEnd, k);
                yield return null;
            }
            player.position = pEnd;
            boss.position = bEnd;

            EndIntro();
        }

        void PlaceActors()
        {
            player.position = GroundedEnd(playerSpawn.position + Vector3.forward * walkDistance, 0f);
            boss.position = GroundedEnd(bossSpawn.position + Vector3.back * walkDistance, 1f);
            FaceEachOther();
        }

        void FaceEachOther()
        {
            Vector3 toBoss = bossSpawn.position - playerSpawn.position;
            toBoss.y = 0f;
            if (toBoss.sqrMagnitude < 0.001f) return;
            player.rotation = Quaternion.LookRotation(toBoss, Vector3.up);
            boss.rotation = Quaternion.LookRotation(-toBoss, Vector3.up);
        }

        Vector3 GroundedEnd(Vector3 marker, float rootHeight)
        {
            Vector3 p = marker;
            if (Physics.Raycast(marker + Vector3.up * 8f, Vector3.down, out RaycastHit hit, 24f))
                p.y = hit.point.y;
            else
                p.y = marker.y;
            return new Vector3(p.x, p.y + rootHeight, p.z);
        }

        void SpawnShot(Vector3 camPos, Vector3 lookPos)
        {
            if (camGo == null)
            {
                camGo = new GameObject("EntryCamera");
                introCam = camGo.AddComponent<CinemachineCamera>();
                introCam.Priority.Value = 100;
                Debug.Log("[Entry] tạo camera phán xử (priority 100)");
            }
            camGo.transform.SetPositionAndRotation(camPos,
                Quaternion.LookRotation(lookPos - camPos));
        }

        void EndIntro()
        {
            if (camGo != null) Destroy(camGo);
            camGo = null;
            introCam = null;
            showCaption = false;
            running = false;
            if (playerInput != null) playerInput.enabled = true;
            if (cameraInput != null) cameraInput.enabled = true;
            IntroActive = false;
            Debug.Log("[Entry] cắt cảnh xong — trả quyền điều khiển");
        }

        void EnsureStyles()
        {
            if (captionStyle != null) return;
            captionStyle = new GUIStyle(GUI.skin.box)
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
        }
    }
}
