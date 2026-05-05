using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;
using Dalamud.Game.ClientState.Objects.Enums;
using IGameObj = OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds.IGameObject;

namespace HiAuRo.Data;

/// <summary>
/// 对象扫描 —— 一次扫描 OmenTools ObjectTable，语义分类
/// </summary>
public static class Objects
{
    public static readonly List<IGameObj> All = [];
    public static readonly List<IGameObj> Allies = [];
    public static readonly List<IGameObj> Enemies = [];
    public static readonly List<IGameObj> Party = [];
    public static readonly List<IGameObj> Pets = [];
    public static readonly List<IGameObj> Summons = [];
    public static readonly List<IGameObj> Environment = [];
    public static readonly List<IGameObj> Others = [];

    public static void Refresh()
    {
        ClearAll();

        if (!Data.IsReady) return;

        var self = Me.Object;
        if (self == null) return;

        foreach (var obj in DService.Instance().ObjectTable)
        {
            All.Add(obj);

            switch (obj.ObjectKind)
            {
                case ObjectKind.Pc:
                    Party.Add(obj);
                    Allies.Add(obj);
                    break;

                case ObjectKind.BattleNpc:
                    if (obj is IBattleNPC battleNpc)
                    {
                        if (IsOwnedByParty(obj) || IsOwnedByBuddy(obj))
                        {
                            Pets.Add(obj);
                            Allies.Add(obj);
                        }
                        else if (battleNpc.BattleNPCKind == BattleNpcSubKind.Pet)
                        {
                            Summons.Add(obj);
                            Others.Add(obj);
                        }
                        else if (IsHostile(obj))
                        {
                            if (IsActuallyFriendly(obj))
                                Allies.Add(obj);
                            else
                                Enemies.Add(obj);
                        }
                        else
                        {
                            Others.Add(obj);
                        }
                    }
                    else
                    {
                        Others.Add(obj);
                    }
                    break;

                case ObjectKind.EventNpc:
                case ObjectKind.Retainer:
                case ObjectKind.Companion:
                case ObjectKind.Mount:
                case ObjectKind.Ornament:
                case ObjectKind.EventObj:
                    Environment.Add(obj);
                    break;

                default:
                    Others.Add(obj);
                    break;
            }
        }
    }

    private static void ClearAll()
    {
        All.Clear(); Allies.Clear(); Enemies.Clear(); Party.Clear();
        Pets.Clear(); Summons.Clear(); Environment.Clear(); Others.Clear();
    }

    /// <summary>是否为敌对目标 (可被攻击 + 可选中 + 未死亡)</summary>
    private static bool IsHostile(IGameObj obj)
    {
        return obj.IsTargetable && obj.IsDead != true;
    }

    /// <summary>是否被 BuddyList 中的宠物持有</summary>
    private static bool IsOwnedByBuddy(IGameObj obj)
    {
        if (obj.OwnerID == 0) return false;
        foreach (var buddy in DService.Instance().BuddyList)
        {
            if (buddy.EntityId == obj.OwnerID)
                return true;
        }
        return false;
    }

    /// <summary>是否由队友持有 (通过 OwnerID)</summary>
    private static bool IsOwnedByParty(IGameObj obj)
    {
        if (obj.OwnerID == 0) return false;
        var owner = DService.Instance().ObjectTable.SearchByID(obj.OwnerID);
        return owner is IPlayerCharacter pc && pc.ObjectKind == ObjectKind.Pc;
    }

    /// <summary>排除假阳性 (单人 duty 友方 NPC)</summary>
    private static bool IsActuallyFriendly(IGameObj obj)
    {
        if (obj.OwnerID == Me.Object?.EntityID) return true;
        var owner = DService.Instance().ObjectTable.SearchByID(obj.OwnerID);
        return owner is IPlayerCharacter;
    }
}
