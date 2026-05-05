using System.Text.Json;
using HiAuRo.UI;

namespace HiAuRo.Authoring;

/// <summary>
/// 三轴时间线 Web 编辑器后端 —— 注册 WebSocket 消息处理器
/// 消息流: Web 前端 ←→ WebUiBridge ←→ 文件系统
/// </summary>
public sealed class AuthoringServer
{
    public static AuthoringServer Instance { get; } = new();

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private string ConfigDir => DService.Instance().PI.ConfigDirectory.FullName;

    private AuthoringServer() { }

    public void Register(WebUiBridge bridge)
    {
        bridge.On("editorList", data =>
        {
            if (data == null) return;
            var axis = GetProp(data.Value, "axis") ?? "execution";
            var dir = GetTimelineDir(axis);
            var files = Directory.Exists(dir)
                ? Directory.GetFiles(dir, "*.*")
                    .Select(f => new
                    {
                        name = Path.GetFileName(f),
                        path = f,
                        size = new FileInfo(f).Length,
                        modified = File.GetLastWriteTime(f).ToString("yyyy-MM-dd HH:mm:ss")
                    }).ToList<object>()
                : new List<object>();

            _ = bridge.SendAsync(new { type = "editorListResult", data = new { axis, files } });
        });

        bridge.On("editorLoad", data =>
        {
            if (data == null) return;
            var axis = GetProp(data.Value, "axis") ?? "execution";
            var fileName = GetProp(data.Value, "fileName") ?? "";
            var path = Path.Combine(GetTimelineDir(axis), fileName);

            string? content = null;
            string? error = null;
            if (File.Exists(path))
            {
                content = File.ReadAllText(path);
            }
            else
            {
                error = $"文件不存在: {fileName}";
            }

            _ = bridge.SendAsync(new { type = "editorLoadResult", data = new { axis, fileName, content, error } });
        });

        bridge.On("editorSave", data =>
        {
            if (data == null) return;
            var axis = GetProp(data.Value, "axis") ?? "execution";
            var fileName = GetProp(data.Value, "fileName") ?? "untitled.json";
            var content = GetProp(data.Value, "content") ?? "";
            var dir = GetTimelineDir(axis);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, fileName);

            var success = false;
            var error = (string?)null;
            try
            {
                File.WriteAllText(path, content);
                success = true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            _ = bridge.SendAsync(new { type = "editorSaveResult", data = new { axis, fileName, success, error } });
        });

        bridge.On("editorDelete", data =>
        {
            if (data == null) return;
            var axis = GetProp(data.Value, "axis") ?? "execution";
            var fileName = GetProp(data.Value, "fileName") ?? "";
            var path = Path.Combine(GetTimelineDir(axis), fileName);

            var success = false;
            var error = (string?)null;
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    success = true;
                }
                else error = "文件不存在";
            }
            catch (Exception ex) { error = ex.Message; }

            _ = bridge.SendAsync(new { type = "editorDeleteResult", data = new { axis, fileName, success, error } });
        });

        bridge.On("editorNew", data =>
        {
            if (data == null) return;
            var axis = GetProp(data.Value, "axis") ?? "execution";
            var fileName = GetProp(data.Value, "fileName") ?? "new.json";
            var dir = GetTimelineDir(axis);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, fileName);

            string? error = null;
            var success = false;
            try
            {
                if (File.Exists(path))
                {
                    error = "文件已存在";
                }
                else
                {
                    var template = GetDefaultTemplate(axis);
                    File.WriteAllText(path, JsonSerializer.Serialize(template, _jsonOptions));
                    success = true;
                }
            }
            catch (Exception ex) { error = ex.Message; }

            _ = bridge.SendAsync(new { type = "editorNewResult", data = new { axis, fileName, success, error } });
        });

        bridge.On("editorValidate", data =>
        {
            if (data == null) return;
            var axis = GetProp(data.Value, "axis") ?? "execution";
            var content = GetProp(data.Value, "content") ?? "";

            string? error = null;
            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                if (axis == "fact")
                {
                    if (!root.TryGetProperty("phases", out _))
                        error = "缺少 'phases' 字段";
                }
                else
                {
                    if (!root.TryGetProperty("TreeRoot", out _))
                        error = "缺少 'TreeRoot' 字段";
                }
            }
            catch (Exception ex) { error = $"JSON 解析失败: {ex.Message}"; }

            _ = bridge.SendAsync(new { type = "editorValidateResult", data = new { axis, valid = error == null, error } });
        });
    }

    private string GetTimelineDir(string axis) => axis switch
    {
        "fact" => Path.Combine(ConfigDir, "FactTimelines"),
        "assist" => Path.Combine(ConfigDir, "AssistTimelines"),
        _ => Path.Combine(ConfigDir, "ExecutionTimelines")
    };

    private static string? GetProp(JsonElement elem, string name) =>
        elem.TryGetProperty(name, out var val) ? val.GetString() : null;

    private static object GetDefaultTemplate(string axis) => axis switch
    {
        "fact" => new
        {
            name = "新事实轴",
            territoryId = 0,
            author = "",
            phases = Array.Empty<object>()
        },
        _ => new
        {
            Name = "新执行轴",
            TerritoryTypeId = 0,
            Note = "",
            ExposedVars = Array.Empty<string>(),
            TreeRoot = new
            {
                TypeName = "AEAssist.Trigger.TriggerNode.TreeRoot, AEAssist",
                DisplayName = "Root",
                Id = 1,
                Enable = true,
                Remark = "",
                Tag = "",
                Childs = Array.Empty<object>()
            }
        }
    };
}
