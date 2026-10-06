using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Gà chín cựa: puts the rigged, textured rooster (Assets/Art/Characters/Rooster/rooster.fbx, made in Blender from the Meshy model in
    // ArtSource/Meshy/Rooster) in place of the placeholder shape on every "Pickup_GaChinCua".
    // Menu: Tools > Son Tinh Thuy Tinh > Rooster > Setup Ga chin cua. Safe to run again (it skips pickups that already have the rooster).
    // Steps: FBX import settings (Generic rig, clips Walk/Eat/Fly looping, no FBX materials), textures, URP Lit material, Animator controller
    // (the pickup plays Fly: wings flapping, so it reads as a collectible; change the default state of AC_Rooster to Eat / Walk if you prefer),
    // prefab, then the swap in Map_SonTinh and Sandbox_Combat (Thuy Tinh has a different gift, so Map_ThuyTinh keeps the placeholder).
    public static class RoosterGiftSetup
    {
        const string Dir = "Assets/Art/Characters/Rooster/";
        public const string ModelPath = Dir + "rooster.fbx";
        const string BaseColorPath = Dir + "Textures/rooster_basecolor.png";
        const string NormalPath = Dir + "Textures/rooster_normal.png";
        const string MaterialPath = Dir + "M_Rooster.mat";
        const string ControllerPath = Dir + "AC_Rooster.controller";
        const string PrefabDir = "Assets/Prefabs/Gifts";
        const string PrefabPath = PrefabDir + "/Rooster_GaChinCua.prefab";
        public const string ChildName = "Rooster";

        // The model is 0.7 m tall; scaled up so the gift reads from a distance. Visual sits this high above the pickup's origin (bobs around it).
        public const float ModelScale = 1.2f;
        public const float VisualHeight = 0.9f;

        static readonly string[] Scenes =
        {
            "Assets/Scenes/Map_SonTinh.unity",
            "Assets/Scenes/Sandbox_Combat.unity",
        };

        [MenuItem("Tools/Son Tinh Thuy Tinh/Rooster/Setup Ga chin cua (prefab + Son Tinh scenes)")]
        public static void SetupAll()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
            {
                Debug.LogError("Missing " + ModelPath + ". Copy rooster.fbx and the Textures folder into Assets/Art/Characters/Rooster first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ConfigureModel();
            SetTexture(BaseColorPath, false);
            SetTexture(NormalPath, true);
            Material material = EnsureMaterial();
            AnimatorController controller = EnsureController();
            GameObject prefab = EnsurePrefab(material, controller);

            string openScene = EditorSceneManager.GetActiveScene().path;
            int swapped = 0;
            foreach (string path in Scenes) swapped += ApplyToScene(path, prefab);
            if (!string.IsNullOrEmpty(openScene)) EditorSceneManager.OpenScene(openScene, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log("Rooster gift ready: " + swapped + " pickup(s) swapped.");
        }

        static void ConfigureModel()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;   // the material below is used, not the FBX's
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.SaveAndReimport();

            // One clip per take (Blender names them "ChickenRig|ChickenRig|Walk"); keep the last part, loop all of them.
            var clips = importer.defaultClipAnimations
                .Where(c => { string n = c.takeName.Split('|').Last(); return n == "Walk" || n == "Eat" || n == "Fly"; })
                .Select(c => { c.name = c.takeName.Split('|').Last(); c.loopTime = true; return c; })
                .ToArray();
            if (clips.Length == 0) { Debug.LogWarning("No Walk/Eat/Fly takes found in " + ModelPath); return; }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        static void SetTexture(string path, bool normalMap)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            bool wanted = normalMap ? importer.textureType == TextureImporterType.NormalMap : importer.textureType == TextureImporterType.Default;
            if (wanted && importer.maxTextureSize <= 2048) return;
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.maxTextureSize = 2048;
            importer.sRGBTexture = !normalMap;
            importer.SaveAndReimport();
        }

        static Material EnsureMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath));
            material.SetColor("_BaseColor", Color.white);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
            }
            material.SetFloat("_Metallic", 0.05f);
            material.SetFloat("_Smoothness", 0.3f);
            material.SetFloat("_Cull", 0f);   // wing and tail feathers are thin single sheets
            EditorUtility.SetDirty(material);
            return material;
        }

        static AnimatorController EnsureController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller != null) return controller;

            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview")).ToDictionary(c => c.name, c => c);
            AnimatorState fly = null;
            foreach (string name in new[] { "Fly", "Eat", "Walk" })
            {
                if (!clips.TryGetValue(name, out AnimationClip clip)) { Debug.LogWarning("Clip not found: " + name); continue; }
                AnimatorState state = machine.AddState(name);
                state.motion = clip;
                if (name == "Fly") fly = state;
            }
            if (fly != null) machine.defaultState = fly;
            return controller;
        }

        static GameObject EnsurePrefab(Material material, AnimatorController controller)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;   // delete the prefab to rebuild it

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/Prefabs", "Gifts");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                var shared = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < shared.Length; i++) shared[i] = material;
                r.sharedMaterials = shared;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;   // bounds are taken from the rest pose, Fly lifts the body
            }
            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            instance.name = "Rooster_GaChinCua";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        static int ApplyToScene(string path, GameObject prefab)
        {
            if (!System.IO.File.Exists(path)) { Debug.LogWarning("Scene not found: " + path); return 0; }
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform pickup in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Pickup_GaChinCua").ToArray())
                {
                    Transform visual = pickup.Find("Visual");
                    if (visual == null) { Debug.LogWarning(path + ": Pickup_GaChinCua has no Visual child."); continue; }
                    if (visual.Find(ChildName) != null) continue;   // already done

                    // the placeholder shape
                    foreach (Collider c in visual.GetComponents<Collider>()) Object.DestroyImmediate(c);
                    var renderer = visual.GetComponent<MeshRenderer>();
                    if (renderer != null) Object.DestroyImmediate(renderer);
                    var filter = visual.GetComponent<MeshFilter>();
                    if (filter != null) Object.DestroyImmediate(filter);

                    var rooster = (GameObject)PrefabUtility.InstantiatePrefab(prefab, visual);
                    rooster.name = ChildName;
                    rooster.transform.localPosition = Vector3.zero;
                    rooster.transform.localRotation = Quaternion.identity;
                    rooster.transform.localScale = Vector3.one * ModelScale;

                    visual.localScale = Vector3.one;   // the placeholder was scaled 0.6
                    visual.localPosition = new Vector3(0f, VisualHeight, 0f);
                    count++;
                }
            }
            if (count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log(path + ": " + count + " pickup(s) swapped.");
            return count;
        }
    }
}
