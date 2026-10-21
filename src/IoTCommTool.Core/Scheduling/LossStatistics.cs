namespace IoTCommTool.Core.Scheduling;

/// <summary>丢包/乱序/重复统计，线程安全；Changed 事件内置 1 秒节流</summary>
public sealed class LossStatistics
{
    private const int WindowSize = 4096;
    private static readonly TimeSpan s_throttle = TimeSpan.FromSeconds(1);

    private readonly object _gate = new();

    // 发送侧：已发出但未经 RecordReceived 确认的序号（回显场景）
    private readonly HashSet<uint> _outstanding = [];
    private readonly Queue<uint> _outstandingOrder = [];

    // 接收侧：序号连续性跟踪
    private bool _hasReceived;
    private uint _maxReceivedSeq;
    private readonly HashSet<uint> _receivedWindow = [];
    private readonly Queue<uint> _receivedOrder = [];
    private readonly HashSet<uint> _missing = [];
    private readonly Queue<uint> _missingOrder = [];
    private readonly HashSet<uint> _lostSet = [];
    private readonly Queue<uint> _lostOrder = [];

    private DateTime _lastChangedAt = DateTime.MinValue;

    /// <summary>已发送总数</summary>
    public long SentCount { get; private set; }

    /// <summary>已接收总数（含重复与迟到包）</summary>
    public long ReceivedCount { get; private set; }

    /// <summary>判定丢失总数（序号滑出统计窗口仍未收到）</summary>
    public long LostCount { get; private set; }

    /// <summary>乱序总数（缺口序号迟到到达）</summary>
    public long OutOfOrderCount { get; private set; }

    /// <summary>重复总数（同一序号收到多次）</summary>
    public long DuplicateCount { get; private set; }

    /// <summary>丢包率 = LostCount / SentCount（未记录发送时以 ReceivedCount 为基数）</summary>
    public double LossRate
    {
        get
        {
            long basis = SentCount > 0 ? SentCount : ReceivedCount;
            return basis == 0 ? 0d : (double)LostCount / basis;
        }
    }

    /// <summary>统计变化时触发（1 秒节流）</summary>
    public event EventHandler? Changed;

    /// <summary>记录一次发送；seq 为负载中的递增序号，无序号传 null</summary>
    public void RecordSent(uint? seq)
    {
        bool raise;
        lock (_gate)
        {
            SentCount++;
            if (seq.HasValue && _outstanding.Add(seq.Value))
            {
                _outstandingOrder.Enqueue(seq.Value);
                if (_outstandingOrder.Count > WindowSize)
                {
                    uint oldest = _outstandingOrder.Dequeue();
                    if (_outstanding.Remove(oldest))
                        MarkLostLocked(oldest);
                }
            }

            raise = ShouldRaiseLocked();
        }

        if (raise)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>记录一次接收；seq 为负载中解析出的序号，无序号传 null</summary>
    public void RecordReceived(uint? seq)
    {
        bool raise;
        lock (_gate)
        {
            ReceivedCount++;
            if (seq.HasValue)
            {
                uint s = seq.Value;
                bool wasOutstanding = _outstanding.Remove(s);

                if (!_hasReceived)
                {
                    _hasReceived = true;
                    _maxReceivedSeq = s;
                    AddReceivedLocked(s);
                }
                else if (s > _maxReceivedSeq)
                {
                    // 产生缺口；发送侧仍在跟踪的序号交由发送侧判丢，避免双向统计时重复计数
                    for (uint g = _maxReceivedSeq + 1; g < s; g++)
                    {
                        if (!_outstanding.Contains(g) && _missing.Add(g))
                            _missingOrder.Enqueue(g);
                    }

                    _maxReceivedSeq = s;
                    AddReceivedLocked(s);
                    EvictMissingLocked();
                }
                else
                {
                    // s <= _maxReceivedSeq：缺口迟到、已判丢失后到达、或重复
                    bool wasMissing = _missing.Remove(s);
                    bool wasLost = _lostSet.Remove(s);

                    if (wasOutstanding || wasMissing || wasLost)
                    {
                        OutOfOrderCount++;
                        if (wasLost)
                            LostCount--;
                        AddReceivedLocked(s);
                    }
                    else
                    {
                        DuplicateCount++;
                    }
                }
            }

            raise = ShouldRaiseLocked();
        }

        if (raise)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>清零所有计数</summary>
    public void Reset()
    {
        lock (_gate)
        {
            SentCount = 0;
            ReceivedCount = 0;
            LostCount = 0;
            OutOfOrderCount = 0;
            DuplicateCount = 0;
            _hasReceived = false;
            _maxReceivedSeq = 0;
            _outstanding.Clear();
            _outstandingOrder.Clear();
            _receivedWindow.Clear();
            _receivedOrder.Clear();
            _missing.Clear();
            _missingOrder.Clear();
            _lostSet.Clear();
            _lostOrder.Clear();
            _lastChangedAt = DateTime.MinValue;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void AddReceivedLocked(uint seq)
    {
        if (_receivedWindow.Add(seq))
        {
            _receivedOrder.Enqueue(seq);
            while (_receivedOrder.Count > WindowSize)
                _receivedWindow.Remove(_receivedOrder.Dequeue());
        }
    }

    private void EvictMissingLocked()
    {
        while (_missingOrder.Count > 0 && _maxReceivedSeq - _missingOrder.Peek() > WindowSize)
        {
            uint oldest = _missingOrder.Dequeue();
            if (_missing.Remove(oldest))
                MarkLostLocked(oldest);
        }
    }

    private void MarkLostLocked(uint seq)
    {
        LostCount++;
        if (_lostSet.Add(seq))
        {
            _lostOrder.Enqueue(seq);
            while (_lostOrder.Count > WindowSize)
                _lostSet.Remove(_lostOrder.Dequeue());
        }
    }

    private bool ShouldRaiseLocked()
    {
        var now = DateTime.UtcNow;
        if (now - _lastChangedAt < s_throttle)
            return false;

        _lastChangedAt = now;
        return true;
    }
}
