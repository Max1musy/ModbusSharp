namespace ModbusSharp.Client;

/// <summary>
/// 长连接modbus
/// </summary>
public abstract class ModbusLongConnection<T> : ModbusClient<T> where T : BaseConfig
{
    /// <summary>
    /// 是否连接成功
    /// </summary>
    public abstract bool Connected { get; }
    /// <summary>
    /// 是否正在重连
    /// </summary>
    protected EventWaitHandle ReConnectHandle = new (false, EventResetMode.AutoReset);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="config"></param>
    public ModbusLongConnection(T config) : base(config)
    {
        try
        {
            Connect();
            Task.Run(() =>
            {
                var LastReconnect = DateTime.Now;
                while (!IsExit)
                {
                    ReConnectHandle.WaitOne(Config.ConnectTimeout * 2);
                    if (DateTime.Now - LastReconnect < TimeSpan.FromMilliseconds(Config.ConnectTimeout * 2))
                    {
                        continue;
                    }
                    if (!Connected && !IsExit)
                    {
                        Connect();
                        LastReconnect = DateTime.Now;
                    }
                }
            });
        }
        catch { }
    }

    /// <summary>
    /// 
    /// </summary>
    protected abstract void Connect();
    /// <summary>
    /// 重连
    /// </summary>
    public void ReConnect() => ReConnectHandle.Set();
}
