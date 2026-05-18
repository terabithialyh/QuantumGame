# Quantum Deterministic Physics System

## Overview

Quantum includes a fully deterministic physics engine that runs inside the simulation layer. It supports both 2D and 3D physics with colliders, rigid bodies, triggers, joints, and raycasts — all using fixed-point math (`FP`) to guarantee identical results across all clients.

Unlike Unity's PhysX, Quantum's physics is:
- **Deterministic** — same inputs always produce same outputs on any platform
- **Integrated with ECS** — physics components are regular Quantum components
- **Baked from Unity** — static colliders are exported from Unity scenes at build time
- **Rollback-safe** — physics state is part of the Frame and can be rolled back

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                  UNITY EDITOR                         │
│  QuantumStaticBoxCollider3D, QuantumStaticSphere...  │
│  (Authoring tools — baked into Map asset at build)   │
└──────────────────────┬──────────────────────────────┘
                       │ Map Baking
┌──────────────────────▼──────────────────────────────┐
│              QUANTUM SIMULATION                       │
│  PhysicsCollider2D/3D — dynamic entity colliders     │
│  PhysicsBody2D/3D — rigid body dynamics              │
│  Static Colliders — baked into Map asset             │
│  frame.Physics2D / frame.Physics3D — query API       │
└─────────────────────────────────────────────────────┘
```

## Choosing 2D vs 3D

Quantum supports both independently. You can enable/disable each via assembly defines:

- `QUANTUM_ENABLE_PHYSICS2D` — enabled when `com.unity.modules.physics2d` is present
- `QUANTUM_ENABLE_PHYSICS3D` — enabled when `com.unity.modules.physics` is present

Use 2D for top-down or side-scrolling games. Use 3D for full 3D environments. You can use both simultaneously if needed.

## Static Colliders (Map Baking)

Static colliders are part of the map and don't move at runtime. They're defined in Unity and baked into the Quantum Map asset.

### Available Static Collider Types

**3D:**
- `QuantumStaticBoxCollider3D` — box shape
- `QuantumStaticSphereCollider3D` — sphere shape
- `QuantumStaticCapsuleCollider3D` — capsule shape
- `QuantumStaticMeshCollider3D` — arbitrary mesh (expensive)
- `QuantumStaticTerrainCollider3D` — Unity terrain

**2D:**
- `QuantumStaticBoxCollider2D` — rectangle
- `QuantumStaticCircleCollider2D` — circle
- `QuantumStaticCapsuleCollider2D` — capsule
- `QuantumStaticEdgeCollider2D` — line segment
- `QuantumStaticPolygonCollider2D` — convex polygon

### Setting Up Static Colliders

1. Add a `QuantumStaticBoxCollider3D` (or other type) script to a GameObject in your scene
2. Configure size, offset, and rotation
3. Optionally link a Unity `BoxCollider` as `SourceCollider` — it will auto-sync size/position
4. Set collider settings (layer, trigger, etc.)
5. Bake the map: **Quantum > Map Baking > Bake All**

```
// In Unity scene hierarchy:
Environment/
├── Wall_North    [QuantumStaticBoxCollider3D]
├── Wall_South    [QuantumStaticBoxCollider3D]
├── Floor         [QuantumStaticBoxCollider3D]
└── Pillar        [QuantumStaticSphereCollider3D]
```

### Static Collider Settings

```csharp
public class QuantumStaticColliderSettings
{
    public int Layer;           // Physics layer for filtering
    public bool Trigger;        // Trigger (no physical response, only callbacks)
    public AssetRef<PhysicsMaterial> PhysicsMaterial;  // Friction, restitution
}
```

### Map Baking Process

When you bake the map, Quantum:
1. Scans the scene for all `QuantumStatic*Collider*` scripts
2. Converts their transforms and shapes to fixed-point (`FP`)
3. Builds a spatial acceleration structure (BVH)
4. Stores everything in the `Map` asset

Static colliders are immutable at runtime — they cannot be moved, added, or removed during simulation.

## Dynamic Colliders (Entity Components)

Dynamic colliders are attached to entities and can move every frame.

### Components

| Component | Purpose |
|-----------|---------|
| `PhysicsCollider2D` / `PhysicsCollider3D` | Shape definition (box, sphere, capsule) |
| `PhysicsBody2D` / `PhysicsBody3D` | Rigid body dynamics (mass, velocity, forces) |
| `PhysicsCallbacks2D` / `PhysicsCallbacks3D` | Enable collision/trigger callbacks |
| `PhysicsJoints2D` / `PhysicsJoints3D` | Constraints between entities |
| `Transform2D` / `Transform3D` | Position and rotation (required) |

### Setting Up a Dynamic Physics Entity

**In DSL (.qtn):**
```qtn
component Projectile {
    FP Speed;
    FP Lifetime;
    EntityRef Owner;
}
```

**Entity Prototype (Unity Inspector):**
1. Create a GameObject
2. Add `QuantumEntityPrototype`
3. Add `QPrototypeTransform3D`
4. Add `QPrototypePhysicsCollider3D` — configure shape (sphere, radius 0.5)
5. Add `QPrototypePhysicsBody3D` — configure mass, gravity scale
6. Add `QPrototypePhysicsCallbacks3D` — if you need collision events
7. Save as prefab

### PhysicsBody Configuration

```
PhysicsBody3D:
├── Mass: FP              — Mass of the body (affects forces)
├── Drag: FP              — Linear drag (velocity damping)
├── AngularDrag: FP       — Rotational drag
├── GravityScale: FP      — Multiplier for gravity (0 = no gravity)
├── FreezeRotation: bool  — Lock rotation axes
├── IsKinematic: bool     — Moved by code only, not forces
└── CenterOfMass: FPVector3
```

### Shape Types

**3D Shapes:**
```csharp
// Sphere
Shape3D.CreateSphere(FP radius)

// Box
Shape3D.CreateBox(FPVector3 extents)

// Capsule
Shape3D.CreateCapsule(FP radius, FP height)

// Compound (multiple shapes on one entity)
Shape3D.CreateCompound()
```

**2D Shapes:**
```csharp
Shape2D.CreateCircle(FP radius)
Shape2D.CreateBox(FPVector2 extents, FP rotation)
Shape2D.CreateCapsule(FP radius, FP height)
Shape2D.CreatePolygon(FPVector2[] vertices)  // convex only
Shape2D.CreateEdge(FPVector2 start, FPVector2 end)
```

## Physics Queries (Raycasts, Overlaps)

Use `frame.Physics2D` or `frame.Physics3D` to perform spatial queries.

### Raycasts

```csharp
public unsafe class WeaponSystem : SystemMainThreadFilter<WeaponSystem.Filter>
{
    public struct Filter
    {
        public EntityRef Entity;
        public Transform3D* Transform;
        public Weapon* Weapon;
        public PlayerLink* PlayerLink;
    }

    public override void Update(Frame frame, ref Filter filter)
    {
        var input = frame.GetPlayerInput(filter.PlayerLink->Player);
        if (!input->Attack.WasPressed) return;

        var origin = filter.Transform->Position;
        var direction = filter.Transform->Forward;
        var maxDistance = filter.Weapon->Range;

        // Single raycast — returns first hit
        var hit = frame.Physics3D.Raycast(origin, direction, maxDistance);
        if (hit.HasValue)
        {
            var hitEntity = hit.Value.Entity;
            var hitPoint = hit.Value.Point;
            var hitNormal = hit.Value.Normal;
            var hitDistance = hit.Value.Distance;

            if (frame.TryGet<Health>(hitEntity, out var health))
            {
                health.Current -= filter.Weapon->Damage;
                frame.Set(hitEntity, health);
            }
        }
    }
}
```

### Raycast All (Multiple Hits)

```csharp
// Get all entities along a ray
var hits = frame.Physics3D.RaycastAll(origin, direction, maxDistance);
for (int i = 0; i < hits.Count; i++)
{
    var hit = hits[i];
    // Process each hit...
}
```

### Overlap Queries

```csharp
// Sphere overlap — find all entities within radius
var hits = frame.Physics3D.OverlapShape(
    position,
    FPQuaternion.Identity,
    Shape3D.CreateSphere(FP._5)  // radius 5
);

for (int i = 0; i < hits.Count; i++)
{
    var entity = hits[i].Entity;
    // Apply area damage, check proximity, etc.
}
```

### Linecast

```csharp
// Check if anything blocks line of sight between two points
var hit = frame.Physics3D.Linecast(pointA, pointB);
bool hasLineOfSight = !hit.HasValue;
```

### Layer Filtering

```csharp
// Only hit entities on specific layers
int layerMask = (1 << LayerInfo.Enemy) | (1 << LayerInfo.Destructible);

var hit = frame.Physics3D.Raycast(origin, direction, maxDistance, layerMask);
```

## Collision Callbacks

To receive collision events, entities need the `PhysicsCallbacks2D` or `PhysicsCallbacks3D` component.

### Available Signals

```csharp
// 3D collision signals
ISignalOnCollisionEnter3D    // First frame of contact
ISignalOnCollisionExit3D     // Contact ended
ISignalOnTriggerEnter3D      // Entered trigger volume
ISignalOnTriggerExit3D       // Left trigger volume

// 2D collision signals
ISignalOnCollisionEnter2D
ISignalOnCollisionExit2D
ISignalOnTriggerEnter2D
ISignalOnTriggerExit2D
```

### Implementing Collision Callbacks

```csharp
public unsafe class DamageOnContactSystem : SystemSignalsOnly,
    ISignalOnCollisionEnter3D,
    ISignalOnTriggerEnter3D
{
    public void OnCollisionEnter3D(Frame frame, CollisionInfo3D info)
    {
        // info.Entity — the entity with PhysicsCallbacks
        // info.Other — the other entity in the collision
        // info.ContactNormal — collision normal
        // info.ContactPoint — world-space contact point

        // Example: damage zone
        if (frame.Has<DamageZone>(info.Entity) &&
            frame.TryGet<Health>(info.Other, out var health))
        {
            var zone = frame.Get<DamageZone>(info.Entity);
            health.Current -= zone.DamagePerHit;
            frame.Set(info.Other, health);
        }
    }

    public void OnTriggerEnter3D(Frame frame, ExitInfo3D info)
    {
        // Trigger volumes — no physical response, just detection
        // info.Entity — the trigger entity
        // info.Other — entity that entered

        if (frame.Has<PickupItem>(info.Entity))
        {
            // Collect the pickup
            ApplyPickup(frame, info.Entity, info.Other);
            frame.Destroy(info.Entity);
        }
    }
}
```

### Ignoring Collisions

```csharp
public void OnCollisionEnter2D(Frame frame, CollisionInfo2D info)
{
    // Prevent physical response for this collision pair
    info.IgnoreCollision = true;
}
```

## Applying Forces and Velocities

### Direct Velocity Control

```csharp
public override void Update(Frame frame, ref Filter filter)
{
    var body = filter.PhysicsBody;

    // Set velocity directly
    body->Velocity = new FPVector3(FP._0, FP._10, FP._0);

    // Set angular velocity
    body->AngularVelocity = new FPVector3(FP._0, FP._5, FP._0);
}
```

### Applying Forces

```csharp
// Apply force (affected by mass)
body->AddForce(new FPVector3(FP._0, FP._100, FP._0));

// Apply impulse (instant velocity change, affected by mass)
body->AddLinearImpulse(direction * force);

// Apply torque
body->AddTorque(new FPVector3(FP._0, FP._10, FP._0));
```

### Kinematic Bodies

Kinematic bodies are moved by code, not by physics forces. They still participate in collision detection.

```csharp
// Move kinematic body (use this instead of setting Transform directly)
body->MovePosition(frame, entity, targetPosition);
body->MoveRotation(frame, entity, targetRotation);
```

## Physics Materials

Control friction and bounciness:

```
PhysicsMaterial:
├── Friction: FP           — Surface friction (0 = ice, 1 = rubber)
├── Restitution: FP        — Bounciness (0 = no bounce, 1 = perfect bounce)
├── FrictionCombine: enum  — How to combine friction between two surfaces
└── RestitutionCombine: enum
```

Create a `PhysicsMaterial` asset in Unity and assign it to colliders.

## Character Controller

Quantum provides a built-in character controller for player movement that handles slopes, steps, and ground detection:

```csharp
// Components needed: Transform3D + CharacterController3D + PhysicsCollider3D

public unsafe class PlayerMovementSystem : SystemMainThreadFilter<PlayerMovementSystem.Filter>
{
    public struct Filter
    {
        public EntityRef Entity;
        public CharacterController3D* Kcc;
        public Transform3D* Transform;
        public PlayerLink* PlayerLink;
    }

    public override void Update(Frame frame, ref Filter filter)
    {
        var input = frame.GetPlayerInput(filter.PlayerLink->Player);

        var direction = new FPVector3(
            input->Movement.X,
            FP._0,
            input->Movement.Y
        ).Normalized;

        // Move with built-in ground detection, slopes, steps
        filter.Kcc->Move(frame, filter.Entity, direction * FP._5);

        // Jump
        if (input->Jump.WasPressed && filter.Kcc->Grounded)
        {
            filter.Kcc->Jump(frame, filter.Entity, FP._8);
        }
    }
}
```

### CharacterController3D Configuration

```
CharacterController3D:
├── MaxSpeed: FP              — Maximum movement speed
├── Acceleration: FP          — How fast to reach max speed
├── BaseJumpImpulse: FP       — Jump force
├── MaxSlope: FP              — Maximum walkable slope angle (degrees)
├── MaxStepHeight: FP         — Maximum step height to auto-climb
├── Gravity: FP               — Gravity multiplier
└── SkinWidth: FP             — Collision skin (prevents tunneling)
```

## Physics Layers

Define collision layers to control which objects can collide:

1. Open **Quantum > Simulation Config**
2. Define layers in the Physics section
3. Set up the collision matrix (which layers collide with which)

```csharp
// In simulation code, check layer
var collider = frame.Get<PhysicsCollider3D>(entity);
int layer = collider.Layer;

// Filter queries by layer
int enemyLayer = frame.Layers.GetLayerMask("Enemy");
var hits = frame.Physics3D.OverlapShape(pos, rot, shape, enemyLayer);
```

## Joints

Connect entities with physical constraints:

```csharp
// Available joint types:
// - DistanceJoint — maintains distance between two points
// - SpringJoint — elastic connection
// - HingeJoint — rotation around an axis

// Joints are configured via QPrototypePhysicsJoints3D in the entity prototype
```

## Performance Tips

1. **Use simple shapes** — spheres and boxes are cheapest; meshes are expensive
2. **Minimize dynamic colliders** — static colliders (baked) are nearly free
3. **Use layers** — filter out unnecessary collision checks
4. **Avoid OverlapAll on large areas** — use smaller query volumes
5. **Prefer triggers over collision** when you don't need physical response
6. **Use compound shapes sparingly** — each sub-shape adds cost
7. **Set appropriate broadphase cell size** in SimulationConfig for your game's scale

## Common Patterns

### Projectile with Raycast (No Collider)

For fast-moving projectiles, raycasting is more reliable than colliders:

```csharp
public unsafe class ProjectileRaycastSystem : SystemMainThreadFilter<ProjectileRaycastSystem.Filter>
{
    public struct Filter
    {
        public EntityRef Entity;
        public Transform3D* Transform;
        public Projectile* Projectile;
    }

    public override void Update(Frame frame, ref Filter filter)
    {
        var velocity = filter.Transform->Forward * filter.Projectile->Speed;
        var movement = velocity * frame.DeltaTime;

        // Raycast along movement path to detect hits
        var hit = frame.Physics3D.Raycast(
            filter.Transform->Position,
            movement.Normalized,
            movement.Magnitude
        );

        if (hit.HasValue)
        {
            // Hit something — apply damage and destroy
            if (frame.TryGet<Health>(hit.Value.Entity, out var health))
            {
                health.Current -= filter.Projectile->Damage;
                frame.Set(hit.Value.Entity, health);
            }
            frame.Destroy(filter.Entity);
        }
        else
        {
            // No hit — move forward
            filter.Transform->Position += movement;
        }

        // Lifetime
        filter.Projectile->Lifetime -= frame.DeltaTime;
        if (filter.Projectile->Lifetime <= FP._0)
        {
            frame.Destroy(filter.Entity);
        }
    }
}
```

### Area of Effect (AoE)

```csharp
public void ApplyExplosion(Frame frame, FPVector3 center, FP radius, FP damage)
{
    var hits = frame.Physics3D.OverlapShape(
        center,
        FPQuaternion.Identity,
        Shape3D.CreateSphere(radius)
    );

    for (int i = 0; i < hits.Count; i++)
    {
        var entity = hits[i].Entity;
        if (frame.TryGet<Health>(entity, out var health))
        {
            // Damage falloff by distance
            var transform = frame.Get<Transform3D>(entity);
            var distance = FPVector3.Distance(center, transform.Position);
            var falloff = FP._1 - (distance / radius);
            var finalDamage = damage * FPMath.Clamp(falloff, FP._0, FP._1);

            health.Current -= finalDamage;
            frame.Set(entity, health);
        }

        // Apply knockback force
        if (frame.TryGet<PhysicsBody3D>(entity, out var body))
        {
            var transform = frame.Get<Transform3D>(entity);
            var direction = (transform.Position - center).Normalized;
            body.AddLinearImpulse(direction * damage * FP._2);
            frame.Set(entity, body);
        }
    }
}
```

## Debugging Physics

- **Quantum Debug Draw**: Enable in QuantumEditorSettings to visualize colliders, raycasts, and contacts in Scene view
- **Physics Profiler**: Shows collision pair counts, broadphase stats
- **Gizmos**: Static collider scripts draw gizmos in the Unity editor automatically
