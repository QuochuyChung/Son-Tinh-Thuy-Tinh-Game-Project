using UnityEditor;
using UnityEngine;

namespace SonTinhThuyTinh.Dialogue.Editor
{
    public static class VoiceLibraryCreator
    {
        const string Path = "Assets/Resources/VoiceLibrary.asset";

        [MenuItem("Tools/Son Tinh Thuy Tinh/Create Voice Library")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<VoiceLibrary>(Path) != null)
            {
                Debug.Log("VoiceLibrary already exists at " + Path);
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<VoiceLibrary>(Path));
                return;
            }

            VoiceLibrary library = ScriptableObject.CreateInstance<VoiceLibrary>();
            library.entries = VoiceLibrary.CreateDefaults();
            System.IO.Directory.CreateDirectory("Assets/Resources");
            AssetDatabase.CreateAsset(library, Path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(library);
            Debug.Log("Created " + Path + " - edit volume/pitch per speaker there.");
        }
    }
}
