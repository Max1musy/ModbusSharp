using System.IO.Ports;

namespace ModbusSharp.Client;

/// <summary>
/// 串口Modbus
/// </summary>
public abstract class ModbusSerialPort : ModbusLongConnection<SerialPortConfig>
{
    SerialPort serialPort;
    /// <summary>
    /// ModbusClientSerialPort构造函数
    /// </summary>
    /// <param name="config">配置</param>
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
    public ModbusSerialPort(SerialPortConfig config) : base(config)
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
    {
    }

    /// <summary>
    /// 是否连接成功
    /// </summary>
    public override bool Connected => serialPort == null ? false : serialPort.IsOpen;

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
                var sendData = request.Build();
                serialPort.Write(sendData, 0, sendData.Length);
                var bytesToRead = request.BytesToRead();
                DateTime start = DateTime.Now;
                var buffer = new byte[bytesToRead];
                var alreadyRead = 0;
                while (true)
                {
                    Thread.Sleep(5);
                    if (serialPort.BytesToRead > 0)
                    {
                        int count = serialPort.Read(buffer, alreadyRead, buffer.Length - alreadyRead);
                        alreadyRead += count;
                        if (alreadyRead >= bytesToRead)
                            break;
                    }
                    else if ((DateTime.Now - start).TotalMilliseconds > Config.ReceiveTimeout)
                    {
                        break;
                    }
                }
                if (alreadyRead < 6)
                {
                    throw new TimeoutException("读取超时");
                }
                return ModbusResponse.CreateSuccessResult(buffer);
            }
            catch (Exception e)
            {
                if (e is TimeoutException || e.Message.Contains("port"))
                {
                    Task.Run(() => ReConnect());
                }
                return ModbusResponse.CreateFailedResult(e.Message);
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    protected override void Connect()
    {
        try
        {
            if (serialPort != null)
            {
                serialPort.Close();
            }
            if (IsExit)
            {
                return;
            }
            serialPort = new (Config.PortName);
            serialPort.BaudRate = Config.BaudRate;
            serialPort.Parity = Config.Parity;
            serialPort.DataBits = Config.DataBits;
            serialPort.StopBits = Config.StopBits;
            serialPort.WriteTimeout = Config.SendTimeout;
            serialPort.ReadTimeout = Config.ReceiveTimeout;
            serialPort.Open();
        }
        catch { }
    }

    /// <summary>
    /// 析构函数
    /// </summary>
    public override void Dispose()
    {
        base.Dispose();
        try
        {
            if (serialPort != null)
            {
                serialPort.Close();
            }
        }
        catch { }
    }
}
