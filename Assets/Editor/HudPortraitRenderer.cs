using System.IO;
using System.Linq;
using SonTinhThuyTinh.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Renders the HUD portraits (head and shoulders, transparent background, soft bottom/side edges) from the in-game models
    // and assigns them to the character definitions. Runs in a preview scene, so the open scene is not touched.
    public static class HudPortraitRenderer
    {
        struct Entry
        {
            public string Definition, Model, Idle, Output;
        }

        static readonly Entry[] Entries =
        {
            new() { Definition = "Assets/Data/Characters/Character_SonTinh.asset", Model = "Assets/Prefabs/Characters/SonTinh_v2.prefab", Idle = "Assets/Animations/SonTinh_v2/son_tinh_v2_idle.fbx", Output = "Assets/Art/UI/Portraits/Portrait_SonTinh.png" },
            new() { Definition = "Assets/Data/Characters/Character_ThuyTinh.asset", Model = "Assets/Prefabs/Characters/ThuyTinh_v2.prefab", Idle = "Assets/Animations/ThuyTinh_v2/thuy_tinh_v2_idle_3.fbx", Output = "Assets/Art/UI/Portraits/Portrait_ThuyTinh.png" },
        };

        const int Size = 512;

        [MenuItem("Tools/Son Tinh Thuy Tinh/Render HUD portraits")]
        public static void RenderAll()
        {
            foreach (Entry entry in Entries)
            {
                Render(entry.Model, entry.Idle, entry.Output);
                var importer = (TextureImporter)AssetImporter.GetAtPath(entry.Output);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = Size;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();

                var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(entry.Definition);
                var so = new SerializedObject(definition);
                so.FindProperty("portrait").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(entry.Output);
                so.ApplyModifiedProperties();
            }
            AssetDatabase.SaveAssets();
        }

        static void Render(string modelPath, string idlePath, string outputPath)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), scene);
                var animator = model.GetComponent<Animator>();
                if (animator != null) animator.enabled = false;
                AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(idlePath).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
                idle.SampleAnimation(model, 0.2f);

                Transform head = Bone(model, "mixamorig:Head"), top = Bone(model, "mixamorig:HeadTop_End"), neck = Bone(model, "mixamorig:Neck");
                float headLength = (top.position - head.position).magnitude;
                float topY = top.position.y + 0.75f * headLength, bottomY = neck.position.y - 1.4f * headLength;   // room for the tall crowns
                float window = topY - bottomY;
                const float fov = 18f;
                float distance = window * 0.5f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
                var focus = new Vector3(head.position.x, (topY + bottomY) * 0.5f, head.position.z);

                var cameraObject = new GameObject("PortraitCamera");
                MoveTo(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene;
                camera.fieldOfView = fov;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = distance * 6f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                // turned a little to the character's left, so the face looks towards the bars on its right
                cameraObject.transform.position = focus + Quaternion.Euler(0f, 18f, 0f) * new Vector3(0f, window * 0.03f, distance);
                cameraObject.transform.LookAt(focus);

                AddLight(scene, 35f, 205f, 1.7f, new Color(1f, 0.95f, 0.88f));
                AddLight(scene, 10f, 150f, 0.7f, new Color(0.7f, 0.82f, 1f));
                AddLight(scene, 20f, 20f, 1.3f, Color.white);

                // Render on black and on white: the difference gives the alpha, whatever the render pipeline does with alpha.
                Color[] onBlack = Shoot(camera, Color.black), onWhite = Shoot(camera, Color.white);
                var output = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                var pixels = new Color[Size * Size];
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        int i = y * Size + x;
                        Color b = onBlack[i], w = onWhite[i];
                        float alpha = Mathf.Clamp01(1f - ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f);
                        float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                        float fade = Mathf.SmoothStep(0f, 1f, v / 0.28f) * Mathf.SmoothStep(0f, 1f, u / 0.1f) * Mathf.SmoothStep(0f, 1f, (1f - u) / 0.1f);
                        Color c = alpha > 0.004f ? new Color(Mathf.Clamp01(b.r / alpha), Mathf.Clamp01(b.g / alpha), Mathf.Clamp01(b.b / alpha)) : Color.black;
                        c.a = alpha * fade;
                        pixels[i] = c;
                    }
                output.SetPixels(pixels);
                output.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(outputPath, output.EncodeToPNG());
                Object.DestroyImmediate(output);
                AssetDatabase.ImportAsset(outputPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void MoveTo(GameObject go, UnityEngine.SceneManagement.Scene scene) => UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);

        static void AddLight(UnityEngine.SceneManagement.Scene scene, float pitch, float yaw, float intensity, Color color)
        {
            var go = new GameObject("PortraitLight");
            MoveTo(go, scene);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = color;
            go.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        static Color[] Shoot(Camera camera, Color background)
        {
            var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.backgroundColor = background;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            Color[] pixels = texture.GetPixels();
            Object.DestroyImmediate(texture);
            camera.targetTexture = null;
            target.Release();
            Object.DestroyImmediate(target);
            return pixels;
        }

        static Transform Bone(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
    }
}
