using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Utils.Editor
{
    public static class ScenesToolbarMenu
    {
        private const string ScenesMenuPath = "SpektraGames/ScenesToolbarMenu";

        [MainToolbarElement(ScenesMenuPath, defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static MainToolbarElement CreateScenesMenu()
        {
            var icon = EditorGUIUtility.IconContent("d_SceneAsset Icon").image as Texture2D;
            var content = new MainToolbarContent("Scenes", icon, "Open scenes from project");

            return new MainToolbarButton(content, ShowScenesMenu);
        }

        private static void ShowScenesMenu()
        {
            GenericMenu menu = new GenericMenu();

            Scene currentScene = SceneManager.GetActiveScene();
            string currentSceneName = currentScene.IsValid() ? currentScene.name : null;

            string scenesPath = "Assets/_Game/Scenes/";
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { scenesPath });
            List<string> paths = new List<string>();
            for (var i = 0; i < guids.Length; i++)
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));
            }

            List<(string sceneName, string scenePath)> scenes = new();

            // Add all scenes
            // for (var i = 0; i < paths.Count; i++)
            // {
            //     string pathNormalized = paths[i].Replace("\\", "/");
            //     string relativePath = pathNormalized.Replace(scenesPath, "");
            //     // Remove .unity extension
            //     if (relativePath.EndsWith(".unity"))
            //         relativePath = relativePath.Substring(0, relativePath.Length - 6);
            //
            //     scenes.Add(new ValueTuple<string, string>(relativePath, paths[i]));
            // }
            
            scenes.Add(new ValueTuple<string, string>("Main Menu", "Assets/_Game/Scenes/MainMenu.unity"));
            scenes.Add(new ValueTuple<string, string>("Game", "Assets/_Game/Scenes/Game.unity"));

            // Sort scenes by name
            //scenes.Sort((a, b) => string.Compare(a.sceneName, b.sceneName, StringComparison.Ordinal));

            // Draw scenes
            for (var i = 0; i < scenes.Count; i++)
            {
                var sceneData = scenes[i];
                string displayName = Path.GetFileNameWithoutExtension(sceneData.scenePath);
                bool isCurrentScene = displayName == currentSceneName;

                menu.AddItem(new GUIContent(sceneData.sceneName), isCurrentScene, () =>
                {
                    if (isCurrentScene)
                        return;

                    EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                    EditorSceneManager.OpenScene(sceneData.scenePath, OpenSceneMode.Single);
                });
            }

            if (scenes.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No scenes found"));
            }

            menu.ShowAsContext();
        }
    }
}