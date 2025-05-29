namespace ModbusSharp.Client;

/// <summary>
/// ASCII协议的Modbus
/// </summary>
public class ModbusAscii : ModbusSerialPort
{
    /// <summary>
    /// ModbusAscii构造函数
    /// </summary>
    /// <param name="config"></param>
    public ModbusAscii(SerialPortConfig config) : base(config)
    {
    }

    /// <summary>
    /// 协议类型
    /// </summary>
    /// <returns></returns>
    public override ModbusType Type => ModbusType.Ascii;
}
