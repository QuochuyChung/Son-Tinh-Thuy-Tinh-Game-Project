using System.Collections.Generic;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    [CreateAssetMenu(menuName = "Son Tinh Thuy Tinh/Gift Quest", fileName = "Quest_")]
    public class GiftQuest : ScriptableObject
    {
        [SerializeField] string title = "Sính lễ";
        [SerializeField] GiftItem[] requiredGifts;

        public string Title => title;
        public IReadOnlyList<GiftItem> RequiredGifts => requiredGifts;
        public bool IsComplete => CollectedCount == requiredGifts.Length;

        public int CollectedCount
        {
            get
            {
                int count = 0;
                foreach (GiftItem gift in requiredGifts)
                    if (GiftTracker.Has(gift)) count++;
                return count;
            }
        }
    }
}
