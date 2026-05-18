# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Interaction Rules

- Communicate with the user in Chinese (中文).
- Write documentation in English, and generate a corresponding Chinese version alongside it (e.g., `README.md` + `README.zh-CN.md`).
- Do NOT create, edit, delete, or commit `.meta` files — Unity manages these automatically.
- All agent-generated documentation goes into `AIDoc/`, organized by feature/topic (e.g., `AIDoc/quantum-basics/`, `AIDoc/networking/`, `AIDoc/ecs/`).

## Project Overview

Unity 6000.3.13f1 multiplayer game built on Photon Quantum SDK. Quantum uses a deterministic ECS (Entity Component System) with a strict simulation/view separation — game logic runs in a deterministic simulation layer, and Unity handles rendering via a separate view layer.

## Build & Run

```bash
# Open in Unity Editor
/Applications/Unity/Hub/Editor/6000.3.13f1/Unity.app/Contents/MacOS/Unity -projectPath .

# Run EditMode tests (headless)
/Applications/Unity/Hub/Editor/6000.3.13f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -runTests -testPlatform EditMode

# Check LFS status before pushing binary assets
git lfs status
```

Normal development uses the Unity Editor for iteration, scene editing, and play testing.

## Architecture

### Quantum Simulation/View Split

- **Simulation** (`Assets/QuantumUser/Simulation/`, `Assets/Photon/QuantumAsteroids/Simulation/`): Deterministic game logic. Systems run on all clients identically. No Unity API allowed here — only Quantum's fixed-point math (`FP`) and ECS primitives.
- **View** (`Assets/QuantumUser/View/`, `Assets/Photon/QuantumAsteroids/View/`): Unity MonoBehaviours that read simulation state and render it. Input collection also lives here.
- **DSL files** (`.qtn`): Define components, signals, inputs, and global state. Code generation produces C# from these into `*/Generated/` folders.

### Key Extension Points

- `Assets/QuantumUser/Simulation/SystemSetup.User.cs` — register custom systems
- `Assets/QuantumUser/Simulation/RuntimeConfig.User.cs` — extend runtime config
- `Assets/QuantumUser/Simulation/RuntimePlayer.User.cs` — extend per-player data
- `Assets/QuantumUser/Simulation/Frame.User.cs` — extend frame API
- `Assets/QuantumUser/Editor/CodeGen/QuantumCodeGenSettings.User.cs` — codegen settings

### Included Sample

`Assets/Photon/QuantumAsteroids/` — a complete Asteroids game demonstrating Quantum patterns: input handling, ship movement, projectiles, wave spawning, collisions, and entity prototypes.

## Coding Conventions

- C#, 4-space indentation, Unity naming: `PascalCase` for types/methods/public fields, `camelCase` for locals/parameters
- Simulation code uses Quantum's `FP` (fixed-point) instead of `float`/`double` for determinism
- `.meta` files are managed by Unity — do not touch them
- Generated code in `*/Generated/` folders — do not edit manually; regenerate via Quantum CodeGen in the Unity Editor

## Git LFS

Binary assets (textures, models, audio, fonts) are tracked via LFS — see `.gitattributes`. Always verify with `git lfs status` before pushing large asset changes.
