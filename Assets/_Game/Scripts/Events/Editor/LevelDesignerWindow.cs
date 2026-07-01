using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Events.Editor
{
    /// <summary>
    /// Level design tool for Jump Challenge / Time Trial layouts. Workflow (in the Game scene):
    ///  1. Pick (or create) a <see cref="LevelData"/> asset and press "Load Level": the tool spawns a
    ///     "[LevelEditor]" root at the world origin with the level's pieces as real, editable prefab instances.
    ///  2. Click a piece in the palette to drop it in, then move/rotate/scale/duplicate (Ctrl+D) freely with the
    ///     normal Unity gizmos - they are ordinary scene objects.
    ///  3. Press "Save Level" to write the current scene layout back into the asset.
    ///
    /// The editing pieces are STRICTLY transient: they carry <see cref="HideFlags.DontSaveInEditor"/> /
    /// <see cref="HideFlags.DontSaveInBuild"/> (never written to a saved scene or a build) and are force-removed
    /// when entering Play Mode, switching scenes, or on editor load - so they can never leak into a running or
    /// built game. Namespace is intentionally descriptive (never "Editor") per the project rules.
    /// </summary>
    public sealed class LevelDesignerWindow : EditorWindow
    {
        private const string RootPrefix = "[LevelEditor] ";
        private const string DefaultLevelFolder = "Assets/_Game/Data/Resources/Levels";
        private const string GameScenePath = "Assets/_Game/Scenes/Game.unity";

        private const HideFlags TransientFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        private const float TileSize = 96f;
        private const float TileSpacing = 8f;
        private const float LabelHeight = 26f;

        [SerializeField] private LevelData _target;
        [SerializeField] private string _search = string.Empty;
        private GameObject _levelRoot;
        private Vector2 _paletteScroll;
        private SearchField _searchField;

        private GUIStyle _tileStyle;
        private GUIStyle _headerStyle;
        private readonly List<LevelObject> _filtered = new List<LevelObject>();

        private static bool IsGameSceneActive => EditorSceneManager.GetActiveScene().path == GameScenePath;

        // -----------------------------------------------------------------
        // Global safety net: [LevelEditor] objects must NEVER reach play mode, a saved scene, or a build.
        // -----------------------------------------------------------------

        [InitializeOnLoadMethod]
        private static void RegisterGlobalCleanup()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.sceneOpening -= OnSceneOpening;
            EditorSceneManager.sceneOpening += OnSceneOpening;

            // Sweep any roots orphaned by a crash / previous session once the editor is idle.
            EditorApplication.delayCall += DestroyAllEditorRoots;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            // About to enter play mode: strip the authoring objects so they never exist at runtime.
            if (state == PlayModeStateChange.ExitingEditMode)
                DestroyAllEditorRoots();
        }

        private static void OnSceneOpening(string path, OpenSceneMode mode)
        {
            // Switching scenes: don't let the authoring objects linger into the next scene.
            DestroyAllEditorRoots();
        }

        // Destroys every "[LevelEditor]" root in all loaded scenes. Editor-only; scene scanning is fine here.
        private static void DestroyAllEditorRoots()
        {
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded)
                    continue;

                foreach (GameObject go in scene.GetRootGameObjects())
                {
                    if (go && go.name.StartsWith(RootPrefix))
                        DestroyImmediate(go);
                }
            }
        }

        [MenuItem("Tools/EventSystem/Level Designer")]
        public static void ShowWindow()
        {
            var window = GetWindow<LevelDesignerWindow>("Level Designer");
            window.minSize = new Vector2(360f, 480f);
            window.Show();
        }

        private void OnGUI()
        {
            EnsureStyles();
            ReacquireRoot(); // reattach to an existing root after a domain reload lost our reference

            DrawHeader();
            DrawLevelBar();
            DrawActionBar();

            GUILayout.Space(6f);
            DrawPalette();
        }

        // -----------------------------------------------------------------
        // Top sections
        // -----------------------------------------------------------------

        private void DrawHeader()
        {
            GUILayout.Space(6f);
            GUILayout.Label("Event Level Designer", _headerStyle);
            EditorGUILayout.HelpBox(
                "1)  In the Game scene, pick or create a Level, then press Load Level.\n" +
                "2)  Click a piece below to add it, then move / rotate / scale / duplicate it freely in the Scene.\n" +
                "3)  Press Save Level to store the layout back into the asset.",
                MessageType.None);
            GUILayout.Space(4f);
        }

        private void DrawLevelBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _target = (LevelData)EditorGUILayout.ObjectField("Level", _target, typeof(LevelData), false);
                if (EditorGUI.EndChangeCheck() && _levelRoot)
                    ClearScene(); // switching levels invalidates the loaded layout

                if (GUILayout.Button("New", GUILayout.Width(52f), GUILayout.Height(18f)))
                    CreateNewLevel();
            }
        }

        private void DrawActionBar()
        {
            if (!IsGameSceneActive)
            {
                EditorGUILayout.HelpBox("Level editing happens in the Game scene. Open it to load a level.", MessageType.Warning);
                if (GUILayout.Button("Open Game Scene", GUILayout.Height(24f)))
                    OpenGameScene();
                GUILayout.Space(4f);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_target || _levelRoot || !IsGameSceneActive))
                {
                    if (GUILayout.Button(new GUIContent("Load Level", "Spawn the level's pieces into the Game scene for editing."),
                            GUILayout.Height(30f)))
                        LoadLevel();
                }

                using (new EditorGUI.DisabledScope(!_levelRoot || !_target))
                {
                    if (GUILayout.Button(new GUIContent("Save Level", "Write the current scene layout back into the Level asset."),
                            GUILayout.Height(30f)))
                        SaveLevel();
                }

                using (new EditorGUI.DisabledScope(!_levelRoot))
                {
                    if (GUILayout.Button(new GUIContent("Clear Scene", "Remove the editing pieces from the scene (does not save)."),
                            GUILayout.Height(30f)))
                        ClearScene();
                }
            }
        }

        // -----------------------------------------------------------------
        // Palette (searchable 3D thumbnail grid)
        // -----------------------------------------------------------------

        private void DrawPalette()
        {
            EventObjectContainer container = EventObjectContainer.Instance;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Palette", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                _searchField ??= new SearchField();
                _search = _searchField.OnToolbarGUI(_search ?? string.Empty, GUILayout.MaxWidth(220f));
            }

            if (!container || container.Presets.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No spawnable pieces registered. Create an 'EventObjectContainer' asset in a Resources folder " +
                    "and use its 'Sync From Folder' button (or add prefabs with a LevelObject to that folder).",
                    MessageType.Warning);
                return;
            }

            if (!_levelRoot)
                EditorGUILayout.HelpBox("Load a level first to place pieces.", MessageType.Info);

            BuildFilteredList(container);

            using (new EditorGUI.DisabledScope(!_levelRoot))
            using (var scroll = new EditorGUILayout.ScrollViewScope(_paletteScroll))
            {
                _paletteScroll = scroll.scrollPosition;

                if (_filtered.Count == 0)
                {
                    EditorGUILayout.LabelField($"No pieces match \"{_search}\".", EditorStyles.centeredGreyMiniLabel);
                    return;
                }

                float viewWidth = EditorGUIUtility.currentViewWidth - 28f;
                int columns = Mathf.Max(1, Mathf.FloorToInt((viewWidth + TileSpacing) / (TileSize + TileSpacing)));
                int rows = Mathf.CeilToInt(_filtered.Count / (float)columns);

                float totalHeight = rows * (TileSize + LabelHeight + TileSpacing) + TileSpacing;
                Rect grid = GUILayoutUtility.GetRect(viewWidth, totalHeight, GUILayout.ExpandWidth(true));

                bool anyPreviewPending = false;
                for (int i = 0; i < _filtered.Count; i++)
                {
                    int col = i % columns;
                    int row = i / columns;
                    var cell = new Rect(
                        grid.x + TileSpacing + col * (TileSize + TileSpacing),
                        grid.y + TileSpacing + row * (TileSize + LabelHeight + TileSpacing),
                        TileSize,
                        TileSize + LabelHeight);

                    anyPreviewPending |= DrawTile(cell, _filtered[i]);
                }

                // Asset previews render asynchronously: keep repainting until they've all finished.
                if (anyPreviewPending)
                    Repaint();
            }
        }

        // Returns true if this tile is still waiting on its 3D preview to render.
        private bool DrawTile(Rect cell, LevelObject preset)
        {
            Texture preview = AssetPreview.GetAssetPreview(preset.gameObject);
            bool pending = false;
            if (!preview)
            {
                preview = AssetPreview.GetMiniThumbnail(preset.gameObject); // instant placeholder while the 3D preview loads
                pending = true;
            }

            var content = new GUIContent(preset.name, preview, $"{preset.name}\nRole: {preset.Role}\n\nClick to add to the level.");
            if (GUI.Button(cell, content, _tileStyle))
                PlacePiece(preset);

            return pending;
        }

        private void BuildFilteredList(EventObjectContainer container)
        {
            _filtered.Clear();
            IReadOnlyList<LevelObject> presets = container.Presets;
            bool hasSearch = !string.IsNullOrEmpty(_search);

            for (int i = 0; i < presets.Count; i++)
            {
                LevelObject preset = presets[i];
                if (!preset)
                    continue;

                if (hasSearch && preset.name.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                _filtered.Add(preset);
            }
        }

        private void EnsureStyles()
        {
            _tileStyle ??= new GUIStyle(GUI.skin.button)
            {
                imagePosition = ImagePosition.ImageAbove,
                alignment = TextAnchor.LowerCenter,
                wordWrap = true,
                fontSize = 10,
                padding = new RectOffset(4, 4, 6, 6),
            };

            _headerStyle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        }

        // -----------------------------------------------------------------
        // Actions
        // -----------------------------------------------------------------

        private static void OpenGameScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        }

        private void CreateNewLevel()
        {
            // Ask which mode (DisplayDialogComplex: 0 = first, 1 = second/Cancel, 2 = third).
            int choice = EditorUtility.DisplayDialogComplex(
                "New Level", "Which event type is this level?", "Jump Challenge", "Cancel", "Time Trial");
            if (choice == 1)
                return;

            Events.EventType type = choice == 0 ? Events.EventType.JumpChallenge : Events.EventType.TimeTrial;

            if (!AssetDatabase.IsValidFolder(DefaultLevelFolder))
                CreateFolders(DefaultLevelFolder);

            // Auto level number = next in that mode; name the asset to match.
            int number = (EventLevelContainer.Instance ? EventLevelContainer.Instance.GetLevelCount(type) : 0) + 1;
            string modeName = type == Events.EventType.JumpChallenge ? "Jump" : "TimeTrial";
            string path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultLevelFolder}/{modeName}_{number:00}.asset");

            var level = CreateInstance<LevelData>();
            AssetDatabase.CreateAsset(level, path);
            level.EditorSetEventType(type);
            level.EditorSetLevelNumber(number);
            AssetDatabase.SaveAssets();

            // Add it to the container immediately (and normalize ordering/numbering).
            EventLevelContainer.EditorSync();

            _target = level;
            EditorGUIUtility.PingObject(level);
            Selection.activeObject = level;
        }

        private void LoadLevel()
        {
            if (!_target)
                return;

            if (!IsGameSceneActive)
            {
                EditorUtility.DisplayDialog("Level Designer",
                    "Open the Game scene (Assets/_Game/Scenes/Game.unity) before loading a level.", "OK");
                return;
            }

            ClearScene();

            _levelRoot = new GameObject(RootPrefix + _target.name);
            _levelRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            MarkTransient(_levelRoot);
            Undo.RegisterCreatedObjectUndo(_levelRoot, "Load Level");

            IReadOnlyList<LevelPlacement> placements = _target.Placements;
            for (int i = 0; i < placements.Count; i++)
            {
                LevelPlacement placement = placements[i];
                if (placement == null || !placement.Prefab)
                    continue;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(placement.Prefab.gameObject);
                instance.transform.SetParent(_levelRoot.transform, true);
                instance.transform.SetPositionAndRotation(placement.WorldPosition, placement.WorldRotation);
                instance.transform.localScale = placement.WorldScale;
                MarkTransient(instance);
                Undo.RegisterCreatedObjectUndo(instance, "Load Level Piece");
            }

            Selection.activeGameObject = _levelRoot;
            SceneView.FrameLastActiveSceneView();
        }

        private void PlacePiece(LevelObject preset)
        {
            if (!_levelRoot || !preset)
                return;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(preset.gameObject);
            instance.transform.SetParent(_levelRoot.transform, false);

            // Drop new pieces at the scene-view pivot (in the designer's view).
            SceneView view = SceneView.lastActiveSceneView;
            instance.transform.position = view ? view.pivot : Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            MarkTransient(instance);
            Undo.RegisterCreatedObjectUndo(instance, "Place Level Piece");
            Selection.activeGameObject = instance;
        }

        private void SaveLevel()
        {
            if (!_levelRoot || !_target)
                return;

            var placements = new List<LevelPlacement>();
            Transform root = _levelRoot.transform;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                var levelObject = child.GetComponent<LevelObject>();
                if (!levelObject)
                {
                    Debug.LogError($"[LevelDesigner] Child '{child.name}' has no LevelObject and was skipped.", child);
                    continue;
                }

                var sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(levelObject) as LevelObject;
                if (!sourcePrefab)
                {
                    Debug.LogError($"[LevelDesigner] Child '{child.name}' is not a prefab instance and was skipped.", child);
                    continue;
                }

                // Store WORLD transform so play spawns each piece exactly where it sits in the editor.
                placements.Add(new LevelPlacement(
                    sourcePrefab,
                    child.position,
                    child.eulerAngles,
                    child.lossyScale));
            }

            _target.EditorSetPlacements(placements);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LevelDesigner] Saved {placements.Count} piece(s) into '{_target.name}'.", _target);
        }

        private void ClearScene()
        {
            // Remove every editor root (defensive: also clears any stragglers), then drop our reference.
            DestroyAllEditorRoots();
            _levelRoot = null;
        }

        // Reattach to an existing "[LevelEditor]" root when our reference was lost to a domain reload.
        private void ReacquireRoot()
        {
            if (_levelRoot)
                return;

            Scene active = EditorSceneManager.GetActiveScene();
            if (!active.isLoaded)
                return;

            foreach (GameObject go in active.GetRootGameObjects())
            {
                if (go && go.name.StartsWith(RootPrefix))
                {
                    _levelRoot = go;
                    return;
                }
            }
        }

        private static void MarkTransient(GameObject go)
        {
            // Never persisted to a saved scene or a build; the play/scene-change hooks destroy them outright.
            go.hideFlags = TransientFlags;
        }

        private static void CreateFolders(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
