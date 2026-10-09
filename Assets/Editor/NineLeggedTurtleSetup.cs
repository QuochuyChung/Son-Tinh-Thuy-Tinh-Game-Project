using System;
using System.IO;
using System.Linq;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.Quest;
using SonTinhThuyTinh.UI.Map;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SonTinhThuyTinh.EditorTools
{
    public static class NineLeggedTurtleSetup
    {
        const string ScenePath = "Assets/Scenes/Map_ThuyTinh.unity";
        const string FbxPath = "Assets/Art/Characters/NineLeggedTurtle/NineLeggedTurtle_Animated.fbx";
        const string MaterialDirectory = "Assets/Art/Characters/NineLeggedTurtle/Materials";
        const string MaterialPath = MaterialDirectory + "/M_NineLeggedTurtle.mat";
        const string AnimationDirectory = "Assets/Animations/NineLeggedTurtle";
        const string ControllerPath = AnimationDirectory + "/AC_NineLeggedTurtle.controller";
        const string PrefabPath = "Assets/Prefabs/Characters/NineLeggedTurtle.prefab";
        const string ResourcePrefabPath = "Assets/Resources/Rua9Chan.prefab";
        const string GiftPath = "Assets/Data/Gifts/Gift_Rua9Chan.asset";
        const string OldGiftPath = "Assets/Data/Gifts/Gift_GaChinCua.asset";
        const string QuestPath = "Assets/Data/Gifts/Quest_SinhLe_ThuyTinh.asset";
        const string PickupName = "Pickup_Rua9Chan";
        const string OldPickupName = "Pickup_GaChinCua";
        const string TurtleName = "Rua9Chan";
        const float DisplayHeight = 0.32f;

        [MenuItem("Tools/Son Tinh Thuy Tinh/Setup Rua Chin Chan In Thuy Tinh Map")]
        public static void SetupAndPlace()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Stop Play mode before setting up Rua Chin Chan.");
                return;
            }
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Map_ThuyTinh scene was not found.", ScenePath);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform generated = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "Generated");
            if (generated == null) throw new InvalidOperationException("Map_ThuyTinh has no Generated root.");

            AddToMap(generated);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Rua Chin Chan replaced Ga Chin Cua in Map_ThuyTinh and now starts Battle_Rua9Chan.");
        }

        public static void SetupAndPlaceBatch()
        {
            SetupAndPlace();
            EditorApplication.Exit(0);
        }

        public static void AddToMap(Transform generatedRoot)
        {
            GameObject prefab = EnsureAssets();
            GiftItem gift = AssetDatabase.LoadAssetAtPath<GiftItem>(GiftPath);
            Scene scene = generatedRoot.gameObject.scene;

            Transform pickupContainer = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name is "GiftPickups" or "GiftPickups (test)");
            if (pickupContainer == null) throw new InvalidOperationException("Map_ThuyTinh has no GiftPickups container.");

            Transform pickup = pickupContainer.Find(PickupName) ?? pickupContainer.Find(OldPickupName);
            if (pickup == null) throw new InvalidOperationException("Map_ThuyTinh has no Ga Chin Cua pickup to replace.");
            pickup.name = PickupName;

            GiftPickup oldPickup = pickup.GetComponent<GiftPickup>();
            Transform oldVisual = null;
            if (oldPickup != null)
            {
                SerializedObject pickupData = new(oldPickup);
                oldVisual = pickupData.FindProperty("visual").objectReferenceValue as Transform;
                UnityEngine.Object.DestroyImmediate(oldPickup);
            }
            if (oldVisual != null && oldVisual.parent == pickup)
                UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);

            Transform staleTurtle = pickup.Find(TurtleName);
            if (staleTurtle != null) UnityEngine.Object.DestroyImmediate(staleTurtle.gameObject);

            NineLeggedTurtleEncounterTrigger encounter = pickup.GetComponent<NineLeggedTurtleEncounterTrigger>();
            if (encounter == null) encounter = pickup.gameObject.AddComponent<NineLeggedTurtleEncounterTrigger>();
            SerializedObject encounterData = new(encounter);
            encounterData.FindProperty("gift").objectReferenceValue = gift;
            encounterData.FindProperty("battleScene").stringValue = SceneNames.NineLeggedTurtleBattle;
            encounterData.FindProperty("returnClearance").floatValue = 2.5f;
            encounterData.ApplyModifiedPropertiesWithoutUndo();

            SphereCollider trigger = pickup.GetComponent<SphereCollider>();
            if (trigger == null) trigger = pickup.gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.8f, 0f);
            trigger.radius = 2.9f;

            GameObject turtle = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            turtle.name = TurtleName;
            turtle.transform.SetParent(pickup, false);
            turtle.transform.localPosition = new Vector3(0f, DisplayHeight, 0f);
            turtle.transform.localRotation = Quaternion.identity;

            ConfigureThuyTinhQuest(scene, gift);
        }

        public static GameObject EnsureAssets()
        {
            EnsureFolder(MaterialDirectory);
            EnsureFolder(AnimationDirectory);
            EnsureFolder("Assets/Prefabs/Characters");
            EnsureFolder("Assets/Resources");
            ConfigureModelImporter();
            GiftItem gift = EnsureGift();
            Material material = EnsureMaterial();
            RuntimeAnimatorController controller = EnsureController();
            GameObject prefab = EnsurePrefab(material, controller);
            EditorUtility.SetDirty(gift);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        static void ConfigureModelImporter()
        {
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);
            ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null) throw new FileNotFoundException("Nine-legged turtle FBX was not found.", FbxPath);

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = true;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.name = ShortClipName(clip.name);
                clip.loopTime = clip.name == "Move";
                clip.loopPose = clip.name == "Move";
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }
            if (clips.Length > 0) importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        static GiftItem EnsureGift()
        {
            GiftItem gift = AssetDatabase.LoadAssetAtPath<GiftItem>(GiftPath);
            if (gift == null)
            {
                gift = ScriptableObject.CreateInstance<GiftItem>();
                gift.name = "Gift_Rua9Chan";
                AssetDatabase.CreateAsset(gift, GiftPath);
            }
            SerializedObject data = new(gift);
            data.FindProperty("displayName").stringValue = "Rùa chín chân";
            data.FindProperty("description").stringValue = "Linh quy đầu rồng canh giữ thủy vực, sở hữu sức mạnh và chín chân thần thoại.";
            data.ApplyModifiedPropertiesWithoutUndo();
            return gift;
        }

        static Material EnsureMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (material == null)
            {
                material = new Material(shader) { name = "M_NineLeggedTurtle" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else material.shader = shader;

            Color shell = new(0.16f, 0.34f, 0.25f, 1f);
            material.color = shell;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", shell);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.08f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.38f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static RuntimeAnimatorController EnsureController()
        {
            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null) AssetDatabase.DeleteAsset(ControllerPath);

            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                .ToArray();
            AnimationClip move = FindClip(clips, "Move");
            AnimationClip attack = FindClip(clips, "Attack");
            AnimationClip defeated = FindClip(clips, "Defeated");

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState moveState = AddState(stateMachine, "Turtle_Move", move, new Vector3(220f, 80f));
            AddState(stateMachine, "Turtle_Attack", attack, new Vector3(440f, 20f));
            AddState(stateMachine, "Turtle_Defeated", defeated, new Vector3(440f, 140f));
            stateMachine.defaultState = moveState;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static GameObject EnsurePrefab(Material material, RuntimeAnimatorController controller)
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (modelAsset == null) throw new InvalidOperationException("Nine-legged turtle FBX could not be loaded as a prefab.");

            GameObject root = new(TurtleName);
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.transform.localScale = Vector3.one * 5.2f;

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }

            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.SaveAsPrefabAsset(root, ResourcePrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static void ConfigureThuyTinhQuest(Scene scene, GiftItem turtleGift)
        {
            GiftItem oldGift = AssetDatabase.LoadAssetAtPath<GiftItem>(OldGiftPath);
            GiftQuest quest = AssetDatabase.LoadAssetAtPath<GiftQuest>(QuestPath);
            if (quest == null) throw new FileNotFoundException("Thuy Tinh gift quest was not found.", QuestPath);

            SerializedObject questData = new(quest);
            SerializedProperty required = questData.FindProperty("requiredGifts");
            for (int i = 0; i < required.arraySize; i++)
            {
                SerializedProperty item = required.GetArrayElementAtIndex(i);
                if (item.objectReferenceValue == oldGift || i == 1) item.objectReferenceValue = turtleGift;
            }
            questData.ApplyModifiedPropertiesWithoutUndo();

            foreach (GiftTrackerHUD hud in ComponentsInScene<GiftTrackerHUD>(scene))
            {
                SerializedObject hudData = new(hud);
                hudData.FindProperty("quest").objectReferenceValue = quest;
                hudData.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (MapRoute route in ComponentsInScene<MapRoute>(scene))
            {
                SerializedObject routeData = new(route);
                SerializedProperty pins = routeData.FindProperty("giftPins");
                if (pins == null || !pins.isArray) continue;
                for (int i = 0; i < pins.arraySize; i++)
                {
                    SerializedProperty gift = pins.GetArrayElementAtIndex(i).FindPropertyRelative("gift");
                    if (gift.objectReferenceValue == oldGift || i == 1) gift.objectReferenceValue = turtleGift;
                }
                routeData.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static T[] ComponentsInScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();

        static AnimatorState AddState(AnimatorStateMachine machine, string name, AnimationClip clip, Vector3 position)
        {
            AnimatorState state = machine.AddState(name, position);
            state.motion = clip;
            state.writeDefaultValues = true;
            return state;
        }

        static AnimationClip FindClip(AnimationClip[] clips, string name)
        {
            AnimationClip clip = clips.FirstOrDefault(candidate => ShortClipName(candidate.name) == name);
            if (clip == null) throw new InvalidOperationException("Missing turtle animation clip: " + name);
            return clip;
        }

        static string ShortClipName(string name)
        {
            int separator = name.LastIndexOf('|');
            return separator >= 0 ? name[(separator + 1)..] : name;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
