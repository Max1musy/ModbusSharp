using Newtonsoft.Json;
using System.Runtime.InteropServices;

namespace ModbusSharp.Client;

/// <summary>
/// IO基类
/// </summary>
public abstract record IOBase
{
    /// <summary>
    /// 客户端
    /// </summary>
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
    public IModbusClient Client { get; internal set; } 
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。

    /// <summary>
    /// io地址
    /// </summary>
    public int Address { get; set; } = 0;

    /// <summary>
    /// 在寄存器读写bit位(bool)时候作为bitindex
    /// </summary>
    public int BitIndex { get; set; } = 0;

    /// <summary>
    /// 别名
    /// </summary>
    public string Name { get; internal set; } = string.Empty;

    /// <summary>
    /// IO类型
    /// </summary>
    public abstract IOType Type { get; }

    /// <summary>
    /// 数据占几个寄存器
    /// </summary>
    public abstract int Size { get; }
}


/// <summary>
/// 只读IO
/// </summary>
/// <typeparam name="T"></typeparam>
[JsonConverter(typeof(IOReadBaseConverter))]
public abstract record IOReadBase<T> : IOBase where T : struct
{
    /// <summary>
    /// 上次的值,外部无法直接赋值，需要调用InvokeChange
    /// </summary>
    public T LastData { get; protected set; }

    /// <summary>
    /// 读方法
    /// </summary>
    /// <returns></returns>
    public abstract Result<T> Read();

    /// <summary>
    /// 数据占几个寄存器
    /// </summary>
    public override int Size { 
        get{
            if (typeof(bool).IsAssignableFrom(typeof(T)))
            {
                return 1;
            }
            else if (typeof(char).IsAssignableFrom(typeof(T)))
            {
                return 1;
            }
            else
            {
                return Marshal.SizeOf<T>() / 2;
            }
        } }

    /// <summary>
    /// 数据变化的委托
    /// </summary>
    /// <param name="from">变化前</param>
    /// <param name="to">变化后</param>
    public delegate void DataChangedHandler(T from, T to);

    /// <summary>
    /// 数据变化的事件
    /// </summary>
    public event DataChangedHandler? DataChanged;

    /// <summary>
    /// 通知数据变化
    /// </summary>
    /// <param name="to">变化后</param>
    public virtual void InvokeChange(T to)
    {
        if (to.Equals(LastData))
        {
            return;
        }
        if (DataChanged != null)
        {
            var tmp = LastData;
            Task.Run(()=> DataChanged.Invoke(tmp, to));
        }
        LastData = to;
    }

    /// <summary>
    /// 隐式转换
    /// </summary>
    /// <param name="io">io</param>
    public static implicit operator T(IOReadBase<T> io) => io.LastData;
}


/// <summary>
/// 读写IO
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract record IOWriteBase<T> : IOReadBase<T> where T : struct
{
    /// <summary>
    /// 写方法
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public abstract Result Write(T value);
}

/// <summary>
/// IO类型
/// </summary>
public enum IOType
{
    /// <summary>
    /// 线圈
    /// </summary>
    Coil,
    /// <summary>
    /// 离散输入
    /// </summary>
    DiscreteInput,
    /// <summary>
    /// 保持寄存器
    /// </summary>
    HoldingRegister,
    /// <summary>
    /// 输入寄存器
    /// </summary>
    InputRegister
}
