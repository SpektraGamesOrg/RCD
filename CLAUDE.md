---
alwaysApply: true
---

# CLAUDE.md

## Project Overview

This is a Unity C# mobile car simulation project.

## Game Design Document (GDD)

- The authoritative game design reference lives at the repo root: **`DRIVE01_GDD.pdf`** (36 pages). Read it for general game context and design intent before making gameplay or feature decisions.
- It covers the full product design: executive summary and strategy, target player, the three design pillars, core loop, driving model and physics feel, skill/scoring system, damage/repair, car roster and unlock economy, customization, environment and traffic, UI/HUD, audio, economy and monetization, technical spec and performance budgets, analytics, ASO, and the production plan.
- DRIVE01 in a nutshell (per the GDD): a casual, single-scene open-world driving game for Android / Google Play, monetized entirely through ads, built on Unity 2022 LTS + URP. The player picks one of ~10 cars and earns soft currency by going fast, drifting, and jumping. No missions, no narrative, no IAP, no multiplayer. "Feel over fidelity," icon-only UI, and low-end mobile performance are first-class constraints.
- Treat the GDD as **design intent, not a description of the current code**. When the document and the actual codebase disagree, follow the code and flag the discrepancy.
- Reading the PDF: the `Read` tool may fail to render it in some environments. If so, extract the text instead (e.g. `pip install pypdf`, then a short `PdfReader('DRIVE01_GDD.pdf')` page loop) and write the output as UTF-8.

## Technology Stack and Documentation

- Unity for client runtime, gameplay, UI, and platform integration
- C# for gameplay and client systems

## Critical Rules (MUST follow before writing ANY code)

- **NEVER use `namespace Editor`** in editor scripts. This breaks other scripts in the project. Always use a descriptive namespace like `ProfileAndMissionsDebugEditor`, `FigmaPNGDownloader`, `Scripts.Editor`, `_Game.Scripts.Editor`, etc. Check existing files in the same folder for the convention.
- Before creating or editing any file, check sibling files in the same folder for namespace, style, and structural conventions. Match them.
- The project structure is so important! When you create new scripts/assets, always create it inside "Assets/_Game/*" folders. For example, if you create a editor script, then you can create it in Assets/_Game/Scripts/Editor folder
- When you put comments inside scripts, do not use Turkish or something different language, ALWAYS PREFER ENGLISH EVEN IF YOU CHATTING IN TURKISH LANGUAGE!!!
- **NEVER add `[SerializeField]` to a variable just to make it visible in the Inspector if it is runtime-only and does not need to be serialized.** Keep it non-serialized (`private`, a `get`/`set` property, or `[System.NonSerialized]`) and use Odin's `[ShowInInspector, ReadOnly]` attributes to expose it for debugging. `[SerializeField]` is only for fields that must be set in the Inspector or persisted in the scene/prefab.
- **NEVER use `null` comparison for Unity objects** (anything deriving from `UnityEngine.Object` — MonoBehaviours, GameObjects, Components, etc.). Do not write `if (myUnityObject == null)` or `if (myUnityObject != null)`. Instead rely on Unity's implicit `bool` operator: use `if (myUnityObject)` for the non-null/alive case and `if (!myUnityObject)` for the null/destroyed case. This correctly accounts for destroyed objects that are not truly `null`.

## Core Principles

- Write clear, technical, and precise C# and Unity code
- Prefer Unity built-in systems and tools when possible
- Prioritize readability, maintainability, and modularity
- Follow C# coding conventions and Unity best practices
- Use descriptive names and consistent naming conventions
- Follow the existing project architecture, folder structure, and naming patterns
- Keep systems DRY and avoid copy-paste duplication
- Use component-based design and separation of concerns
- Keep gameplay logic, UI, and networking responsibilities separated
- Prefer extending existing systems instead of creating parallel ones
- Never run git commit, git push, or any mutating git command; report changes so the developer can commit manually

## UnityMCP Usage (Editor Automation)

This project has **UnityMCP** available as an MCP server. It exposes live Unity Editor tooling (`mcp__UnityMCP__*`) that lets you interact with the running Editor instance directly.

**Who can use it:** Any agent or workflow in this repo (main Claude Code session, GSD subagents, etc.) is permitted — and encouraged — to use UnityMCP whenever it would produce a better outcome than editing files blindly. This includes GSD commands (`/gsd-plan-phase`, `/gsd-execute-phase`, `/gsd-debug`, etc.) and their spawned agents.

**When to prefer UnityMCP over plain file edits:**
- **Verifying compilation** after creating/modifying C# scripts — use `read_console` to catch errors before proceeding. Poll `editor_state.isCompiling` to wait for domain reload.
- **Inspecting Editor/project state** — scene contents, GameObjects, components, assets, tags, layers, tests. Use resources first (`editor_state`, `project_info`, etc.), then tools for mutations.
- **Scene, prefab, material, and asset mutations** — use `manage_scene`, `manage_prefabs`, `manage_gameobject`, `manage_components`, `manage_material`, `manage_asset`, etc., instead of hand-writing `.unity`/`.prefab` YAML.
- **Running tests** — `run_tests` + `get_test_job`.
- **Targeted script edits** — `script_apply_edits` / `apply_text_edits` / `find_in_file` can be safer than raw Edit when the script is loaded by the Editor.
- **Project-specific capabilities** — always check `mcpforunity://custom-tools` resource first to see what dynamic tools this project exposes.

**Rules when using UnityMCP:**
- Always use forward slashes in paths; paths are relative to `Assets/` unless stated otherwise.
- After any script creation/modification (yours or `manage_script`), run `read_console` to verify clean compilation before using the new types.
- If multiple Unity instances are connected, use the `mcpforunity://instances` resource and `set_active_instance` (or pass `unity_instance` per call) — never assume routing.
- Prefer resources for reads, tools for writes. Do not use a mutating tool to discover state.
- If UnityMCP is **not** available in the current session (no `mcp__UnityMCP__*` tools listed), fall back to normal file edits — do not block on it.

**GSD integration:** When a GSD phase involves Unity Editor work (scene setup, prefab wiring, component configuration, running tests, verifying compilation), the executing agent should reach for UnityMCP before resorting to manual YAML edits or asking the user to do it in the Editor.

## Unity and C#

- Use MonoBehaviour for GameObject-attached behaviors
- Prefer ScriptableObject for shared configuration, static data, and reusable data containers
- Keep game logic in scripts and use the Unity Editor for scene composition and initial setup
- Use Prefabs for reusable game objects and UI elements
- Use Unity UI systems for interface implementation
- Use the Unity Input System for player input across platforms
- Use Unity physics and collision systems where appropriate
- Use UniTask for time-based flows and simple asynchronous operations in Unity
- Use async and network tasks carefully and defensively in multiplayer flows
- Keep client models aligned with backend DTOs and contracts
- Avoid hidden side effects in MonoBehaviours; prefer explicit flow through services and controllers
- Do not introduce unnecessary singletons, reflection-heavy systems, or editor-only dependencies into runtime code

## UI Development (uGUI + GameUIManager)

These rules are MANDATORY whenever you create or modify in-game UI.

- **Always use the existing `GameUIManager` and UI system.** Never build a parallel canvas/menu system. New views extend the existing base classes (`ScreenBase`, `PopupBase`, `OverlayBase`, `TabBase`) and are shown/hidden through `GameUIManager` (`SwitchScreen`, `ShowPopup`, `Open`, `Back`, etc.). Use the project's `EnhancedButton` for buttons (not raw `Button`). Extend the existing system instead of duplicating it.
- **`Content` is the UI root — do NOT create a `UIRoot` object inside it.** Every `UIViewBase` already has a `Content` child (with its `CanvasGroup`) that serves as the view's root. Build the UI directly as children of `Content`. Never wrap the layout in an extra container like `UIRoot` inside `Content`.
- **Always prefix UI GameObject names by their type.** Format is `Type_Name`. Examples: `Button_Play`, `Text_Price`, `Image_BuyCoin`, `Panel_TopBar`, `Toggle_Vibration`, `Slider_Master`, `ScrollView_Garage`. This keeps the hierarchy readable and searchable.
- **Responsive design is critical — this is a landscape car game.** The Canvas reference resolution is **1920×1080** (`CanvasScaler` = Scale With Screen Size). Anchor every element to the nearest screen edge/corner (top-right, bottom-left, side-center, etc.) with margins in canvas units — do not lay things out from the center with absolute offsets. Verify the layout holds across landscape aspect ratios (16:9, 19.5:9, 4:3) with no overlap or off-screen elements. Prefer anchored corners over fixed positions.

## Error Handling and Debugging

- Use try-catch where appropriate, especially for file I/O and network operations
- Use Debug.Log and Debug.LogError for useful diagnostics
- NEVER use `Debug.LogWarning` or `Debug.LogWarningFormat`. Always prefer `Debug.LogError` (or `Debug.LogErrorFormat`) instead
- Use Debug.Assert to catch logical errors during development
- Add clear custom error messages where they improve debugging speed
- Use profiler tools and frame debugging to investigate performance issues
- Prefer debug tooling and visualization that help identify gameplay and network state problems quickly

## Performance and Mobile Rules

- Mobile performance matters in every feature
- Prefer allocation-safe and GC-conscious solutions
- Avoid per-frame allocations in hot paths
- Avoid LINQ in hot paths in runtime, but you can use linq for editor scripts and editor tools
- NEVER use scene-scanning lookup methods in runtime code — `FindAnyObjectByType`, `FindFirstObjectByType`, `FindObjectsByType`, `FindObjectOfType`, `FindObjectsOfType`, `GameObject.Find`, `GameObject.FindWithTag`, etc. They are slow at runtime. Instead wire references via the Inspector, dependency injection, or the project's existing singleton/service accessors. These methods are acceptable ONLY in editor-only code (editor scripts and editor tools)
- Avoid excessive async task churn
- Use object pooling for frequently spawned and destroyed objects
- Optimize draw calls with batching and atlases where appropriate
- Use simplified colliders and appropriate fixed timestep settings for physics performance
- Use LOD systems for complex 3D content when needed
- Use Unity Job System and Burst Compiler for CPU-intensive systems when appropriate
- Profile before and after changes that affect performance-critical systems

## Visual and Content Systems

- Use Animator and Animation Clips for animation workflows
- Use Unity lighting and post-processing features appropriately
- Use tags and layers carefully for categorization and collision filtering
- Use asset management and loading strategies appropriate for mobile memory constraints

## What Not To Do

- Do not create new architecture patterns when an established one already exists in the project
- Do not introduce unnecessary complexity when a simple Unity-native solution is enough
- Do not assume third-party plugins are safe; check compatibility, maintenance, and runtime cost first

## Custom Packages

- Some dependencies are defined in `Packages/manifest.json` and `Packages/packages-lock.json`
- If a package name contains `.spektragames.` in its bundle/package id, it is a custom internal package
- Internal Spektra Unity packages are maintained outside this project
- To inspect or edit those packages, use `../SpektraUnityPackagesProject/`
- Package sources can be found under `../SpektraUnityPackagesProject/Packages/`
- Do not assume internal package code lives inside this Unity project even if it is referenced by Package Manager
- When changing systems that depend on a `.spektragames.` package, check the package source in `../SpektraUnityPackagesProject/Packages/` before making assumptions