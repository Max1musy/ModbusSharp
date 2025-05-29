namespace ModbusSharp.Client;

/// <summary>
/// ModbusClient
/// </summary>
public interface IModbusClient
{
    /// <summary>
    /// 读(bool为线圈或离散输入，其他为寄存器)
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="address">起始地址</param>
    /// <param name="Input">是否为输入寄存器或离散输入</param>
    /// <returns></returns>
    Result<T> Read<T>(int address, bool Input = false) where T : struct;

    /// <summary>
    /// 读(bool为线圈或离散输入，其他为寄存器)
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="address">起始地址</param>
    /// <param name="quantity">数量</param>
    /// <param name="Input">是否为输入寄存器或离散输入</param>
    /// <returns></returns>
    Result<T[]> Read<T>(int address, int quantity, bool Input = false) where T : struct;

    /// <summary>
    /// 寄存器读bit位(bool)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="bitIndex">起始位地址</param>
    /// <param name="Input">是否为输入寄存器</param>
    /// <returns></returns>
    Result<bool> ReadBit(int address, int bitIndex, bool Input = false);

    /// <summary>
    /// 寄存器读bit位(bool)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="bitIndex">起始位地址</param>
    /// <param name="quantity">数量</param>
    /// <param name="Input">是否为输入寄存器</param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    Result<bool[]> ReadBit(int address, int bitIndex, int quantity, bool Input = false);

    /// <summary>
    /// 写(bool为线圈，其他为寄存器)
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="address">起始地址</param>
    /// <param name="value">数据</param>
    /// <returns></returns>
    Result Write<T>(int address, T value) where T : struct;

    /// <summary>
    /// 写(bool为线圈，其他为寄存器)
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="address">起始地址</param>
    /// <param name="values">数据</param>
    /// <returns></returns>
    Result Write<T>(int address, T[] values) where T : struct;

    /// <summary>
    /// 寄存器写bit位(bool)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="bitIndex">起始位地址</param>
    /// <param name="value">数据</param>
    /// <returns></returns>
    Result WriteBit(int address, int bitIndex, bool value);

    /// <summary>
    /// 寄存器写bit位(bool)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="bitIndex">起始位地址</param>
    /// <param name="values">数据</param>
    /// <returns></returns>
    Result WriteBit(int address, int bitIndex, bool[] values);
}