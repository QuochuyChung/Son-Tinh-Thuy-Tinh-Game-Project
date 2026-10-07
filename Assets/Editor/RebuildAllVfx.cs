using UnityEditor;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    public static class RebuildAllVfx
    {
        [MenuItem("Tools/Son Tinh Thuy Tinh/Rebuild All Combat VFX")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Cannot rebuild VFX in Play Mode.");
                return;
            }

            Debug.Log("Rebuilding all Thuy Tinh and Son Tinh combat VFX and movesets...");
            CombatSetupBuilder.Build();
            SonTinhCombatBuilder.Build();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>Successfully rebuilt all combat VFX and movesets for Son Tinh and Thuy Tinh!</color>");
        }

        [InitializeOnLoadMethod]
        static void SetupHooks()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.delayCall += TryRebuild;
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += TryRebuild;
            }
        }

        static void OnEditorUpdate()
        {
            string flagPath = "Temp/rebuild_requested.flag";
            if (System.IO.File.Exists(flagPath))
            {
                if (EditorApplication.isPlaying)
                {
                    Debug.Log("[RebuildAllVfx] Exiting Play Mode to rebuild combat VFX assets...");
                    EditorApplication.isPlaying = false;
                    return;
                }

                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                try { System.IO.File.Delete(flagPath); } catch {}
                Rebuild();
            }
        }

        public static void TryRebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var zonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Combat/VFX_EarthZone.prefab");
            bool hasMountain = zonePrefab != null && zonePrefab.GetComponent<SonTinhThuyTinh.Combat.RisingMountainZone>() != null;

            var spellAsset = AssetDatabase.LoadAssetAtPath<SonTinhThuyTinh.Combat.SpellData>("Assets/Data/Combat/SonTinh/Spell_EarthZone.asset");
            bool isRenamed = spellAsset != null && spellAsset.displayName == "Núi dâng";

            if (!hasMountain || !isRenamed)
            {
                Debug.Log("[RebuildAllVfx] Detected missing mountain VFX or outdated spell name. Auto-rebuilding combat assets now...");
                Rebuild();
            }
        }
    }
}
