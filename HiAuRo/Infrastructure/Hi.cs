namespace HiAuRo.Infrastructure;

public static class Hi
{
    public static void Print(string msg) =>
        DService.Instance().Chat.Print($"[HiAuRo] {msg}");

    public static void Debug(string msg) =>
        DService.Instance().Log.Debug($"[HiAuRo] {msg}");

    public static void Info(string msg) =>
        DService.Instance().Log.Information($"[HiAuRo] {msg}");

    public static void Warn(string msg) =>
        DService.Instance().Log.Warning($"[HiAuRo] {msg}");

    public static void Error(string msg) =>
        DService.Instance().Log.Error($"[HiAuRo] {msg}");
}
