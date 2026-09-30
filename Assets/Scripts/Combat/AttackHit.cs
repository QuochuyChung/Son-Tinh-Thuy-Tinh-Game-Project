using System;
using System.Collections.Generic;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    public struct AttackHit
    {
        public float damage;
        public bool isHeavy;
        public bool causesKnockdown;
        public Vector3 origin;
        public Vector3 direction;
        public GameObject source;
    }

    public interface IAttackReceiver
    {
        bool ReceiveAttack(in AttackHit hit);
    }

    public static class AttackResolver
    {
        public static IAttackReceiver Melee(in AttackHit hit, float radius, float distance)
        {
            RaycastHit[] casts = Physics.SphereCastAll(hit.origin, radius, hit.direction, distance, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(casts, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit cast in casts)
            {
                IAttackReceiver receiver = Consume(hit, cast.collider);
                if (receiver != null) return receiver;
            }
            return null;
        }

        public static int Area(in AttackHit hit, float radius)
        {
            Collider[] cols = Physics.OverlapSphere(hit.origin, radius, ~0, QueryTriggerInteraction.Ignore);
            int count = 0;
            HashSet<IAttackReceiver> consumed = null;
            foreach (Collider col in cols)
            {
                IAttackReceiver receiver = Consume(hit, col);
                if (receiver == null) continue;
                if (consumed == null) consumed = new HashSet<IAttackReceiver>();
                if (!consumed.Add(receiver)) continue;
                count++;
            }
            return count;
        }

        public static int Fan(in AttackHit hit, float radius, float halfAngleDegrees, List<IAttackReceiver> results = null)
        {
            Collider[] cols = Physics.OverlapSphere(hit.origin, radius, ~0, QueryTriggerInteraction.Ignore);
            int count = 0;
            HashSet<IAttackReceiver> consumed = null;
            foreach (Collider col in cols)
            {
                Vector3 to = col.bounds.center - hit.origin;
                to.y = 0f;
                if (to.sqrMagnitude < 0.0001f) to = hit.direction;
                if (Vector3.Angle(hit.direction, to) > halfAngleDegrees) continue;
                IAttackReceiver receiver = Consume(hit, col);
                if (receiver == null) continue;
                if (consumed == null) consumed = new HashSet<IAttackReceiver>();
                if (!consumed.Add(receiver)) continue;
                count++;
                results?.Add(receiver);
            }
            return count;
        }

        static IAttackReceiver Consume(in AttackHit hit, Collider collider)
        {
            if (collider == null) return null;
            IAttackReceiver receiver = collider.GetComponentInParent<IAttackReceiver>();
            if (receiver == null) return null;
            Component comp = receiver as Component;
            if (comp != null && hit.source != null && comp.gameObject == hit.source) return null;
            return receiver.ReceiveAttack(in hit) ? receiver : null;
        }
    }
}
