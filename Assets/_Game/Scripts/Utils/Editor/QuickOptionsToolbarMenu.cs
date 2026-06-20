using System;
using System.Reflection;
using Sirenix.OdinValidator.Editor;
using SpektraGames.SpektraUtilities.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Utils.Editor.QuickOptionsToolbarMenu
{
    public static class QuickOptionsToolbarMenu
    {
        private const string QuickOptionsPath = "SpektraGames/QuickOptionsToolbarMenu";

        [MainToolbarElement(QuickOptionsPath, defaultDockPosition = MainToolbarDockPosition.Left)]
        public static MainToolbarElement CreateQuickOptionsMenu()
        {
            var icon = EditorGUIUtility.IconContent("d_Settings").image as Texture2D;
            var content = new MainToolbarContent("Quick Options", icon, "Quick Options Menu");

            return new MainToolbarButton(content, ShowQuickOptionsMenu);
        }

        private static void ShowQuickOptionsMenu()
        {
            GenericMenu menu = new GenericMenu();

            //menu.AddItem(new GUIContent("Scriptable Object Browser"), false, ScriptableObjectBrowserWindow.OpenWindow);

            menu.AddItem(new GUIContent("Run Custom Odin Validator"), false, RunOdinCustomValidator);

            menu.ShowAsContext();
        }

        private static void RunOdinCustomValidator()
        {
            var profile = AssetDatabase.LoadAssetAtPath<ValidationProfile>(
                "Assets/Plugins/Sirenix/Odin Validator/Editor/Profiles/CustomGameValidationProfile.asset");

            ValidationSessionEditor validationSessionEditor = OdinValidatorWindow.OpenWindow(profile);
            OdinValidatorWindow window = validationSessionEditor.Window;

            FieldInfo handleFieldInfo = typeof(OdinValidatorWindow).GetField("handle",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Default);
            if (handleFieldInfo == null)
            {
                Debug.LogError("handleFieldInfo is null");
                return;
            }

            if (handleFieldInfo.GetValue(window) == null)
            {
                Debug.LogError("handle is null");
                return;
            }

            ValidationSessionAssetHandle handle =
                handleFieldInfo.GetValue(window) as ValidationSessionAssetHandle;
            ValidationSession session = handle.Session;

            MethodInfo validateEverythingNowMethodInfo =
                typeof(ValidationSession).GetMethod("ValidateEverythingNow",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Default);
            if (validateEverythingNowMethodInfo == null)
            {
                Debug.LogError("validateEverythingNowMethodInfo is null");
                return;
            }

            try
            {
                validateEverythingNowMethodInfo.Invoke(session, new object[] { true, true });
                SceneView.RepaintAll();
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
            }
        }
    }
}