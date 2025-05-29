namespace ModbusSharp.Server;

/// <summary>
/// modbus单元基类
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class ModbusBase<T>
{
    public ModbusServer modbusServer;
    public int count { get; set; } = 65535;

    internal T[] localArray;
    public bool enable { get; set; } = true;
    public virtual void Build() => localArray = new T[count];

    public T this[int x] { get => localArray[x]; set => localArray[x] = value; }
}

/// <summary>
/// 保持寄存器
/// </summary>
public class HoldingRegisterServer : ModbusBase<short>
{
}

/// <summary>
/// 输入寄存器
/// </summary>
public class InputRegisterServer : ModbusBase<short>
{
}


/// <summary>
/// 线圈
/// </summary>
public class CoilServer : ModbusBase<bool>
{
}

/// <summary>
/// 离散输入
/// </summary>
public class DiscreteInputServer : ModbusBase<bool>
{
}
