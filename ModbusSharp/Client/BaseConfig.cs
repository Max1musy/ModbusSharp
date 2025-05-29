using System.IO.Ports;

namespace ModbusSharp.Client;

/// <summary>
/// Modbus客户端基本配置
/// </summary>
public abstract class BaseConfig
{
    /// <summary>
    /// 连接超时时间
    /// </summary>
    public int ConnectTimeout { get; set; } = 1000;

    /// <summary>
    /// 接收数据等待超时时间
    /// </summary>
    public int ReceiveTimeout { get; set; } = 1000;

    /// <summary>
    /// 发送数据等待超时时间
    /// </summary>
    public int SendTimeout { get; set; } = 1000;

    /// <summary>
    /// 协议标识符，用于标识Modbus协议，通常为 0
    /// </summary>
    public ushort ProtocolIdentifier { get; set; } = 0;

    /// <summary>
    /// 从站地址，用于标识 Modbus 从站
    /// </summary>
    public byte UnitIdentifier { get; set; } = 0x01;

    /// <summary>
    /// 标识号，用于区分
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 写寄存器bit位时，是否用MaskWrite功能码(有些PLC不支持此功能码，所以要按写整个寄存器的方式写位)
    /// </summary>
    public bool EnableMask { get; set; } = true;

    /// <summary>
    /// 是否自动读取
    /// </summary>
    public bool AutoRead { get; set; } = false;

    /// <summary>
    /// 自动读取的Sleep时间
    /// </summary>
    public int AutoReadTimeSpan { get; set; } = 400;
}

/// <summary>
/// 串口配置
/// </summary>
public class SerialPortConfig : BaseConfig
{
    /// <summary>
    /// 串口名
    /// </summary>
    public string PortName { get; set; } = "COM2";

    /// <summary>
    /// 波特率
    /// </summary>
    public int BaudRate { get; set; } = 9600;

    /// <summary>
    /// 数据位
    /// </summary>
    public int DataBits { get; set; } = 8;

    /// <summary>
    /// 检验位
    /// </summary>
    public Parity Parity { get; set; } = Parity.None;

    /// <summary>
    /// 停止位
    /// </summary>
    public StopBits StopBits { get; set; } = StopBits.One;
}

/// <summary>
/// socket配置
/// </summary>
public class SocketConfig : BaseConfig
{
    /// <summary>
    /// IP地址
    /// </summary>
    public string IP { get; set; } = "127.0.0.1";

    /// <summary>
    /// 端口
    /// </summary>
    public int Port { get; set; } = 502;

    /// <summary>
    /// 指定本地端口（0为不指定，随机端口）
    /// </summary>
    public int LocalPort { get; set; } = 0;
}
