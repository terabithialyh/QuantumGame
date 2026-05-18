# Quantum 确定性物理系统

## 概述

Quantum 包含一个完全确定性的物理引擎，运行在模拟层内部。它支持 2D 和 3D 物理，包括碰撞体、刚体、触发器、关节和射线检测 — 全部使用定点数学（`FP`）来保证所有客户端上的结果完全一致。

与 Unity 的 PhysX 不同，Quantum 的物理是：
- **确定性的** — 相同输入在任何平台上始终产生相同输出
- **与 ECS 集成** — 物理组件就是普通的 Quantum 组件
- **从 Unity 烘焙** — 静态碰撞体在构建时从 Unity 场景导出
- **支持回滚** — 物理状态是 Frame 的一部分，可以被回滚

## 架构

```
┌─────────────────────────────────────────────────────┐
│                  UNITY 编辑器                         │
│  QuantumStaticBoxCollider3D, QuantumStaticSphere...  │
│  (创作工具 — 构建时烘焙到 Map 资产中)                │
└──────────────────────┬──────────────────────────────┘
                       │ Map 烘焙
┌──────────────────────▼──────────────────────────────┐
│              QUANTUM 模拟层                           │
│  PhysicsCollider2D/3D — 动态实体碰撞体               │
│  PhysicsBody2D/3D — 刚体动力学                       │
│  静态碰撞体 — 烘焙到 Map 资产中                      │
│  frame.Physics2D / frame.Physics3D — 查询 API        │
└─────────────────────────────────────────────────────┘
```

## 选择 2D 还是 3D

Quantum 独立支持两者。你可以通过程序集定义启用/禁用：

- `QUANTUM_ENABLE_PHYSICS2D` — 当 `com.unity.modules.physics2d` 存在时启用
- `QUANTUM_ENABLE_PHYSICS3D` — 当 `com.unity.modules.physics` 存在时启用

俯视角或横版游戏用 2D。完整 3D 环境用 3D。如果需要，两者可以同时使用。

## 静态碰撞体（Map 烘焙）

静态碰撞体是地图的一部分，运行时不会移动。它们在 Unity 中定义，烘焙到 Quantum Map 资产中。

### 可用的静态碰撞体类型

**3D：**
- `QuantumStaticBoxCollider3D` — 盒子形状
- `QuantumStaticSphereCollider3D` — 球体形状
- `QuantumStaticCapsuleCollider3D` — 胶囊形状
- `QuantumStaticMeshCollider3D` — 任意网格（开销大）
- `QuantumStaticTerrainCollider3D` — Unity 地形

**2D：**
- `QuantumStaticBoxCollider2D` — 矩形
- `QuantumStaticCircleCollider2D` — 圆形
- `QuantumStaticCapsuleCollider2D` — 胶囊
- `QuantumStaticEdgeCollider2D` — 线段
- `QuantumStaticPolygonCollider2D` — 凸多边形

### 设置静态碰撞体

1. 在场景中的 GameObject 上添加 `QuantumStaticBoxCollider3D`（或其他类型）脚本
2. 配置大小、偏移和旋转
3. 可选：链接一个 Unity `BoxCollider` 作为 `SourceCollider` — 它会自动同步大小/位置
4. 设置碰撞体设置（层、触发器等）
5. 烘焙地图：**Quantum > Map Baking > Bake All**

```
// Unity 场景层级中：
Environment/
├── Wall_North    [QuantumStaticBoxCollider3D]
├── Wall_South    [QuantumStaticBoxCollider3D]
├── Floor         [QuantumStaticBoxCollider3D]
└── Pillar        [QuantumStaticSphereCollider3D]
```

### 静态碰撞体设置

```csharp
public class QuantumStaticColliderSettings
{
    public int Layer;           // 物理层，用于过滤
    public bool Trigger;        // 触发器（无物理响应，只有回调）
    public AssetRef<PhysicsMaterial> PhysicsMaterial;  // 摩擦力、弹性
}
```

### Map 烘焙过程

当你烘焙地图时，Quantum：
1. 扫描场景中所有 `QuantumStatic*Collider*` 脚本
2. 将它们的变换和形状转换为定点数（`FP`）
3. 构建空间加速结构（BVH）
4. 将所有内容存储在 `Map` 资产中

静态碰撞体在运行时是不可变的 — 不能在模拟期间移动、添加或删除。

## 动态碰撞体（实体组件）

动态碰撞体附加到实体上，每帧都可以移动。

### 组件

| 组件 | 用途 |
|------|------|
| `PhysicsCollider2D` / `PhysicsCollider3D` | 形状定义（盒子、球体、胶囊） |
| `PhysicsBody2D` / `PhysicsBody3D` | 刚体动力学（质量、速度、力） |
| `PhysicsCallbacks2D` / `PhysicsCallbacks3D` | 启用碰撞/触发器回调 |
| `PhysicsJoints2D` / `PhysicsJoints3D` | 实体间的约束 |
| `Transform2D` / `Transform3D` | 位置和旋转（必需） |

### 设置动态物理实体

**在 DSL（.qtn）中：**
```qtn
component Projectile {
    FP Speed;
    FP Lifetime;
    EntityRef Owner;
}
```

**实体原型（Unity Inspector）：**
1. 创建一个 GameObject
2. 添加 `QuantumEntityPrototype`
3. 添加 `QPrototypeTransform3D`
4. 添加 `QPrototypePhysicsCollider3D` — 配置形状（球体，半径 0.5）
5. 添加 `QPrototypePhysicsBody3D` — 配置质量、重力缩放
6. 添加 `QPrototypePhysicsCallbacks3D` — 如果需要碰撞事件
7. 保存为预制体

### PhysicsBody 配置

```
PhysicsBody3D:
├── Mass: FP              — 物体质量（影响力的效果）
├── Drag: FP              — 线性阻力（速度衰减）
├── AngularDrag: FP       — 旋转阻力
├── GravityScale: FP      — 重力倍数（0 = 无重力）
├── FreezeRotation: bool  — 锁定旋转轴
├── IsKinematic: bool     — 只通过代码移动，不受力影响
└── CenterOfMass: FPVector3
```

### 形状类型

**3D 形状：**
```csharp
// 球体
Shape3D.CreateSphere(FP radius)

// 盒子
Shape3D.CreateBox(FPVector3 extents)

// 胶囊
Shape3D.CreateCapsule(FP radius, FP height)

// 复合形状（一个实体上多个形状）
Shape3D.CreateCompound()
```

**2D 形状：**
```csharp
Shape2D.CreateCircle(FP radius)
Shape2D.CreateBox(FPVector2 extents, FP rotation)
Shape2D.CreateCapsule(FP radius, FP height)
Shape2D.CreatePolygon(FPVector2[] vertices)  // 仅凸多边形
Shape2D.CreateEdge(FPVector2 start, FPVector2 end)
```

## 物理查询（射线检测、重叠检测）

使用 `frame.Physics2D` 或 `frame.Physics3D` 执行空间查询。

### 射线检测（Raycast）

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

        // 单次射线检测 — 返回第一个命中
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

### 射线检测全部（多个命中）

```csharp
// 获取射线路径上的所有实体
var hits = frame.Physics3D.RaycastAll(origin, direction, maxDistance);
for (int i = 0; i < hits.Count; i++)
{
    var hit = hits[i];
    // 处理每个命中...
}
```

### 重叠查询

```csharp
// 球体重叠 — 查找半径内的所有实体
var hits = frame.Physics3D.OverlapShape(
    position,
    FPQuaternion.Identity,
    Shape3D.CreateSphere(FP._5)  // 半径 5
);

for (int i = 0; i < hits.Count; i++)
{
    var entity = hits[i].Entity;
    // 应用范围伤害、检查距离等
}
```

### 线段检测（Linecast）

```csharp
// 检查两点之间是否有障碍物阻挡视线
var hit = frame.Physics3D.Linecast(pointA, pointB);
bool hasLineOfSight = !hit.HasValue;
```

### 层过滤

```csharp
// 只命中特定层的实体
int layerMask = (1 << LayerInfo.Enemy) | (1 << LayerInfo.Destructible);

var hit = frame.Physics3D.Raycast(origin, direction, maxDistance, layerMask);
```

## 碰撞回调

要接收碰撞事件，实体需 `PhysicsCallbacks2D` 或 `PhysicsCallbacks3D` 组件。

### 可用信号

```csharp
// 3D 碰撞信号
ISignalOnCollisionEnter3D    // 接触的第一帧
ISignalOnCollisionExit3D     // 接触结束
ISignalOnTriggerEnter3D      // 进入触发器体积
ISignalOnTriggerExit3D       // 离开触发器体积

// 2D 碰撞信号
ISignalOnCollisionEnter2D
ISignalOnCollisionExit2D
ISignalOnTriggerEnter2D
ISignalOnTriggerExit2D
```

### 实现碰撞回调

```csharp
public unsafe class DamageOnContactSystem : SystemSignalsOnly,
    ISignalOnCollisionEnter3D,
    ISignalOnTriggerEnter3D
{
    public void OnCollisionEnter3D(Frame frame, CollisionInfo3D info)
    {
        // info.Entity — 带有 PhysicsCallbacks 的实体
        // info.Other — 碰撞中的另一个实体
        // info.ContactNormal — 碰撞法线
        // info.ContactPoint — 世界空间中的接触点

        // 示例：伤害区域
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
        // 触发器体积 — 无物理响应，只有检测
        // info.Entity — 触发器实体
        // info.Other — 进入的实体

        if (frame.Has<PickupItem>(info.Entity))
        {
            // 收集拾取物
            ApplyPickup(frame, info.Entity, info.Other);
            frame.Destroy(info.Entity);
        }
    }
}
```

### 忽略碰撞

```csharp
public void OnCollisionEnter2D(Frame frame, CollisionInfo2D info)
{
    // 阻止此碰撞对的物理响应
    info.IgnoreCollision = true;
}
```

## 施加力和速度

### 直接速度控制

```csharp
public override void Update(Frame frame, ref Filter filter)
{
    var body = filter.PhysicsBody;

    // 直接设置速度
    body->Velocity = new FPVector3(FP._0, FP._10, FP._0);

    // 设置角速度
    body->AngularVelocity = new FPVector3(FP._0, FP._5, FP._0);
}
```

### 施加力

```csharp
// 施加力（受质量影响）
body->AddForce(new FPVector3(FP._0, FP._100, FP._0));

// 施加冲量（瞬间速度变化，受质量影响）
body->AddLinearImpulse(direction * force);

// 施加扭矩
body->AddTorque(new FPVector3(FP._0, FP._10, FP._0));
```

### 运动学物体（Kinematic）

运动学物体通过代码移动，不受物理力影响。它们仍然参与碰撞检测。

```csharp
// 移动运动学物体（使用这个而不是直接设置 Transform）
body->MovePosition(frame, entity, targetPosition);
body->MoveRotation(frame, entity, targetRotation);
```

## 物理材质

控制摩擦力和弹性：

```
PhysicsMaterial:
├── Friction: FP           — 表面摩擦力（0 = 冰面，1 = 橡胶）
├── Restitution: FP        — 弹性（0 = 不弹，1 = 完美弹跳）
├── FrictionCombine: enum  — 两个表面间摩擦力的组合方式
└── RestitutionCombine: enum
```

在 Unity 中创建 `PhysicsMaterial` 资产并分配给碰撞体。

## 角色控制器

Quantum 提供内置的角色控制器用于玩家移动，处理斜坡、台阶和地面检测：

```csharp
// 需要的组件：Transform3D + CharacterController3D + PhysicsCollider3D

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

        // 使用内置的地面检测、斜坡、台阶处理进行移动
        filter.Kcc->Move(frame, filter.Entity, direction * FP._5);

        // 跳跃
        if (input->Jump.WasPressed && filter.Kcc->Grounded)
        {
            filter.Kcc->Jump(frame, filter.Entity, FP._8);
        }
    }
}
```

### CharacterController3D 配置

```
CharacterController3D:
├── MaxSpeed: FP              — 最大移动速度
├── Acceleration: FP          — 达到最大速度的加速度
├── BaseJumpImpulse: FP       — 跳跃力
├── MaxSlope: FP              — 最大可行走斜坡角度（度）
├── MaxStepHeight: FP         — 最大自动攀爬台阶高度
├── Gravity: FP               — 重力倍数
└── SkinWidth: FP             — 碰撞皮肤（防止穿透）
```

## 物理层

定义碰撞层来控制哪些物体可以碰撞：

1. 打开 **Quantum > Simulation Config**
2. 在 Physics 部分定义层
3. 设置碰撞矩阵（哪些层与哪些层碰撞）

```csharp
// 在模拟代码中检查层
var collider = frame.Get<PhysicsCollider3D>(entity);
int layer = collider.Layer;

// 按层过滤查询
int enemyLayer = frame.Layers.GetLayerMask("Enemy");
var hits = frame.Physics3D.OverlapShape(pos, rot, shape, enemyLayer);
```

## 关节

用物理约束连接实体：

```csharp
// 可用的关节类型：
// - DistanceJoint — 维持两点间的距离
// - SpringJoint — 弹性连接
// - HingeJoint — 绕轴旋转

// 关节通过实体原型中的 QPrototypePhysicsJoints3D 配置
```

## 性能建议

1. **使用简单形状** — 球体和盒子最便宜；网格开销大
2. **减少动态碰撞体** — 静态碰撞体（烘焙的）几乎免费
3. **使用层** — 过滤掉不必要的碰撞检查
4. **避免在大区域使用 OverlapAll** — 使用更小的查询体积
5. **不需要物理响应时优先使用触发器**
6. **谨慎使用复合形状** — 每个子形状都增加开销
7. **在 SimulationConfig 中设置合适的宽相单元格大小**，匹配你游戏的尺度

## 常用模式

### 使用射线检测的子弹（无碰撞体）

对于高速移动的子弹，射线检测比碰撞体更可靠：

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

        // 沿移动路径射线检测以检测命中
        var hit = frame.Physics3D.Raycast(
            filter.Transform->Position,
            movement.Normalized,
            movement.Magnitude
        );

        if (hit.HasValue)
        {
            // 命中目标 — 施加伤害并销毁
            if (frame.TryGet<Health>(hit.Value.Entity, out var health))
            {
                health.Current -= filter.Projectile->Damage;
                frame.Set(hit.Value.Entity, health);
            }
            frame.Destroy(filter.Entity);
        }
        else
        {
            // 未命中 — 向前移动
            filter.Transform->Position += movement;
        }

        // 生命周期
        filter.Projectile->Lifetime -= frame.DeltaTime;
        if (filter.Projectile->Lifetime <= FP._0)
        {
            frame.Destroy(filter.Entity);
        }
    }
}
```

### 范围效果（AoE）

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
            // 伤害随距离衰减
            var transform = frame.Get<Transform3D>(entity);
            var distance = FPVector3.Distance(center, transform.Position);
            var falloff = FP._1 - (distance / radius);
            var finalDamage = damage * FPMath.Clamp(falloff, FP._0, FP._1);

            health.Current -= finalDamage;
            frame.Set(entity, health);
        }

        // 施加击退力
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

## 调试物理

- **Quantum Debug Draw**：在 QuantumEditorSettings 中启用，可在 Scene 视图中可视化碰撞体、射线和接触点
- **Physics Profiler**：显示碰撞对数量、宽相统计
- **Gizmos**：静态碰撞体脚本在 Unity 编辑器中自动绘制 Gizmos
