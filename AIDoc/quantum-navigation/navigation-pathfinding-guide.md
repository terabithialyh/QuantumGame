# Quantum NavMesh Navigation and Pathfinding System

## Overview

Quantum includes a deterministic navigation system built on NavMesh (navigation mesh). It provides pathfinding, steering, and avoidance — all running inside the simulation layer with fixed-point math, ensuring identical behavior across all clients.

The system is designed for AI-controlled entities (NPCs, enemies, minions) but can also be used for click-to-move player characters.

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    UNITY EDITOR                           │
│  Unity NavMesh Surface → Baked into Quantum Map asset    │
│  QuantumNavMeshRegion → Toggleable areas                 │
│  QuantumMapNavMeshUnity → Import settings                │
└──────────────────────┬──────────────────────────────────┘
                       │ Map Baking
┌──────────────────────▼──────────────────────────────────┐
│              QUANTUM SIMULATION                           │
│                                                          │
│  NavMeshPathfinder — computes paths (A*)                 │
│  NavMeshSteeringAgent — follows paths smoothly           │
│  NavMeshAvoidanceAgent — avoids other agents             │
│  NavMeshAvoidanceObstacle — static/dynamic obstacles     │
│                                                          │
│  frame.Navigation.* — pathfinding API                    │
└─────────────────────────────────────────────────────────┘
```

## Key Concepts

### NavMesh

A navigation mesh is a simplified representation of walkable surfaces in your game world. It's a collection of convex polygons that define where agents can move. Quantum imports Unity's NavMesh data and converts it to a deterministic format.

### Components

| Component | Purpose |
|-----------|---------|
| `NavMeshPathfinder` | Computes paths from A to B using A* on the NavMesh |
| `NavMeshSteeringAgent` | Smoothly follows a computed path (velocity, rotation) |
| `NavMeshAvoidanceAgent` | Local avoidance to prevent agents from overlapping |
| `NavMeshAvoidanceObstacle` | Marks an entity as an obstacle for avoidance |

### Separation of Concerns

- **Pathfinder** answers: "What is the sequence of waypoints from here to there?"
- **Steering** answers: "How do I smoothly move along these waypoints?"
- **Avoidance** answers: "How do I not collide with other moving agents?"

You can use them independently or together depending on your needs.

## Step 1: Bake the NavMesh in Unity

### Using Unity's NavMesh Surface

1. Install `com.unity.ai.navigation` package (already in your project)
2. Add a `NavMeshSurface` component to a GameObject in your scene
3. Configure agent settings (radius, height, step height, slope)
4. Click **Bake** to generate Unity's NavMesh

```
Scene Hierarchy:
├── Navigation/
│   └── NavMeshSurface    [NavMeshSurface component]
├── Environment/
│   ├── Floor             [MeshRenderer + MeshCollider]
│   ├── Walls             [MeshRenderer + MeshCollider]
│   └── Obstacles         [MeshRenderer + MeshCollider]
```

### Import into Quantum

1. On your Map GameObject (the one with `QuantumMapData`), add `QuantumMapNavMeshUnity`
2. Assign the NavMesh Surface GameObjects to the `NavMeshSurfaces` array
3. Configure import settings:

```
QuantumMapNavMeshUnity:
├── NavMeshSurfaces: [NavMeshSurface GameObject]
└── Settings:
    ├── MinRegionArea: FP        — Minimum polygon area (filters tiny triangles)
    ├── WeldVertexDistance: FP   — Merge vertices closer than this
    ├── FixTriangulation: bool   — Fix degenerate triangles
    ├── DelaunayTriangulation: bool — Improve triangle quality
    ├── EnableQuantum_XY: bool   — Use XY plane (2D games)
    └── ClosestTriangleCalculation: enum — How to find nearest point
```

4. Bake the Quantum map: **Quantum > Map Baking > Bake All**

This converts Unity's NavMesh into Quantum's deterministic format stored in the Map asset.

## Step 2: Define Navigation Components (DSL)

```qtn
component AIAgent {
    FP DetectionRange;
    FP AttackRange;
    EntityRef Target;
    byte State; // 0=Idle, 1=Patrol, 2=Chase, 3=Attack
}

component PatrolRoute {
    array<FPVector3>[8] Waypoints;
    Int32 CurrentWaypoint;
    Int32 WaypointCount;
}
```

## Step 3: Set Up Entity Prototype

Create an AI entity prototype in Unity:

1. Create a GameObject
2. Add `QuantumEntityPrototype`
3. Add `QPrototypeTransform3D`
4. Add `QPrototypeNavMeshPathfinder` — configure:
   - **NavMesh Agent Config**: reference to a NavMeshAgentConfig asset
5. Add `QPrototypeNavMeshSteeringAgent` — configure:
   - **Max Speed**, **Acceleration**, **Rotation Speed**
6. Add `QPrototypeNavMeshAvoidanceAgent` (optional) — for local avoidance
7. Add your custom component prototypes (e.g., `QPrototypeAIAgent`)
8. Save as prefab

### NavMeshAgentConfig Asset

Create via **Assets > Create > Quantum > NavMeshAgentConfig**:

```
NavMeshAgentConfig:
├── Radius: FP                — Agent radius (for avoidance and path offset)
├── MaxSpeed: FP              — Maximum movement speed
├── Acceleration: FP          — How fast to reach max speed
├── StoppingDistance: FP      — Distance from target to stop
├── AutoBraking: bool         — Slow down when approaching destination
├── AvoidancePriority: int    — Lower = higher priority in avoidance
├── MaxSlopeAngle: FP         — Maximum slope the agent can traverse
├── StepHeight: FP            — Maximum step height
└── CacheSize: int            — Path cache size (waypoints stored)
```

## Step 4: Write Navigation Systems

### Basic Pathfinding System

```csharp
namespace Quantum
{
    using Photon.Deterministic;

    public unsafe class AINavigationSystem : SystemMainThreadFilter<AINavigationSystem.Filter>
    {
        public struct Filter
        {
            public EntityRef Entity;
            public Transform3D* Transform;
            public NavMeshPathfinder* Pathfinder;
            public NavMeshSteeringAgent* SteeringAgent;
            public AIAgent* AI;
        }

        public override void Update(Frame frame, ref Filter filter)
        {
            switch (filter.AI->State)
            {
                case 0: // Idle
                    UpdateIdle(frame, ref filter);
                    break;
                case 1: // Patrol
                    UpdatePatrol(frame, ref filter);
                    break;
                case 2: // Chase
                    UpdateChase(frame, ref filter);
                    break;
                case 3: // Attack
                    UpdateAttack(frame, ref filter);
                    break;
            }
        }

        private void UpdateIdle(Frame frame, ref Filter filter)
        {
            // Scan for targets
            var target = FindNearestTarget(frame, filter.Transform->Position, filter.AI->DetectionRange);
            if (target != EntityRef.None)
            {
                filter.AI->Target = target;
                filter.AI->State = 2; // Chase
            }
        }

        private void UpdateChase(Frame frame, ref Filter filter)
        {
            if (filter.AI->Target == EntityRef.None ||
                !frame.Exists(filter.AI->Target))
            {
                filter.AI->State = 0; // Back to idle
                return;
            }

            var targetPos = frame.Get<Transform3D>(filter.AI->Target).Position;
            var distance = FPVector3.Distance(filter.Transform->Position, targetPos);

            if (distance <= filter.AI->AttackRange)
            {
                filter.AI->State = 3; // Attack
                StopMoving(frame, filter.Entity, filter.Pathfinder);
                return;
            }

            // Set navigation target — pathfinder will compute the path
            frame.Navigation.SetTarget(
                filter.Entity,
                targetPos,
                frame.Map.NavMeshes["NavMesh"]  // NavMesh name
            );
        }

        private void UpdateAttack(Frame frame, ref Filter filter)
        {
            if (filter.AI->Target == EntityRef.None ||
                !frame.Exists(filter.AI->Target))
            {
                filter.AI->State = 0;
                return;
            }

            var targetPos = frame.Get<Transform3D>(filter.AI->Target).Position;
            var distance = FPVector3.Distance(filter.Transform->Position, targetPos);

            // If target moved out of range, chase again
            if (distance > filter.AI->AttackRange * FP.FromFloat_UNSAFE(1.2f))
            {
                filter.AI->State = 2;
                return;
            }

            // Perform attack logic...
        }

        private void StopMoving(Frame frame, EntityRef entity, NavMeshPathfinder* pathfinder)
        {
            frame.Navigation.Stop(entity);
        }

        private EntityRef FindNearestTarget(Frame frame, FPVector3 position, FP range)
        {
            var nearest = EntityRef.None;
            var nearestDist = range;

            var filter = frame.Filter<Transform3D, PlayerLink>();
            while (filter.NextUnsafe(out var entity, out var transform, out _))
            {
                var dist = FPVector3.Distance(position, transform->Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = entity;
                }
            }

            return nearest;
        }
    }
}
```

### Patrol System

```csharp
namespace Quantum
{
    using Photon.Deterministic;

    public unsafe class PatrolSystem : SystemMainThreadFilter<PatrolSystem.Filter>
    {
        public struct Filter
        {
            public EntityRef Entity;
            public Transform3D* Transform;
            public NavMeshPathfinder* Pathfinder;
            public PatrolRoute* Patrol;
            public AIAgent* AI;
        }

        public override void Update(Frame frame, ref Filter filter)
        {
            if (filter.AI->State != 1) return; // Only process patrol state
            if (filter.Patrol->WaypointCount == 0) return;

            // Check if we reached the current waypoint
            var target = filter.Patrol->Waypoints[filter.Patrol->CurrentWaypoint];
            var distance = FPVector3.Distance(filter.Transform->Position, target);

            if (distance < FP.FromFloat_UNSAFE(0.5f))
            {
                // Move to next waypoint
                filter.Patrol->CurrentWaypoint =
                    (filter.Patrol->CurrentWaypoint + 1) % filter.Patrol->WaypointCount;

                var nextTarget = filter.Patrol->Waypoints[filter.Patrol->CurrentWaypoint];
                frame.Navigation.SetTarget(
                    filter.Entity,
                    nextTarget,
                    frame.Map.NavMeshes["NavMesh"]
                );
            }
            else if (!filter.Pathfinder->HasPath)
            {
                // No path yet — set initial target
                frame.Navigation.SetTarget(
                    filter.Entity,
                    target,
                    frame.Map.NavMeshes["NavMesh"]
                );
            }
        }
    }
}
```

## Step 5: Navigation API Reference

### frame.Navigation

```csharp
// Set a destination — agent will pathfind and move there
frame.Navigation.SetTarget(EntityRef entity, FPVector3 target, NavMesh* navMesh);

// Stop the agent
frame.Navigation.Stop(EntityRef entity);

// Check if agent has reached destination
bool arrived = pathfinder->HasReachedTarget;

// Check if agent has a valid path
bool hasPath = pathfinder->HasPath;

// Check if pathfinding is in progress
bool computing = pathfinder->IsSearching;

// Get current waypoint the agent is moving toward
FPVector3 currentWaypoint = pathfinder->CurrentWaypoint;
```

### NavMesh Queries (Without an Agent)

```csharp
// Find the closest point on the NavMesh to a world position
bool found = frame.Navigation.TryGetClosestPoint(
    worldPosition,
    navMesh,
    out FPVector3 closestPoint
);

// Check if a position is on the NavMesh
bool onNavMesh = frame.Navigation.IsOnNavMesh(worldPosition, navMesh);

// Raycast on NavMesh (check if path is clear)
bool blocked = frame.Navigation.Raycast(
    startPosition,
    endPosition,
    navMesh,
    out FPVector3 hitPoint
);
```

## Step 6: NavMesh Regions (Dynamic Areas)

Regions allow you to toggle parts of the NavMesh on/off at runtime — useful for doors, bridges, destructible terrain.

### Setup in Unity

1. Create a GameObject with a MeshRenderer covering the area
2. Add `QuantumNavMeshRegion` component
3. Set the **Id** (string) — all regions with the same Id toggle together
4. Set **CastRegion** to `CastRegion`
5. Optionally set **Cost** to make the area more expensive to traverse

### Toggle at Runtime

```csharp
public unsafe class DoorSystem : SystemSignalsOnly, ISignalOnDoorToggle
{
    public void OnDoorToggle(Frame frame, string regionId, bool open)
    {
        // Enable or disable a NavMesh region
        frame.Navigation.SetRegionEnabled(regionId, open);

        // Agents currently pathing through this region will automatically repath
    }
}
```

### Cost Modifiers

Regions can have different traversal costs:

```csharp
// Make a region more expensive (agents will prefer other routes)
frame.Navigation.SetRegionCost(regionId, FP.FromFloat_UNSAFE(3.0f));

// Reset to default cost
frame.Navigation.SetRegionCost(regionId, FP._1);
```

Use cases:
- Swamp/mud areas (high cost — agents avoid unless necessary)
- Roads (low cost — agents prefer)
- Danger zones (very high cost — only use if no alternative)

## Step 7: Avoidance

Local avoidance prevents agents from overlapping when multiple agents move in the same area.

### Components

- `NavMeshAvoidanceAgent` — the agent participates in avoidance
- `NavMeshAvoidanceObstacle` — marks an entity as an obstacle (doesn't move itself)

### Configuration

```
NavMeshAvoidanceAgent:
├── Radius: FP              — Avoidance radius (usually matches agent radius)
├── MaxSpeed: FP            — Used for velocity prediction
├── Priority: int           — Lower priority agents yield to higher priority
└── Layer: int              — Avoidance layer (for filtering)

NavMeshAvoidanceObstacle:
├── Radius: FP              — Obstacle radius
├── Velocity: FPVector3     — Predicted velocity (for moving obstacles)
└── Layer: int              — Avoidance layer
```

### How It Works

Quantum uses a variant of ORCA (Optimal Reciprocal Collision Avoidance):
1. Each agent computes velocity obstacles from nearby agents/obstacles
2. Finds the closest velocity to its desired velocity that avoids all collisions
3. Applies the adjusted velocity

This runs every tick and is fully deterministic.

### Priority-Based Avoidance

```qtn
component Boss {
    // Boss entities get high avoidance priority
}
```

```csharp
// In your spawn system, set priority based on entity type
var avoidance = frame.Get<NavMeshAvoidanceAgent>(entity);
avoidance.Priority = isBoss ? 0 : 50;  // Lower = higher priority
frame.Set(entity, avoidance);
```

## Step 8: Click-to-Move Player Character

Navigation isn't just for AI. Here's how to use it for player-controlled click-to-move:

### DSL

```qtn
input {
    FPVector3 ClickPosition;
    button Click;
}
```

### System

```csharp
public unsafe class ClickToMoveSystem : SystemMainThreadFilter<ClickToMoveSystem.Filter>
{
    public struct Filter
    {
        public EntityRef Entity;
        public NavMeshPathfinder* Pathfinder;
        public PlayerLink* PlayerLink;
    }

    public override void Update(Frame frame, ref Filter filter)
    {
        var input = frame.GetPlayerInput(filter.PlayerLink->Player);

        if (input->Click.WasPressed)
        {
            var targetPos = input->ClickPosition;

            // Validate target is on NavMesh
            if (frame.Navigation.TryGetClosestPoint(targetPos, frame.Map.NavMeshes["NavMesh"], out var validPos))
            {
                frame.Navigation.SetTarget(
                    filter.Entity,
                    validPos,
                    frame.Map.NavMeshes["NavMesh"]
                );
            }
        }
    }
}
```

### Input Collection (View Layer)

```csharp
using UnityEngine;
using Quantum;
using Photon.Deterministic;

public class ClickToMoveInput : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask groundLayer;

    private void OnEnable()
    {
        QuantumCallback.Subscribe(this, (CallbackPollInput callback) => PollInput(callback));
    }

    public void PollInput(CallbackPollInput callback)
    {
        var input = new Quantum.Input();

        if (UnityEngine.Input.GetMouseButtonDown(1)) // Right click
        {
            var ray = mainCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 100f, groundLayer))
            {
                input.ClickPosition = hit.point.ToFPVector3();
                input.Click = true;
            }
        }

        callback.SetInput(input, DeterministicInputFlags.Repeatable);
    }
}
```

## Performance Considerations

### Path Computation

- Pathfinding uses A* which can be expensive on large NavMeshes
- Quantum spreads path computation across multiple ticks automatically
- `IsSearching` will be true while the path is being computed
- Agents continue moving on their old path until the new one is ready

### Agent Count

- Avoidance cost scales with nearby agent density (O(n²) locally)
- For large crowds (100+ agents), consider:
  - Reducing avoidance radius
  - Using avoidance layers to limit which agents avoid each other
  - Disabling avoidance for distant agents

### NavMesh Size

- Larger NavMeshes = more memory and slower queries
- Use `MinRegionArea` to filter out tiny polygons
- Consider splitting into multiple NavMeshes for very large worlds

## Debugging Navigation

### In Unity Editor

- Enable **Quantum > Debug > Navigation** to visualize:
  - NavMesh triangles
  - Agent paths (current waypoints)
  - Avoidance velocities
  - Region states (enabled/disabled)

### Runtime Debug

```csharp
// Log path state
if (pathfinder->HasPath)
{
    Log.Info($"Agent has path, waypoint {pathfinder->WaypointIndex}");
}
else if (pathfinder->IsSearching)
{
    Log.Info("Agent is computing path...");
}
else
{
    Log.Info("Agent has no path");
}
```

### Common Issues

| Problem | Cause | Solution |
|---------|-------|----------|
| Agent doesn't move | No NavMesh baked | Bake map with NavMesh surfaces assigned |
| Agent gets stuck | NavMesh gap | Check NavMesh visualization, ensure surfaces connect |
| Agent walks through walls | Static colliders not on NavMesh | Ensure obstacles are included in NavMesh bake |
| Path not found | Target off NavMesh | Use `TryGetClosestPoint` to snap to NavMesh |
| Agents overlap | No avoidance component | Add `NavMeshAvoidanceAgent` |
| Jittery movement | Steering config too aggressive | Reduce acceleration, increase smoothing |

## Complete Example: Enemy AI with Patrol + Chase

```qtn
// AI.qtn
component EnemyAI {
    FP DetectionRange;
    FP AttackRange;
    FP AttackCooldown;
    FP AttackTimer;
    EntityRef Target;
    byte State;
}

component Patrol {
    FPVector3 Origin;
    FP PatrolRadius;
    FP WaitTimer;
    FP WaitDuration;
}

signal OnEnemyAttack(EntityRef enemy, EntityRef target);
```

```csharp
namespace Quantum
{
    using Photon.Deterministic;

    public unsafe class EnemyAISystem : SystemMainThreadFilter<EnemyAISystem.Filter>
    {
        public struct Filter
        {
            public EntityRef Entity;
            public Transform3D* Transform;
            public NavMeshPathfinder* Pathfinder;
            public NavMeshSteeringAgent* Steering;
            public EnemyAI* AI;
            public Patrol* Patrol;
        }

        public override void Update(Frame frame, ref Filter filter)
        {
            // Always check for targets regardless of state
            var nearestPlayer = FindNearestPlayer(frame, filter.Transform->Position, filter.AI->DetectionRange);

            switch (filter.AI->State)
            {
                case 0: // Idle / Patrol
                    if (nearestPlayer != EntityRef.None)
                    {
                        filter.AI->Target = nearestPlayer;
                        filter.AI->State = 1; // Chase
                    }
                    else
                    {
                        DoPatrol(frame, ref filter);
                    }
                    break;

                case 1: // Chase
                    if (nearestPlayer == EntityRef.None)
                    {
                        filter.AI->Target = EntityRef.None;
                        filter.AI->State = 0; // Return to patrol
                        ReturnToPatrolOrigin(frame, ref filter);
                        break;
                    }

                    filter.AI->Target = nearestPlayer;
                    var targetPos = frame.Get<Transform3D>(nearestPlayer).Position;
                    var dist = FPVector3.Distance(filter.Transform->Position, targetPos);

                    if (dist <= filter.AI->AttackRange)
                    {
                        filter.AI->State = 2; // Attack
                        frame.Navigation.Stop(filter.Entity);
                    }
                    else
                    {
                        frame.Navigation.SetTarget(filter.Entity, targetPos, frame.Map.NavMeshes["NavMesh"]);
                    }
                    break;

                case 2: // Attack
                    if (!frame.Exists(filter.AI->Target))
                    {
                        filter.AI->State = 0;
                        break;
                    }

                    var attackTargetPos = frame.Get<Transform3D>(filter.AI->Target).Position;
                    var attackDist = FPVector3.Distance(filter.Transform->Position, attackTargetPos);

                    if (attackDist > filter.AI->AttackRange * FP.FromFloat_UNSAFE(1.3f))
                    {
                        filter.AI->State = 1; // Chase again
                        break;
                    }

                    // Attack cooldown
                    filter.AI->AttackTimer -= frame.DeltaTime;
                    if (filter.AI->AttackTimer <= FP._0)
                    {
                        filter.AI->AttackTimer = filter.AI->AttackCooldown;
                        frame.Signals.OnEnemyAttack(filter.Entity, filter.AI->Target);
                    }
                    break;
            }
        }

        private void DoPatrol(Frame frame, ref Filter filter)
        {
            // Wait at waypoint
            if (filter.Patrol->WaitTimer > FP._0)
            {
                filter.Patrol->WaitTimer -= frame.DeltaTime;
                return;
            }

            // If arrived or no path, pick new random point
            if (filter.Pathfinder->HasReachedTarget || !filter.Pathfinder->HasPath)
            {
                var randomOffset = new FPVector3(
                    frame.RNG->Next(-filter.Patrol->PatrolRadius, filter.Patrol->PatrolRadius),
                    FP._0,
                    frame.RNG->Next(-filter.Patrol->PatrolRadius, filter.Patrol->PatrolRadius)
                );

                var target = filter.Patrol->Origin + randomOffset;

                if (frame.Navigation.TryGetClosestPoint(target, frame.Map.NavMeshes["NavMesh"], out var validTarget))
                {
                    frame.Navigation.SetTarget(filter.Entity, validTarget, frame.Map.NavMeshes["NavMesh"]);
                    filter.Patrol->WaitTimer = filter.Patrol->WaitDuration;
                }
            }
        }

        private void ReturnToPatrolOrigin(Frame frame, ref Filter filter)
        {
            frame.Navigation.SetTarget(filter.Entity, filter.Patrol->Origin, frame.Map.NavMeshes["NavMesh"]);
        }

        private EntityRef FindNearestPlayer(Frame frame, FPVector3 position, FP range)
        {
            var nearest = EntityRef.None;
            var nearestDist = range;

            var playerFilter = frame.Filter<Transform3D, PlayerLink>();
            while (playerFilter.NextUnsafe(out var entity, out var transform, out _))
            {
                var dist = FPVector3.Distance(position, transform->Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = entity;
                }
            }

            return nearest;
        }
    }
}
```

## References

- Photon Quantum Navigation Docs: https://doc.photonengine.com/quantum/current/manual/navigation/navigation-overview
- Unity AI Navigation Package: https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/index.html
