using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace SonTinhThuyTinh.Dialogue.Editor
{
    public static class VoiceGenRunner
    {
        const string LibraryPath = "Assets/Resources/VoiceLibrary.asset";

        public static VoiceGenProgress Active { get; private set; }
        public static bool IsRunning => Active != null && !Active.Finished;

        static Action finishedCallback;

        [MenuItem("Tools/Voice/Regenerate Missing Clips")]
        static void MenuMissing()
        {
            Start(LoadLibrary(), null, false);
        }

        [MenuItem("Tools/Voice/Regenerate All Clips (Force)")]
        static void MenuForce()
        {
            Start(LoadLibrary(), null, true);
        }

        [MenuItem("Tools/Voice/Refresh Voice List")]
        static void MenuRefreshVoices()
        {
            if (VoiceCatalog.Refresh())
                Debug.Log("[Voice] voice list refreshed: " + VoiceCatalog.Count + " voices");
            else
                Debug.LogWarning("[Voice] voice list refresh failed: " + VoiceCatalog.LastError);
        }

        static VoiceLibrary LoadLibrary()
        {
            return AssetDatabase.LoadAssetAtPath<VoiceLibrary>(LibraryPath);
        }

        public static void Start(VoiceLibrary lib, string onlyFolder, bool force, Action onFinished = null)
        {
            if (IsRunning)
            {
                EditorUtility.DisplayDialog("Voice generation", "A generation run is already in progress.", "OK");
                return;
            }

            string validation = VoiceGenerator.Validate(lib, onlyFolder);
            if (validation != null)
            {
                EditorUtility.DisplayDialog("Voice generation - invalid settings", validation, "OK");
                return;
            }

            if (!VoiceGenerator.TryBuildJobs(lib, onlyFolder, out List<VoiceJob> jobs, out int voiced,
                    out string unknown, out string error))
            {
                EditorUtility.DisplayDialog("Voice generation", error ?? "Could not build jobs.", "OK");
                return;
            }

            if (jobs.Count == 0)
            {
                EditorUtility.DisplayDialog("Voice generation", "Nothing to do: 0 clips match this filter.", "OK");
                return;
            }

            var progress = new VoiceGenProgress
            {
                Total = jobs.Count,
                Voiced = voiced,
                OnlyFolder = onlyFolder,
                Force = force,
                Unknown = unknown,
                Cts = new CancellationTokenSource(),
            };
            Active = progress;
            finishedCallback = onFinished;
            EditorApplication.update += Pump;

            Debug.Log("[Voice] generating " + jobs.Count + " clips (force=" + force
                + ", filter=" + (onlyFolder ?? "all") + ", " + voiced + " voiced lines)...");
            _ = Task.Run(() => VoiceGenerator.RunAsync(progress, jobs));
        }

        static void Pump()
        {
            VoiceGenProgress progress = Active;
            if (progress == null)
            {
                EditorApplication.update -= Pump;
                return;
            }
            if (progress.Finished)
            {
                Finish();
                return;
            }

            int done = Volatile.Read(ref progress.Done);
            string label = Mathf.RoundToInt(progress.Fraction * 100f) + "%  (" + done + "/" + progress.Total
                + ")  " + progress.CurrentLabel;

            if (progress.CancelRequested)
            {
                EditorUtility.DisplayProgressBar("Generating voice clips (cancelling...)", label, progress.Fraction);
                return;
            }
            if (EditorUtility.DisplayCancelableProgressBar("Generating voice clips", label, progress.Fraction))
            {
                progress.CancelRequested = true;
                try
                {
                    progress.Cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        static void Finish()
        {
            VoiceGenProgress progress = Active;
            EditorApplication.update -= Pump;
            Active = null;
            EditorUtility.ClearProgressBar();

            try
            {
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Voice] AssetDatabase.Refresh failed: " + e.Message);
            }

            Debug.Log(progress.BuildSummary());
            if (!string.IsNullOrEmpty(progress.Unknown))
                Debug.LogWarning("[Voice] Speakers without voice (skipped): " + progress.Unknown);

            List<string> failures;
            lock (progress.Failures)
                failures = new List<string>(progress.Failures);
            if (failures.Count > 0)
                Debug.LogWarning("[Voice] " + failures.Count + " failure(s):\n" + string.Join("\n", failures));

            try
            {
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
            catch
            {
            }

            try
            {
                progress.Cts?.Dispose();
            }
            catch
            {
            }

            Action callback = finishedCallback;
            finishedCallback = null;
            try
            {
                callback?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    [CustomEditor(typeof(VoiceLibrary))]
    public class VoiceLibraryEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var library = (VoiceLibrary)target;
            serializedObject.Update();

            VoiceCatalog.EnsureLoaded();

            EditorGUILayout.LabelField("Voice generation (edge-tts)", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                string state = VoiceCatalog.IsLoaded
                    ? "Voice list: " + VoiceCatalog.Count + " voices"
                    : "Voice list unavailable" + (string.IsNullOrEmpty(VoiceCatalog.LastError) ? "" : " (" + VoiceCatalog.LastError + ")");
                EditorGUILayout.LabelField(state, VoiceCatalog.IsLoaded ? EditorStyles.miniLabel : EditorStyles.miniLabel);
                if (GUILayout.Button("Refresh", GUILayout.Width(68)))
                {
                    VoiceCatalog.Refresh();
                    Repaint();
                }
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("masterVolume"));
            EditorGUILayout.Space(4);

            SerializedProperty entries = serializedObject.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
                DrawEntry(library, i, entries.GetArrayElementAtIndex(i));

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(VoiceGenRunner.IsRunning))
            {
                if (GUILayout.Button("Regenerate missing clips (all speakers)"))
                    VoiceGenRunner.Start(library, null, false);
                if (GUILayout.Button("Regenerate ALL clips (force)"))
                    VoiceGenRunner.Start(library, null, true);
            }

            VoiceGenProgress progress = VoiceGenRunner.Active;
            if (progress != null && !progress.Finished)
            {
                int done = Volatile.Read(ref progress.Done);
                EditorGUILayout.HelpBox(
                    "Running: " + done + "/" + progress.Total
                    + "  new=" + Volatile.Read(ref progress.Made)
                    + "  skipped=" + Volatile.Read(ref progress.Skipped)
                    + "  failed=" + Volatile.Read(ref progress.Failed), MessageType.Info);
            }
        }

        void DrawEntry(VoiceLibrary library, int index, SerializedProperty entry)
        {
            SerializedProperty speaker = entry.FindPropertyRelative("speaker");
            SerializedProperty folder = entry.FindPropertyRelative("folder");
            SerializedProperty voice = entry.FindPropertyRelative("voice");
            SerializedProperty rate = entry.FindPropertyRelative("rate");
            SerializedProperty volume = entry.FindPropertyRelative("volume");
            SerializedProperty pitch = entry.FindPropertyRelative("pitch");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string title = string.IsNullOrEmpty(speaker.stringValue) ? "(unnamed)" : speaker.stringValue;
                    EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                    using (new EditorGUI.DisabledScope(VoiceGenRunner.IsRunning || string.IsNullOrEmpty(folder.stringValue)))
                    {
                        if (GUILayout.Button("Regen", GUILayout.Width(60)))
                            VoiceGenRunner.Start(library, folder.stringValue, true);
                    }
                }

                EditorGUILayout.PropertyField(folder);
                DrawVoiceField(library, index, voice);

                if (!VoiceCatalog.IsLoaded)
                {
                    voice.stringValue = EditorGUILayout.TextField("Voice (manual)", voice.stringValue);
                }
                else
                {
                    VoiceInfo info = VoiceCatalog.Find(voice.stringValue);
                    if (info != null)
                        EditorGUILayout.LabelField(info.FriendlyName, EditorStyles.miniLabel);
                    else if (!string.IsNullOrEmpty(voice.stringValue))
                        EditorGUILayout.HelpBox("Voice \"" + voice.stringValue + "\" is not in the fetched list — regeneration may fail.", MessageType.Warning);
                }

                EditorGUILayout.PropertyField(rate);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("Volume / Pitch");
                    volume.floatValue = EditorGUILayout.FloatField(volume.floatValue, GUILayout.Width(60));
                    pitch.floatValue = EditorGUILayout.FloatField(pitch.floatValue, GUILayout.Width(60));
                }
            }
        }

        void DrawVoiceField(VoiceLibrary library, int index, SerializedProperty voice)
        {
            string current = voice.stringValue;
            string label = string.IsNullOrEmpty(current) ? "Select voice..." : current;

            if (!VoiceCatalog.IsLoaded)
                return;

            bool known = VoiceCatalog.Contains(current);
            Color old = GUI.backgroundColor;
            if (!known && !string.IsNullOrEmpty(current))
                GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);

            if (GUILayout.Button(new GUIContent(label, "Click to choose an edge-tts voice"), EditorStyles.popup))
            {
                int pickedIndex = index;
                var picker = new VoicePickerDropdown(picked =>
                {
                    if (library == null || pickedIndex >= library.entries.Length)
                        return;
                    Undo.RecordObject(library, "Set voice");
                    library.entries[pickedIndex].voice = picked;
                    EditorUtility.SetDirty(library);
                });
                picker.Show(GUILayoutUtility.GetLastRect());
            }

            GUI.backgroundColor = old;
        }
    }
}
