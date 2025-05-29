namespace ModbusSharp.Client;

/// <summary>
/// 属性验证特性
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class ModbusPropertyAttribute : Attribute
{
    /// <summary>
    /// 地址位(0~65535)
    /// </summary>
    public int Address { get; set; } = 0;

    /// <summary>
    /// 在寄存器读写bit位(bool)时候作为bitindex
    /// </summary>
    public int BitIndex { get; set; } = 0;
}
