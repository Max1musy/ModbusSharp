using System.Net;
using System.Net.Sockets;

namespace ModbusSharp.Client;

/// <summary>
/// Tcp协议的Modbus
/// </summary>
public class ModbusTcp : ModbusLongConnection<SocketConfig>
{
    Socket socket;
    /// <summary>
    /// 是否连接成功
    /// </summary>
    public override bool Connected
    {
        get
        {
            try
            {
                if (socket == null)
                {
                    return false;
                }

                return (!socket.Poll(1000, SelectMode.SelectRead) || socket.Available != 0) && socket.Connected;
            }
            catch
            {
                return false;
            }
        }
    }
    /// <summary>
    /// ModbusTcp构造函数
    /// </summary>
    /// <param name="config">配置</param>
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
    public ModbusTcp(SocketConfig config) : base(config)
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
    {
    }

    /// <summary>
    /// 
    /// </summary>
    protected override void Connect()
    {
        try
        {
            if (socket != null)
            {
                socket.Dispose();
            }
            if (IsExit)
            {
                return;
            }
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.ReceiveTimeout = Config.ReceiveTimeout;
            socket.SendTimeout = Config.SendTimeout;
            if (Config.LocalPort != 0)
            {
                socket.Bind(new IPEndPoint(IPAddress.Any, Config.LocalPort));
            }
            var result = socket.BeginConnect(new IPEndPoint(IPAddress.Parse(Config.IP), Config.Port), null, null);
            if (!result.AsyncWaitHandle.WaitOne(Config.ConnectTimeout))
            {
                throw new TimeoutException("Socket连接超时");
            }
            socket.EndConnect(result);
        }
        catch { }
    }

    /// <summary>
    /// 协议类型
    /// </summary>
    /// <returns></returns>
    public override ModbusType Type => ModbusType.Tcp;

    /// <summary>
    /// 发送和读取
    /// </summary>
    /// <param name="request">发送的报文</param>
    /// <returns>接收的报文</returns>
    protected override ModbusResponse Fetch(ModbusRequest request)
    {
        lock (this)
        {
            try
            {
                socket.Send(request.Build());
                var data = new byte[request.BytesToRead()];
                int NumberOfBytes = socket.Receive(data);
                if (NumberOfBytes < 8)
                {
                    throw new TimeoutException("读取超时");
                }
                var receiveData = new byte[NumberOfBytes];
                Array.Copy(data, 0, receiveData, 0, NumberOfBytes);
                return ModbusResponse.CreateSuccessResult(receiveData);
            }
            catch (Exception e)
            {
                if (e is IOException || e is SocketException)
                {
                    ReConnect();
                }
                return ModbusResponse.CreateFailedResult(e.Message);
            }
        }
    }


    /// <summary>
    /// 析构函数
    /// </summary>
    public override void Dispose()
    {
        base.Dispose();
        try
        {
            if (socket != null)
            {
                socket.Dispose();
            }
        }
        catch { }
    }
}
