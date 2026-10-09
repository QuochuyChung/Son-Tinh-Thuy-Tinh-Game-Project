using System;
using System.IO;
using System.Linq;
using SonTinhThuyTinh.Quest;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SonTinhThuyTinh.EditorTools
{
    /// <summary>
    /// Chuẩn hóa asset voi chín ngà, tạo material/controller/prefab và thay placeholder
    /// tại bàn thờ sính lễ trong Map_SonTinh.
    /// </summary>
    [InitializeOnLoad]
    public static class VoiChinNgaSetup
    {
        const string ModelPath = "Assets/Art/Characters/VoiChinNga/Models/voi_chin_nga_rigged.fbx";
        const string TextureDir = "Assets/Art/Characters/VoiChinNga/Textures";
        const string BaseColorPath = TextureDir + "/voi_chin_nga_basecolor.png";
        const string NormalPath = TextureDir + "/voi_chin_nga_normal.png";
        const string MetallicPath = TextureDir + "/voi_chin_nga_metallic.png";
        const string RoughnessPath = TextureDir + "/voi_chin_nga_roughness.png";
        const string PackedTexturePath = TextureDir + "/voi_chin_nga_metallic_smoothness.png";
        const string MaterialPath = "Assets/Art/Characters/VoiChinNga/Materials/M_VoiChinNga.mat";
        const string ControllerPath = "Assets/Animations/VoiChinNga/AC_VoiChinNga.controller";
        const string PrefabPath = "Assets/Prefabs/Characters/VoiChinNga.prefab";
        const string IconPath = "Assets/Art/UI/Portraits/Portrait_VoiChinNga.png";
        const string GiftPath = "Assets/Data/Gifts/Gift_VoiChinNga.asset";
        const string ScenePath = "Assets/Scenes/Map_SonTinh.unity";
        const string SessionKey = "SonTinhThuyTinh.VoiChinNgaSetup.Completed";

        static readonly string[] ClipNames = { "Idle", "Walk_Forward", "Walk_Backward", "Attack", "Death" };

        static VoiChinNgaSetup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        static void AutoSetup()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoSetup;
                return;
            }

            SessionState.SetBool(SessionKey, true);
            try
            {
                Setup();
            }
            catch (Exception exception)
            {
                SessionState.SetBool(SessionKey, false);
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/Son Tinh Thuy Tinh/Setup Voi Chin Nga")]
        public static void Setup()
        {
            if (!File.Exists(ModelPath))
            {
                Debug.LogError("Thiếu model voi chín ngà: " + ModelPath);
                return;
            }

            ConfigureModel();
            ConfigureTexture(BaseColorPath, false, false, 2048);
            ConfigureTexture(NormalPath, true, false, 2048);
            ConfigureTexture(MetallicPath, false, true, 2048);
            ConfigureTexture(RoughnessPath, false, true, 2048);
            BuildMetallicSmoothness();
            Material material = BuildMaterial();
            AnimatorController controller = BuildController();
            BuildPrefab(material, controller);
            ConfigureGiftIcon();
            SetupMapScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Voi chín ngà đã được đặt vào bàn thờ sính lễ trong Map_SonTinh.");
        }

        static void ConfigureModel()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(ModelPath) is not ModelImporter importer) return;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.globalScale = 1f;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.loopTime = clip.name.Contains("Idle", StringComparison.OrdinalIgnoreCase)
                    || clip.name.Contains("Walk", StringComparison.OrdinalIgnoreCase);
                clip.loopPose = clip.loopTime;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        static void ConfigureTexture(string path, bool normalMap, bool readable, int maxSize)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;

            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normalMap && path == BaseColorPath;
            importer.isReadable = readable;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        static void BuildMetallicSmoothness()
        {
            Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicPath);
            Texture2D roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(RoughnessPath);
            if (metallic == null || roughness == null)
                throw new InvalidOperationException("Không đọc được texture metallic/roughness của voi chín ngà.");

            int width = metallic.width;
            int height = metallic.height;
            Color[] metallicPixels = metallic.GetPixels();
            Color[] packedPixels = new Color[metallicPixels.Length];

            if (roughness.width == width && roughness.height == height)
            {
                Color[] roughnessPixels = roughness.GetPixels();
                for (int i = 0; i < packedPixels.Length; i++)
                {
                    float metal = metallicPixels[i].r;
                    packedPixels[i] = new Color(metal, metal, metal, 1f - roughnessPixels[i].r);
                }
            }
            else
            {
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    float metal = metallicPixels[index].r;
                    float rough = roughness.GetPixelBilinear((x + 0.5f) / width, (y + 0.5f) / height).r;
                    packedPixels[index] = new Color(metal, metal, metal, 1f - rough);
                }
            }

            var packed = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            packed.SetPixels(packedPixels);
            packed.Apply(false, false);
            File.WriteAllBytes(Path.GetFullPath(PackedTexturePath), packed.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(packed);

            AssetDatabase.ImportAsset(PackedTexturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(PackedTexturePath) is TextureImporter packedImporter)
            {
                packedImporter.textureType = TextureImporterType.Default;
                packedImporter.sRGBTexture = false;
                packedImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                packedImporter.maxTextureSize = 2048;
                packedImporter.textureCompression = TextureImporterCompression.CompressedHQ;
                packedImporter.SaveAndReimport();
            }

            SetReadable(MetallicPath, false);
            SetReadable(RoughnessPath, false);
        }

        static void SetReadable(string path, bool readable)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer || importer.isReadable == readable) return;
            importer.isReadable = readable;
            importer.SaveAndReimport();
        }

        static Material BuildMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Không tìm thấy URP/Lit shader.");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "M_VoiChinNga" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath));
            material.SetFloat("_BumpScale", 1f);
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PackedTexturePath));
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        static AnimatorController BuildController()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            foreach (string clipName in ClipNames)
            {
                AnimationClip clip = FindClip(clipName);
                if (clip == null)
                {
                    Debug.LogWarning("Không tìm thấy animation clip " + clipName + " trong " + ModelPath);
                    continue;
                }

                AnimatorState state = stateMachine.states.Select(child => child.state).FirstOrDefault(item => item.name == clipName);
                if (state == null) state = stateMachine.AddState(clipName);
                state.motion = clip;
                state.speed = 1f;
                state.writeDefaultValues = true;
                if (clipName == "Idle") stateMachine.defaultState = state;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimationClip FindClip(string clipName)
        {
            return AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal)
                    && (clip.name.Equals(clipName, StringComparison.OrdinalIgnoreCase)
                        || clip.name.EndsWith("|" + clipName, StringComparison.OrdinalIgnoreCase)));
        }

        static void BuildPrefab(Material material, RuntimeAnimatorController controller)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null) throw new InvalidOperationException("Unity chưa import được model voi chín ngà.");

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, previewScene);
                instance.name = "VoiChinNga_Model";
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));
                // Voi thần khổng lồ: cao gần 9,5 m, nổi bật hẳn so với Sơn Tinh (~2 m).
                instance.transform.localScale = Vector3.one * 14f;

                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = material;
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                Animator animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        static void ConfigureGiftIcon()
        {
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(IconPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }

            GiftItem gift = AssetDatabase.LoadAssetAtPath<GiftItem>(GiftPath);
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            if (gift == null || icon == null) return;

            var serializedGift = new SerializedObject(gift);
            serializedGift.FindProperty("icon").objectReferenceValue = icon;
            serializedGift.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gift);
        }

        static void SetupMapScene()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedForSetup = !scene.IsValid() || !scene.isLoaded;
            if (openedForSetup) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            Transform pickup = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "Pickup_VoiChinNga");

            if (pickup == null)
            {
                if (openedForSetup) EditorSceneManager.CloseScene(scene, true);
                throw new InvalidOperationException("Không tìm thấy Pickup_VoiChinNga trong Map_SonTinh.");
            }

            ApplyPickupVisual(pickup);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (openedForSetup) EditorSceneManager.CloseScene(scene, true);
        }

        public static void ApplyPickupVisual(Transform pickup)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return;

            // Đưa voi khỏi bệ đá xuống con đường trước bàn thờ.
            Vector3 groundPosition = new(-10.984891f, 0f, 238f);
            Terrain terrain = pickup.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Terrain>(true))
                .FirstOrDefault();
            groundPosition.y = terrain != null
                ? terrain.SampleHeight(groundPosition) + terrain.transform.position.y - 0.04f
                : 7.244f;
            pickup.SetPositionAndRotation(groundPosition, Quaternion.identity);

            Transform visual = pickup.Find("Visual");
            if (visual == null)
            {
                visual = new GameObject("Visual").transform;
                visual.SetParent(pickup, false);
            }

            while (visual.childCount > 0) UnityEngine.Object.DestroyImmediate(visual.GetChild(0).gameObject);
            foreach (MeshRenderer renderer in visual.GetComponents<MeshRenderer>()) UnityEngine.Object.DestroyImmediate(renderer);
            foreach (MeshFilter filter in visual.GetComponents<MeshFilter>()) UnityEngine.Object.DestroyImmediate(filter);
            foreach (Collider collider in visual.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, visual);
            model.name = "VoiChinNga_Model";
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.transform.localScale = Vector3.one * 14f;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;

            Material elephantMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Renderer[] modelRenderers = model.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer modelRenderer in modelRenderers)
            {
                if (elephantMaterial == null) continue;
                Material[] materials = modelRenderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = elephantMaterial;
                modelRenderer.sharedMaterials = materials;
            }

            // Căn chân voi sát mặt đất dựa trên bounds thật, không dựa vào pivot giữa thân của FBX.
            if (modelRenderers.Length > 0)
            {
                float lowestPoint = modelRenderers[0].bounds.min.y;
                for (int i = 1; i < modelRenderers.Length; i++)
                    lowestPoint = Mathf.Min(lowestPoint, modelRenderers[i].bounds.min.y);
                visual.position += Vector3.up * (pickup.position.y - lowestPoint);
            }

            GiftPickup giftPickup = pickup.GetComponent<GiftPickup>();
            if (giftPickup != null)
            {
                var serializedPickup = new SerializedObject(giftPickup);
                serializedPickup.FindProperty("visual").objectReferenceValue = visual;
                serializedPickup.FindProperty("bobHeight").floatValue = 0f;
                serializedPickup.FindProperty("bobSpeed").floatValue = 0.65f;
                serializedPickup.FindProperty("spinSpeed").floatValue = 0f;
                serializedPickup.ApplyModifiedPropertiesWithoutUndo();
            }

            SphereCollider trigger = pickup.GetComponent<SphereCollider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
                trigger.radius = 7.5f;
                trigger.center = new Vector3(0f, visual.localPosition.y, 0f);
            }

            VoiChinNgaPatrol patrol = pickup.GetComponent<VoiChinNgaPatrol>();
            if (patrol == null) patrol = pickup.gameObject.AddComponent<VoiChinNgaPatrol>();
            var serializedPatrol = new SerializedObject(patrol);
            serializedPatrol.FindProperty("visual").objectReferenceValue = visual;
            serializedPatrol.FindProperty("elephantMaterial").objectReferenceValue = elephantMaterial;
            serializedPatrol.FindProperty("elephantScale").floatValue = 14f;
            serializedPatrol.FindProperty("patrolDistance").floatValue = 8f;
            serializedPatrol.FindProperty("moveSpeed").floatValue = 1.35f;
            serializedPatrol.FindProperty("turnSpeed").floatValue = 100f;
            serializedPatrol.FindProperty("groundOffset").floatValue = -0.04f;
            serializedPatrol.ApplyModifiedPropertiesWithoutUndo();

            Transform glow = pickup.Find("Glow");
            if (glow != null)
            {
                glow.localPosition = new Vector3(0f, 8.5f, 0f);
                Light light = glow.GetComponent<Light>();
                if (light != null)
                {
                    light.color = new Color(1f, 0.72f, 0.3f);
                    light.range = 25f;
                    light.intensity = 6f;
                }
            }
        }
    }
}
