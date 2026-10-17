using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Imports the Blender-authored in-place charge action and adds it to the existing
    // elephant controller. Kept separate from the full elephant setup so no scene is rebuilt.
    [InitializeOnLoad]
    public static class VoiChinNgaChargeSetup
    {
        const string ChargeModelPath = "Assets/Art/Characters/VoiChinNga/Models/voi_chin_nga_charge.fbx";
        const string ControllerPath = "Assets/Animations/VoiChinNga/AC_VoiChinNga.controller";
        const string StateName = "Charge_Burst";
        const string SessionKey = "SonTinhThuyTinh.VoiChinNgaChargeSetup.v1";

        static VoiChinNgaChargeSetup()
        {
            EditorApplication.delayCall += SetupWhenReady;
        }

        static void SetupWhenReady()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += SetupWhenReady;
                return;
            }

            try
            {
                Setup();
                SessionState.SetBool(SessionKey, true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/Son Tinh Thuy Tinh/Setup Voi Chin Nga Charge")]
        public static void Setup()
        {
            AssetDatabase.ImportAsset(ChargeModelPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(ChargeModelPath) is not ModelImporter importer)
                throw new InvalidOperationException("Không import được animation húc: " + ChargeModelPath);

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.name = StateName;
                clip.loopTime = false;
                clip.loopPose = false;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            AnimationClip chargeClip = AssetDatabase.LoadAllAssetsAtPath(ChargeModelPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (chargeClip == null)
                throw new InvalidOperationException("FBX không chứa clip " + StateName);

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                throw new InvalidOperationException("Không tìm thấy controller voi: " + ControllerPath);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState state = stateMachine.states.Select(child => child.state)
                .FirstOrDefault(candidate => candidate.name == StateName);
            if (state == null) state = stateMachine.AddState(StateName);
            state.motion = chargeClip;
            state.speed = 1f;
            state.writeDefaultValues = true;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("Đã nối animation Charge_Burst của Voi Chín Ngà vào Animator Controller.");
        }
    }
}
