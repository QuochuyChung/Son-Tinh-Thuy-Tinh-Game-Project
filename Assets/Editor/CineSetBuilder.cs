using System.IO;
using System.Linq;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Cutscene;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Flow;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SonTinhThuyTinh.EditorTools
{
    // The shared parts of the 3D cutscene scenes (opening, judgement, ending): a copy of Map_HungVuong without its gameplay (the
    // palace, the avenue, the lighting), the king's throne on a dais, the cast with their ActorRig (the hand-keyed gestures + each
    // character's walk), the dialogue UI moved over from the Prologue, the CinematicCamera with a cinematic Volume (depth of field,
    // colour grade, vignette), the CineStage (rain, dust, rock, water, light mood). The scene builders add their own marks and director.
    public static class CineSetBuilder
    {
        public const string MapPath = "Assets/Scenes/Map_HungVuong.unity";
        public const string ProloguePath = "Assets/Scenes/Prologue.unity";
        public const string KingPrefab = "Assets/Prefabs/Characters/HungVuong.prefab";
        public const string MiNuongPrefab = "Assets/Prefabs/Characters/MiNuong.prefab";
        public const string RosterPath = "Assets/Data/Characters/CharacterRoster.asset";
        public const string GesturesFbx = "Assets/Art/Characters/Cutscene/cutscene_gestures.fbx";
        const string ProfilePath = "Assets/Data/Cutscene/VP_Cinematic.asset";
        const string PuddleMat = "Assets/Art/Combat/Mat_CutscenePuddle.mat";

        // the set, in Map_HungVuong coordinates: palace front (steps) at z = 36, avenue along +z, plateau top at y = 14
        public static readonly Vector3 DaisCenter = new(0f, 14f, 33.4f);
        public const float DaisTop = 0.5f;

        static readonly string[] RemoveRoots = { "PlayerFollowCamera", "PlayerSpawn", "GameplayHUD", "PlayerHUD", "PauseCanvas", "PauseMenu", "MapRoute", "MapCanvas" };
        static readonly string[] RemoveGenerated = { "Exit_To_Next", "Spawns" };

        // A fresh copy of the palace map written over the scene file (so the scene keeps its GUID), gameplay stripped. Returns the open scene.
        public static Scene CopyPalaceScene(string scenePath)
        {
            if (!File.Exists(scenePath)) AssetDatabase.CopyAsset(MapPath, scenePath);
            else { File.Copy(MapPath, scenePath, true); AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceUpdate); }
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                if (RemoveRoots.Contains(root.name)) Object.DestroyImmediate(root);
            var generated = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Generated");
            if (generated != null)
                foreach (string n in RemoveGenerated) { var t = generated.transform.Find(n); if (t != null) Object.DestroyImmediate(t.gameObject); }
            foreach (var trigger in Object.FindObjectsByType<SceneTransitionTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(trigger.gameObject);
            ClearPlaza(generated);
            return scene;
        }

        // The cameras move all over the plaza and the avenue: no trees, bushes, boulders or plants stand there (the banners, torches, drums
        // and the palace stay; the forest beyond the box stays too).
        static readonly string[] Scenery = { "BlossomTree", "BroadleafTree", "Tree", "Bush", "Rock", "Stone", "Plant", "Flower", "Mushroom", "Grass", "Fern" };

        static bool InPlaza(Vector3 p) => p.x > -16f && p.x < 16f && p.z > 6f && p.z < 34.5f;

        static void ClearPlaza(GameObject generated)
        {
            if (generated == null) return;
            int removed = 0;
            foreach (Transform child in generated.transform.Cast<Transform>().ToList())
            {
                if (child.name == "Undergrowth")
                {
                    foreach (Transform plant in child.Cast<Transform>().ToList())
                        if (InPlaza(plant.position)) { Object.DestroyImmediate(plant.gameObject); removed++; }
                }
                else if (Scenery.Any(n => child.name.StartsWith(n)) && InPlaza(child.position)) { Object.DestroyImmediate(child.gameObject); removed++; }
            }
            Debug.Log("CineSetBuilder: " + removed + " plants and rocks cleared from the plaza.");
        }

        // The Prologue's dialogue canvas and runner, moved into this scene (the Prologue file is not saved, so it keeps them).
        public static DialogueRunner MoveDialogueUi(Scene target)
        {
            var prologue = EditorSceneManager.OpenScene(ProloguePath, OpenSceneMode.Additive);
            var roots = prologue.GetRootGameObjects();
            var canvas = roots.First(g => g.name == "PrologueCanvas");
            var runnerGo = roots.First(g => g.GetComponent<DialogueRunner>() != null);
            SceneManager.MoveGameObjectToScene(canvas, target);
            SceneManager.MoveGameObjectToScene(runnerGo, target);
            EditorSceneManager.CloseScene(prologue, true);
            canvas.name = "DialogueCanvas";
            runnerGo.name = "Dialogue";
            var title = canvas.transform.Find("TitleCard");
            if (title != null) Object.DestroyImmediate(title.gameObject);
            var hint = canvas.transform.Find("Hint"); var panel = canvas.transform.Find("DialoguePanel");
            if (hint != null && panel != null) hint.SetParent(panel, true);
            var prologueDirector = runnerGo.GetComponent<PrologueDirector>();
            if (prologueDirector != null) Object.DestroyImmediate(prologueDirector);
            var runner = runnerGo.GetComponent<DialogueRunner>();
            var so = new SerializedObject(runner);
            so.FindProperty("keepIllustration").boolValue = false;   // pictures only where a line has one, the 3D scene otherwise
            so.ApplyModifiedPropertiesWithoutUndo();
            return runner;
        }

        // ---------------------------------------------------------------- cast

        static AnimationClip FirstClip(string fbx) =>
            AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));

        static ActorRig.NamedClip[] ClipsFor(string walkFbx)
        {
            CutsceneGesturesImporter.Ensure();
            var list = AssetDatabase.LoadAllAssetsAtPath(GesturesFbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview"))
                .Select(c => new ActorRig.NamedClip { name = c.name, clip = c }).ToList();
            if (!string.IsNullOrEmpty(walkFbx))
            {
                var walk = FirstClip(walkFbx);
                if (walk != null) list.Add(new ActorRig.NamedClip { name = "Walk", clip = walk });
            }
            return list.ToArray();
        }

        // Heroic proportions: the gods a tenth taller than the king, Mị Nương a little shorter than her model scale (she stood out too tall beside the seated king) (heights measured on the
        // animated skeletons: the king 1.52 m head-to-foot, the gods 1.47, the princess 1.20).
        public const float GodScale = 1.10f, PrincessScale = 0.96f;

        static ActorRig AddRig(GameObject root, string walkFbx, float scale = 1f, string rest = null)
        {
            if (!Mathf.Approximately(scale, 1f))
            {
                var model = root.GetComponentInChildren<Animator>().transform;
                model.localScale *= scale;
            }
            var animator = root.GetComponentInChildren<Animator>();
            var rig = root.AddComponent<ActorRig>();
            var so = new SerializedObject(rig);
            so.FindProperty("animator").objectReferenceValue = animator;
            var clips = ClipsFor(walkFbx);
            var arr = so.FindProperty("clips");
            arr.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
            {
                arr.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue = clips[i].name;
                arr.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue = clips[i].clip;
            }
            so.FindProperty("restClip").stringValue = rest ?? "";
            so.ApplyModifiedPropertiesWithoutUndo();
            return rig;
        }

        public static ActorRig PlacePrefab(Transform parent, string prefabPath, string walkFbx, Vector3 pos, Quaternion rot, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.SetPositionAndRotation(pos, rot);
            bool princess = prefabPath == MiNuongPrefab;
            return AddRig(go, walkFbx, princess ? PrincessScale : 1f, princess ? "Poise" : null);
        }

        // A suitor: the visual part of the character's player prefab (mesh, Animator, weapon, outfit springs), no gameplay code.
        public static ActorRig PlaceSuitor(Transform parent, CharacterId id, string walkFbx, Vector3 pos, Quaternion rot, string name)
        {
            var roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>(RosterPath);
            var def = roster.Get(id);
            var go = Object.Instantiate(def.PlayerPrefab.Animator.gameObject);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.SetPositionAndRotation(pos, rot);
            return AddRig(go, walkFbx, GodScale);   // on top of the model's own scale (2.05 in the player prefab)
        }

        public static Transform Mark(Transform parent, string name, Vector3 pos, Vector3 lookAt)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent);
            Vector3 d = lookAt - pos; d.y = 0f;
            t.SetPositionAndRotation(pos, d.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(d) : Quaternion.identity);
            return t;
        }

        // ---------------------------------------------------------------- throne

        // A dais with a high-backed lacquered throne: a stepped base, a seat, arms, a tall back with a gold crest, two posts with banners.
        // The seated king's root stands on the dais top (DaisCenter + DaisTop).
        public static Transform BuildThrone(Transform parent)
        {
            Material lacquer = MapDecor.Lit("Mat_ThroneLacquer", new Color(0.50f, 0.07f, 0.06f), 0.1f, 0.65f);
            Material gold = MapDecor.Lit("Mat_ThroneGold", new Color(0.92f, 0.70f, 0.22f), 0.85f, 0.7f);
            Material wood = MapDecor.Lit("Mat_Wood", new Color(0.33f, 0.22f, 0.12f), 0f, 0.15f);
            var root = new GameObject("Throne").transform;
            root.SetParent(parent);
            root.SetPositionAndRotation(DaisCenter, Quaternion.identity);   // faces the avenue (-z)
            MapDecor.Part(PrimitiveType.Cube, "DaisLow", root, new Vector3(0f, 0.12f, 0f), new Vector3(4.6f, 0.25f, 3.4f), Quaternion.identity, wood, true);
            MapDecor.Part(PrimitiveType.Cube, "Dais", root, new Vector3(0f, 0.37f, 0.1f), new Vector3(3.6f, 0.25f, 2.6f), Quaternion.identity, lacquer, true);
            MapDecor.Part(PrimitiveType.Cube, "Trim", root, new Vector3(0f, 0.5f, 0.1f), new Vector3(3.7f, 0.04f, 2.7f), Quaternion.identity, gold);
            float y = DaisTop;
            MapDecor.Part(PrimitiveType.Cube, "Seat", root, new Vector3(0f, y + 0.14f, 0.55f), new Vector3(1.1f, 0.28f, 0.8f), Quaternion.identity, lacquer, true);
            MapDecor.Part(PrimitiveType.Cube, "SeatTrim", root, new Vector3(0f, y + 0.29f, 0.55f), new Vector3(1.14f, 0.03f, 0.84f), Quaternion.identity, gold);
            foreach (float x in new[] { -0.62f, 0.62f })
                MapDecor.Part(PrimitiveType.Cube, "Arm", root, new Vector3(x, y + 0.52f, 0.5f), new Vector3(0.14f, 0.6f, 0.8f), Quaternion.identity, lacquer);
            MapDecor.Part(PrimitiveType.Cube, "Back", root, new Vector3(0f, y + 1.15f, 0.98f), new Vector3(1.3f, 1.7f, 0.12f), Quaternion.identity, lacquer);
            MapDecor.Part(PrimitiveType.Cube, "BackGold", root, new Vector3(0f, y + 1.15f, 0.9f), new Vector3(1.0f, 1.4f, 0.03f), Quaternion.identity, gold);
            MapDecor.Part(PrimitiveType.Cylinder, "Crest", root, new Vector3(0f, y + 2.05f, 0.98f), new Vector3(1.1f, 0.05f, 1.1f), Quaternion.Euler(90f, 0f, 0f), gold);
            // two tall posts joined by a gilded crossbar, with long banners hanging from it on either side of the throne back
            Material cloth = MapDecor.Lit("Mat_ThroneBanner", new Color(0.66f, 0.09f, 0.07f), 0f, 0.2f, true);
            foreach (float x in new[] { -1.7f, 1.7f })
            {
                MapDecor.Part(PrimitiveType.Cylinder, "Post", root, new Vector3(x, y + 1.9f, 0.9f), new Vector3(0.14f, 1.9f, 0.14f), Quaternion.identity, wood);
                MapDecor.Part(PrimitiveType.Sphere, "PostCap", root, new Vector3(x, y + 3.9f, 0.9f), new Vector3(0.3f, 0.3f, 0.3f), Quaternion.identity, gold);
            }
            MapDecor.Part(PrimitiveType.Cylinder, "Crossbar", root, new Vector3(0f, y + 3.55f, 0.9f), new Vector3(0.07f, 1.7f, 0.07f), Quaternion.Euler(0f, 0f, 90f), gold);
            foreach (float x in new[] { -1.3f, 1.3f })
            {
                MapDecor.Part(PrimitiveType.Quad, "Banner", root, new Vector3(x, y + 2.7f, 0.86f), new Vector3(0.42f, 1.7f, 1f), Quaternion.identity, cloth);
                MapDecor.Part(PrimitiveType.Quad, "BannerTrim", root, new Vector3(x, y + 1.9f, 0.855f), new Vector3(0.42f, 0.1f, 1f), Quaternion.identity, gold);
            }
            return root;
        }

        // ---------------------------------------------------------------- camera, look, stage

        static VolumeProfile CinematicProfile()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath));
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, ProfilePath); }
            profile.components.Clear();
            var dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Bokeh); dof.focusDistance.Override(10f); dof.aperture.Override(3.2f); dof.focalLength.Override(50f);
            dof.active = false;
            var grade = profile.Add<ColorAdjustments>(true);
            grade.contrast.Override(14f); grade.saturation.Override(10f); grade.postExposure.Override(0.1f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f); vignette.smoothness.Override(0.45f);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.5f); bloom.threshold.Override(1.0f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static Material Puddle()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(PuddleMat);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, PuddleMat); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Combat/Tex_WaterDrop.png"));
            m.SetColor("_BaseColor", new Color(0.25f, 0.6f, 0.82f, 0.75f));
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        // The camera, the volume and the stage; returns them to wire into a director.
        public static (CinematicCamera cam, CineStage stage) BuildCameraAndStage(Transform parent)
        {
            var brain = Object.FindFirstObjectByType<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            // the map's own volumes would keep their depth of field / bloom settings: the cinematic volume is above them
            var volGo = new GameObject("CinematicVolume");
            volGo.transform.SetParent(parent);
            var volume = volGo.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 50f; volume.weight = 1f;
            volume.sharedProfile = CinematicProfile();

            var camGo = new GameObject("CinematicCamera");
            camGo.transform.SetParent(parent);
            var vcamGo = new GameObject("CM_Cinematic");
            vcamGo.transform.SetParent(camGo.transform);
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            var lens = vcam.Lens; lens.FieldOfView = 40f; lens.NearClipPlane = 0.1f; lens.FarClipPlane = 1500f; vcam.Lens = lens;
            vcam.Priority = 200;
            var cam = camGo.AddComponent<CinematicCamera>();
            var cso = new SerializedObject(cam);
            cso.FindProperty("vcam").objectReferenceValue = vcam;
            cso.FindProperty("volume").objectReferenceValue = volume;
            cso.ApplyModifiedPropertiesWithoutUndo();

            var stageGo = new GameObject("CineStage");
            stageGo.transform.SetParent(parent);
            var stage = stageGo.AddComponent<CineStage>();
            var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            var sso = new SerializedObject(stage);
            sso.FindProperty("softDot").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Combat/Mat_WaterDrop.mat");
            sso.FindProperty("ring").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Combat/Mat_WaterRipple.mat");
            sso.FindProperty("stone").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Maps/Decor/Mat_ArenaStone.mat");
            sso.FindProperty("puddle").objectReferenceValue = Puddle();
            sso.FindProperty("volume").objectReferenceValue = volume;
            var rocks = new[] { "P_fwOF_Rock_01.prefab", "P_fwOF_Rock_02.prefab", "P_fwOF_Rock_03.prefab" }
                .Select(n => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TriForge Assets/Fantasy Worlds - DEMO Content/Prefabs/" + n)).Where(p => p != null).ToArray();
            var rp = sso.FindProperty("rockPrefabs");
            rp.arraySize = rocks.Length;
            for (int i = 0; i < rocks.Length; i++) rp.GetArrayElementAtIndex(i).objectReferenceValue = rocks[i];
            sso.FindProperty("sun").objectReferenceValue = sun;
            sso.FindProperty("cam").objectReferenceValue = cam;
            sso.ApplyModifiedPropertiesWithoutUndo();
            return (cam, stage);
        }

        // ---------------------------------------------------------------- flow

        public static void AddToBuildSettings(string scenePath, string after)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == scenePath)) return;
            int i = scenes.FindIndex(s => s.path == after);
            scenes.Insert(i >= 0 ? i + 1 : scenes.Count, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
