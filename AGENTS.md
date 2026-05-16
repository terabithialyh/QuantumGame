# Repository Guidelines

## Project Structure & Module Organization

This is a Unity 6000.3 project. Keep source assets under `Assets/` and project-wide Unity settings under `ProjectSettings/`. Package manifests live in `Packages/manifest.json` and `Packages/packages-lock.json`.

Important asset areas:
- `Assets/Scenes/`: Unity scenes, currently including the sample scene.
- `Assets/Settings/`: URP render pipeline and volume settings.
- `Assets/QuantumUser/`: project-specific Quantum simulation, view, resources, scenes, and editor code.
- `Assets/Photon/`: Photon Quantum, Realtime, menu, and sample content.
- `Assets/TextMesh Pro/`: imported TMP resources.

Do not commit generated folders such as `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Build/`, or generated `.csproj` / `.sln` files.

## Build, Test, and Development Commands

Open the project with Unity Editor `6000.3.13f1` or a compatible Unity 6.3 editor.

Useful CLI examples on macOS:

```bash
/Applications/Unity/Hub/Editor/6000.3.13f1/Unity.app/Contents/MacOS/Unity -projectPath .
/Applications/Unity/Hub/Editor/6000.3.13f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -runTests -testPlatform EditMode
git lfs status
```

Use the Unity Editor for normal iteration, package restoration, scene validation, and build profile setup.

## Coding Style & Naming Conventions

Use C# with 4-space indentation and Unity naming conventions: `PascalCase` for types, methods, properties, and public fields; `camelCase` for locals and parameters. Keep MonoBehaviour scripts named after their primary class. Keep `.meta` files with every asset rename, move, or deletion.

Unity serialization is configured for text assets. Prefer serialized fields and explicit asset references over runtime path lookups when practical.

## Testing Guidelines

Use Unity Test Framework tests under `Assets/Tests/EditMode/` and `Assets/Tests/PlayMode/` when adding gameplay, simulation, or editor behavior. Name test files after the subject under test, for example `AsteroidSpawnerTests.cs`. Run EditMode tests before committing logic changes; add PlayMode tests for scene, view, or integration behavior.

## Commit & Pull Request Guidelines

Current history uses short imperative commit subjects, for example `Initial Unity project commit` and `Enable Git LFS for binary assets`. Keep subjects concise and explain the reason in the body when behavior changes.

Pull requests should include a summary, test results, linked issue or task, and screenshots or short clips for visual changes. Mention Unity version changes, package updates, and any required migration steps.

## Git LFS & Asset Handling

Binary assets such as `*.png`, `*.jpg`, `*.psd`, `*.psb`, `*.fbx`, audio, video, and fonts are tracked by Git LFS through `.gitattributes`. Verify large asset changes with `git lfs status` before pushing.
