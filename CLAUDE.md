---
alwaysApply: true
---

# CLAUDE.md

## Project Overview

This is a Unity C# mobile car simulation project.

## Technology Stack and Documentation

- Unity for client runtime, gameplay, UI, and platform integration
- C# for gameplay and client systems

## Critical Rules (MUST follow before writing ANY code)

- **NEVER use `namespace Editor`** in editor scripts. This breaks other scripts in the project. Always use a descriptive namespace like `ProfileAndMissionsDebugEditor`, `FigmaPNGDownloader`, `Scripts.Editor`, `_Game.Scripts.Editor`, etc. Check existing files in the same folder for the convention.
- Before creating or editing any file, check sibling files in the same folder for namespace, style, and structural conventions. Match them.
- The project structure is so important! When you create new scripts/assets, always create it inside "Assets/_Game/*" folders. For example, if you create a editor script, then you can create it in Assets/_Game/Scripts/Editor folder
- When you put comments inside scripts, do not use Turkish or something different language, ALWAYS PREFER ENGLISH EVEN IF YOU CHATTING IN TURKISH LANGUAGE!!!

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

## Error Handling and Debugging

- Use try-catch where appropriate, especially for file I/O and network operations
- Use Debug.Log, Debug.LogWarning, and Debug.LogError for useful diagnostics
- Use Debug.Assert to catch logical errors during development
- Add clear custom error messages where they improve debugging speed
- Use profiler tools and frame debugging to investigate performance issues
- Prefer debug tooling and visualization that help identify gameplay and network state problems quickly

## Performance and Mobile Rules

- Mobile performance matters in every feature
- Prefer allocation-safe and GC-conscious solutions
- Avoid per-frame allocations in hot paths
- Avoid LINQ in hot paths in runtime, but you can use linq for editor scripts and editor tools
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