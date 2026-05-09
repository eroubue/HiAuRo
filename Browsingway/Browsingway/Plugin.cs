using Browsingway.Common;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Browsingway;

/// <summary>
/// Browsingway Overlay 宿主工具 —— 被外部宿主插件（如 HiAuRo）引用使用
/// 原 Plugin.cs 改造：去掉 IDalamudPlugin 入口、命令系统、Settings，自动创建 HiAuRo 三个 overlay
/// </summary>
	public class BrowserHost : IDisposable
{
	private readonly DependencyManager _dependencyManager;
	private readonly Dictionary<Guid, Overlay> _overlays = new();
	private readonly Dictionary<string, Guid> _overlayByName = new(StringComparer.OrdinalIgnoreCase);
	private readonly string _pluginConfigDir;
	private readonly string _pluginDir;

	private RenderProcess? _renderProcess;

	public BrowserHost(IDalamudPluginInterface pluginInterface)
	{
		_ = pluginInterface.Create<Services>()!;

		_pluginDir = pluginInterface.AssemblyLocation.DirectoryName ?? "";
		if (String.IsNullOrEmpty(_pluginDir))
		{
			throw new Exception("Could not determine plugin directory");
		}

		_pluginConfigDir = pluginInterface.GetPluginConfigDirectory();
		Services.PluginLog.Info($"[BW] BrowserHost 构造: pluginDir={_pluginDir}, configDir={_pluginConfigDir}");

		_dependencyManager = new DependencyManager(_pluginDir, _pluginConfigDir);
		_dependencyManager.DependenciesReady += (_, _) => DependenciesReady();
		_dependencyManager.Initialise();

		pluginInterface.UiBuilder.Draw += Render;
		Services.PluginLog.Info("[BW] BrowserHost 构造完成 (等待依赖就绪...)");
	}

	public void Dispose()
	{
		foreach (Overlay overlay in _overlays.Values) { overlay.Dispose(); }
		_overlays.Clear();

		_renderProcess?.Dispose();

		WndProcHandler.Shutdown();
		DxHandler.Shutdown();

		_dependencyManager.Dispose();
	}

	private void DependenciesReady()
	{
		Services.PluginLog.Info("[BW] 依赖就绪, 初始化 DxHandler + WndProcHandler + RenderProcess");
		DxHandler.Initialise(Services.PluginInterface);

		WndProcHandler.Initialise(DxHandler.WindowHandle);
		WndProcHandler.WndProcMessage += OnWndProc;

		int pid = Process.GetCurrentProcess().Id;
		_renderProcess = new RenderProcess(pid, _pluginDir, _pluginConfigDir, _dependencyManager, Services.PluginLog);
		_renderProcess.Rpc!.RendererReady += msg =>
		{
			Services.PluginLog.Info($"[BW] RendererReady: DxSharedTextures={msg.HasDxSharedTexturesSupport}");
			if (!msg.HasDxSharedTexturesSupport)
			{
				Services.PluginLog.Error("Could not initialize shared textures transport. Browsingway will not work.");
				return;
			}

			Services.Framework.RunOnFrameworkThread(CreateHiAuRoOverlays);
		};
		_renderProcess.Rpc.SetCursor += msg =>
		{
			Services.Framework.RunOnFrameworkThread(() =>
			{
				Guid guid = new(msg.Guid.Span);
				Overlay? overlay = _overlays.Values.FirstOrDefault(overlay => overlay.RenderGuid == guid);
				overlay?.SetCursor(msg.Cursor);
			});
		};
		_renderProcess.Rpc.UpdateTexture += msg =>
		{
			Services.Framework.RunOnFrameworkThread(() =>
			{
				Guid guid = new(msg.Guid.Span);
				if (_overlays.TryGetValue(guid, out Overlay? overlay))
				{
					overlay.SetTexture((IntPtr)msg.TextureHandle);
				}
				else
				{
					Services.PluginLog.Error("Overlay Id not found");
				}
			});
		};
		Services.PluginLog.Info($"[BW] 启动 RenderProcess (exe路径={Path.Combine(_pluginDir, "renderer", "Browsingway.Renderer.exe")})");
		_renderProcess.Start();
	}

	private int _renderFrameCount = 0;

	private void CreateHiAuRoOverlays()
	{
		Services.PluginLog.Info($"[BW] CreateHiAuRoOverlays 开始 (overlays={_overlays.Count})");
		void Add(string name, string url, int w, int h)
		{
			var config = new InlayConfiguration
			{
				Name = name,
				Url = url,
				Zoom = 100f,
				Framerate = 30,
				Muted = true,
				CustomCss = "",
				Guid = StableGuid(name),
				Locked = true, // 默认锁定，防止 ImGui 吃掉点击
			};
			var overlay = new Overlay(_renderProcess!, config, _pluginDir);
			_overlays[config.Guid] = overlay;
			_overlayByName[name] = config.Guid;
			Services.PluginLog.Info($"[BW] 创建 overlay: {name} {w}x{h} url={url} guid={config.Guid}");
		}

		Add("MainWindow", "http://localhost:5678/main.html", 310, 480);
		Add("QtWindow", "http://localhost:5678/qt.html", 200, 50);
		Add("HotkeyWindow", "http://localhost:5678/hotkey.html", 260, 130);
		Services.PluginLog.Info($"[BW] CreateHiAuRoOverlays 完成 (共{_overlays.Count}个overlay)");
	}

	/// <summary>更新已有 overlay 的 URL / 尺寸 / 缩放 / 锁定</summary>
	public void UpdateOverlay(string name, string? url = null, int? width = null, int? height = null, float? zoom = null, bool? locked = null)
	{
		if (!_overlayByName.TryGetValue(name, out var guid) || !_overlays.TryGetValue(guid, out var overlay))
		{
			Services.PluginLog.Warning($"Overlay not found: {name}");
			return;
		}

		if (url is not null) overlay.Navigate(url);
		if (zoom is not null) overlay.Zoom(zoom.Value);
		if (locked is not null) overlay.SetLocked(locked.Value);
		if (width is not null && height is not null)
			_ = _renderProcess?.Rpc?.ResizeOverlay(guid, width.Value, height.Value);
	}

	private (bool, long) OnWndProc(WindowsMessage msg, ulong wParam, long lParam)
	{
		IEnumerable<(bool, long)> responses = _overlays.Select(pair => pair.Value.WndProcMessage(msg, wParam, lParam));
		return responses.FirstOrDefault(pair => pair.Item1);
	}

	private void Render()
	{
		if (++_renderFrameCount == 1)
			Services.PluginLog.Info($"[BW] 首帧渲染 (overlays={_overlays.Count}, renderProcess={_renderProcess != null})");

		_dependencyManager.Render();

		ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));

		_renderProcess?.EnsureRenderProcessIsAlive();

		foreach (Overlay overlay in _overlays.Values) { overlay.Render(); }

		ImGui.PopStyleVar();
	}

	/// <summary>从名称派生稳定的 GUID，确保 ImGui 窗口位置跨加载持久化</summary>
	private static Guid StableGuid(string name)
	{
		var hash = MD5.HashData(Encoding.UTF8.GetBytes("HiAuRo.Overlay." + name));
		return new Guid(hash);
	}
}