using UnityEngine;

namespace SonTinhThuyTinh.Combat.Environment
{
    public sealed class ComboChainTracker : MonoBehaviour
    {
        public const int HitsPerStep = 5;

        public int Count { get; private set; }

        public void Add(int weight)
        {
            if (weight <= 0) return;
            Count += weight;
        }

        public bool TryConsumeStep()
        {
            if (Count < HitsPerStep) return false;
            Count -= HitsPerStep;
            return true;
        }

        public void ResetChain() => Count = 0;
    }
}
