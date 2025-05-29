namespace ModbusSharp.Client;

/// <summary>
/// Rtu协议的Modbus
/// </summary>
public class ModbusRtu : ModbusSerialPort
{
    /// <summary>
    /// ModbusRtu构造函数
    /// </summary>
    /// <param name="config"></param>
    public ModbusRtu(SerialPortConfig config) : base(config)
    {
    }

    /// <summary>
    /// 协议类型
    /// </summary>
    /// <returns></returns>
    public override ModbusType Type => ModbusType.Rtu;
}
