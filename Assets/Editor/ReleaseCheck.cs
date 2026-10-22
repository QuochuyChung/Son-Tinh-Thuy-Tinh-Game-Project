using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SonTinhThuyTinh.EditorTools
{
    // Command-line checks before a release build (Unity -batchmode -executeMethod ...):
    //   ReleaseCheck.Audit        opens every scene of the Build Settings and reports missing scripts, missing prefabs and empty
    //                             serialized object references on our own components; writes Logs/release_audit.txt
    //   ReleaseCheck.BuildWindows builds Builds/Windows/SonTinhThuyTinh.exe from the Build Settings scenes; writes Logs/release_build.txt
    public static class ReleaseCheck
    {
        public static void Audit()
        {
            var sb = new StringBuilder();
            int problems = 0;
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled) continue;
                Scene scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                int missingScripts = 0, missingPrefabs = 0, nullRefs = 0, objects = 0;
                var nullList = new List<string>();
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        objects++;
                        missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                        if (PrefabUtility.IsPrefabAssetMissing(t.gameObject)) missingPrefabs++;
                        foreach (MonoBehaviour mb in t.GetComponents<MonoBehaviour>())
                        {
                            if (mb == null) continue;
                            string ns = mb.GetType().Namespace ?? "";
                            if (!ns.StartsWith("SonTinhThuyTinh")) continue;
                            var so = new SerializedObject(mb);
                            SerializedProperty it = so.GetIterator();
                            for (bool enter = true; it.NextVisible(enter); enter = false)
                            {
                                if (it.propertyType != SerializedPropertyType.ObjectReference || it.name == "m_Script") continue;
                                // a reference that points to a destroyed object (the fileID is set but the object is gone)
                                if (it.objectReferenceValue == null && it.objectReferenceInstanceIDValue != 0)
                                {
                                    nullRefs++;
                                    if (nullList.Count < 8) nullList.Add(t.name + "/" + mb.GetType().Name + "." + it.name);
                                }
                            }
                        }
                    }
                sb.AppendLine($"{scene.name}: objects={objects} missingScripts={missingScripts} missingPrefabs={missingPrefabs} brokenReferences={nullRefs}" + (nullList.Count > 0 ? "  e.g. " + string.Join(", ", nullList) : ""));
                problems += missingScripts + missingPrefabs + nullRefs;
            }
            sb.AppendLine("TOTAL problems: " + problems);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/release_audit.txt", sb.ToString());
            Debug.Log("[ReleaseCheck] audit done: " + problems + " problem(s)");
        }

        public static void BuildWindows()
        {
            var scenes = new List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = "Builds/Windows/SonTinhThuyTinh.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            var sb = new StringBuilder();
            sb.AppendLine($"result={report.summary.result} errors={report.summary.totalErrors} warnings={report.summary.totalWarnings} size={report.summary.totalSize / (1024 * 1024)}MB time={report.summary.totalTime}");
            foreach (BuildStep step in report.steps)
                foreach (BuildStepMessage m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception) sb.AppendLine("ERROR: " + m.content);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/release_build.txt", sb.ToString());
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
