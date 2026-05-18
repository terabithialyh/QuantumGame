# Setting Up Photon Quantum on a Local Area Network (LAN)

## Overview

This guide covers how to run Photon Quantum multiplayer games on a LAN without requiring internet access. You'll set up a self-hosted Photon Server on one machine in your network, and configure Unity clients to connect to it directly.

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    LAN (192.168.x.x)                    │
│                                                         │
│  ┌──────────────────┐                                   │
│  │  Photon Server    │  ← Input authority                │
│  │  192.168.1.100    │  ← Collects, orders, distributes │
│  │  Port: 5055 (UDP) │     player inputs                 │
│  └────────┬─────────┘                                   │
│           │                                              │
│     ┌─────┼──────┬──────────┐                           │
│     │     │      │          │                           │
│  ┌──▼──┐ ┌▼───┐ ┌▼───┐  ┌──▼──┐                       │
│  │ PC1 │ │PC2 │ │PC3 │  │PC4  │  ← Game clients       │
│  │     │ │    │ │    │  │     │  ← Each runs full      │
│  └─────┘ └────┘ └────┘  └─────┘     simulation         │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

The Photon Server does NOT run game logic. It only:
- Accepts client connections
- Collects input from all clients each tick
- Assigns tick numbers to inputs
- Broadcasts confirmed inputs back to all clients

Each client runs the full deterministic simulation independently.

## Step 1: Download and Install Photon Server SDK

### Requirements

- A Windows machine on your LAN (Photon Server SDK is Windows-only)
- .NET Framework 4.7.2+ or .NET 6+ runtime
- Firewall configured to allow inbound UDP/TCP on ports 5055-5058

### Download

1. Go to https://www.photonengine.com/server
2. Sign in with your Photon account
3. Download "Photon Server SDK v5" (or latest)
4. Extract to a folder, e.g., `C:\PhotonServer\`

### Directory Structure After Extraction

```
C:\PhotonServer\
├── bin_Win64/
│   ├── PhotonSocketServer.exe    ← The server executable
│   └── ...
├── deploy/
│   ├── Quantum/                  ← Quantum server plugin
│   │   ├── bin/
│   │   └── Quantum.Server.dll
│   └── ...
└── doc/
```

## Step 2: Configure Photon Server

### Basic Configuration

Edit `deploy/Quantum/bin/PhotonServer.config` (or the equivalent config for your version):

```xml
<?xml version="1.0" encoding="utf-8"?>
<Configuration>
  <Quantum>
    <!-- No license needed for LAN/development (100 CCU free) -->
    <License></License>
    
    <!-- Server settings -->
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

### Firewall Rules (Windows)

Open PowerShell as Administrator:

```powershell
# Allow Photon Server UDP
New-NetFirewallRule -DisplayName "Photon Server UDP" -Direction Inbound -Protocol UDP -LocalPort 5055-5058 -Action Allow

# Allow Photon Server TCP
New-NetFirewallRule -DisplayName "Photon Server TCP" -Direction Inbound -Protocol TCP -LocalPort 4530,9090 -Action Allow
```

## Step 3: Start the Server

### Option A: Run as Console Application (Development)

```cmd
cd C:\PhotonServer\bin_Win64
PhotonSocketServer.exe /run Quantum /configPath ..\deploy\Quantum\bin
```

You should see output indicating the server is listening on the configured ports.

### Option B: Install as Windows Service (Production/Persistent)

```cmd
cd C:\PhotonServer\bin_Win64
PhotonSocketServer.exe /install Quantum /configPath ..\deploy\Quantum\bin
net start PhotonQuantum
```

### Verify Server is Running

From another machine on the LAN:

```bash
# Test UDP connectivity
nc -u -z 192.168.1.100 5055

# Or on Windows
Test-NetConnection -ComputerName 192.168.1.100 -Port 5055
```

## Step 4: Configure Unity Client

### Option A: Modify PhotonServerSettings Asset

In Unity Inspector, find `PhotonServerSettings` (usually at `Assets/Photon/Quantum/Resources/PhotonServerSettings.asset`):

- **Use Name Server**: `false` (disable — no cloud routing)
- **Server Address**: `192.168.1.100` (your Photon Server machine's LAN IP)
- **Port**: `5055`
- **Protocol**: `UDP`
- **App Id Quantum**: any non-empty string (e.g., `"lan-dev"`)

### Option B: Configure via Code

Create `Assets/QuantumUser/View/LanGameStarter.cs`:

```csharp
using System;
using UnityEngine;
using Quantum;
using Photon.Deterministic;
using Photon.Realtime;

public class LanGameStarter : MonoBehaviour
{
    [Header("LAN Settings")]
    [SerializeField] private string serverAddress = "192.168.1.100";
    [SerializeField] private int serverPort = 5055;
    [SerializeField] private ConnectionProtocol protocol = ConnectionProtocol.Udp;

    [Header("Game Settings")]
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
            Debug.Log($"Connected to LAN server. Room: {client.CurrentRoom.Name}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to connect to LAN server: {e.Message}");
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
        Debug.Log("Quantum session started on LAN");

        var playerData = new RuntimePlayer
        {
            PlayerNickname = $"LanPlayer_{UnityEngine.Random.Range(0, 9999):0000}"
        };
        runner.Game.AddPlayer(0, playerData);
    }
}
```

### Option C: Minimal Local Mode (No Server at All)

If you just want to test simulation logic without any networking:

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
        Debug.Log($"Local game started with {localPlayerCount} players");

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

## Step 5: Input Collection for Multiple Local Players

When testing with multiple players on one machine, you need to route different inputs to different players:

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
            case 0: // Player 1: WASD + Space
                input.Movement = new FPVector2(
                    GetAxis(KeyCode.A, KeyCode.D),
                    GetAxis(KeyCode.S, KeyCode.W)
                );
                input.Jump = UnityEngine.Input.GetKey(KeyCode.Space);
                input.Attack = UnityEngine.Input.GetKey(KeyCode.F);
                break;

            case 1: // Player 2: Arrow keys + Enter
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

## Step 6: Scene Setup in Unity

1. Create a new scene or use an existing game scene
2. Add a GameObject with `LanGameStarter` (or `LocalGameStarter`)
3. Add a GameObject with your input script (`MultiLocalInput`)
4. Assign `RuntimeConfig` and `SessionConfig` references in the Inspector
5. Create a simple UI with "Create Room" and "Join Room" buttons wired to `StartLanGame(true)` / `StartLanGame(false)`

## Troubleshooting

### Cannot Connect to Server

| Symptom | Cause | Fix |
|---------|-------|-----|
| Connection timeout | Firewall blocking | Open ports 5055-5058 on server machine |
| Connection refused | Server not running | Start PhotonSocketServer.exe |
| "AppId missing" | Empty AppId | Set any non-empty string in AppSettings |
| DNS resolution fail | Using hostname | Use IP address directly (e.g., 192.168.1.100) |

### Finding Your Server's LAN IP

**Windows (server machine):**
```cmd
ipconfig
```
Look for "IPv4 Address" under your active adapter (usually 192.168.x.x or 10.x.x.x).

**macOS/Linux (server machine):**
```bash
ifconfig | grep "inet " | grep -v 127.0.0.1
```

### Desync Issues on LAN

Even on LAN, desync can happen if:
- Different Unity versions on different machines
- Different Quantum SDK versions
- Using `float` instead of `FP` in simulation code
- Non-deterministic iteration (Dictionary, HashSet)

Enable frame checksums to detect desync early:
```csharp
sessionRunnerArgs.SessionConfig.ChecksumInterval = 30; // Check every 30 ticks
```

## Comparison of All Modes

| Feature | Local | LAN (Self-hosted) | Cloud |
|---------|-------|-------------------|-------|
| Server needed | No | Yes (Windows) | No (Photon manages) |
| Internet required | No | No | Yes |
| Predict/Rollback | No | Yes | Yes |
| Latency testing | No | Yes (near-zero) | Yes (real-world) |
| Max CCU (free) | Unlimited | 100 | 20 |
| Setup complexity | Minimal | Medium | Minimal |
| Use case | Logic testing | LAN parties, dev team testing | Production |

## macOS/Linux as Server (Alternative)

Photon Server SDK is Windows-only. If your LAN only has macOS/Linux machines, alternatives:

1. **Run Photon Server in a Windows VM** (Parallels, VirtualBox) — bridge networking so it gets a LAN IP
2. **Use Docker with Wine** — community solutions exist but are not officially supported
3. **Use Photon Cloud with a free tier** — 20 CCU, requires internet but zero server setup
4. **Use a cheap Windows VPS on your LAN** — a Raspberry Pi won't work, but any old Windows PC will

For your current macOS setup, the simplest path is either a Windows VM or just using the free Photon Cloud tier for development.
