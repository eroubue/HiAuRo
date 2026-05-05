namespace HiAuRo.Data;

/// <summary>
/// HiAuRo 游戏数据统一入口
/// </summary>
public static class Data
{
    public static bool IsReady =>
        OmenTools.OmenService.GameState.IsLoggedIn &&
        OmenTools.OmenService.GameState.IsTerritoryLoaded;
}
