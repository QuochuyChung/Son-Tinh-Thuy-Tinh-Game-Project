using System;
using System.Collections;
using UnityEngine;

namespace SonTinhThuyTinh.Combat.Skills
{
    public sealed class PerfectDodgeDetector : MonoBehaviour
    {
        public static PerfectDodgeDetector Instance { get; private set; }

        [Tooltip("Số giây sau khi bắt đầu né mà cú đánh chạm tới vẫn tính là né hoàn hảo.")]
        [SerializeField, Min(0f)] float perfectWindow = 0.3f;
        [Tooltip("Bóng người của lần né hoàn hảo tồn tại bấy nhiêu giây trước khi mờ đi.")]
        [SerializeField, Min(0.05f)] float ghostDuration = 0.35f;
        [Tooltip("Đóng băng màn hình (hitstop) bấy nhiêu giây thực sau mỗi lần né hoàn hảo.")]
        [SerializeField, Min(0f)] float hitstopSeconds = 0.25f;
        [Tooltip("Trong hitstop timeScale hạ về mức này rồi tự khôi phục.")]
        [SerializeField, Min(0f)] float hitstopTimeScale = 0.05f;
        [Tooltip("Prefab bóng người spawn tại vị trí né (bỏ trống thì không spawn).")]
        [SerializeField] GameObject ghostPrefab;

        float dodgeStartedAt = float.NegativeInfinity;
        bool windowConsumed;
        float timeScaleBeforeHitstop = 1f;
        Coroutine hitstopRoutine;

        public event Action PerfectDodged;

        public bool IsDodging { get; private set; }
        public float DodgeElapsed => IsDodging ? Time.time - dodgeStartedAt : float.PositiveInfinity;
        public bool IsInPerfectWindow => IsDodging && !windowConsumed && DodgeElapsed <= perfectWindow;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDisable()
        {
            if (hitstopRoutine != null)
            {
                StopCoroutine(hitstopRoutine);
                hitstopRoutine = null;
                Time.timeScale = timeScaleBeforeHitstop;
            }
            IsDodging = false;
            if (Instance == this) Instance = null;
        }

        public void NotifyDodgeStarted()
        {
            IsDodging = true;
            windowConsumed = false;
            dodgeStartedAt = Time.time;
        }

        public void NotifyDodgeEnded() => IsDodging = false;

        public void ConsumePerfectDodged()
        {
            windowConsumed = true;
            PerfectDodged?.Invoke();
            SpawnAfterImage();
            if (hitstopSeconds <= 0f) return;
            if (hitstopRoutine != null)
            {
                StopCoroutine(hitstopRoutine);
                Time.timeScale = timeScaleBeforeHitstop;
            }
            hitstopRoutine = StartCoroutine(HitStopRoutine());
        }

        IEnumerator HitStopRoutine()
        {
            timeScaleBeforeHitstop = Time.timeScale;
            Time.timeScale = hitstopTimeScale;
            yield return new WaitForSecondsRealtime(hitstopSeconds);
            Time.timeScale = timeScaleBeforeHitstop;
            hitstopRoutine = null;
        }

        void SpawnAfterImage()
        {
            if (ghostPrefab == null) return;
            GameObject ghost = Instantiate(ghostPrefab, transform.position, transform.rotation);
            AfterImageGhost fade = ghost.GetComponent<AfterImageGhost>();
            if (fade != null) fade.Lifetime = ghostDuration;
            else Destroy(ghost, ghostDuration);
        }
    }
}
