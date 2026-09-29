using System.Collections;
using SonTinhThuyTinh.Player;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    public sealed class TrainingDummy : MonoBehaviour, IAttackReceiver, ISkillTarget
    {
        [SerializeField] float maxHp = 100f;
        [SerializeField] float attackInterval = 4f;
        [SerializeField] float attackRange = 3.5f;
        [SerializeField] float telegraphDuration = 0.6f;
        [SerializeField] float attackDamage = 20f;
        [SerializeField] float downDuration = 2f;

        PlayerController player;
        Coroutine tipRoutine;
        float nextSwingAt;
        bool busy;

        public float Hp { get; private set; }
        public float MaxHp => maxHp;
        public bool IsDown { get; private set; }
        public bool IsDead => Hp <= 0f;
        public Transform TargetTransform => transform;

        void Start()
        {
            Hp = maxHp;
            player = FindFirstObjectByType<PlayerController>();
            nextSwingAt = Time.time + attackInterval;
        }

        void Update()
        {
            if (busy || IsDown || IsDead || player == null) return;
            if (Time.time < nextSwingAt) return;
            if (FlatDirectionToPlayer().sqrMagnitude > attackRange * attackRange) return;
            StartCoroutine(SwingRoutine());
        }

        public void SwingHeavy()
        {
            if (busy || IsDead) return;
            StartCoroutine(SwingRoutine());
        }

        public bool ReceiveAttack(in AttackHit hit)
        {
            if (IsDead) return true;
            Hp = Mathf.Max(0f, Hp - hit.damage);
            if (IsDead)
            {
                Settle(hit.direction, 90f, 0.3f, 0f, 0f);
                return true;
            }
            if (hit.causesKnockdown) Settle(hit.direction, 80f, 0.25f, downDuration, 0.4f);
            return true;
        }

        IEnumerator SwingRoutine()
        {
            busy = true;
            nextSwingAt = Time.time + attackInterval;

            Vector3 dir = FlatDirectionToPlayer();
            if (!IsDown && dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);
            yield return new WaitForSeconds(telegraphDuration);

            if (player != null)
            {
                dir = FlatDirectionToPlayer();
                if (!IsDown && dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);
            }

            AttackHit hit = new AttackHit
            {
                damage = attackDamage,
                isHeavy = true,
                causesKnockdown = false,
                origin = transform.position + Vector3.up * 1.2f + dir * 0.5f,
                direction = dir,
                source = gameObject
            };
            AttackResolver.Melee(in hit, 0.6f, 3f);
            busy = false;
        }

        void Settle(Vector3 dir, float degrees, float tiltSeconds, float holdSeconds, float riseSeconds)
        {
            if (tipRoutine != null) StopCoroutine(tipRoutine);
            if (riseSeconds > 0f) IsDown = true;
            tipRoutine = StartCoroutine(SettleRoutine(dir, degrees, tiltSeconds, holdSeconds, riseSeconds));
        }

        IEnumerator SettleRoutine(Vector3 dir, float degrees, float tiltSeconds, float holdSeconds, float riseSeconds)
        {
            if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
            Quaternion start = transform.rotation;
            Quaternion down = Quaternion.AngleAxis(degrees, Vector3.Cross(Vector3.up, dir.normalized)) * start;

            float elapsed = 0f;
            while (elapsed < tiltSeconds)
            {
                elapsed += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(start, down, Mathf.Clamp01(elapsed / tiltSeconds));
                yield return null;
            }
            transform.rotation = down;

            if (riseSeconds <= 0f) yield break;

            yield return new WaitForSeconds(holdSeconds);
            elapsed = 0f;
            while (elapsed < riseSeconds)
            {
                elapsed += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(down, start, Mathf.Clamp01(elapsed / riseSeconds));
                yield return null;
            }
            transform.rotation = start;
            IsDown = false;
        }

        Vector3 FlatDirectionToPlayer()
        {
            if (player == null) return transform.forward;
            Vector3 dir = player.transform.position - transform.position;
            dir.y = 0f;
            return dir;
        }
    }
}
