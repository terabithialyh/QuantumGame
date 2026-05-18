# Building a Deterministic Frame-Sync Game with Photon Quantum from Scratch

## What is Photon Quantum?

Photon Quantum is a high-performance deterministic ECS (Entity Component System) framework for building multiplayer games with **predict/rollback** networking. Unlike traditional state-sync or lockstep approaches, Quantum uses a hybrid model:

- All clients run the **same deterministic simulation** independently
- Only **player inputs** are transmitted over the network (extremely low bandwidth)
- The server acts as an **input authority** — it collects, orders, and distributes inputs
- Clients **predict** future frames locally for responsiveness
- When authoritative inputs arrive, clients **rollback** and re-simulate if predictions were wrong

This gives you the responsiveness of client-side prediction with the consistency of server-authoritative networking.

## Core Architecture

### The Simulation/View Separation

This is the most important concept in Quantum. Your project is split into two completely independent layers:

```
┌─────────────────────────────────────────────────┐
│                   VIEW LAYER                     │
│  (Unity MonoBehaviours, Rendering, Audio, UI)   │
│  - Reads simulation state                       │
│  - Collects player input                        │
│  - Interpolates/extrapolates for visuals        │
│  - NO game logic here                           │
└─────────────────────┬───────────────────────────┘
                      │ reads (one-way)
┌─────────────────────▼───────────────────────────┐
│               SIMULATION LAYER                   │
│  (Deterministic ECS — no Unity API allowed)     │
│  - All game logic lives here                    │
│  - Fixed-point math (FP) only                   │
│  - Systems process components each tick         │
│  - Identical execution on all clients           │
└─────────────────────────────────────────────────┘
```

**Why this matters:**
- The simulation layer must produce **identical results** on every machine, every time
- Unity's `float` math is NOT deterministic across platforms — Quantum uses `FP` (fixed-point)
- No `UnityEngine` API calls in simulation code — no `Random.Range`, no `Time.deltaTime`, no `Physics.Raycast`
- The view layer is purely cosmetic — it can lag, skip frames, or interpolate without affecting gameplay correctness

### The ECS Model

Quantum's ECS is not Unity's DOTS. It's a custom implementation optimized for deterministic simulation:

- **Entity**: A lightweight ID with a set of components attached
- **Component**: Pure data (defined in `.qtn` DSL files). No methods, no logic.
- **System**: Stateless logic that processes entities with specific component combinations each frame
- **Frame**: The complete game state at a given tick. Contains all entities, components, globals, and provides the API for querying/modifying state.

### Frame and Tick

A "frame" in Quantum is one simulation tick (not a render frame). The simulation runs at a fixed tick rate (default 60 Hz). Each tick:

1. Inputs for this tick are resolved (predicted or confirmed)
2. All registered Systems execute in order
3. The Frame state advances

The `Frame` object is your gateway to everything during simulation:
- `frame.Get<ComponentType>(entity)` — read component data
- `frame.Set(entity, component)` — write component data
- `frame.Create(prototype)` — spawn entities
- `frame.Destroy(entity)` — remove entities
- `frame.Global->` — access global state

## Project Structure

Here's how to organize a Quantum project:

```
Assets/
├── QuantumUser/                    # YOUR game code goes here
│   ├── Simulation/                 # Deterministic game logic
│   │   ├── Systems/                # Your ECS systems
│   │   │   ├── MovementSystem.cs
│   │   │   ├── CombatSystem.cs
│   │   │   └── SpawnSystem.cs
│   │   ├── DSL/                    # .qtn files defining data
│   │   │   ├── Components.qtn
│   │   │   ├── Input.qtn
│   │   │   └── Events.qtn
│   │   ├── Generated/              # Auto-generated (don't edit)
│   │   ├── SystemSetup.User.cs     # Register your systems
│   │   ├── CommandSetup.User.cs    # Register commands
│   │   ├── RuntimeConfig.User.cs   # Game-wide config
│   │   ├── RuntimePlayer.User.cs   # Per-player data
│   │   ├── Frame.User.cs           # Extend Frame API
│   │   └── FrameContext.User.cs    # Extend context
│   ├── View/                       # Unity rendering layer
│   │   ├── EntityViews/            # Visual representations
│   │   │   ├── PlayerView.cs
│   │   │   └── ProjectileView.cs
│   │   ├── Input/                  # Input collection
│   │   │   └── LocalInput.cs
│   │   ├── UI/                     # Game UI
│   │   └── Generated/              # Auto-generated (don't edit)
│   ├── Scenes/                     # Game scenes
│   ├── Resources/                  # Quantum assets (configs, prototypes)
│   └── Editor/                     # Editor tools
│       └── CodeGen/
├── Photon/                         # SDK (don't modify)
│   ├── Quantum/                    # Core Quantum SDK
│   ├── PhotonRealtime/             # Networking layer
│   └── QuantumMenu/                # Built-in menu system
├── Scenes/
├── Settings/                       # URP settings
└── Resources/
```

### Key Principles for Organization

1. **All your code goes in `QuantumUser/`** — never modify files under `Photon/`
2. **Simulation/ has zero Unity dependencies** — it references only `Quantum.Simulation` assembly
3. **View/ references both Unity and Quantum** — it reads simulation state to drive visuals
4. **DSL files (`.qtn`) define all data** — components, inputs, events, signals
5. **Generated/ folders are output-only** — CodeGen fills these; never hand-edit

## Step-by-Step: Building Your First Game

### Step 1: Define Your Data (DSL)

Create `Assets/QuantumUser/Simulation/DSL/GameComponents.qtn`:

```qtn
// Player input — what buttons/axes the player can press
input {
    button Jump;
    button Attack;
    FPVector2 Movement;
}

// Global state shared across all players
global {
    Int32 GameTimer;
    Int32 AlivePlayerCount;
}

// Components — pure data attached to entities
component PlayerLink {
    PlayerRef Player;
}

component Health {
    FP Current;
    FP Max;
}

component Movement {
    FP Speed;
    FP JumpForce;
    FPVector2 Velocity;
}

component Weapon {
    FP Damage;
    FP Cooldown;
    FP CooldownTimer;
}

// Signals — system-to-system communication
signal OnPlayerDeath(EntityRef player);
signal OnPlayerSpawn(EntityRef player, PlayerRef playerRef);

// Events — simulation-to-view communication (one-way, not deterministic)
event PlayerDamaged {
    EntityRef Entity;
    FP Damage;
}

event GameOver {
    PlayerRef Winner;
}
```

After saving, trigger CodeGen in Unity: **Quantum > Code Generation > Run All**

### Step 2: Register Your Systems

Edit `Assets/QuantumUser/Simulation/SystemSetup.User.cs`:

```csharp
namespace Quantum
{
    using System.Collections.Generic;

    public static partial class DeterministicSystemSetup
    {
        static partial void AddSystemsUser(
            ICollection<SystemBase> systems,
            RuntimeConfig gameConfig,
            SimulationConfig simulationConfig,
            SystemsConfig systemsConfig)
        {
            // Systems execute in the order they are added
            systems.Add(new PlayerSpawnSystem());
            systems.Add(new MovementSystem());
            systems.Add(new CombatSystem());
            systems.Add(new GameTimerSystem());
        }
    }
}
```

### Step 3: Write Systems

Create `Assets/QuantumUser/Simulation/Systems/MovementSystem.cs`:

```csharp
namespace Quantum
{
    using Photon.Deterministic;

    public unsafe class MovementSystem : SystemMainThreadFilter<MovementSystem.Filter>
    {
        public struct Filter
        {
            public EntityRef Entity;
            public CharacterController3D* Kcc;
            public Movement* Movement;
            public PlayerLink* PlayerLink;
        }

        public override void Update(Frame frame, ref Filter filter)
        {
            var input = frame.GetPlayerInput(filter.PlayerLink->Player);

            // Read input and apply movement
            var direction = new FPVector3(
                input->Movement.X,
                FP._0,
                input->Movement.Y
            );

            if (direction.SqrMagnitude > FP._1)
            {
                direction = direction.Normalized;
            }

            filter.Kcc->Move(frame, filter.Entity, direction * filter.Movement->Speed);

            // Handle jump
            if (input->Jump.WasPressed)
            {
                filter.Kcc->Jump(frame, filter.Entity, filter.Movement->JumpForce);
            }
        }
    }
}
```

Create `Assets/QuantumUser/Simulation/Systems/CombatSystem.cs`:

```csharp
namespace Quantum
{
    using Photon.Deterministic;

    public unsafe class CombatSystem : SystemMainThreadFilter<CombatSystem.Filter>,
        ISignalOnPlayerDeath
    {
        public struct Filter
        {
            public EntityRef Entity;
            public Weapon* Weapon;
            public PlayerLink* PlayerLink;
        }

        public override void Update(Frame frame, ref Filter filter)
        {
            var input = frame.GetPlayerInput(filter.PlayerLink->Player);

            // Tick cooldown
            if (filter.Weapon->CooldownTimer > FP._0)
            {
                filter.Weapon->CooldownTimer -= frame.DeltaTime;
                return;
            }

            if (input->Attack.WasPressed)
            {
                filter.Weapon->CooldownTimer = filter.Weapon->Cooldown;
                PerformAttack(frame, filter.Entity, filter.Weapon);
            }
        }

        private void PerformAttack(Frame frame, EntityRef attacker, Weapon* weapon)
        {
            // Use Quantum's deterministic physics for hit detection
            var transform = frame.Get<Transform3D>(attacker);
            var hits = frame.Physics3D.OverlapShape(
                transform.Position + transform.Forward,
                FPQuaternion.Identity,
                Shape3D.CreateSphere(FP._1)
            );

            for (int i = 0; i < hits.Count; i++)
            {
                var hit = hits[i];
                if (hit.Entity == attacker) continue;

                if (frame.TryGet<Health>(hit.Entity, out var health))
                {
                    health.Current -= weapon->Damage;
                    frame.Set(hit.Entity, health);

                    // Fire event to view layer (for VFX, sound)
                    frame.Events.PlayerDamaged(hit.Entity, weapon->Damage);

                    if (health.Current <= FP._0)
                    {
                        frame.Signals.OnPlayerDeath(hit.Entity);
                    }
                }
            }
        }

        public void OnPlayerDeath(Frame frame, EntityRef player)
        {
            frame.Global->AlivePlayerCount--;
            frame.Destroy(player);

            if (frame.Global->AlivePlayerCount <= 1)
            {
                // Find the winner and fire game over event
            }
        }
    }
}
```

### Step 4: Collect Input (View Layer)

Create `Assets/QuantumUser/View/Input/LocalInput.cs`:

```csharp
using UnityEngine;
using Quantum;
using Photon.Deterministic;

public class LocalInput : MonoBehaviour
{
    private void OnEnable()
    {
        QuantumCallback.Subscribe(this, (CallbackPollInput callback) => PollInput(callback));
    }

    public void PollInput(CallbackPollInput callback)
    {
        var input = new Quantum.Input();

        // Read Unity input and convert to Quantum input
        var moveDir = new Vector2(
            UnityEngine.Input.GetAxisRaw("Horizontal"),
            UnityEngine.Input.GetAxisRaw("Vertical")
        );

        input.Movement = moveDir.ToFPVector2();
        input.Jump = UnityEngine.Input.GetKey(KeyCode.Space);
        input.Attack = UnityEngine.Input.GetMouseButton(0);

        callback.SetInput(input, DeterministicInputFlags.Repeatable);
    }
}
```

### Step 5: Create Entity Views (View Layer)

Create `Assets/QuantumUser/View/EntityViews/PlayerView.cs`:

```csharp
using UnityEngine;
using Quantum;

public class PlayerView : QuantumEntityViewComponent
{
    [SerializeField] private Animator animator;
    [SerializeField] private ParticleSystem hitEffect;

    public override void OnActivate(Frame frame)
    {
        // Called when entity is created
    }

    public override void OnUpdateView()
    {
        // Called every render frame — interpolate visuals here
        if (PredictedFrame.TryGet<Movement>(EntityRef, out var movement))
        {
            // Drive animations based on simulation state
            animator.SetFloat("Speed", movement.Velocity.Magnitude.AsFloat);
        }
    }

    public override void OnDeactivate()
    {
        // Called when entity is destroyed
    }
}
```

### Step 6: Handle Events (View Layer)

Create `Assets/QuantumUser/View/Events/GameEventHandler.cs`:

```csharp
using UnityEngine;
using Quantum;

public class GameEventHandler : MonoBehaviour
{
    private void OnEnable()
    {
        QuantumEvent.Subscribe<EventPlayerDamaged>(this, OnPlayerDamaged);
        QuantumEvent.Subscribe<EventGameOver>(this, OnGameOver);
    }

    private void OnPlayerDamaged(EventPlayerDamaged e)
    {
        // Spawn VFX, play sound — purely cosmetic
        Debug.Log($"Entity {e.Entity} took {e.Damage} damage");
    }

    private void OnGameOver(EventGameOver e)
    {
        Debug.Log($"Game Over! Winner: {e.Winner}");
    }

    private void OnDisable()
    {
        QuantumEvent.UnsubscribeListener(this);
    }
}
```

## Configuration

### Photon App Setup

1. Go to [Photon Dashboard](https://dashboard.photonengine.com)
2. Create a new **Quantum** app
3. Copy the App ID
4. In Unity: **Photon > Quantum > Setup** — paste your App ID into `PhotonServerSettings`

### SimulationConfig

Located in `Assets/QuantumUser/Resources/`. Controls:
- **Tick Rate**: Default 60. Higher = smoother but more CPU. 30 is fine for turn-based or slower games.
- **Rollback Window**: How many frames can be rolled back. Default 60 (1 second at 60Hz).
- **Input Delay**: Frames of input delay before prediction kicks in. Higher = fewer rollbacks but more latency feel.

### RuntimeConfig

Extended via `RuntimeConfig.User.cs`. Use this for game-wide settings that can vary per match:

```csharp
namespace Quantum
{
    partial class RuntimeConfig
    {
        public AssetRef<EntityPrototype> PlayerPrototype;
        public FP RoundDuration;
        public Int32 MaxPlayers;
    }
}
```

### RuntimePlayer

Extended via `RuntimePlayer.User.cs`. Per-player data sent when joining:

```csharp
namespace Quantum
{
    partial class RuntimePlayer
    {
        public AssetRef<EntityPrototype> SelectedCharacter;
        public string Nickname;
    }
}
```

## Entity Prototypes

Prototypes are Quantum's equivalent of prefabs for the simulation layer. They define what components an entity starts with.

1. Create a Unity GameObject
2. Add `QuantumEntityPrototype` component
3. Add Quantum component scripts (e.g., `QPrototypeHealth`, `QPrototypeMovement`)
4. Configure default values in the Inspector
5. Save as a prefab in `Assets/QuantumUser/Resources/`

The `EntityPrototype` asset is what the simulation references. The GameObject with `EntityView` is what Unity renders.

## Key Rules for Determinism

1. **Never use `float`** in simulation — use `FP` (fixed-point)
2. **Never use `System.Random`** — use `frame.RNG`
3. **Never use Unity API** in simulation code
4. **Never use `DateTime`** or system clock in simulation
5. **Never use `Dictionary`** (non-deterministic iteration order) — use Quantum's collections
6. **Never use LINQ** in simulation (allocations break determinism guarantees)
7. **Component order matters** — always iterate in a deterministic order
8. **Floating-point literals** must be converted: `FP._1` not `1.0f`, `FP.FromFloat_UNSAFE(0.5f)` for constants

## Common Patterns

### Spawning Players

```csharp
public class PlayerSpawnSystem : SystemSignalsOnly, ISignalOnPlayerAdded
{
    public void OnPlayerAdded(Frame frame, PlayerRef player, bool firstTime)
    {
        var runtimePlayer = frame.GetPlayerData(player);
        var entity = frame.Create(runtimePlayer.SelectedCharacter);

        if (frame.TryGet<PlayerLink>(entity, out var link))
        {
            link.Player = player;
            frame.Set(entity, link);
        }

        frame.Signals.OnPlayerSpawn(entity, player);
    }
}
```

### Commands (Client-to-Simulation RPC)

For actions that don't fit the per-tick input model (e.g., buying items, chat):

```qtn
command BuyItem {
    Int32 ItemId;
    Int32 Quantity;
}
```

```csharp
// In CommandSetup.User.cs
public static partial class CommandSetup
{
    static partial void AddCommandHandlersUser(CommandHandlerList handlers)
    {
        handlers.Add<BuyItem>(BuyItemHandler);
    }

    private static void BuyItemHandler(Frame frame, BuyItem command, PlayerRef player)
    {
        // Process purchase
    }
}
```

### Timer Pattern

```csharp
public unsafe class GameTimerSystem : SystemMainThread
{
    public override void Update(Frame frame)
    {
        frame.Global->GameTimer++;

        if (frame.Global->GameTimer >= frame.RuntimeConfig.RoundDuration * frame.SessionConfig.TickRate)
        {
            // Round over
        }
    }
}
```

## Development Workflow

1. **Define data** → Write `.qtn` files
2. **Run CodeGen** → Quantum menu in Unity Editor
3. **Write systems** → Implement game logic
4. **Register systems** → `SystemSetup.User.cs`
5. **Create prototypes** → Entity prefabs with Quantum components
6. **Build view** → MonoBehaviours that read simulation state
7. **Test locally** → Use Quantum's local multiplayer (multiple players in editor)
8. **Test online** → Connect to Photon Cloud

## Debugging Tips

- **QuantumConsole**: Enable in-game debug overlay showing tick, predicted frame, rollback count
- **Frame Diff**: Quantum can detect desync by comparing frame checksums across clients
- **Replay System**: Record and replay matches deterministically for debugging
- **Local Multiplayer**: Test with multiple local players before going online

## References

- Photon Quantum Documentation: https://doc.photonengine.com/quantum/current/getting-started/overview
- Photon Dashboard: https://dashboard.photonengine.com
- Quantum API Reference: https://doc-api.photonengine.com/en/quantum/current/
- Photon Quantum Blog: https://blog.photonengine.com/the-evolution-of-deterministic-multiplayer-photon-quantum-now-a-unity-verified-solution/
