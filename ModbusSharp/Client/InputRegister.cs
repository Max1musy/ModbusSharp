using System.Collections;
using System.Runtime.InteropServices;

namespace ModbusSharp.Client;

/// <summary>
/// 输入寄存器(只有bool、short、int、double、float、long、char、uint、ushort、ulong)
/// </summary>
public record InputRegister<T> : IOReadBase<T> where T : struct, IConvertible, IComparable
{

    /// <summary>
    /// 类型
    /// </summary>
    public override IOType Type => IOType.InputRegister;

    /// <summary>
    /// 读
    /// </summary>
    /// <returns></returns>
    public override Result<T> Read()
    {
        try
        {
            if (typeof(bool).IsAssignableFrom(typeof(T)))
            {
                var res = Client.ReadBit(Address, BitIndex);
                res.ThrowExceptionIfError();
                InvokeChange((T)Convert.ChangeType(res.Content, typeof(T)));
            }
            else
            {
                var res = Client.Read<T>(Address, 1, true);
                res.ThrowExceptionIfError();
                InvokeChange(res.Content[0]);
            }
            return Result<T>.CreateSuccessResult(LastData);
        }
        catch (Exception ex)
        {
            return Result<T>.CreateFailedResult(ex.Message);
        }
    }

    /// <summary>
    /// 外部设置LastDta
    /// </summary>
    /// <param name="values">一组数据</param>
    /// <param name="start">数据起点地址</param>
    public void SetValue(ushort[] values, int start = 0)
    {
        if (Address >= start && Address - start < values.Length && !typeof(char).IsAssignableFrom(typeof(T)))
        {
            if (typeof(bool).IsAssignableFrom(typeof(T)))
            {
                var ba = new BitArray(BitConverter.GetBytes(values[Address - start]));
                InvokeChange((T)Convert.ChangeType(ba[BitIndex], typeof(bool)));
            }
            else
            {
                int size = Marshal.SizeOf<T>() / 2;
                var tmp = new ushort[size];
                Array.Copy(values, Address - start, tmp, 0, size);
                var res = tmp.To<T>();
                res.ThrowExceptionIfError();
                InvokeChange(res.Content);
            }
        }
    }
}
