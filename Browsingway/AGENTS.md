# Browsingway AGENTS.md

## OVERVIEW
Browsingway provides CEF (Chromium Embedded Framework) web rendering for in-game overlays via a separate renderer subprocess, D3D11 shared textures, and FlatBuffers RPC over SharedMemory.

---

## STRUCTURE
```
Browsingway/
├── Browsingway/                          # Plugin-side integration
│   ├── BrowserHost.cs                    # Entry point (partial class for HiAuRo)
│   ├── RenderProcess.cs                  # Spawns/manages renderer process
│   ├── Overlay.cs                        # ImGui window + input forwarding
│   ├── SharedTextureHandler.cs           # Opens D3D11 shared texture for ImGui
│   ├── DxHandler.cs                     # D3D11 device/queue init
│   ├── WndProcHandler.cs                # Win32 message hook
│   └── Services.cs                       # OmenTools DService wrappers
├── Browsingway.Renderer/                  # CEF renderer subprocess
│   ├── Program.cs                        # Entry point, process args deserialization
│   ├── CefHandler.cs                    # CefSharp.OffScreen init/shutdown
│   ├── TextureRenderHandler.cs           # IRenderHandler → D3D11 shared texture
│   ├── DxHandler.cs                     # D3D11 device init
│   └── Overlay.cs                        # Per-overlay ChromiumWebBrowser instance
└── Browsingway.Common/                    # Shared IPC types
    ├── BrowsingwayRpc.cs                 # Async RPC client (plugin side)
    ├── IpcBase.cs                        # SharedMemory RPC base
    ├── RenderParamsSerializer.cs         # Args serialization (Base64)
    └── FlatBuffers/
        ├── Rpc.fbs                       # Union of all messages
        ├── ToRendererMessages.fbs        # Plugin → Renderer messages
        └── ToPluginMessages.fbs         # Renderer → Plugin messages
```

---

## WHERE TO LOOK

| Task | Location | Notes |
|------|----------|-------|
| Integrate Browsingway into HiAuRo | `Browsingway/BrowserHost.cs` | Instantiate `new BrowserHost(pluginInterface)` |
| Understand overlay creation | `BrowserHost.CreateHiAuRoOverlays()` L107-131 | Creates MainWindow, QtWindow, HotkeyWindow |
| Update overlay props at runtime | `BrowserHost.UpdateOverlay()` L134-147 | URL/size/zoom/locked |
| Texture sharing flow | `SharedTextureHandler.cs` + `TextureRenderHandler.cs` | Renderer writes shared texture, plugin reads it |
| IPC message definitions | `FlatBuffers/Rpc.fbs` | NewOverlay, Navigate, Resize, Mouse, Key, etc. |
| FlatSharp code generation | `Browsingway.Common/obj/Debug/` | Generated `.g.cs` files from `.fbs` schemas |
| Spawn renderer process | `RenderProcess.cs SetupProcess()` L191-222 | Launches `out/Browsingway.Renderer.exe` |
| Crash recovery logic | `RenderProcess.EnsureRenderProcessIsAlive()` L75-141 | Auto-restart up to 5 times |

---

## KEY COMPONENTS

### Browsingway/ (Plugin Side)
- **BrowserHost** — Main entry: receives `IDalamudPluginInterface`, creates 3 overlays, wires up `UiBuilder.Draw` render callback.
- **RenderProcess** — Manages `Browsingway.Renderer.exe` subprocess: start, restart-on-crash, stop. Exposes `Rpc` (BrowsingwayRpc) for sending commands.
- **Overlay** — Wraps each browser window: ImGui window flags, mouse/key events → RPC calls, texture rendering via `SharedTextureHandler`.
- **SharedTextureHandler** — Opens renderer-provided shared D3D11 texture handle, creates shader resource view for ImGui.Image().

### Browsingway.Renderer/ (Subprocess)
- **Program.Main** — Deserializes RenderParams (Dx adapter LUID, CEF paths, IPC channel), initializes D3D11 + CEF, enters wait loop.
- **CefHandler** — Configures CefSettings (OffScreen rendering, audio, user-agent), calls `Cef.Initialize()`.
- **TextureRenderHandler** — Implements CefSharp `IRenderHandler`: `OnPaint()` writes BGRA buffer to D3D11 texture; `SharedTextureHandle` property exposes DXGI shared handle for plugin-side `OpenSharedResource`.

### Browsingway.Common/ (Shared)
- **BrowsingwayRpc** — `IpcBase` subclass with typed async methods: `NewOverlay`, `Navigate`, `ResizeOverlay`, `Zoom`, `Mute`, `MouseButton`, `KeyEvent`, etc.
- **IpcBase** — SharedMemory `RpcBuffer` wrapper: serializes FlatBuffers messages, sends via `RemoteRequestAsync`, dispatches incoming calls.
- **FlatBuffers schemas** — `Rpc.fbs` defines a table union of all messages; generated C# code lives in `obj/` (FlatSharp).

---

## CEF INTEGRATION

```
HiAuRo Plugin                              Renderer Subprocess
     │                                            │
     │  new BrowserHost(pluginInterface)         │
     │         │                                 │
     │         ▼                                 │
     │  RenderProcess.Start()                    │
     │         │  spawns                         │
     │         │ ───────────────────────────────► │
     │         │  Browsingway.Renderer.exe        │
     │         │  (args: DxLuid, CefPaths, IPC)   │
     │         │                                 │
     │         │ ◄─── IPC channel created ────── │
     │         │     (SharedMemory)              │
     │         │                                 │
     │  NewOverlay() ──────────────────────────► │ CefHandler.Init()
     │  (URL, size, GUID)                        │ ChromiumWebBrowser created
     │                                           │ TextureRenderHandler bound
     │                                           │
     │ ◄── UpdateTexture(handle) ────────────── │ SharedTextureHandle queried
     │     (DXGI shared handle)                  │ ID3D11Texture2D written each frame
     │                                           │
     │  SharedTextureHandler.OpenSharedResource │
     │         │                                 │
     │         ▼                                 │
     │  ImGui.Image(_textureId, size)            │
```

**Partial Class Pattern**: The original `Plugin.cs` is adapted as `Browsingway.BrowserHost` (renamed to avoid Dalamud plugin entry point conflicts). HiAuRo instantiates it directly instead of inheriting from `IDalamudPlugin`.

---

## BUILD NOTES

- **Browsingway.Common** must build first — its `FlatSharpSchema` target generates `.cs` from `.fbs` files into `obj/`; Common DLL is referenced by both plugin and renderer.
- **Browsingway.Renderer** requires `CefSharp.OffScreen.NETCore` + `CefSharp.Common.NETCore` (NuGet). CEF binaries resolved via custom `AssemblyResolve` at runtime from `cef` dependency path.
- **winres** post-build step patches the renderer EXE manifest (for DPI awareness).
- **Output**: Both DLLs + Renderer EXE land in `out/`. Renderer expects `out/renderer/` subdir with Common DLL.
- **NoDalamudPackager**: The `.csproj` sets `<DisableDalamudPackager>true</DisableDalamudPackager>` — this is a library, not a standalone plugin.
