using System.Numerics;
using System.Reflection;
using Dalamud.Interface.ManagedFontAtlas;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// 图标辅助 — 从 EmbeddedResource TTF 加载自定义 Game-Icon-Pack 字体，支持双字号
/// </summary>
public static class IconHelper
{
    /// <summary>Game-Icon-Pack 图标 Unicode 码点 (Private Use Area U+EA00+)</summary>
    public static class Icons
    {
        public const string Play = "\ueb15";
        public const string Stop = "\ueb20";
        public const string Pause = "\ueb13";
        public const string Save = "\uebe1";
        public const string ArrowUp = "\ueb39";
        public const string ArrowDown = "\ueb30";
        public const string Cross = "\uebb2";
        // 共 504 个图标可用，命名格式: Category_iconname，如 UI_settings, Game_trophy 等
    }

    private static ImFontPtr? _iconFont18;
    private static ImFontPtr? _iconFont24;
    private static readonly object _initLock = new();
    private static bool _initialized;
    private static IFontHandle? _fontHandle18;
    private static IFontHandle? _fontHandle24;

    /// <summary>初始化图标字体 — 在 Plugin 构造器中调用一次</summary>
    public static void Init()
    {
        if (_initialized) return;
        lock (_initLock)
        {
            if (_initialized) return;

            try
            {
                // 1. 从 EmbeddedResource 提取 TTF 到配置目录
                var configDir = DService.Instance().PI.ConfigDirectory.FullName;
                var ttfPath = Path.Combine(configDir, "game-icons.ttf");

                // 始终覆盖，确保 TTF 版本与嵌入资源一致
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream("HiAuRo.Resources.Fonts.game-icons.ttf");
                if (stream == null)
                {
                    DService.Instance().Log.Warning("[IconHelper] TTF resource not found");
                    return;
                }
                using var fs = File.Create(ttfPath);
                stream.CopyTo(fs);

                // 2. 在游戏字体图集中注册两个字号
                var fontAtlas = DService.Instance().UIBuilder.FontAtlas;
                var iconRange = new ushort[] { 0xEA00, 0xEBF9, 0 };

                _fontHandle18 = fontAtlas.NewDelegateFontHandle(e =>
                {
                    e.OnPreBuild(tk =>
                    {
                        tk.AddFontFromFile(ttfPath, new()
                        {
                            SizePx = 18f,
                            PixelSnapH = true,
                            GlyphRanges = iconRange,
                        });
                    });
                });

                _fontHandle24 = fontAtlas.NewDelegateFontHandle(e =>
                {
                    e.OnPreBuild(tk =>
                    {
                        tk.AddFontFromFile(ttfPath, new()
                        {
                            SizePx = 24f,
                            PixelSnapH = true,
                            GlyphRanges = iconRange,
                        });
                    });
                });

                DService.Instance().Log.Information("[IconHelper] Game-Icon-Pack font registered (18px + 24px)");
                _initialized = true;
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Warning($"[IconHelper] Font init failed: {ex.Message}");
            }
        }
    }

    /// <summary>确保字体已构建（首次渲染时调用）</summary>
    private static void EnsureFontsBuilt()
    {
        if (_iconFont18 != null) return;
        lock (_initLock)
        {
            if (_iconFont18 != null) return;

            try
            {
                using var lk18 = _fontHandle18!.Lock();
                _iconFont18 = lk18.ImFont;
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Warning($"[IconHelper] 18px font build failed: {ex.Message}");
                return;
            }

            try
            {
                using var lk24 = _fontHandle24!.Lock();
                _iconFont24 = lk24.ImFont;
                DService.Instance().Log.Information("[IconHelper] Game-Icon-Pack fonts built");
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Warning($"[IconHelper] 24px font build failed: {ex.Message}");
            }
        }
    }

    /// <summary>在指定中心绘制图标文本</summary>
    public static void DrawIcon(ImDrawListPtr dl, Vector2 center, string iconChar, uint color, float sizePx = 18f)
    {
        EnsureFontsBuilt();
        var fontPtr = sizePx >= 22f ? _iconFont24 : _iconFont18;
        if (fontPtr == null) return;
        using var font = ImRaii.PushFont(fontPtr.Value);
        var size = ImGui.CalcTextSize(iconChar);
        dl.AddText(center - size / 2, color, iconChar);
    }
}
