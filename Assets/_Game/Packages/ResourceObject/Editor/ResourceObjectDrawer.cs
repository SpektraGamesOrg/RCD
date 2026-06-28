using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using SpektraGames.ResourceObject.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpektraGames.ResourceObject.Editor
{
    /// <summary>
    /// Odin drawer that gives <see cref="ResourceObject{T}"/> an AssetReference-like inspector: a single object field
    /// that only accepts assets of type T living under a Resources folder. It stores guid + Resources path (never a hard
    /// asset reference), auto-heals moved/renamed assets, and clearly flags a deleted (missing) asset.
    /// </summary>
    public class ResourceObjectDrawer<T> : OdinValueDrawer<ResourceObject<T>> where T : Object
    {
        // Persisted across repaints (Odin keeps one drawer instance per property), so a rejected drag stays visible
        // until the user assigns a valid asset.
        private string rejectionError;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var entry = ValueEntry;
            var value = entry.SmartValue;
            if (value == null)
            {
                value = new ResourceObject<T>();
                entry.SmartValue = value;
            }

            // Keep the runtime path in sync if the asset was moved/renamed since it was assigned.
            if (value.EditorSyncFromGuid())
                entry.Values.ForceMarkDirty();

            bool missing = value.EditorIsMissing();
            Object current = missing ? null : value.GetEditorAsset();
            
            // Build a fresh GUIContent every repaint instead of mutating the passed-in label: Odin reuses the same
            // label instance across repaints, so appending to it would accumulate (" [R][R][R]...").
            string baseText = label != null ? label.text : "No Name";
            GUIContent labelGUIContent = new GUIContent(baseText + " [R]",
                label != null ? label.image : null,
                label != null ? label.tooltip : null);

            EditorGUI.BeginChangeCheck();
            Object next = SirenixEditorFields.UnityObjectField(labelGUIContent, current, typeof(T), allowSceneObjects: false);
            if (EditorGUI.EndChangeCheck())
            {
                if (next == null)
                {
                    value.Clear();
                    rejectionError = null;
                    entry.Values.ForceMarkDirty();
                }
                else if (IsAssignable(next, out rejectionError))
                {
                    value.SetEditorAsset(next);
                    entry.Values.ForceMarkDirty();
                }
                // else: invalid asset -> field is left unchanged, rejectionError is shown below.
            }

            if (!string.IsNullOrEmpty(rejectionError))
            {
                SirenixEditorGUI.ErrorMessageBox(rejectionError);
            }
            else if (missing)
            {
                SirenixEditorGUI.ErrorMessageBox(
                    "Assigned resource is missing (deleted from project). Reassign it, or clear the reference.");
                if (GUILayout.Button("Clear missing reference"))
                {
                    value.Clear();
                    entry.Values.ForceMarkDirty();
                }
            }
            else if (value.IsValid)
            {
                SirenixEditorGUI.MessageBox($"Resources path: {value.ResourcesPath}", MessageType.None);
            }
            else if (!string.IsNullOrEmpty(value.Guid))
            {
                SirenixEditorGUI.ErrorMessageBox(
                    "This asset is no longer inside a 'Resources' folder, so it can't be loaded at runtime. " +
                    "Move it back under a 'Resources/' folder, or reassign.");
            }
        }

        /// <summary>Validate a candidate asset for inspector feedback: it must be a saved asset under a Resources folder.</summary>
        private static bool IsAssignable(Object asset, out string error)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
            {
                error = $"'{asset.name}' is not a saved project asset and cannot be referenced.";
                return false;
            }

            if (string.IsNullOrEmpty(ResourceObject<T>.ToResourcesPath(path)))
            {
                error = $"'{path}' is not inside a 'Resources' folder, so it can't be loaded at runtime. " +
                        "Move the asset under a 'Resources/' folder, then assign it.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
