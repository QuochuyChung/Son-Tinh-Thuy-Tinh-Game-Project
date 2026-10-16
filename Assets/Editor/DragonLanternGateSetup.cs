using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Imports the Meshy dragon-lantern gate and swaps only the visual portion of the
// two route exits. The original Gate hierarchy stays in place (with its renderers
// hidden), so its colliders and the separate Exit_To_Next trigger remain intact.
public static class DragonLanternGateSetup
{
    public const string PrefabPath = "Assets/Prefabs/Environment/DragonLanternGate.prefab";

    const string Folder = "Assets/Art/Environment/DragonLanternGate";
    const string ModelPath = Folder + "/DragonLanternGate.fbx";
    const string BaseColorPath = Folder + "/DragonLanternGate_BaseColor.png";
    const string NormalPath = Folder + "/DragonLanternGate_Normal.png";
    const string MetallicPath = Folder + "/DragonLanternGate_Metallic.png";
    const string RoughnessPath = Folder + "/DragonLanternGate_Roughness.png";
    const string MaterialPath = Folder + "/DragonLanternGate.mat";
    const string VisualName = "DragonLanternGate_Visual";
    const float TargetWidth = 11.6f;

    static readonly string[] ScenePaths =
    {
        "Assets/Scenes/Map_SonTinh.unity",
        "Assets/Scenes/Map_ThuyTinh.unity"
    };

    [InitializeOnLoadMethod]
    static void ScheduleOrientationCheck()
    {
        EditorApplication.delayCall += EnsureUprightPrefab;
    }

    [MenuItem("Tools/Son Tinh Thuy Tinh/Apply Dragon Lantern Gate To Both Maps")]
    public static void ApplyToBothMaps()
    {
        GameObject prefab = EnsurePrefab();
        if (prefab == null)
            throw new InvalidOperationException("Dragon lantern gate prefab could not be created.");

        int changed = 0;
        foreach (string scenePath in ScenePaths)
            if (ApplyToScene(scenePath, prefab)) changed++;

        AssetDatabase.SaveAssets();
        Debug.Log($"Dragon lantern gate setup complete. Updated {changed} scene(s); exit triggers and colliders were preserved.");
    }

    // Used by the map builders so rebuilding either map keeps the new art.
    public static GameObject UseNewVisual(GameObject gate)
    {
        if (gate == null) return null;
        GameObject prefab = EnsurePrefab();
        if (prefab == null)
        {
            Debug.LogWarning("Dragon lantern gate prefab is unavailable; keeping the procedural gate visual.");
            return gate;
        }

        AddVisual(gate, prefab);
        return gate;
    }

    static void EnsureUprightPrefab()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureUprightPrefab;
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Transform model = prefab != null ? prefab.transform.Find("Model") : null;
        Quaternion upright = Quaternion.Euler(-90f, 0f, 0f);
        if (model != null && Quaternion.Angle(model.localRotation, upright) < 0.1f) return;

        EnsurePrefab(true);
        Debug.Log("Dragon lantern gate orientation corrected: Meshy Z-up converted to Unity Y-up.");
    }

    static GameObject EnsurePrefab(bool rebuild = false)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null && !rebuild) return existing;

        ConfigureTexture(BaseColorPath, false);
        ConfigureTexture(NormalPath, true);
        ConfigureTexture(MetallicPath, false);
        ConfigureTexture(RoughnessPath, false);

        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer != null)
        {
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
        }

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null) return null;

        Material material = EnsureMaterial();
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath) ?? "Assets/Prefabs/Environment");

        var root = new GameObject("DragonLanternGate");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        // Meshy exported this FBX Z-up. Convert it to Unity's Y-up convention so
        // the gate stands vertically instead of lying across the road.
        model.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        model.transform.localScale = Vector3.one;

        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(collider);

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            UnityEngine.Object.DestroyImmediate(root);
            throw new InvalidOperationException("The dragon lantern FBX contains no renderers.");
        }

        Bounds bounds = CalculateBounds(renderers);
        float scale = TargetWidth / Mathf.Max(0.001f, bounds.size.x);
        model.transform.localScale = Vector3.one * scale;
        bounds = CalculateBounds(renderers);
        model.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);

        foreach (Renderer renderer in renderers)
        {
            var materials = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        return prefab;
    }

    static void ConfigureTexture(string path, bool normalMap)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        TextureImporterType wanted = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        bool linear = path == MetallicPath || path == RoughnessPath || normalMap;
        if (importer.textureType == wanted && importer.sRGBTexture == !linear) return;

        importer.textureType = wanted;
        importer.sRGBTexture = !linear;
        importer.SaveAndReimport();
    }

    static Material EnsureMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = "DragonLanternGate" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicPath);

        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", baseColor);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", baseColor);
        if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", normal);
        if (material.HasProperty("_MetallicGlossMap")) material.SetTexture("_MetallicGlossMap", metallic);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.35f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.28f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.28f);
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        EditorUtility.SetDirty(material);
        return material;
    }

    static Bounds CalculateBounds(Renderer[] renderers)
    {
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static bool ApplyToScene(string scenePath, GameObject prefab)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        try
        {
            Transform exit = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "Exit_To_Next");
            if (exit == null)
                throw new MissingReferenceException($"{scenePath} has no Exit_To_Next trigger.");

            Transform gate = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(item => item.name == "Gate")
                .OrderBy(item => Vector3.Distance(item.position, exit.position))
                .FirstOrDefault();
            if (gate == null)
                throw new MissingReferenceException($"{scenePath} has no Gate visual near Exit_To_Next.");

            bool changed = AddVisual(gate.gameObject, prefab);
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Component transition = exit.GetComponents<Component>()
                .FirstOrDefault(component => component != null && component.GetType().Name == "SceneTransitionTrigger");
            BoxCollider triggerCollider = exit.GetComponent<BoxCollider>();
            if (transition == null || triggerCollider == null || !triggerCollider.isTrigger)
                throw new InvalidOperationException($"{scenePath}: Exit_To_Next functionality validation failed.");

            Debug.Log($"{scenePath}: dragon lantern visual ready; Exit_To_Next trigger preserved.");
            return changed;
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }

    static bool AddVisual(GameObject gate, GameObject prefab)
    {
        if (gate.transform.Find(VisualName) != null) return false;

        foreach (Renderer renderer in gate.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;

        var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, gate.transform);
        visual.name = VisualName;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        return true;
    }
}
