using UnityEngine;

namespace SonTinhThuyTinh.Environment
{
    // Where the player last stood safely on a route with pits (RespawnCheckpoint writes, FallRespawnZone reads). Per scene.
    public static class FallRespawn
    {
        static Vector3 lastPosition;
        static int lastScene = int.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => lastScene = int.MinValue;

        public static void Remember(int scene, Vector3 position)
        {
            lastScene = scene;
            lastPosition = position;
        }

        public static bool TryGetLast(int scene, out Vector3 position)
        {
            position = lastPosition;
            return lastScene == scene;
        }
    }
}
