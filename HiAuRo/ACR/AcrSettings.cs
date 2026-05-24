using HiAuRo.Setting;

namespace HiAuRo.ACR;

/// <summary>
/// ACR 作者继承此类获得 .Save() 方法，宿主自动回填 _author / _jobId
/// </summary>
public abstract class AcrSettings
{
    internal string? _author;
    internal uint _jobId;

    /// <summary>立即将当前 settings 写入磁盘</summary>
    public void Save()
    {
        if (_author == null) return;
        SettingMgr.SaveAcrJobSetting(_author, _jobId, this);
    }
}
