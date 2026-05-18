# Quantum NavMesh 导航与寻路系统

## 概述

Quantum 包含一个确定性导航系统，基于 NavMesh（导航网格）构建。它提供寻路、转向和避障功能 — 全部运行在模拟层内部，使用定点数学，确保所有客户端上的行为完全一致。

该系统为 AI 控制的实体（NPC、敌人、小兵）设计，但也可用于点击移动的玩家角色。

## 架构

```
┌─────────────────────────────────────────────────────────┐
│                    UNITY 编辑器                           │
│  Unity NavMesh Surface → 烘焙到 Quantum Map 资产中       │
│  QuantumNavMeshRegion → 可切换的区域                     │
│  QuantumMapNavMeshUnity → 导入设置                       │
└──────────────────────┬──────────────────────────────────┘
                       │ Map 烘焙
┌──────────────────────▼──────────────────────────────────┐
│              QUANTUM 模拟层                               │
│                                                          │
│  NavMeshPathfinder — 计算路径（A*）                       │
│  NavMeshSteeringAgent — 平滑跟随路径                     │
│  NavMeshAvoidanceAgent — 避开其他代理                     │
│  NavMeshAvoidanceObstacle — 静态/动态障碍物              │
│                                                          │
│  frame.Navigation.* — 寻路 API                           │
└─────────────────────────────────────────────────────────┘
```

## 核心概念

### NavMesh（导航网格）

导航网格是游戏世界中可行走表面的简化表示。它是一组凸多边形的集合，定义了代理可以移动的区域。Quantum 导入 Unity 的 NavMesh 数据并将其转换为确定性格式。

### 组件

| 组件 | 用途 |
|------|------|
| `NavMeshPathfinder` | 使用 A* 在 NavMesh 上计算从 A 到 B 的路径 |
| `NavMeshSteeringAgent` | 平滑地跟随计算出的路径（速度、旋转） |
| `NavMeshAvoidanceAgent` | 本地避障，防止代理重叠 |
| `NavMeshAvoidanceObstacle` | 将实体标记为避障障碍物（自身不移动） |

### 职责分离

- **Pathfinder** 回答："从这里到那里的路径点序列是什么？"
- **Steering** 回答："如何沿着这些路径点平滑移动？"
- **Avoidance** 回答："如何不与其他移动的代理碰撞？"

你可以根据需要独立使用它们或组合使用。

## 第一步：在 Unity 中烘焙 NavMesh

### 使用 Unity 的 NavMesh Surface

1. 安装 `com.unity.ai.navigation` 包（你的项目中已有）
2. 在场景中的 GameObject 上添加 `NavMeshSurface` 组件
3. 配置代理设置（半径、高度、台阶高度、斜坡）
4. 点击 **Bake** 生成 Unity 的 NavMesh

```
场景层级：
├── Navigation/
│   └── NavMeshSurface    [NavMeshSurface 组件]
├── Environment/
│   ├── Floor             [MeshRenderer + MeshCollider]
│   ├── Walls             [MeshRenderer + MeshCollider]
│   └── Obstacles         [MeshRenderer + MeshCollider]
```

### 导入到 Quantum

1. 在你的 Map GameObject（带有 `QuantumMapData` 的那个）上添加 `QuantumMapNavMeshUnity`
2. 将 NavMesh Surface GameObject 分配到 `NavMeshSurfaces` 数组
3. 配置导入设置：

```
QuantumMapNavMeshUnity:
├── NavMeshSurfaces: [NavMeshSurface GameObject]
└── Settings:
    ├── MinRegionArea: FP        — 最小多边形面积（过滤微小三角形）
    ├── WeldVertexDistance: FP   — 合并距离小于此值的顶点
    ├── FixTriangulation: bool   — 修复退化三角形
    ├── DelaunayTriangulation: bool — 改善三角形质量
    ├── EnableQuantum_XY: bool   — 使用 XY 平面（2D 游戏）
    └── ClosestTriangleCalculation: enum — 如何找到最近点
```

4. 烘焙 Quantum 地图：**Quantum > Map Baking > Bake All**

这会将 Unity 的 NavMesh 转换为存储在 Map 资产中的 Quantum 确定性格式。

## 第二步：定义导航组件（DSL）

```qtn
component AIAgent {
    FP DetectionRange;
    FP AttackRange;
    EntityRef Target;
    byte State; // 0=空闲, 1=巡逻, 2=追击, 3=攻击
}

component PatrolRoute {
    array<FPVector3>[8] Waypoints;
    Int32 CurrentWaypoint;
    Int32 WaypointCount;
}
```

## 第三步：设置实体原型

在 Unity 中创建 AI 实体原型：

1. 创建一个 GameObject
2. 添加 `QuantumEntityPrototype`
3. 添加 `QPrototypeTransform3D`
4. 添加 `QPrototypeNavMeshPathfinder` — 配置：
   - **NavMesh Agent Config**：引用一个 NavMeshAgentConfig 资产
5. 添加 `QPrototypeNavMeshSteeringAgent` — 配置：
   - **Max Speed**、**Acceleration**、**Rotation Speed**
6. 添加 `QPrototypeNavMeshAvoidanceAgent`（可选）— 用于本地避障
7. 添加你的自定义组件原型（如 `QPrototypeAIAgent`）
8. 保存为预制体

### NavMeshAgentConfig 资产

通过 **Assets > Create > Quantum > NavMeshAgentConfig** 创建：

```
NavMeshAgentConfig:
├── Radius: FP                — 代理半径（用于避障和路径偏移）
├── MaxSpeed: FP              — 最大移动速度
├── Acceleration: FP          — 达到最大速度的加速度
├── StoppingDistance: FP      — 距目标多远时停止
├── AutoBraking: bool         — 接近目的地时减速
├── AvoidancePriority: int    — 越低 = 避障优先级越高
├── MaxSlopeAngle: FP         — 代理可通过的最大斜坡角度
├── StepHeight: FP            — 最大台阶高度
└── CacheSize: int            — 路径缓存大小（存储的路径点数）
```

## 第四步：编写导航系统

### 基础寻路系统

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
                case 0: // 空闲
                    UpdateIdle(frame, ref filter);
                    break;
                case 1: // 巡逻
                    UpdatePatrol(frame, ref filter);
                    break;
                case 2: // 追击
                    UpdateChase(frame, ref filter);
                    break;
                case 3: // 攻击
                    UpdateAttack(frame, ref filter);
                    break;
            }
        }

        private void UpdateIdle(Frame frame, ref Filter filter)
        {
            // 扫描目标
            var target = FindNearestTarget(frame, filter.Transform->Position, filter.AI->DetectionRange);
            if (target != EntityRef.None)
            {
                filter.AI->Target = target;
                filter.AI->State = 2; // 追击
            }
        }

        private void UpdateChase(Frame frame, ref Filter filter)
        {
            if (filter.AI->Target == EntityRef.None ||
                !frame.Exists(filter.AI->Target))
            {
                filter.AI->State = 0; // 回到空闲
                return;
            }

            var targetPos = frame.Get<Transform3D>(filter.AI->Target).Position;
            var distance = FPVector3.Distance(filter.Transform->Position, targetPos);

            if (distance <= filter.AI->AttackRange)
            {
                filter.AI->State = 3; // 攻击
                StopMoving(frame, filter.Entity, filter.Pathfinder);
                return;
            }

            // 设置导航目标 — 寻路器会计算路径
            frame.Navigation.SetTarget(
                filter.Entity,
                targetPos,
                frame.Map.NavMeshes["NavMesh"]  // NavMesh 名称
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

            // 如果目标移出范围，继续追击
            if (distance > filter.AI->AttackRange * FP.FromFloat_UNSAFE(1.2f))
            {
                filter.AI->State = 2;
                return;
            }

            // 执行攻击逻辑...
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

### 巡逻系统

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
            if (filter.AI->State != 1) return; // 只处理巡逻状态
            if (filter.Patrol->WaypointCount == 0) return;

            // 检查是否到达当前路径点
            var target = filter.Patrol->Waypoints[filter.Patrol->CurrentWaypoint];
            var distance = FPVector3.Distance(filter.Transform->Position, target);

            if (distance < FP.FromFloat_UNSAFE(0.5f))
            {
                // 移动到下一个路径点
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
                // 还没有路径 — 设置初始目标
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

## 第五步：导航 API 参考

### frame.Navigation

```csharp
// 设置目的地 — 代理会寻路并移动到那里
frame.Navigation.SetTarget(EntityRef entity, FPVector3 target, NavMesh* navMesh);

// 停止代理
frame.Navigation.Stop(EntityRef entity);

// 检查代理是否到达目的地
bool arrived = pathfinder->HasReachedTarget;

// 检查代理是否有有效路径
bool hasPath = pathfinder->HasPath;

// 检查寻路是否正在进行
bool computing = pathfinder->IsSearching;

// 获取代理正在移向的当前路径点
FPVector3 currentWaypoint = pathfinder->CurrentWaypoint;
```

### NavMesh 查询（不需要代理）

```csharp
// 找到世界位置在 NavMesh 上的最近点
bool found = frame.Navigation.TryGetClosestPoint(
    worldPosition,
    navMesh,
    out FPVector3 closestPoint
);

// 检查位置是否在 NavMesh 上
bool onNavMesh = frame.Navigation.IsOnNavMesh(worldPosition, navMesh);

// NavMesh 上的射线检测（检查路径是否畅通）
bool blocked = frame.Navigation.Raycast(
    startPosition,
    endPosition,
    navMesh,
    out FPVector3 hitPoint
);
```

## 第六步：NavMesh 区域（动态区域）

区域允许你在运行时开启/关闭 NavMesh 的部分 — 适用于门、桥梁、可破坏地形。

### 在 Unity 中设置

1. 创建一个带有 MeshRenderer 覆盖该区域的 GameObject
2. 添加 `QuantumNavMeshRegion` 组件
3. 设置 **Id**（字符串）— 相同 Id 的所有区域一起切换
4. 设置 **CastRegion** 为 `CastRegion`
5. 可选设置 **Cost** 使该区域通过代价更高

### 运行时切换

```csharp
public unsafe class DoorSystem : SystemSignalsOnly, ISignalOnDoorToggle
{
    public void OnDoorToggle(Frame frame, string regionId, bool open)
    {
        // 启用或禁用 NavMesh 区域
        frame.Navigation.SetRegionEnabled(regionId, open);

        // 当前正在通过此区域寻路的代理会自动重新寻路
    }
}
```

### 代价修改器

区域可以有不同的通行代价：

```csharp
// 使区域更昂贵（代理会优先选择其他路线）
frame.Navigation.SetRegionCost(regionId, FP.FromFloat_UNSAFE(3.0f));

// 重置为默认代价
frame.Navigation.SetRegionCost(regionId, FP._1);
```

使用场景：
- 沼泽/泥地区域（高代价 — 代理除非必要否则避开）
- 道路（低代价 — 代理优先选择）
- 危险区域（非常高的代价 — 只在没有替代路线时使用）

## 第七步：避障

本地避障防止多个代理在同一区域移动时重叠。

### 组件

- `NavMeshAvoidanceAgent` — 代理参与避障
- `NavMeshAvoidanceObstacle` — 将实体标记为障碍物（自身不移动）

### 配置

```
NavMeshAvoidanceAgent:
├── Radius: FP              — 避障半径（通常匹配代理半径）
├── MaxSpeed: FP            — 用于速度预测
├── Priority: int           — 低优先级的代理让路给高优先级的
└── Layer: int              — 避障层（用于过滤）

NavMeshAvoidanceObstacle:
├── Radius: FP              — 障碍物半径
├── Velocity: FPVector3     — 预测速度（用于移动障碍物）
└── Layer: int              — 避障层
```

### 工作原理

Quantum 使用 ORCA（最优互惠碰撞避免）的变体：
1. 每个代理从附近的代理/障碍物计算速度障碍
2. 找到最接近期望速度且避免所有碰撞的速度
3. 应用调整后的速度

这每个 tick 都运行，完全确定性。

### 基于优先级的避障

```qtn
component Boss {
    // Boss 实体获得高避障优先级
}
```

```csharp
// 在生成系统中，根据实体类型设置优先级
var avoidance = frame.Get<NavMeshAvoidanceAgent>(entity);
avoidance.Priority = isBoss ? 0 : 50;  // 越低 = 优先级越高
frame.Set(entity, avoidance);
```

## 第八步：点击移动的玩家角色

导航不仅仅用于 AI。以下是如何用于玩家控制的点击移动：

### DSL

```qtn
input {
    FPVector3 ClickPosition;
    button Click;
}
```

### 系统

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

            // 验证目标在 NavMesh 上
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

### 输入收集（视图层）

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

        if (UnityEngine.Input.GetMouseButtonDown(1)) // 右键点击
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

## 性能考虑

### 路径计算

- 寻路使用 A*，在大型 NavMesh 上可能开销较大
- Quantum 自动将路径计算分散到多个 tick
- 路径计算期间 `IsSearching` 为 true
- 代理在新路径准备好之前继续沿旧路径移动

### 代理数量

- 避障开销随附近代理密度增长（局部 O(n²)）
- 对于大量代理（100+），考虑：
  - 减小避障半径
  - 使用避障层限制哪些代理互相避障
  - 对远处代理禁用避障

### NavMesh 大小

- 更大的 NavMesh = 更多内存和更慢的查询
- 使用 `MinRegionArea` 过滤微小多边形
- 对于非常大的世界，考虑分割为多个 NavMesh

## 调试导航

### 在 Unity 编辑器中

- 启用 **Quantum > Debug > Navigation** 来可视化：
  - NavMesh 三角形
  - 代理路径（当前路径点）
  - 避障速度
  - 区域状态（启用/禁用）

### 运行时调试

```csharp
// 记录路径状态
if (pathfinder->HasPath)
{
    Log.Info($"代理有路径，路径点 {pathfinder->WaypointIndex}");
}
else if (pathfinder->IsSearching)
{
    Log.Info("代理正在计算路径...");
}
else
{
    Log.Info("代理没有路径");
}
```

### 常见问题

| 问题 | 原因 | 解决方案 |
|------|------|----------|
| 代理不移动 | 没有烘焙 NavMesh | 分配 NavMesh surfaces 后烘焙地图 |
| 代理卡住 | NavMesh 有间隙 | 检查 NavMesh 可视化，确保表面连接 |
| 代理穿墙 | 静态碰撞体不在 NavMesh 上 | 确保障碍物包含在 NavMesh 烘焙中 |
| 找不到路径 | 目标不在 NavMesh 上 | 使用 `TryGetClosestPoint` 吸附到 NavMesh |
| 代理重叠 | 没有避障组件 | 添加 `NavMeshAvoidanceAgent` |
| 移动抖动 | 转向配置太激进 | 降低加速度，增加平滑 |

## 完整示例：带巡逻 + 追击的敌人 AI

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
            // 无论什么状态都检查目标
            var nearestPlayer = FindNearestPlayer(frame, filter.Transform->Position, filter.AI->DetectionRange);

            switch (filter.AI->State)
            {
                case 0: // 空闲 / 巡逻
                    if (nearestPlayer != EntityRef.None)
                    {
                        filter.AI->Target = nearestPlayer;
                        filter.AI->State = 1; // 追击
                    }
                    else
                    {
                        DoPatrol(frame, ref filter);
                    }
                    break;

                case 1: // 追击
                    if (nearestPlayer == EntityRef.None)
                    {
                        filter.AI->Target = EntityRef.None;
                        filter.AI->State = 0; // 返回巡逻
                        ReturnToPatrolOrigin(frame, ref filter);
                        break;
                    }

                    filter.AI->Target = nearestPlayer;
                    var targetPos = frame.Get<Transform3D>(nearestPlayer).Position;
                    var dist = FPVector3.Distance(filter.Transform->Position, targetPos);

                    if (dist <= filter.AI->AttackRange)
                    {
                        filter.AI->State = 2; // 攻击
                        frame.Navigation.Stop(filter.Entity);
                    }
                    else
                    {
                        frame.Navigation.SetTarget(filter.Entity, targetPos, frame.Map.NavMeshes["NavMesh"]);
                    }
                    break;

                case 2: // 攻击
                    if (!frame.Exists(filter.AI->Target))
                    {
                        filter.AI->State = 0;
                        break;
                    }

                    var attackTargetPos = frame.Get<Transform3D>(filter.AI->Target).Position;
                    var attackDist = FPVector3.Distance(filter.Transform->Position, attackTargetPos);

                    if (attackDist > filter.AI->AttackRange * FP.FromFloat_UNSAFE(1.3f))
                    {
                        filter.AI->State = 1; // 继续追击
                        break;
                    }

                    // 攻击冷却
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
            // 在路径点等待
            if (filter.Patrol->WaitTimer > FP._0)
            {
                filter.Patrol->WaitTimer -= frame.DeltaTime;
                return;
            }

            // 如果到达或没有路径，选择新的随机点
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

## 参考资料

- Photon Quantum 导航文档：https://doc.photonengine.com/quantum/current/manual/navigation/navigation-overview
- Unity AI Navigation 包：https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/index.html
