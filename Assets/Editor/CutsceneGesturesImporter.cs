using System.Linq;
using UnityEditor;

namespace SonTinhThuyTinh.EditorTools
{
    // Import settings of Assets/Art/Characters/Cutscene/cutscene_gestures.fbx (the hand-keyed clips of tools/cutscene_gestures.py):
    // Humanoid (its own avatar), no materials, one clip per take named after the take (Blender writes "Armature|Bow"), the resting
    // poses (CrossArms, Talk, SitIdle, SitTalk, Poise) looping, the root locked so the clips never move the actor.
    // Ensure() is called by the cutscene scene builders: after a re-export from Blender with new clips it brings the importer up to
    // date by itself (it only reimports when something differs).
    public static class CutsceneGesturesImporter
    {
        public const string Path = "Assets/Art/Characters/Cutscene/cutscene_gestures.fbx";
        static readonly string[] Loops = { "CrossArms", "Talk", "SitIdle", "SitTalk", "Poise" };

        public static void Ensure()
        {
            var importer = AssetImporter.GetAtPath(Path) as ModelImporter;
            if (importer == null) return;
            var wanted = importer.defaultClipAnimations.Select(c =>
            {
                string name = c.takeName.Split('|').Last();
                c.name = name;
                c.loopTime = Loops.Contains(name);
                c.lockRootRotation = true; c.keepOriginalOrientation = true;
                c.lockRootHeightY = true; c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = true;
                return c;
            }).ToArray();
            bool same = importer.animationType == ModelImporterAnimationType.Human
                && importer.clipAnimations.Length == wanted.Length
                && importer.clipAnimations.All(c => wanted.Any(w => w.name == c.name && w.loopTime == c.loopTime));
            if (same) return;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.clipAnimations = wanted;
            importer.SaveAndReimport();
        }
    }
}
