namespace HiAuRo.ACR;

/// <summary>
/// 起手管理器 —— 状态机驱动起手爆发执行
/// </summary>
public sealed class OpenerMgr
{
    public enum State
    {
        NotStarted,
        Running,
        Finished
    }

    public State CurrentState { get; private set; } = State.NotStarted;

    private IOpener? _currentOpener;
    private int _currentStep;

    /// <summary>开始执行起手</summary>
    public bool Start(IOpener opener)
    {
        if (_currentOpener != null)
            Reset();

        if (opener.StartCheck() < 0)
            return false;

        _currentOpener = opener;
        _currentStep = 0;
        CurrentState = State.Running;
        return true;
    }

    /// <summary>每帧推进，逐个执行 Sequence 中的 Action&lt;Slot&gt;</summary>
    public Slot? Update()
    {
        if (CurrentState != State.Running || _currentOpener == null)
            return null;

        var sequence = _currentOpener.Sequence;
        if (_currentStep >= sequence.Count)
        {
            Finish();
            return null;
        }

        var slot = new Slot();
        sequence[_currentStep](slot);
        _currentStep++;

        // 最后一步时自动结束
        if (_currentStep >= sequence.Count)
            Finish();

        return slot;
    }

    /// <summary>检查是否可在当前步中断</summary>
    public bool CanInterrupt()
    {
        if (_currentOpener == null || CurrentState != State.Running) return true;
        return _currentOpener.StopCheck(_currentStep) >= 0;
    }

    public void Reset()
    {
        _currentOpener = null;
        _currentStep = 0;
        CurrentState = State.NotStarted;
    }

    private void Finish()
    {
        CurrentState = State.Finished;
    }
}
