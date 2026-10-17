using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // Where one Người Giữ Nhiệm Vụ stands on a gift map (placed by Assets/Editor/MapIntroBuilder.cs, "Quest keepers"). `giftIndex` is the
    // gift (0, 1, 2 of the character's quest) whose three questions this keeper asks: one keeper at the start of each leg, so the player
    // never walks back to the beginning of the map after a boss. PlayerSpawner -> QuestKeeperNpc.Spawn makes the figures at run time.
    public class QuestKeeperMark : MonoBehaviour
    {
        public int giftIndex;
    }
}
