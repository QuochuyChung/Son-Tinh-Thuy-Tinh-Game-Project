using System.Collections;
using UnityEngine;

namespace SonTinhThuyTinh.Combat.Skills
{
    public sealed class StoneWall : SkillEffect, IAttackReceiver
    {
        [SerializeField] float lifetime = 5f;
        [SerializeField] float spawnDistance = 1.8f;

        int blocksRemaining = 1;

        public static int TotalBlocked { get; private set; }

        public override void Play(in SkillContext context)
        {
            Vector3 forward = context.Caster.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            transform.position = context.Caster.position + forward * spawnDistance;
            transform.rotation = Quaternion.LookRotation(forward);
            StartCoroutine(LifeRoutine());
        }

        public bool ReceiveAttack(in AttackHit hit)
        {
            if (!hit.isHeavy || blocksRemaining <= 0) return false;
            blocksRemaining--;
            TotalBlocked++;
            Destroy(gameObject);
            return true;
        }

        IEnumerator LifeRoutine()
        {
            yield return new WaitForSeconds(lifetime);
            Destroy(gameObject);
        }
    }
}
