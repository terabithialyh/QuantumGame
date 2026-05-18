# 从零开始构建基于 Photon Quantum 的帧同步游戏

## 什么是 Photon Quantum？

Photon Quantum 是一个高性能的确定性 ECS（实体组件系统）框架，用于构建基于 **预测/回滚（Predict/Rollback）** 网络模型的多人游戏。与传统的状态同步或锁步方案不同，Quantum 使用混合模型：

- 所有客户端独立运行 **相同的确定性模拟**
- 网络上只传输 **玩家输入**（带宽极低）
- 服务器作为 **输入权威** — 收集、排序并分发输入
- 客户端 **预测** 未来帧以保证响应性
- 当权威输入到达时，如果预测错误，客户端 **回滚** 并重新模拟

这让你同时获得客户端预测的响应速度和服务器权威的一致性。

## 核心架构

### 模拟层/视图层分离

这是 Quantum 中最重要的概念。你的项目被分为两个完全独立的层：

```
┌─────────────────────────────────────────────────┐
│                   视图层 (VIEW)                   │
│  (Unity MonoBehaviour、渲染、音频、UI)            │
│  - 读取模拟状态                                   │
│  - 收集玩家输入                                   │
│  - 为视觉效果做插值/外推                          │
│  - 这里不放任何游戏逻辑                           │
└─────────────────────┬───────────────────────────┘
                      │ 读取（单向）
┌─────────────────────▼───────────────────────────┐
│               模拟层 (SIMULATION)                 │
│  (确定性 ECS — 不允许使用 Unity API)             │
│  - 所有游戏逻辑都在这里                           │
│  - 只使用定点数学 (FP)                            │
│  - System 每帧处理 Component                     │
│  - 在所有客户端上执行结果完全一致                  │
└─────────────────────────────────────────────────┘
```

**为什么这很重要：**
- 模拟层必须在每台机器上、每次执行都产生 **完全相同的结果**
- Unity 的 `float` 数学在不同平台上 **不是确定性的** — Quantum 使用 `FP`（定点数）
- 模拟代码中不能调用 `UnityEngine` API — 不能用 `Random.Range`、`Time.deltaTime`、`Physics.Raycast`
- 视图层纯粹是装饰性的 — 它可以延迟、跳帧或插值，不会影响游戏逻辑的正确性

### ECS 模型

Quantum 的 ECS 不是 Unity DOTS，而是为确定性模拟优化的自定义实现：

- **Entity（实体）**：一个轻量级 ID，附带一组组件
- **Component（组件）**：纯数据（在 `.qtn` DSL 文件中定义）。没有方法，没有逻辑。
- **System（系统）**：无状态的逻辑，每帧处理具有特定组件组合的实体
- **Frame（帧）**：给定 tick 的完整游戏状态。包含所有实体、组件、全局变量，并提供查询/修改状态的 API。

### Frame 和 Tick

Quantum 中的"帧"是一个模拟 tick（不是渲染帧）。模拟以固定 tick 率运行（默认 60 Hz）。每个 tick：

1. 解析该 tick 的输入（预测的或已确认的）
2. 所有注册的 System 按顺序执行
3. Frame 状态前进

`Frame` 对象是你在模拟中访问一切的入口：
- `frame.Get<ComponentType>(entity)` — 读取组件数据
- `frame.Set(entity, component)` — 写入组件数据
- `frame.Create(prototype)` — 生成实体
- `frame.Destroy(entity)` — 销毁实体
- `frame.Global->` — 访问全局状态

## 项目结构

以下是 Quantum 项目的推荐组织方式：

```
Assets/
├── QuantumUser/                    # 你的游戏代码放这里
│   ├── Simulation/                 # 确定性游戏逻辑
│   │   ├── Systems/                # 你的 ECS 系统
│   │   │   ├── MovementSystem.cs
│   │   │   ├── CombatSystem.cs
│   │   │   └── SpawnSystem.cs
│   │   ├── DSL/                    # .qtn 文件定义数据
│   │   │   ├── Components.qtn
│   │   │   ├── Input.qtn
│   │   │   └── Events.qtn
│   │   ├── Generated/              # 自动生成（不要编辑）
│   │   ├── SystemSetup.User.cs     # 注册你的系统
│   │   ├── CommandSetup.User.cs    # 注册命令
│   │   ├── RuntimeConfig.User.cs   # 全局游戏配置
│   │   ├── RuntimePlayer.User.cs   # 每个玩家的数据
│   │   ├── Frame.User.cs           # 扩展 Frame API
│   │   └── FrameContext.User.cs    # 扩展上下文
│   ├── View/                       # Unity 渲染层
│   │   ├── EntityViews/            # 实体的视觉表现
│   │   │   ├── PlayerView.cs
│   │   │   └── ProjectileView.cs
│   │   ├── Input/                  # 输入收集
│   │   │   └── LocalInput.cs
│   │   ├── UI/                     # 游戏 UI
│   │   └── Generated/              # 自动生成（不要编辑）
│   ├── Scenes/                     # 游戏场景
│   ├── Resources/                  # Quantum 资产（配置、原型）
│   └── Editor/                     # 编辑器工具
│       └── CodeGen/
├── Photon/                         # SDK（不要修改）
│   ├── Quantum/                    # Quantum SDK 核心
│   ├── PhotonRealtime/             # 网络层
│   └── QuantumMenu/                # 内置菜单系统
├── Scenes/
├── Settings/                       # URP 设置
└── Resources/
```

### 组织原则

1. **所有你的代码放在 `QuantumUser/`** — 永远不要修改 `Photon/` 下的文件
2. **Simulation/ 零 Unity 依赖** — 它只引用 `Quantum.Simulation` 程序集
3. **View/ 同时引用 Unity 和 Quantum** — 它读取模拟状态来驱动视觉
4. **DSL 文件（`.qtn`）定义所有数据** — 组件、输入、事件、信号
5. **Generated/ 文件夹是纯输出** — CodeGen 填充这些；永远不要手动编辑

## 分步指南：构建你的第一个游戏

### 第一步：定义数据（DSL）

创建 `Assets/QuantumUser/Simulation/DSL/GameComponents.qtn`：

```qtn
// 玩家输入 — 玩家可以按的按钮/轴
input {
    button Jump;
    button Attack;
    FPVector2 Movement;
}

// 全局状态，所有玩家共享
global {
    Int32 GameTimer;
    Int32 AlivePlayerCount;
}

// 组件 — 附加到实体上的纯数据
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

// 信号 — 系统间通信
signal OnPlayerDeath(EntityRef player);
signal OnPlayerSpawn(EntityRef player, PlayerRef playerRef);

// 事件 — 模拟层到视图层的通信（单向，非确定性）
event PlayerDamaged {
    EntityRef Entity;
    FP Damage;
}

event GameOver {
    PlayerRef Winner;
}
```

保存后，在 Unity 中触发代码生成：**Quantum > Code Generation > Run All**

### 第二步：注册系统

编辑 `Assets/QuantumUser/Simulation/SystemSetup.User.cs`：

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
            // 系统按添加顺序执行
            systems.Add(new PlayerSpawnSystem());
            systems.Add(new MovementSystem());
            systems.Add(new CombatSystem());
            systems.Add(new GameTimerSystem());
        }
    }
}
```

### 第三步：编写系统

创建 `Assets/QuantumUser/Simulation/Systems/MovementSystem.cs`：

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

            // 读取输入并应用移动
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

            // 处理跳跃
            if (input->Jump.WasPressed)
            {
                filter.Kcc->Jump(frame, filter.Entity, filter.Movement->JumpForce);
            }
        }
    }
}
```

创建 `Assets/QuantumUser/Simulation/Systems/CombatSystem.cs`：

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

            // 冷却计时
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
            // 使用 Quantum 的确定性物理进行命中检测
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

                    // 向视图层发送事件（用于特效、音效）
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
                // 找到获胜者并触发游戏结束事件
            }
        }
    }
}
```

### 第四步：收集输入（视图层）

创建 `Assets/QuantumUser/View/Input/LocalInput.cs`：

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

        // 读取 Unity 输入并转换为 Quantum 输入
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

### 第五步：创建实体视图（视图层）

创建 `Assets/QuantumUser/View/EntityViews/PlayerView.cs`：

```csharp
using UnityEngine;
using Quantum;

public class PlayerView : QuantumEntityViewComponent
{
    [SerializeField] private Animator animator;
    [SerializeField] private ParticleSystem hitEffect;

    public override void OnActivate(Frame frame)
    {
        // 实体创建时调用
    }

    public override void OnUpdateView()
    {
        // 每个渲染帧调用 — 在这里做视觉插值
        if (PredictedFrame.TryGet<Movement>(EntityRef, out var movement))
        {
            // 根据模拟状态驱动动画
            animator.SetFloat("Speed", movement.Velocity.Magnitude.AsFloat);
        }
    }

    public override void OnDeactivate()
    {
        // 实体销毁时调用
    }
}
```

### 第六步：处理事件（视图层）

创建 `Assets/QuantumUser/View/Events/GameEventHandler.cs`：

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
        // 生成特效、播放音效 — 纯装饰性
        Debug.Log($"实体 {e.Entity} 受到 {e.Damage} 点伤害");
    }

    private void OnGameOver(EventGameOver e)
    {
        Debug.Log($"游戏结束！获胜者：{e.Winner}");
    }

    private void OnDisable()
    {
        QuantumEvent.UnsubscribeListener(this);
    }
}
```

## 配置

### Photon App 设置

1. 前往 [Photon Dashboard](https://dashboard.photonengine.com)
2. 创建一个新的 **Quantum** 应用
3. 复制 App ID
4. 在 Unity 中：**Photon > Quantum > Setup** — 将 App ID 粘贴到 `PhotonServerSettings`

### SimulationConfig（模拟配置）

位于 `Assets/QuantumUser/Resources/`。控制：
- **Tick Rate（帧率）**：默认 60。更高 = 更流畅但更耗 CPU。30 对回合制或较慢的游戏足够。
- **Rollback Window（回滚窗口）**：可以回滚多少帧。默认 60（60Hz 下为 1 秒）。
- **Input Delay（输入延迟）**：预测开始前的输入延迟帧数。更高 = 更少回滚但延迟感更强。

### RuntimeConfig（运行时配置）

通过 `RuntimeConfig.User.cs` 扩展。用于每局比赛可变的全局设置：

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

### RuntimePlayer（运行时玩家数据）

通过 `RuntimePlayer.User.cs` 扩展。加入时发送的每玩家数据：

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

## 实体原型（Entity Prototypes）

原型是 Quantum 模拟层的"预制体"等价物。它们定义实体初始拥有哪些组件。

1. 创建一个 Unity GameObject
2. 添加 `QuantumEntityPrototype` 组件
3. 添加 Quantum 组件脚本（如 `QPrototypeHealth`、`QPrototypeMovement`）
4. 在 Inspector 中配置默认值
5. 保存为预制体到 `Assets/QuantumUser/Resources/`

`EntityPrototype` 资产是模拟引用的对象。带有 `EntityView` 的 GameObject 是 Unity 渲染的对象。

## 确定性关键规则

1. **永远不要在模拟中使用 `float`** — 使用 `FP`（定点数）
2. **永远不要使用 `System.Random`** — 使用 `frame.RNG`
3. **永远不要在模拟代码中使用 Unity API**
4. **永远不要在模拟中使用 `DateTime`** 或系统时钟
5. **永远不要使用 `Dictionary`**（迭代顺序不确定） — 使用 Quantum 的集合
6. **永远不要在模拟中使用 LINQ**（内存分配会破坏确定性保证）
7. **组件顺序很重要** — 始终以确定性顺序迭代
8. **浮点字面量必须转换**：用 `FP._1` 而不是 `1.0f`，常量用 `FP.FromFloat_UNSAFE(0.5f)`

## 常用模式

### 生成玩家

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

### 命令（客户端到模拟的 RPC）

用于不适合每帧输入模型的操作（如购买物品、聊天）：

```qtn
command BuyItem {
    Int32 ItemId;
    Int32 Quantity;
}
```

```csharp
// 在 CommandSetup.User.cs 中
public static partial class CommandSetup
{
    static partial void AddCommandHandlersUser(CommandHandlerList handlers)
    {
        handlers.Add<BuyItem>(BuyItemHandler);
    }

    private static void BuyItemHandler(Frame frame, BuyItem command, PlayerRef player)
    {
        // 处理购买逻辑
    }
}
```

### 计时器模式

```csharp
public unsafe class GameTimerSystem : SystemMainThread
{
    public override void Update(Frame frame)
    {
        frame.Global->GameTimer++;

        if (frame.Global->GameTimer >= frame.RuntimeConfig.RoundDuration * frame.SessionConfig.TickRate)
        {
            // 回合结束
        }
    }
}
```

## 开发工作流

1. **定义数据** → 编写 `.qtn` 文件
2. **运行 CodeGen** → Unity 编辑器中的 Quantum 菜单
3. **编写系统** → 实现游戏逻辑
4. **注册系统** → `SystemSetup.User.cs`
5. **创建原型** → 带 Quantum 组件的实体预制体
6. **构建视图** → 读取模拟状态的 MonoBehaviour
7. **本地测试** → 使用 Quantum 的本地多人（编辑器内多玩家）
8. **在线测试** → 连接到 Photon Cloud

## 调试技巧

- **QuantumConsole**：启用游戏内调试覆盖层，显示 tick、预测帧、回滚次数
- **Frame Diff**：Quantum 可以通过比较客户端间的帧校验和来检测不同步
- **回放系统**：确定性地录制和回放比赛用于调试
- **本地多人**：在上线前先用多个本地玩家测试

## 参考资料

- Photon Quantum 文档：https://doc.photonengine.com/quantum/current/getting-started/overview
- Photon Dashboard：https://dashboard.photonengine.com
- Quantum API 参考：https://doc-api.photonengine.com/en/quantum/current/
- Photon Quantum 博客：https://blog.photonengine.com/the-evolution-of-deterministic-multiplayer-photon-quantum-now-a-unity-verified-solution/
