# 在局域网 (LAN) 上配置 Photon Quantum

## 概述

本指南介绍如何在局域网内运行 Photon Quantum 多人游戏，无需互联网。你需要在局域网内的一台机器上搭建自托管的 Photon Server，然后配置 Unity 客户端直接连接它。

## 架构

```
┌─────────────────────────────────────────────────────────┐
│                    局域网 (192.168.x.x)                   │
│                                                         │
│  ┌──────────────────┐                                   │
│  │  Photon Server    │  ← 输入权威                       │
│  │  192.168.1.100    │  ← 收集、排序、分发               │
│  │  端口: 5055 (UDP) │     玩家输入                      │
│  └────────┬─────────┘                                   │
│           │                                              │
│     ┌─────┼──────┬──────────┐                           │
│     │     │      │          │                           │
│  ┌──▼──┐ ┌▼───┐ ┌▼───┐  ┌──▼──┐                       │
│  │ PC1 │ │PC2 │ │PC3 │  │PC4  │  ← 游戏客户端         │
│  │     │ │    │ │    │  │     │  ← 每个都运行完整      │
│  └─────┘ └────┘ └────┘  └─────┘     模拟               │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

Photon Server 不运行游戏逻辑。它只负责：
- 接受客户端连接
- 每个 tick 收集所有客户端的输入
- 为输入分配 tick 编号
- 将确认的输入广播回所有客户端

每个客户端独立运行完整的确定性模拟。

## 第一步：下载并安装 Photon Server SDK

### 系统要求

- 局域网内一台 Windows 机器（Photon Server SDK 仅支持 Windows）
- .NET Framework 4.7.2+ 或 .NET 6+ 运行时
- 防火墙配置允许 UDP/TCP 端口 5055-5058 的入站连接

### 下载

1. 前往 https://www.photonengine.com/server
2. 用你的 Photon 账号登录
3. 下载 "Photon Server SDK v5"（或最新版）
4. 解压到一个文件夹，例如 `C:\PhotonServer\`

### 解压后的目录结构

```
C:\PhotonServer\
├── bin_Win64/
│   ├── PhotonSocketServer.exe    ← 服务器可执行文件
│   └── ...
├── deploy/
│   ├── Quantum/                  ← Quantum 服务器插件
│   │   ├── bin/
│   │   └── Quantum.Server.dll
│   └── ...
└── doc/
```

## 第二步：配置 Photon Server

### 基本配置

编辑 `deploy/Quantum/bin/PhotonServer.config`（或你版本对应的配置文件）：

```xml
<?xml version="1.0" encoding="utf-8"?>
<Configuration>
  <Quantum>
    <!-- 局域网/开发不需要许可证（免费 100 CCU） -->
    <License></License>
    
    <!-- 服务器设置 -->
    <UDPListener>
      <Port>5055</Port>
      <OverrideApplication>Quantum</OverrideApplication>
    </UDPListener>
    
    <TCPListener>
      <Port>4530</Port>
      <OverrideApplication>Quantum</OverrideApplication>
    </TCPListener>
    
    <WebSocketListener>
      <Port>9090</Port>
      <OverrideApplication>Quantum</OverrideApplication>
    </WebSocketListener>
  </Quantum>
</Configuration>
```

### 防火墙规则（Windows）

以管理员身份打开 PowerShell：

```powershell
# 允许 Photon Server UDP
New-NetFirewallRule -DisplayName "Photon Server UDP" -Direction Inbound -Protocol UDP -LocalPort 5055-5058 -Action Allow

# 允许 Photon Server TCP
New-NetFirewallRule -DisplayName "Photon Server TCP" -Direction Inbound -Protocol TCP -LocalPort 4530,9090 -Action Allow
```

## 第三步：启动服务器

### 方式 A：作为控制台应用运行（开发用）

```cmd
cd C:\PhotonServer\bin_Win64
PhotonSocketServer.exe /run Quantum /configPath ..\deploy\Quantum\bin
```

你应该能看到输出信息，表明服务器正在监听配置的端口。

### 方式 B：安装为 Windows 服务（持久运行）

```cmd
cd C:\PhotonServer\bin_Win64
PhotonSocketServer.exe /install Quantum /configPath ..\deploy\Quantum\bin
net start PhotonQuantum
```

### 验证服务器是否运行

从局域网内另一台机器：

```bash
# 测试 UDP 连通性
nc -u -z 192.168.1.100 5055

# 或在 Windows 上
Test-NetConnection -ComputerName 192.168.1.100 -Port 5055
```

## 第四步：配置 Unity 客户端

### 方式 A：修改 PhotonServerSettings 资产

在 Unity Inspector 中找到 `PhotonServerSettings`（通常在 `Assets/Photon/Quantum/Resources/PhotonServerSettings.asset`）：

- **Use Name Server**：`false`（禁用 — 不走云端路由）
- **Server Address**：`192.168.1.100`（你的 Photon Server 机器的局域网 IP）
- **Port**：`5055`
- **Protocol**：`UDP`
- **App Id Quantum**：任意非空字符串（如 `"lan-dev"`）

### 方式 B：通过代码配置

创建 `Assets/QuantumUser/View/LanGameStarter.cs`：

```csharp
using System;
using UnityEngine;
using Quantum;
using Photon.Deterministic;
using Photon.Realtime;

public class LanGameStarter : MonoBehaviour
{
    [Header("局域网设置")]
    [SerializeField] private string serverAddress = "192.168.1.100";
    [SerializeField] private int serverPort = 5055;
    [SerializeField] private ConnectionProtocol protocol = ConnectionProtocol.Udp;

    [Header("游戏设置")]
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private string roomName = "LanRoom";
    [SerializeField] private RuntimeConfigContainer runtimeConfig;
    [SerializeField] private QuantumDeterministicSessionConfigAsset sessionConfig;

    public async void StartLanGame(bool createRoom)
    {
        var appSettings = new AppSettings
        {
            UseNameServer = false,
            Server = serverAddress,
            Port = serverPort,
            Protocol = protocol,
            AppIdQuantum = "lan-dev",
            FixedRegion = ""
        };

        var matchmakingArgs = new MatchmakingArguments
        {
            PhotonSettings = appSettings,
            MaxPlayers = maxPlayers,
            RoomName = roomName,
            CanOnlyJoin = !createRoom,
            AuthValues = new AuthenticationValues
            {
                UserId = $"Player_{Guid.NewGuid().ToString()[..8]}"
            }
        };

        RealtimeClient client;
        try
        {
            client = await MatchmakingExtensions.ConnectToRoomAsync(matchmakingArgs);
            Debug.Log($"已连接到局域网服务器。房间: {client.CurrentRoom.Name}");
        }
        catch (Exception e)
        {
            Debug.LogError($"连接局域网服务器失败: {e.Message}");
            return;
        }

        var sessionRunnerArgs = new SessionRunner.Arguments
        {
            RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
            GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
            ClientId = client.UserId,
            RuntimeConfig = runtimeConfig.Config,
            SessionConfig = sessionConfig != null
                ? sessionConfig.Config
                : QuantumDeterministicSessionConfigAsset.DefaultConfig,
            GameMode = DeterministicGameMode.Multiplayer,
            PlayerCount = maxPlayers,
            Communicator = new QuantumNetworkCommunicator(client),
        };

        var runner = (QuantumRunner)await SessionRunner.StartAsync(sessionRunnerArgs);
        Debug.Log("Quantum 局域网会话已启动");

        var playerData = new RuntimePlayer
        {
            PlayerNickname = $"LanPlayer_{UnityEngine.Random.Range(0, 9999):0000}"
        };
        runner.Game.AddPlayer(0, playerData);
    }
}
```

### 方式 C：纯本地模式（完全不需要服务器）

如果你只想测试模拟逻辑，不需要任何网络：

```csharp
using UnityEngine;
using Quantum;
using Photon.Deterministic;

public class LocalGameStarter : MonoBehaviour
{
    [SerializeField] private int localPlayerCount = 2;
    [SerializeField] private RuntimeConfigContainer runtimeConfig;
    [SerializeField] private QuantumDeterministicSessionConfigAsset sessionConfig;

    public async void StartLocalGame()
    {
        var sessionRunnerArgs = new SessionRunner.Arguments
        {
            RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
            GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
            ClientId = "local-player",
            RuntimeConfig = runtimeConfig.Config,
            SessionConfig = sessionConfig != null
                ? sessionConfig.Config
                : QuantumDeterministicSessionConfigAsset.DefaultConfig,
            GameMode = DeterministicGameMode.Local,
            PlayerCount = localPlayerCount,
        };

        var runner = (QuantumRunner)await SessionRunner.StartAsync(sessionRunnerArgs);
        Debug.Log($"本地游戏已启动，玩家数: {localPlayerCount}");

        for (int i = 0; i < localPlayerCount; i++)
        {
            runner.Game.AddPlayer(i, new RuntimePlayer
            {
                PlayerNickname = $"LocalPlayer_{i}"
            });
        }
    }
}
```

## 第五步：多本地玩家的输入收集

在一台机器上测试多个玩家时，需要将不同的输入路由到不同的玩家：

```csharp
using UnityEngine;
using Quantum;
using Photon.Deterministic;

public class MultiLocalInput : MonoBehaviour
{
    private void OnEnable()
    {
        QuantumCallback.Subscribe(this, (CallbackPollInput callback) => PollInput(callback));
    }

    public void PollInput(CallbackPollInput callback)
    {
        var input = new Quantum.Input();

        switch (callback.PlayerSlot)
        {
            case 0: // 玩家 1：WASD + 空格
                input.Movement = new FPVector2(
                    GetAxis(KeyCode.A, KeyCode.D),
                    GetAxis(KeyCode.S, KeyCode.W)
                );
                input.Jump = UnityEngine.Input.GetKey(KeyCode.Space);
                input.Attack = UnityEngine.Input.GetKey(KeyCode.F);
                break;

            case 1: // 玩家 2：方向键 + 回车
                input.Movement = new FPVector2(
                    GetAxis(KeyCode.LeftArrow, KeyCode.RightArrow),
                    GetAxis(KeyCode.DownArrow, KeyCode.UpArrow)
                );
                input.Jump = UnityEngine.Input.GetKey(KeyCode.Return);
                input.Attack = UnityEngine.Input.GetKey(KeyCode.RightShift);
                break;
        }

        callback.SetInput(input, DeterministicInputFlags.Repeatable);
    }

    private FP GetAxis(KeyCode negative, KeyCode positive)
    {
        var value = 0;
        if (UnityEngine.Input.GetKey(negative)) value -= 1;
        if (UnityEngine.Input.GetKey(positive)) value += 1;
        return FP.FromFloat_UNSAFE(value);
    }
}
```

## 第六步：Unity 场景设置

1. 创建新场景或使用现有游戏场景
2. 添加一个 GameObject 挂载 `LanGameStarter`（或 `LocalGameStarter`）
3. 添加一个 GameObject 挂载你的输入脚本（`MultiLocalInput`）
4. 在 Inspector 中指定 `RuntimeConfig` 和 `SessionConfig` 引用
5. 创建简单 UI，用 "创建房间" 和 "加入房间" 按钮分别调用 `StartLanGame(true)` / `StartLanGame(false)`

## 故障排除

### 无法连接服务器

| 症状 | 原因 | 解决方法 |
|------|------|----------|
| 连接超时 | 防火墙阻止 | 在服务器机器上开放端口 5055-5058 |
| 连接被拒绝 | 服务器未运行 | 启动 PhotonSocketServer.exe |
| "AppId missing" | AppId 为空 | 在 AppSettings 中设置任意非空字符串 |
| DNS 解析失败 | 使用了主机名 | 直接使用 IP 地址（如 192.168.1.100） |

### 查找服务器的局域网 IP

**Windows（服务器机器）：**
```cmd
ipconfig
```
查找活动网卡下的 "IPv4 Address"（通常是 192.168.x.x 或 10.x.x.x）。

**macOS/Linux（服务器机器）：**
```bash
ifconfig | grep "inet " | grep -v 127.0.0.1
```

### 局域网上的不同步问题

即使在局域网上，不同步也可能发生，如果：
- 不同机器上的 Unity 版本不同
- Quantum SDK 版本不同
- 模拟代码中使用了 `float` 而不是 `FP`
- 使用了非确定性迭代（Dictionary、HashSet）

启用帧校验和来尽早检测不同步：
```csharp
sessionRunnerArgs.SessionConfig.ChecksumInterval = 30; // 每 30 tick 检查一次
```

## 所有模式对比

| 特性 | Local（本地） | LAN（自托管） | Cloud（云端） |
|------|--------------|--------------|--------------|
| 需要服务器 | 否 | 是（Windows） | 否（Photon 管理） |
| 需要互联网 | 否 | 否 | 是 |
| 预测/回滚 | 否 | 是 | 是 |
| 延迟测试 | 否 | 是（接近零延迟） | 是（真实网络） |
| 最大 CCU（免费） | 无限 | 100 | 20 |
| 配置复杂度 | 最低 | 中等 | 最低 |
| 适用场景 | 逻辑测试 | 局域网联机、团队开发测试 | 正式上线 |

## macOS/Linux 作为服务器（替代方案）

Photon Server SDK 仅支持 Windows。如果你的局域网只有 macOS/Linux 机器，替代方案：

1. **在 Windows 虚拟机中运行 Photon Server**（Parallels、VirtualBox）— 使用桥接网络让它获得局域网 IP
2. **使用 Docker + Wine** — 社区有方案但非官方支持
3. **使用 Photon Cloud 免费版** — 20 CCU，需要互联网但零服务器配置
4. **使用局域网内的廉价 Windows 机器** — 任何旧 Windows PC 都行

对于你当前的 macOS 环境，最简单的路径是用 Windows 虚拟机，或者直接用 Photon Cloud 免费版进行开发。
