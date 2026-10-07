using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    public static class RunGameFromStart
    {
        [MenuItem("Tools/Son Tinh Thuy Tinh/Run Game From Start (MainMenu) %#r")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return;
            }

            string scenePath = "Assets/Scenes/MainMenu.unity";
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                EditorApplication.isPlaying = true;
            }
        }
    }
}
