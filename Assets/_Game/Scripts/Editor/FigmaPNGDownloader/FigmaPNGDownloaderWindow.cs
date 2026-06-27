#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace FigmaPNGDownloader
{
    public enum FigmaExportScale
    {
        [InspectorName("0.5x")] Scale_0_5x,
        [InspectorName("0.75x")] Scale_0_75x,
        [InspectorName("1x")] Scale_1x,
        [InspectorName("1.5x")] Scale_1_5x,
        [InspectorName("2x")] Scale_2x,
        [InspectorName("3x")] Scale_3x,
        [InspectorName("4x")] Scale_4x,
    }

    public class FigmaImageEntry
    {
        public string NodeId;
        public string Name;
        public string FileName;
        public string NodeType;
        public bool Download = true;
        public Texture2D PreviewTexture;

        public bool IsContainer =>
            NodeType == "GROUP" || NodeType == "FRAME" || NodeType == "COMPONENT" ||
            NodeType == "COMPONENT_SET" || NodeType == "INSTANCE" || NodeType == "SECTION";
    }

    public class FigmaPNGDownloaderWindow : EditorWindow
    {
        private const string ACCESS_TOKEN = "figd_9FZhJeyvytt1wlGJYNkmC3Szcr9-8bQd-QpihHvZ";

        private static readonly string[] ScaleDisplayNames = { "0.5x", "0.75x", "1x", "1.5x", "2x", "3x", "4x" };
        private static readonly float[] ScaleValues = { 0.5f, 0.75f, 1f, 1.5f, 2f, 3f, 4f };

        private string _figmaUrl = "";
        private string _previousUrl = "";

        private string _fileKey = "";
        private string _nodeId = "";
        private string _fileName = "";

        private FigmaExportScale _exportScale = FigmaExportScale.Scale_1x;
        private bool _useAbsoluteBounds = true;

        private bool _isUrlValid;
        private string _urlWarning = "";

        private List<FigmaImageEntry> _imageEntries = new List<FigmaImageEntry>();
        private bool _hasChildren;
        private string _searchFilter = "";
        private bool _downloadGroupAsSinglePng;
        private string _groupFileName = "";

        private string _activeNodeId = "";
        private string _activeNodeName = "";
        private List<BreadcrumbEntry> _breadcrumb = new List<BreadcrumbEntry>();

        private List<FigmaNodeInfo> _pendingChildren = new List<FigmaNodeInfo>();
        private bool _childrenExpanded;

        private bool _isFetchingPreview;
        private bool _isSaving;
        private bool _isBusy;
        private string _statusMessage = "";
        private MessageType _statusType = MessageType.None;

        private string _savePath = "Assets/_Game/Art/UI";
        private string _textureName = "FigmaExport";

        private Vector2 _scrollPos;
        private CancellationTokenSource _cts;

        private static readonly Vector2 MinWindowSize = new(480f, 640f);

        private static readonly Color HeaderColor = new(0.18f, 0.52f, 0.87f, 1f);
        private static readonly Color ValidColor = new(0.3f, 0.78f, 0.47f, 1f);

        private GUIStyle _headerStyle;
        private GUIStyle _sectionHeaderStyle;
        private GUIStyle _labelBoldStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _urlFieldStyle;
        private bool _stylesInitialized;

        [MenuItem("Tools/Figma PNG Downloader")]
        public static void ShowWindow()
        {
            var window = GetWindow<FigmaPNGDownloaderWindow>(false, "Figma PNG Downloader", true);
            window.minSize = MinWindowSize;
            window.Show();
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                padding = new RectOffset(0, 0, 10, 10)
            };

            _sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.8f, 0.85f, 0.95f) },
                padding = new RectOffset(4, 0, 2, 2)
            };

            _labelBoldStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.75f, 0.75f, 0.75f) }
            };

            _valueStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(0.55f, 0.85f, 1f) },
                wordWrap = true
            };

            _urlFieldStyle = new GUIStyle(EditorStyles.textField)
            {
                fontSize = 12,
                fixedHeight = 24,
                padding = new RectOffset(6, 6, 4, 4)
            };

            _stylesInitialized = true;
        }

        // ----- GUI -----

        private void OnGUI()
        {
            InitStyles();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawHeader();
            GUILayout.Space(8);
            DrawUrlSection();
            GUILayout.Space(6);

            if (_isUrlValid)
            {
                DrawParsedInfo();
                GUILayout.Space(6);
                DrawPreviewSection();
                GUILayout.Space(6);
                DrawSaveSection();
            }

            DrawStatusBar();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            var headerRect = GUILayoutUtility.GetRect(GUIContent.none, _headerStyle, GUILayout.Height(48));
            EditorGUI.DrawRect(headerRect, HeaderColor);
            GUI.Label(headerRect, "Figma PNG Downloader", _headerStyle);
        }

        private void DrawUrlSection()
        {
            BeginSection("Figma URL");

            EditorGUILayout.LabelField("Paste the Figma node URL below:", EditorStyles.miniLabel);
            GUILayout.Space(4);

            EditorGUI.BeginChangeCheck();
            _figmaUrl = EditorGUILayout.TextField(_figmaUrl, _urlFieldStyle);
            if (EditorGUI.EndChangeCheck() || _figmaUrl != _previousUrl)
            {
                _previousUrl = _figmaUrl;
                ParseUrl();
            }

            if (!string.IsNullOrEmpty(_urlWarning))
            {
                GUILayout.Space(4);
                EditorGUILayout.HelpBox(_urlWarning, MessageType.Warning);
            }

            if (_isUrlValid)
            {
                GUILayout.Space(2);
                var checkRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.label, GUILayout.Height(20));
                var oldColor = GUI.color;
                GUI.color = ValidColor;
                GUI.Label(checkRect, "  \u2713  URL is valid and ready to use");
                GUI.color = oldColor;
            }

            EndSection();
        }

        private void DrawParsedInfo()
        {
            BeginSection("Parsed Properties");

            DrawInfoRow("File Key", _fileKey);
            DrawInfoRow("Node ID", _nodeId);
            if (!string.IsNullOrEmpty(_fileName))
                DrawInfoRow("File Name", _fileName);

            EndSection();

            BeginSection("Export Options");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Scale", _labelBoldStyle, GUILayout.Width(120));
            _exportScale = (FigmaExportScale)EditorGUILayout.Popup((int)_exportScale, ScaleDisplayNames);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Absolute Bounds", _labelBoldStyle, GUILayout.Width(120));
            _useAbsoluteBounds = EditorGUILayout.Toggle(_useAbsoluteBounds);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(2);
            EditorGUILayout.LabelField("Preview fetches at 1x for speed. Full scale is applied on save.", EditorStyles.miniLabel);
            GUILayout.Space(4);

            GUI.enabled = !_isBusy;
            if (GUILayout.Button(_isFetchingPreview ? "Fetching Preview..." : "Fetch Preview (1x)", GUILayout.Height(30)))
            {
                FetchPreview();
            }
            GUI.enabled = true;

            EndSection();
        }

        private void DrawInfoRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, _labelBoldStyle, GUILayout.Width(80));
            EditorGUILayout.LabelField(value, _valueStyle);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBreadcrumb()
        {
            if (_breadcrumb.Count == 0) return;

            EditorGUILayout.BeginHorizontal();

            GUI.enabled = !_isBusy;
            for (int i = 0; i < _breadcrumb.Count; i++)
            {
                var label = !string.IsNullOrEmpty(_breadcrumb[i].Name) ? _breadcrumb[i].Name : _breadcrumb[i].NodeId;
                if (GUILayout.Button(label, EditorStyles.miniButton))
                {
                    DrillBack(i);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.LabelField(">", GUILayout.Width(12));
            }
            GUI.enabled = true;

            var currentLabel = !string.IsNullOrEmpty(_activeNodeName) ? _activeNodeName : _activeNodeId;
            EditorGUILayout.LabelField(currentLabel, _labelBoldStyle);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        private void DrawPreviewSection()
        {
            bool hasEntries = _imageEntries != null && _imageEntries.Count > 0;
            bool hasPendingChildren = _hasChildren && !_childrenExpanded && _pendingChildren != null && _pendingChildren.Count > 0;

            if (!hasEntries && !hasPendingChildren) return;

            bool isMulti = hasEntries && _imageEntries.Count > 1;

            var selectedCount = 0;
            if (hasEntries)
                foreach (var e in _imageEntries)
                    if (e.Download)
                        selectedCount++;

            BeginSection(isMulti
                ? "Preview  (" + selectedCount + "/" + _imageEntries.Count + " selected)"
                : "Preview");

            DrawBreadcrumb();

            if (isMulti)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select All", EditorStyles.miniButtonLeft))
                {
                    foreach (var e in _imageEntries) e.Download = true;
                }
                if (GUILayout.Button("Deselect All", EditorStyles.miniButtonRight))
                {
                    foreach (var e in _imageEntries) e.Download = false;
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(4);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Search", _labelBoldStyle, GUILayout.Width(50));
                _searchFilter = EditorGUILayout.TextField(_searchFilter);
                if (!string.IsNullOrEmpty(_searchFilter) && GUILayout.Button("X", GUILayout.Width(22)))
                {
                    _searchFilter = "";
                    GUI.FocusControl(null);
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(4);
            }

            float maxWidth = position.width - 50;
            float maxHeight = isMulti ? 80 : 100;
            bool hasSearch = !string.IsNullOrEmpty(_searchFilter);
            int visibleCount = 0;
            string drillTargetNodeId = null;
            string drillTargetName = null;
            bool shouldExpandChildren = false;
            bool shouldCollapseChildren = false;

            bool shouldFetchPreviews = false;

            if (hasEntries)
                foreach (var entry in _imageEntries)
                {
                    if (hasSearch && entry.FileName.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) < 0
                                  && entry.Name.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    visibleCount++;

                    EditorGUILayout.BeginHorizontal();
                    if (isMulti)
                    {
                        entry.Download = EditorGUILayout.Toggle(entry.Download, GUILayout.Width(18));
                    }
                    entry.FileName = EditorGUILayout.TextField(entry.FileName);
                    if (entry.IsContainer)
                    {
                        if (GUILayout.Button("Drill In >", EditorStyles.miniButton, GUILayout.Width(70)))
                        {
                            drillTargetNodeId = entry.NodeId;
                            drillTargetName = entry.Name;
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    if (entry.PreviewTexture != null)
                    {
                        float aspect = (float)entry.PreviewTexture.width / entry.PreviewTexture.height;
                        float drawWidth = Mathf.Min(maxWidth, entry.PreviewTexture.width);
                        float drawHeight = drawWidth / aspect;
                        if (drawHeight > maxHeight)
                        {
                            drawHeight = maxHeight;
                            drawWidth = drawHeight * aspect;
                        }

                        var infoText = entry.PreviewTexture.width + " x " + entry.PreviewTexture.height + " px"
                                       + (entry.IsContainer ? "  [" + entry.NodeType + "]" : "");
                        EditorGUILayout.LabelField(infoText, EditorStyles.centeredGreyMiniLabel);
                        GUILayout.Space(2);

                        var previewRect = GUILayoutUtility.GetRect(drawWidth, drawHeight);
                        previewRect.x = (position.width - drawWidth) / 2f;
                        previewRect.width = drawWidth;

                        DrawCheckerboard(previewRect, 10);
                        GUI.DrawTexture(previewRect, entry.PreviewTexture, ScaleMode.ScaleToFit);
                    }
                    else
                    {
                        var typeLabel = !string.IsNullOrEmpty(entry.NodeType) ? "  [" + entry.NodeType + "]" : "";
                        EditorGUILayout.LabelField("No preview" + typeLabel, EditorStyles.centeredGreyMiniLabel);
                    }

                    if (isMulti)
                    {
                        GUILayout.Space(4);
                        var separatorRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1));
                        EditorGUI.DrawRect(separatorRect, new Color(0.4f, 0.4f, 0.4f, 0.6f));
                        GUILayout.Space(4);
                    }
                    else
                    {
                        GUILayout.Space(6);
                    }
                }

            if (hasSearch && visibleCount == 0)
            {
                EditorGUILayout.LabelField("No results matching \"" + _searchFilter + "\"", EditorStyles.centeredGreyMiniLabel);
            }

            if (_childrenExpanded && hasEntries)
            {
                GUILayout.Space(4);
                GUI.enabled = !_isBusy;
                if (GUILayout.Button("Fetch Selected Previews", GUILayout.Height(26)))
                {
                    shouldFetchPreviews = true;
                }
                GUI.enabled = true;

                if (_imageEntries.Count > 1)
                {
                    GUILayout.Space(6);
                    var groupSepRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1));
                    EditorGUI.DrawRect(groupSepRect, new Color(0.3f, 0.7f, 0.4f, 0.6f));
                    GUILayout.Space(4);

                    _downloadGroupAsSinglePng = EditorGUILayout.ToggleLeft(
                        "Download Group as Single PNG (\"" + _activeNodeName + "\")",
                        _downloadGroupAsSinglePng);

                    if (_downloadGroupAsSinglePng)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Group File Name", _labelBoldStyle, GUILayout.Width(120));
                        _groupFileName = EditorGUILayout.TextField(_groupFileName);
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }

            if (_hasChildren && !_childrenExpanded && _pendingChildren != null && _pendingChildren.Count > 0)
            {
                GUILayout.Space(4);
                var separatorRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1));
                EditorGUI.DrawRect(separatorRect, new Color(0.4f, 0.4f, 0.4f, 0.6f));
                GUILayout.Space(4);

                EditorGUILayout.LabelField(
                    "This node has " + _pendingChildren.Count + " children.",
                    EditorStyles.centeredGreyMiniLabel);
                GUILayout.Space(2);
                GUI.enabled = !_isBusy;
                if (GUILayout.Button("Expand " + _pendingChildren.Count + " Children", GUILayout.Height(26)))
                {
                    shouldExpandChildren = true;
                }
                GUI.enabled = true;
            }

            if (_hasChildren && _childrenExpanded)
            {
                GUILayout.Space(4);
                GUI.enabled = !_isBusy;
                if (GUILayout.Button("Collapse Children", EditorStyles.miniButton))
                {
                    shouldCollapseChildren = true;
                }
                GUI.enabled = true;
            }

            EndSection();

            if (drillTargetNodeId != null)
            {
                DrillInto(drillTargetNodeId, drillTargetName);
            }
            else if (shouldExpandChildren)
            {
                ExpandChildren();
            }
            else if (shouldFetchPreviews)
            {
                FetchSelectedPreviews();
            }
            else if (shouldCollapseChildren)
            {
                ClearEntries();
                _childrenExpanded = false;
                _hasChildren = true;
                _statusMessage = "Node has " + _pendingChildren.Count
                                             + " children. Click 'Expand Children' to view them.";
                _statusType = MessageType.Info;
                Repaint();
            }
        }

        private void DrawCheckerboard(Rect rect, int cellSize)
        {
            var lightGray = new Color(0.35f, 0.35f, 0.35f, 1f);
            var darkGray = new Color(0.25f, 0.25f, 0.25f, 1f);
            EditorGUI.DrawRect(rect, darkGray);

            int cols = Mathf.CeilToInt(rect.width / cellSize);
            int rows = Mathf.CeilToInt(rect.height / cellSize);
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    if ((x + y) % 2 == 0) continue;
                    var cellRect = new Rect(
                        rect.x + x * cellSize,
                        rect.y + y * cellSize,
                        Mathf.Min(cellSize, rect.xMax - (rect.x + x * cellSize)),
                        Mathf.Min(cellSize, rect.yMax - (rect.y + y * cellSize))
                    );
                    EditorGUI.DrawRect(cellRect, lightGray);
                }
            }
        }

        private void DrawSaveSection()
        {
            if (_imageEntries == null || _imageEntries.Count == 0) return;

            BeginSection("Save to Project");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Save Path", _labelBoldStyle, GUILayout.Width(100));
            _savePath = EditorGUILayout.TextField(_savePath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                var selected = EditorUtility.OpenFolderPanel("Select Save Folder", _savePath, "");
                if (!string.IsNullOrEmpty(selected))
                {
                    if (selected.StartsWith(Application.dataPath))
                        _savePath = "Assets" + selected.Substring(Application.dataPath.Length);
                    else
                        EditorUtility.DisplayDialog("Invalid Path", "Please select a folder inside the Assets directory.", "OK");
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            if (_downloadGroupAsSinglePng && _childrenExpanded)
            {
                var groupPath = Path.Combine(_savePath, _groupFileName);
                var groupExists = File.Exists(groupPath);
                var groupIcon = groupExists ? "\u26a0 " : "\u2714 ";
                var groupColor = groupExists ? new Color(1f, 0.4f, 0.4f) : new Color(0.4f, 0.9f, 0.5f);
                var groupLabel = groupIcon + groupPath + (groupExists ? "  (will override)" : "  (new)");
                var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = groupColor }, richText = false };
                EditorGUILayout.LabelField(groupLabel, style);
            }
            else
            {
                foreach (var entry in _imageEntries)
                {
                    if (!entry.Download) continue;
                    var filePath = Path.Combine(_savePath, entry.FileName);
                    var exists = File.Exists(filePath);
                    var icon = exists ? "\u26a0 " : "\u2714 ";
                    var color = exists ? new Color(1f, 0.4f, 0.4f) : new Color(0.4f, 0.9f, 0.5f);
                    var label = icon + filePath + (exists ? "  (will override)" : "  (new)");
                    var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = color }, richText = false };
                    EditorGUILayout.LabelField(label, style);
                }
            }

            GUILayout.Space(4);

            var selectedScale = ScaleValues[(int)_exportScale];
            var downloadCount = 0;
            foreach (var e in _imageEntries)
                if (e.Download)
                    downloadCount++;
            var saveCountLabel = (_downloadGroupAsSinglePng && _childrenExpanded)
                ? "1 group image"
                : downloadCount + " image(s)";
            EditorGUILayout.LabelField(
                "Will fetch and save " + saveCountLabel + " at " + ScaleDisplayNames[(int)_exportScale] + " scale.",
                EditorStyles.miniLabel);

            GUILayout.Space(4);

            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.35f, 1f);
            GUI.enabled = !_isBusy;
            if (GUILayout.Button(_isSaving ? "Fetching & Saving..." : "Save To Project", GUILayout.Height(36)))
            {
                CancelOngoingFetch();
                _cts = new CancellationTokenSource();
                SaveToProjectAsync(selectedScale, _cts.Token).Forget();
            }
            GUI.enabled = true;
            GUI.backgroundColor = oldBg;

            EndSection();
        }

        private void DrawStatusBar()
        {
            if (string.IsNullOrEmpty(_statusMessage)) return;
            GUILayout.Space(6);
            EditorGUILayout.HelpBox(_statusMessage, _statusType);
            GUILayout.Space(6);
        }

        private void BeginSection(string title)
        {
            GUILayout.Space(2);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(2);
            EditorGUILayout.LabelField(title, _sectionHeaderStyle);
            GUILayout.Space(4);
        }

        private void EndSection()
        {
            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
        }

        // ----- URL parsing -----

        private void ParseUrl()
        {
            _isUrlValid = false;
            _urlWarning = "";
            _fileKey = "";
            _nodeId = "";
            _fileName = "";
            _breadcrumb.Clear();
            _activeNodeId = "";
            _activeNodeName = "";
            ClearEntries();

            if (string.IsNullOrWhiteSpace(_figmaUrl))
            {
                _urlWarning = "Please enter a Figma URL.";
                return;
            }

            var trimmed = _figmaUrl.Trim();

            if (!trimmed.StartsWith("https://www.figma.com/") && !trimmed.StartsWith("https://figma.com/"))
            {
                _urlWarning = "URL must start with https://www.figma.com/ or https://figma.com/";
                return;
            }

            var fileKeyMatch = Regex.Match(trimmed, @"figma\.com/(?:design|file)/([a-zA-Z0-9]+)");
            if (!fileKeyMatch.Success)
            {
                _urlWarning = "Could not extract File Key. URL should contain /design/<file_key> or /file/<file_key>.";
                return;
            }
            _fileKey = fileKeyMatch.Groups[1].Value;

            var fileNameMatch = Regex.Match(trimmed, @"figma\.com/(?:design|file)/[a-zA-Z0-9]+/([^?/]+)");
            if (fileNameMatch.Success)
                _fileName = Uri.UnescapeDataString(fileNameMatch.Groups[1].Value).Replace("-", " ");

            var nodeIdMatch = Regex.Match(trimmed, @"[?&]node-id=([^&]+)");
            if (!nodeIdMatch.Success)
            {
                _urlWarning = "Could not extract Node ID. URL must contain a 'node-id' query parameter (e.g. node-id=123-456).";
                return;
            }

            var rawNodeId = Uri.UnescapeDataString(nodeIdMatch.Groups[1].Value);
            if (!Regex.IsMatch(rawNodeId, @"^\d+[-:]\d+$"))
            {
                _urlWarning = "Node ID format is invalid: '" + rawNodeId + "'. Expected format: 123-456 or 123:456.";
                return;
            }

            _nodeId = rawNodeId.Replace("-", ":");
            _isUrlValid = true;
            _statusMessage = "";
        }

        // ----- Fetch logic -----

        private void FetchPreview()
        {
            if (!_isUrlValid || _isFetchingPreview) return;
            _activeNodeId = _nodeId;
            _activeNodeName = "";
            _breadcrumb.Clear();
            CancelOngoingFetch();
            _cts = new CancellationTokenSource();
            FetchPreviewAsync(_cts.Token).Forget();
        }

        private void DrillInto(string childNodeId, string childName)
        {
            if (_isBusy) return;
            _breadcrumb.Add(new BreadcrumbEntry
            {
                NodeId = _activeNodeId,
                Name = _activeNodeName
            });
            _activeNodeId = childNodeId;
            _activeNodeName = childName;
            CancelOngoingFetch();
            _cts = new CancellationTokenSource();
            FetchPreviewAsync(_cts.Token).Forget();
        }

        private void DrillBack(int breadcrumbIndex)
        {
            if (_isBusy || breadcrumbIndex < 0 || breadcrumbIndex >= _breadcrumb.Count) return;
            var target = _breadcrumb[breadcrumbIndex];
            _activeNodeId = target.NodeId;
            _activeNodeName = target.Name;
            _breadcrumb.RemoveRange(breadcrumbIndex, _breadcrumb.Count - breadcrumbIndex);
            CancelOngoingFetch();
            _cts = new CancellationTokenSource();
            FetchPreviewAsync(_cts.Token).Forget();
        }

        private void CancelOngoingFetch()
        {
            if (_cts == null) return;
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        private async UniTaskVoid FetchPreviewAsync(CancellationToken ct)
        {
            _isFetchingPreview = true;
            _isBusy = true;
            ClearEntries();
            _statusMessage = "Inspecting node tree...";
            _statusType = MessageType.Info;
            Repaint();

            try
            {
                var targetNodeId = string.IsNullOrEmpty(_activeNodeId) ? _nodeId : _activeNodeId;
                var nodeResult = await FetchNodeInfoAsync(targetNodeId, ct);

                if (string.IsNullOrEmpty(_activeNodeName) && !string.IsNullOrEmpty(nodeResult.NodeName))
                    _activeNodeName = nodeResult.NodeName;

                var nodeName = !string.IsNullOrEmpty(nodeResult.NodeName)
                    ? SanitizeFileName(nodeResult.NodeName)
                    : _textureName;

                _hasChildren = nodeResult.Children.Count > 0;
                _pendingChildren = nodeResult.Children;
                _childrenExpanded = false;

                if (_hasChildren)
                {
                    _statusMessage = "Node \"" + nodeName + "\" has " + _pendingChildren.Count
                                     + " children. Click 'Expand Children' to view them, or 'Drill In' to navigate.";
                    _statusType = MessageType.Info;
                }
                else
                {
                    _statusMessage = "Fetching preview (1x)...";
                    Repaint();

                    var entries = await FetchImagesAsync(
                        new List<FigmaNodeInfo>
                        {
                            new FigmaNodeInfo
                            {
                                NodeId = targetNodeId,
                                Name = nodeName,
                                Type = nodeResult.NodeType ?? ""
                            }
                        },
                        1f, ct);
                    _imageEntries = entries;

                    _statusMessage = "1 image loaded. Press 'Save To Project' to export at selected scale.";
                    _statusType = MessageType.Info;
                }
            }
            catch (OperationCanceledException)
            {
                _statusMessage = "Fetch cancelled.";
                _statusType = MessageType.Warning;
                Debug.Log("[FigmaPNGDownloader] Fetch cancelled.");
            }
            catch (Exception ex)
            {
                _statusMessage = "Fetch error: " + ex.Message;
                _statusType = MessageType.Error;
            }
            finally
            {
                _isFetchingPreview = false;
                _isBusy = false;
                Repaint();
            }
        }

        private void ExpandChildren()
        {
            if (_isBusy || _pendingChildren == null || _pendingChildren.Count == 0) return;

            ClearEntries();
            foreach (var child in _pendingChildren)
            {
                _imageEntries.Add(new FigmaImageEntry
                {
                    NodeId = child.NodeId,
                    Name = child.Name,
                    FileName = SanitizeFileName(child.Name) + ".png",
                    NodeType = child.Type ?? "",
                    Download = true,
                    PreviewTexture = null
                });
            }
            _childrenExpanded = true;
            _downloadGroupAsSinglePng = false;
            _groupFileName = !string.IsNullOrEmpty(_activeNodeName)
                ? SanitizeFileName(_activeNodeName) + ".png"
                : "FigmaGroup.png";
            _statusMessage = _pendingChildren.Count + " children listed. Select items and click 'Fetch Selected Previews' or use 'Drill In' to navigate.";
            _statusType = MessageType.Info;
            Repaint();
        }

        private void FetchSelectedPreviews()
        {
            if (_isBusy) return;
            CancelOngoingFetch();
            _cts = new CancellationTokenSource();
            FetchSelectedPreviewsAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid FetchSelectedPreviewsAsync(CancellationToken ct)
        {
            var toFetch = new List<FigmaNodeInfo>();
            for (int i = 0; i < _imageEntries.Count; i++)
            {
                var entry = _imageEntries[i];
                if (entry.Download && entry.PreviewTexture == null)
                {
                    toFetch.Add(new FigmaNodeInfo
                    {
                        NodeId = entry.NodeId,
                        Name = entry.Name,
                        Type = entry.NodeType
                    });
                }
            }

            if (toFetch.Count == 0)
            {
                _statusMessage = "No unloaded selected items to fetch.";
                _statusType = MessageType.Warning;
                return;
            }

            _isFetchingPreview = true;
            _isBusy = true;
            _statusMessage = "Fetching " + toFetch.Count + " preview(s) (1x)...";
            _statusType = MessageType.Info;
            Repaint();

            try
            {
                var fetched = await FetchImagesAsync(toFetch, 1f, ct);

                foreach (var fetchedEntry in fetched)
                {
                    for (int i = 0; i < _imageEntries.Count; i++)
                    {
                        if (_imageEntries[i].NodeId == fetchedEntry.NodeId)
                        {
                            _imageEntries[i].PreviewTexture = fetchedEntry.PreviewTexture;
                            break;
                        }
                    }
                }

                _statusMessage = fetched.Count + " preview(s) loaded. Press 'Save To Project' to export at selected scale.";
                _statusType = MessageType.Info;
            }
            catch (OperationCanceledException)
            {
                _statusMessage = "Fetch cancelled.";
                _statusType = MessageType.Warning;
            }
            catch (Exception ex)
            {
                _statusMessage = "Fetch error: " + ex.Message;
                _statusType = MessageType.Error;
            }
            finally
            {
                _isFetchingPreview = false;
                _isBusy = false;
                Repaint();
            }
        }

        private async UniTask<FigmaNodeResult> FetchNodeInfoAsync(string nodeId, CancellationToken ct)
        {
            var apiNodeId = nodeId.Replace(":", "-");
            var url = "https://api.figma.com/v1/files/" + Uri.EscapeDataString(_fileKey)
                                                        + "/nodes?ids=" + Uri.EscapeDataString(apiNodeId)
                                                        + "&depth=1";
            var json = await SendRequest(UnityWebRequest.Get(url), ct, true);

            var result = new FigmaNodeResult();
            var root = JObject.Parse(json);
            var nodesObj = root["nodes"];
            if (nodesObj == null) return result;

            var nodeColonId = nodeId.Contains(":") ? nodeId : nodeId.Replace("-", ":");
            var nodeData = nodesObj[nodeColonId];
            if (nodeData == null)
            {
                foreach (var prop in ((JObject)nodesObj).Properties())
                {
                    nodeData = prop.Value;
                    break;
                }
            }
            if (nodeData == null) return result;

            var document = nodeData["document"];
            if (document == null) return result;

            result.NodeName = document["name"]?.ToString();
            result.NodeType = document["type"]?.ToString();

            var children = document["children"] as JArray;
            if (children == null || children.Count == 0)
                return result;

            foreach (var child in children)
            {
                var childId = child["id"]?.ToString();
                var childName = child["name"]?.ToString() ?? childId;
                var childType = child["type"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(childId))
                    result.Children.Add(new FigmaNodeInfo
                    {
                        NodeId = childId,
                        Name = SanitizeFileName(childName),
                        Type = childType
                    });
            }

            return result;
        }

        private string BuildBatchImageApiUrl(List<FigmaNodeInfo> nodes, float scale)
        {
            var idsCsv = string.Join(",", nodes.Select(n => n.NodeId.Replace(":", "-")));
            return "https://api.figma.com/v1/images/" + Uri.EscapeDataString(_fileKey)
                                                      + "?ids=" + Uri.EscapeDataString(idsCsv)
                                                      + "&format=png"
                                                      + "&scale=" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture)
                                                      + (_useAbsoluteBounds ? "&use_absolute_bounds=true" : "");
        }

        private async UniTask<List<FigmaImageEntry>> FetchImagesAsync(
            List<FigmaNodeInfo> nodes, float scale, CancellationToken ct)
        {
            var imageApiUrl = BuildBatchImageApiUrl(nodes, scale);

            var startTime = DateTime.Now;
            var json = await SendRequest(UnityWebRequest.Get(imageApiUrl), ct, true);
            Debug.Log("[FigmaPNGDownloader] API Duration: " + (DateTime.Now - startTime).TotalSeconds + "s (scale=" + scale + ", count=" + nodes.Count + ")");

            var root = JObject.Parse(json);
            var imagesObj = root["images"] as JObject;
            if (imagesObj == null)
                throw new Exception("Figma API returned no images object.");

            var entries = new List<FigmaImageEntry>();

            foreach (var node in nodes)
            {
                ct.ThrowIfCancellationRequested();

                var colonId = node.NodeId.Contains(":") ? node.NodeId : node.NodeId.Replace("-", ":");
                var imageUrl = imagesObj[colonId]?.ToString();

                if (string.IsNullOrEmpty(imageUrl))
                {
                    Debug.LogWarning("[FigmaPNGDownloader] No image URL for node: " + node.NodeId + " (" + node.Name + ")");
                    continue;
                }

                _statusMessage = "Downloading: " + node.Name + "...";
                Repaint();

                startTime = DateTime.Now;
                var imgRequest = UnityWebRequestTexture.GetTexture(imageUrl);
                await SendRequest(imgRequest, ct, false);
                Debug.Log("[FigmaPNGDownloader] Download '" + node.Name + "': " + (DateTime.Now - startTime).TotalSeconds + "s");

                entries.Add(new FigmaImageEntry
                {
                    NodeId = node.NodeId,
                    Name = node.Name,
                    FileName = SanitizeFileName(node.Name) + ".png",
                    NodeType = node.Type ?? "",
                    PreviewTexture = DownloadHandlerTexture.GetContent(imgRequest)
                });
            }

            return entries;
        }

        private async UniTask<string> SendRequest(UnityWebRequest request, CancellationToken ct, bool useFigmaToken)
        {
            ct.ThrowIfCancellationRequested();

            if (useFigmaToken)
                request.SetRequestHeader("X-Figma-Token", ACCESS_TOKEN);

            var ctReg = ct.Register(() => request.Abort());
            try
            {
                await request.SendWebRequest();
                ct.ThrowIfCancellationRequested();

                if (request.result != UnityWebRequest.Result.Success)
                    throw new Exception(request.error + "\n" + request.downloadHandler?.text);

                return request.downloadHandler?.text;
            }
            finally
            {
                ctReg.Dispose();
            }
        }

        // ----- Save logic -----

        private async UniTaskVoid SaveToProjectAsync(float scale, CancellationToken ct)
        {
            _isSaving = true;
            _isBusy = true;
            Repaint();

            try
            {
                if (_downloadGroupAsSinglePng && _childrenExpanded)
                {
                    await SaveGroupImageAsync(scale, ct);
                    return;
                }

                var fileNameMap = new Dictionary<string, string>();
                var nodesToFetch = new List<FigmaNodeInfo>();
                foreach (var entry in _imageEntries)
                {
                    if (!entry.Download) continue;
                    entry.FileName = EnforcePngExtension(entry.FileName);
                    nodesToFetch.Add(new FigmaNodeInfo { NodeId = entry.NodeId, Name = entry.Name });
                    fileNameMap[entry.NodeId] = entry.FileName;
                }

                if (nodesToFetch.Count == 0)
                {
                    _statusMessage = "No images selected for download.";
                    _statusType = MessageType.Warning;
                    return;
                }

                _statusMessage = "Fetching " + nodesToFetch.Count + " image(s) at " + scale + "x ...";
                _statusType = MessageType.Info;
                Repaint();

                var fullResEntries = await FetchImagesAsync(nodesToFetch, scale, ct);

                _statusMessage = "Saving PNGs...";
                Repaint();

                var directory = _savePath;
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var savedPaths = new List<string>();

                foreach (var entry in fullResEntries)
                {
                    var fileName = fileNameMap.ContainsKey(entry.NodeId)
                        ? fileNameMap[entry.NodeId]
                        : entry.Name + ".png";
                    fileName = EnforcePngExtension(fileName);
                    var assetPath = Path.Combine(_savePath, fileName);

                    var pngBytes = entry.PreviewTexture.EncodeToPNG();
                    DestroyImmediate(entry.PreviewTexture);

                    if (pngBytes == null)
                    {
                        Debug.LogWarning("[FigmaPNGDownloader] Failed to encode: " + entry.Name);
                        continue;
                    }

                    File.WriteAllBytes(assetPath, pngBytes);
                    savedPaths.Add(assetPath);
                }

                AssetDatabase.Refresh();

                foreach (var path in savedPaths)
                    ConfigureAsSingleSprite(path);

                _statusMessage = "Saved " + savedPaths.Count + " sprite(s) to " + _savePath;
                _statusType = MessageType.Info;

                if (savedPaths.Count > 0)
                {
                    var firstAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(savedPaths[0]);
                    EditorGUIUtility.PingObject(firstAsset);
                    Selection.activeObject = firstAsset;
                }
            }
            catch (OperationCanceledException)
            {
                _statusMessage = "Save cancelled.";
                _statusType = MessageType.Warning;
                Debug.Log("[FigmaPNGDownloader] Save cancelled.");
            }
            catch (Exception ex)
            {
                _statusMessage = "Save failed: " + ex.Message;
                _statusType = MessageType.Error;
            }
            finally
            {
                _isSaving = false;
                _isBusy = false;
                Repaint();
            }
        }

        private async UniTask SaveGroupImageAsync(float scale, CancellationToken ct)
        {
            var fileName = EnforcePngExtension(_groupFileName);

            _statusMessage = "Fetching group image at " + ScaleDisplayNames[(int)_exportScale] + " scale...";
            _statusType = MessageType.Info;
            Repaint();

            var groupNodes = new List<FigmaNodeInfo>
            {
                new FigmaNodeInfo { NodeId = _activeNodeId, Name = _activeNodeName, Type = "" }
            };
            var fullResEntries = await FetchImagesAsync(groupNodes, scale, ct);

            _statusMessage = "Saving group PNG...";
            Repaint();

            var directory = _savePath;
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string savedPath = null;
            foreach (var entry in fullResEntries)
            {
                var assetPath = Path.Combine(_savePath, fileName);
                var pngBytes = entry.PreviewTexture.EncodeToPNG();
                DestroyImmediate(entry.PreviewTexture);

                if (pngBytes == null)
                {
                    Debug.LogWarning("[FigmaPNGDownloader] Failed to encode group image: " + _activeNodeName);
                    continue;
                }

                File.WriteAllBytes(assetPath, pngBytes);
                savedPath = assetPath;
            }

            AssetDatabase.Refresh();

            if (!string.IsNullOrEmpty(savedPath))
            {
                ConfigureAsSingleSprite(savedPath);

                var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(savedPath);
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }

            _statusMessage = "Saved group image to " + _savePath + "/" + fileName;
            _statusType = MessageType.Info;
        }

        private void ConfigureAsSingleSprite(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;

            var platformSettings = new TextureImporterPlatformSettings
            {
                format = TextureImporterFormat.ETC2_RGBA8,
                overridden = true,
                maxTextureSize = 2048,
            };

            platformSettings.name = "Android";
            importer.SetPlatformTextureSettings(platformSettings);

            platformSettings.name = "iPhone";
            importer.SetPlatformTextureSettings(platformSettings);

            importer.SaveAndReimport();
        }

        // ----- Helpers -----

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

        private static string EnforcePngExtension(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "Untitled.png";

            fileName = fileName.Trim();

            var dotIndex = fileName.LastIndexOf('.');
            if (dotIndex >= 0)
            {
                var ext = fileName.Substring(dotIndex);
                if (!ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
                    fileName = fileName.Substring(0, dotIndex) + ".png";
            }
            else
            {
                fileName += ".png";
            }

            return fileName;
        }

        private void ClearEntries()
        {
            if (_imageEntries != null)
            {
                foreach (var entry in _imageEntries)
                {
                    if (entry.PreviewTexture != null)
                        DestroyImmediate(entry.PreviewTexture);
                }
                _imageEntries.Clear();
            }
            _hasChildren = false;
            _childrenExpanded = false;
            _downloadGroupAsSinglePng = false;
            _groupFileName = "";
            _searchFilter = "";
        }

        // ----- Lifecycle -----

        private void OnDisable()
        {
            CancelOngoingFetch();
        }

        private void OnDestroy()
        {
            CancelOngoingFetch();
            ClearEntries();
        }
    }

    public struct FigmaNodeInfo
    {
        public string NodeId;
        public string Name;
        public string Type;
    }

    public struct BreadcrumbEntry
    {
        public string NodeId;
        public string Name;
    }

    public class FigmaNodeResult
    {
        public string NodeName;
        public string NodeType;
        public List<FigmaNodeInfo> Children = new List<FigmaNodeInfo>();
    }
}
#endif