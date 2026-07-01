using UnityEditor;

namespace Events.Editor
{
    /// <summary>
    /// Keeps <see cref="EventLevelContainer"/> in sync automatically when <see cref="LevelData"/> assets are
    /// created, deleted, moved or reimported - so the designer never hand-edits the container. Complements
    /// <see cref="LevelData"/>.OnValidate (which covers in-inspector mode/number edits): this covers the cases
    /// OnValidate cannot see, especially deletions. All syncs funnel through the coalesced
    /// <see cref="EventLevelContainer.EditorSyncDeferred"/>, so a batch import triggers a single rebuild.
    /// </summary>
    public sealed class LevelDataPostprocessor : AssetPostprocessor
    {
        private const string LevelsFolder = "/Levels/";

        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (TouchesLevelData(importedAssets) ||
                TouchesLevelData(deletedAssets) ||
                TouchesLevelData(movedAssets) ||
                TouchesLevelData(movedFromAssetPaths))
            {
                EventLevelContainer.EditorSyncDeferred();
            }
        }

        private static bool TouchesLevelData(string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".asset"))
                    continue;

                // Existing assets (imported/moved): resolve the type. Deleted assets can't be loaded, so fall back
                // to the Levels folder convention.
                if (AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(LevelData) ||
                    path.Replace('\\', '/').Contains(LevelsFolder))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
