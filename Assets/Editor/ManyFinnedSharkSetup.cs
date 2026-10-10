using System;
using System.IO;
using System.Linq;
using SonTinhThuyTinh.Environment;
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
    public static class ManyFinnedSharkSetup
    {
        const string ScenePath = "Assets/Scenes/Map_ThuyTinh.unity";
        const string FbxPath = "Assets/Art/Characters/ManyFinnedShark/ManyFinnedShark_Rigged.fbx";
        const string TextureDirectory = "Assets/Art/Characters/ManyFinnedShark/Textures";
        const string BaseColorPath = TextureDirectory + "/CaMap9Vay_BaseColor.png";
        const string NormalPath = TextureDirectory + "/CaMap9Vay_Normal.png";
        const string MaterialDirectory = "Assets/Art/Characters/ManyFinnedShark/Materials";
        const string MaterialPath = MaterialDirectory + "/M_ManyFinnedShark.mat";
        const string AnimationDirectory = "Assets/Animations/ManyFinnedShark";
        const string ControllerPath = AnimationDirectory + "/AC_ManyFinnedShark.controller";
        const string PrefabPath = "Assets/Prefabs/Characters/ManyFinnedShark.prefab";
        const string SetupRequestPath = "Temp/CodexSetupManyFinnedShark.request";
        const string SharkName = "Map9Vay";
        const string OldSharkName = "ManyFinnedShark";
        const string PickupName = "Pickup_Map9Vay";
        const string OldPickupName = "Pickup_NguaChinHongMao";
        const string GiftPath = "Assets/Data/Gifts/Gift_Map9Vay.asset";
        const string QuestPath = "Assets/Data/Gifts/Quest_SinhLe_ThuyTinh.asset";
        const string OriginalQuestPath = "Assets/Data/Gifts/Quest_SinhLe.asset";
        // The shark replaces the gift on the river bank. Keep its belly just above
        // the terrain instead of deriving its height from the water plane.
        const float ShoreClearance = 0.65f;

        static string SetupRequestFullPath => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", SetupRequestPath));

        [InitializeOnLoadMethod]
        static void RunPendingSetup()
        {
            if (!File.Exists(SetupRequestFullPath)) return;
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(SetupRequestFullPath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                try
                {
                    SetupAndPlaceAdditive();
                    File.Delete(SetupRequestFullPath);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            };
        }

        [MenuItem("Tools/Son Tinh Thuy Tinh/Setup Many-Finned Shark In Thuy Tinh Map")]
        public static void SetupAndPlace()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Stop Play mode before setting up the shark.");
                return;
            }

            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("Map_ThuyTinh scene was not found.", ScenePath);

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform generated = GameObject.Find("Generated")?.transform;
            if (generated == null)
                throw new InvalidOperationException("Map_ThuyTinh has no Generated root. Rebuild the map first.");

            AddToMap(generated);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Map 9 Vay replaced the horse pickup in Map_ThuyTinh with Meshy textures and swim animation.");
        }

        // Entry point used by command-line validation/setup.
        public static void SetupAndPlaceBatch() => SetupAndPlace();

        public static void SetupAndPlaceAdditive()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasAlreadyOpen = scene.IsValid() && scene.isLoaded;
            if (!wasAlreadyOpen) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            Transform generated = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "Generated");
            if (generated == null)
                throw new InvalidOperationException("Map_ThuyTinh has no Generated root. Rebuild the map first.");

            AddToMap(generated);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            if (!wasAlreadyOpen) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("Map 9 Vay replaced the horse pickup in Map_ThuyTinh with Meshy textures and swim animation.");
        }

        public static void AddToMap(Transform generatedRoot)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? EnsureAssets();
            Scene scene = generatedRoot.gameObject.scene;
            Transform pickupContainer = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name is "GiftPickups" or "GiftPickups (test)");
            if (pickupContainer == null)
                throw new InvalidOperationException("Map_ThuyTinh has no GiftPickups container.");

            Transform pickup = pickupContainer.Find(PickupName) ?? pickupContainer.Find(OldPickupName);
            if (pickup == null)
                throw new InvalidOperationException("Map_ThuyTinh has no first gift pickup to replace.");

            pickup.name = PickupName;
            pickup.gameObject.SetActive(true);
            GiftItem gift = AssetDatabase.LoadAssetAtPath<GiftItem>(GiftPath);
            if (gift == null) throw new FileNotFoundException("Map 9 Vay gift asset was not found.", GiftPath);

            GiftPickup giftPickup = pickup.GetComponent<GiftPickup>();
            Transform oldVisual = null;
            if (giftPickup != null)
            {
                var pickupObject = new SerializedObject(giftPickup);
                oldVisual = pickupObject.FindProperty("visual").objectReferenceValue as Transform;
                UnityEngine.Object.DestroyImmediate(giftPickup);
            }
            if (oldVisual != null && oldVisual.parent == pickup)
                UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);

            ManyFinnedSharkEncounterTrigger encounter = pickup.GetComponent<ManyFinnedSharkEncounterTrigger>();
            if (encounter == null) encounter = pickup.gameObject.AddComponent<ManyFinnedSharkEncounterTrigger>();
            var encounterObject = new SerializedObject(encounter);
            encounterObject.FindProperty("gift").objectReferenceValue = gift;
            encounterObject.FindProperty("battleScene").stringValue = SceneNames.ManyFinnedSharkBattle;
            encounterObject.FindProperty("returnClearance").floatValue = 2.5f;
            encounterObject.ApplyModifiedPropertiesWithoutUndo();

            SphereCollider trigger = pickup.GetComponent<SphereCollider>();
            if (trigger == null) trigger = pickup.gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 2.8f;

            Transform oldGiftShark = pickup.Find(SharkName) ?? pickup.Find(OldSharkName);
            if (oldGiftShark != null) UnityEngine.Object.DestroyImmediate(oldGiftShark.gameObject);

            // Remove the older decorative copy that swam beside the horse pickup.
            Transform wildlife = generatedRoot.Find("Wildlife");
            Transform oldDecorativeShark = wildlife?.Find(OldSharkName) ?? wildlife?.Find(SharkName);
            if (oldDecorativeShark != null) UnityEngine.Object.DestroyImmediate(oldDecorativeShark.gameObject);

            GameObject shark = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            shark.name = SharkName;
            shark.transform.SetParent(pickup, false);
            shark.transform.localPosition = new Vector3(0f, ShoreClearance, 0f);
            shark.transform.localRotation = Quaternion.identity;

            ConfigureThuyTinhQuest(scene, gift);
        }

        static GameObject EnsureAssets()
        {
            EnsureFolder(MaterialDirectory);
            EnsureFolder(AnimationDirectory);
            ConfigureModelImporter();
            ConfigureTextureImporter(BaseColorPath, false);
            ConfigureTextureImporter(NormalPath, true);

            Material material = EnsureMaterial();
            RuntimeAnimatorController controller = EnsureController();
            return EnsurePrefab(material, controller);
        }

        static void ConfigureModelImporter()
        {
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null) throw new FileNotFoundException("Shark FBX was not found.", FbxPath);

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = true;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.name = ShortClipName(clip.name);
                clip.loopTime = clip.name == "Shark_Swim";
                clip.loopPose = clip.name == "Shark_Swim";
            }

            if (clips.Length > 0) importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        static void ConfigureTextureImporter(string path, bool normalMap)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("Shark texture was not found.", path);

            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normalMap;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        static Material EnsureMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            // Use an unlit texture shader so the Meshy base colour remains visible
            // in the deliberately dark/blue lighting of Map_ThuyTinh.
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Texture")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            if (material == null)
            {
                material = new Material(shader) { name = "M_ManyFinnedShark" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            if (baseColor == null || normal == null)
                throw new InvalidOperationException("Map 9 Vay textures were not imported correctly.");

            material.color = Color.white;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", baseColor);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", baseColor);
            if (material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.DisableKeyword("_NORMALMAP");
            }
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
                material.DisableKeyword("_EMISSION");
            }
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.02f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.32f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void ConfigureThuyTinhQuest(Scene scene, GiftItem gift)
        {
            GiftQuest quest = AssetDatabase.LoadAssetAtPath<GiftQuest>(QuestPath);
            GiftQuest originalQuest = AssetDatabase.LoadAssetAtPath<GiftQuest>(OriginalQuestPath);
            if (quest == null) throw new FileNotFoundException("Thuy Tinh gift quest was not found.", QuestPath);

            foreach (GiftTrackerHUD hud in ComponentsInScene<GiftTrackerHUD>(scene))
            {
                var serialized = new SerializedObject(hud);
                serialized.FindProperty("quest").objectReferenceValue = quest;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (SceneTransitionTrigger trigger in ComponentsInScene<SceneTransitionTrigger>(scene))
            {
                var serialized = new SerializedObject(trigger);
                SerializedProperty requiredQuest = serialized.FindProperty("requiredQuest");
                if (requiredQuest.objectReferenceValue == originalQuest || requiredQuest.objectReferenceValue == quest)
                {
                    requiredQuest.objectReferenceValue = quest;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            foreach (MapRoute route in ComponentsInScene<MapRoute>(scene))
            {
                var serialized = new SerializedObject(route);
                SerializedProperty pins = serialized.FindProperty("giftPins");
                if (pins != null && pins.isArray && pins.arraySize > 0)
                {
                    pins.GetArrayElementAtIndex(0).FindPropertyRelative("gift").objectReferenceValue = gift;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        static T[] ComponentsInScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();

        static RuntimeAnimatorController EnsureController()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                .ToArray();

            AnimationClip swim = FindClip(clips, "Shark_Swim");
            AnimationClip attack = FindClip(clips, "Shark_Attack");
            AnimationClip defeated = FindClip(clips, "Shark_Defeated");

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState swimState = AddState(stateMachine, "Shark_Swim", swim, new Vector3(220f, 80f));
            AddState(stateMachine, "Shark_Attack", attack, new Vector3(440f, 20f));
            AddState(stateMachine, "Shark_Defeated", defeated, new Vector3(440f, 140f));
            stateMachine.defaultState = swimState;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static GameObject EnsurePrefab(Material material, RuntimeAnimatorController controller)
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (modelAsset == null) throw new InvalidOperationException("Shark FBX could not be loaded as a prefab.");

            var root = new GameObject(SharkName);
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.transform.localScale = Vector3.one * 4.5f;

            foreach (Renderer modelRenderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = modelRenderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                modelRenderer.sharedMaterials = materials;
            }

            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            ManyFinnedSharkSwimmer swimmer = root.AddComponent<ManyFinnedSharkSwimmer>();
            var serializedSwimmer = new SerializedObject(swimmer);
            serializedSwimmer.FindProperty("visual").objectReferenceValue = model.transform;
            serializedSwimmer.FindProperty("sharkMaterial").objectReferenceValue = material;
            serializedSwimmer.FindProperty("sharkScale").floatValue = 4.5f;
            serializedSwimmer.FindProperty("swimSpeed").floatValue = 2.4f;
            serializedSwimmer.FindProperty("patrolRadiusX").floatValue = 2.5f;
            serializedSwimmer.FindProperty("patrolRadiusZ").floatValue = 3.5f;
            serializedSwimmer.FindProperty("turnSpeed").floatValue = 120f;
            serializedSwimmer.FindProperty("verticalBob").floatValue = 0.05f;
            serializedSwimmer.FindProperty("followGround").boolValue = true;
            serializedSwimmer.FindProperty("groundClearance").floatValue = ShoreClearance;
            serializedSwimmer.FindProperty("initialPhase").floatValue = 25f;
            serializedSwimmer.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static AnimatorState AddState(AnimatorStateMachine stateMachine, string name, AnimationClip clip, Vector3 position)
        {
            AnimatorState state = stateMachine.AddState(name, position);
            state.motion = clip;
            state.writeDefaultValues = true;
            return state;
        }

        static AnimationClip FindClip(AnimationClip[] clips, string name)
        {
            AnimationClip clip = clips.FirstOrDefault(candidate => ShortClipName(candidate.name) == name);
            if (clip == null) throw new InvalidOperationException("Missing shark animation clip: " + name);
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
