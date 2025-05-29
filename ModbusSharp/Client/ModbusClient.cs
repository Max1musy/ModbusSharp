using Newtonsoft.Json;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ModbusSharp.Client;

/// <summary>
/// modbus客户端
/// </summary>
public abstract class ModbusClient<TConfig> : IModbusClient, IDisposable where TConfig : BaseConfig
{
    /// <summary>
    /// 配置
    /// </summary>
    [JsonIgnore]
    public TConfig Config { get; init; }

    /// <summary>
    /// 存放每一种io的地址的最大值和最小值
    /// </summary>
    [JsonIgnore]
    readonly Dictionary<IOType, (int, int)> _Dict = new ();

    /// <summary>
    /// 所有绑定在此连接上的io
    /// </summary>
    [JsonIgnore]
    public readonly HashSet<IOBase> IOBases = new ();

    /// <summary>
    /// 退出线程
    /// </summary>
    [JsonIgnore]
    protected bool IsExit = false;

    /// <summary>
    /// 默认构造
    /// </summary>
    /// <param name="Config"></param>
    public ModbusClient(TConfig Config)
    {
        this.Config = Config;
        BindIOAttribute();
        Task.Run(() =>      //自动读取IO的线程
        {
            while (!IsExit)
            {
                Thread.Sleep(Config.AutoReadTimeSpan);
                try
                {
                    if (!Config.AutoRead)
                    {
                        break;
                    }
                    if (_Dict.ContainsKey(IOType.Coil))
                    {
                        var mm = _Dict[IOType.Coil];
                        var res = Read<bool>(mm.Item1, mm.Item2 - mm.Item1 + 1);
                        if (res.Success)
                        {
                            foreach (var item in IOBases.Where(x => x is Coil))
                            {
                                (item as dynamic).SetValue(res.Content, mm.Item1);
                            }
                        }
                    }
                    if (_Dict.ContainsKey(IOType.DiscreteInput))
                    {
                        var mm = _Dict[IOType.DiscreteInput];
                        var res = Read<bool>(mm.Item1, mm.Item2 - mm.Item1 + 1, true);
                        if (res.Success)
                        {
                            foreach (var item in IOBases.Where(x => x is DiscreteInput))
                            {
                                (item as dynamic).SetValue(res.Content, mm.Item1);
                            }
                        }
                    }
                    if (_Dict.ContainsKey(IOType.HoldingRegister))
                    {
                        var mm = _Dict[IOType.HoldingRegister];
                        var res = Read<ushort>(mm.Item1, mm.Item2 - mm.Item1 + 1);
                        if (res.Success)
                        {
                            foreach (var item in IOBases.Where(x => x.GetType().IsGenericType 
                            && x.GetType().GetGenericTypeDefinition() == typeof(HoldingRegister<>)))
                            {
                                (item as dynamic).SetValue(res.Content, mm.Item1);
                            }
                        }
                    }
                    if (_Dict.ContainsKey(IOType.InputRegister))
                    {
                        var mm = _Dict[IOType.InputRegister];
                        var res = Read<ushort>(mm.Item1, mm.Item2 - mm.Item1 + 1, true);
                        if (res.Success)
                        {
                            foreach (var item in IOBases.Where(x => x.GetType().IsGenericType 
                            && x.GetType().GetGenericTypeDefinition() == typeof(InputRegister<>)))
                            {
                                (item as dynamic).SetValue(res.Content, mm.Item1);
                            }
                        }
                    }
                }
                catch { }
            }
        });
    }

    /// <summary>
    /// 析构函数
    /// </summary>
    public virtual void Dispose() => IsExit = true;

    /// <summary>
    /// 绑定成员IO特性
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="offset"></param>
    public void BindIOAttribute(object? parent = null, int offset = 0)
    {
        var _parent = parent ?? this;
        Type type;
        if (parent is IOBase io) //传入的是单个io
        {
            type = io.GetType();
            io.Client = this;
            if (Attribute.GetCustomAttribute(type, typeof(ModbusPropertyAttribute)) is ModbusPropertyAttribute attribute)
            {
                io.Address = attribute.Address + offset;
                io.BitIndex = attribute.BitIndex;
                IOBases.Add(io);
                if (_Dict.TryGetValue(io.Type, out var mm))
                {
                    mm.Item1 = Math.Min(mm.Item1, io.Address);
                    mm.Item2 = Math.Max(mm.Item2, io.Address + io.Size - 1);
                    _Dict[io.Type] = mm;
                }
                else
                {
                    _Dict[io.Type] = (io.Address, io.Address + io.Size - 1);
                }
            }
            return;
        }
        else if (_parent is Type)//传入的是带io静态类
        {
            type = (Type)_parent;
            _parent = null;
        }
        else//传入的是带io的类
        {
            type = _parent.GetType();
        }
        foreach (var memberInfo in type.GetMembers())
        {
            if (memberInfo.MemberType != MemberTypes.Field && memberInfo.MemberType != MemberTypes.Property)
            {
                continue;
            }
            var value = (memberInfo as dynamic).GetValue(_parent);
            Type vType = memberInfo.MemberType == MemberTypes.Field ? (memberInfo as dynamic).FieldType : (memberInfo as dynamic).PropertyType;
            if (typeof(IOBase).IsAssignableFrom(vType))
            {
                if (value == null)  //若没有初始化自动初始化
                {
                    value = Activator.CreateInstance(vType);
                    (memberInfo as dynamic).SetValue(_parent, value);
                }
                if (value is IOBase IObase)
                {
                    IObase.Client = this;
                    IObase.Name = memberInfo.Name;
                    if (Attribute.GetCustomAttribute(memberInfo, typeof(ModbusPropertyAttribute)) is ModbusPropertyAttribute attribute)
                    {
                        IObase.Address = attribute.Address + offset;
                        IObase.BitIndex = attribute.BitIndex;
                        IOBases.Add(IObase);
                        if (_Dict.TryGetValue(IObase.Type, out var mm))
                        {
                            mm.Item1 = Math.Min(mm.Item1, IObase.Address);
                            mm.Item2 = Math.Max(mm.Item2, IObase.Address + IObase.Size - 1);
                            _Dict[IObase.Type] = mm;
                        }
                        else
                        {
                            _Dict[IObase.Type] = (IObase.Address, IObase.Address + IObase.Size - 1);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 检查地址和长度是否合法
    /// </summary>
    /// <param name="address"></param>
    /// <param name="quantity"></param>
    /// <param name="limit"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    void Check(int address, int quantity, int limit)
    {
        if (address > ushort.MaxValue || address < 0)
        {
            throw new ArgumentOutOfRangeException($"起始地址({address})超出范围0 - {ushort.MaxValue}");
        }
        if (quantity > limit || quantity < 0)
        {
            throw new ArgumentOutOfRangeException($"数量({quantity})超出范围0 - {limit}");
        }
        var sum = quantity + address;
        if (sum > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException($"起始地址{address}+数量{quantity}={sum}超出范围 0 - {ushort.MaxValue}");
        }
    }

    /// <summary>
    /// 发送和读取
    /// </summary>
    /// <param name="request">发送的报文</param>
    /// <returns>接收的报文</returns>
    protected abstract ModbusResponse Fetch(ModbusRequest request);

    /// <summary>
    /// 协议类型
    /// </summary>
    /// <returns></returns>
    [JsonIgnore]
    public abstract ModbusType Type { get; }

    /// <summary>
    /// 建立基础请求
    /// </summary>
    /// <param name="FunctionCode">功能码</param>
    /// <param name="address">起始地址</param>
    /// <param name="quantity">数量</param>
    /// <returns></returns>
    ModbusRequest CreateBaseRequest(FunctionCode FunctionCode, ushort address, ushort quantity)
    {
        Extention.TransactionIdentifier++;
        if (Extention.TransactionIdentifier >= ushort.MaxValue)
        {
            Extention.TransactionIdentifier = 0;
        }
        return new()
        {
            ModbusType = Type,
            FunctionCode = FunctionCode,
            TransactionIdentifier = Extention.TransactionIdentifier,
            Address = address,
            Quantity = quantity,
            UnitIdentifier = Config.UnitIdentifier,
            ProtocolIdentifier = Config.ProtocolIdentifier
        };
    }

    /// <summary>
    /// 读,内部使用,有长度限制
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list">存放数据的array</param>
    /// <param name="curr">当前位置</param>
    /// <param name="address">地址</param>
    /// <param name="quantity">长度</param>
    /// <param name="Input">是否是输入</param>
    void read<T>(T[] list, int curr, int address, int quantity, bool Input = false) where T : struct
    {
        if (typeof(bool).IsAssignableFrom(typeof(T)))
        {
            Result<bool[]> res;
            if (Input)
                res = ReadDiscreteInputs(address + curr, quantity);
            else
                res = ReadCoils(address + curr, quantity);
            res.ThrowExceptionIfError();
            for (int j = 0; j < res.Content.Length; j++)
                list[curr + j] = (T)Convert.ChangeType(res.Content[j], typeof(T));
        }
        else
        {
            Result<ushort[]> res;
            if (Input)
                res = ReadInputRegisters(address + curr, quantity);
            else
                res = ReadHoldingRegisters(address + curr, quantity);
            res.ThrowExceptionIfError();
            int size = Marshal.SizeOf<T>() / 2;
            for (int i = 0; i < res.Content.Length; i += size)
            {
                var tmp = new ushort[size];
                Array.Copy(res.Content, i, tmp, 0, size);
                var res1 = tmp.To<T>();
                res1.ThrowExceptionIfError();
                list[(curr + i) / size] = (T)Convert.ChangeType(res1.Content, typeof(T));
            }
        }
    }

    /// <summary>
    /// 写,内部使用,有长度限制
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="address">地址</param>
    /// <param name="values">长度</param>
    void write<T>(int address, T[] values) where T : struct
    {
        if (typeof(bool).IsAssignableFrom(typeof(T)))
        {
            var res = WriteMultipleCoils(address, values.Cast<bool>().ToArray());
            res.ThrowExceptionIfError();
        }
        else
        {
            int size = Marshal.SizeOf<T>() / 2;
            var sendValue = new ushort[values.Length * size];
            for (int i = 0; i < values.Length; i++)
            {
                var res = values[i].ToRegisters();
                res.ThrowExceptionIfError();
                Array.Copy(res.Content, 0, sendValue, i * size, size);
            }
            var res1 = WriteMultipleRegisters(address, sendValue);
            res1.ThrowExceptionIfError();
        }

    }

    /// <summary>
    /// 读离散输入
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="quantity">读取位数</param>
    /// <returns>返回数据</returns>
    public Result<bool[]> ReadDiscreteInputs(int address, int quantity)
    {
        try
        {
            Check(address, quantity, Extention.CoilLimit);
            var request = CreateBaseRequest(FunctionCode.ReadDiscreteInputs, (ushort)address, (ushort)quantity);
            var reponse = Fetch(request);
            reponse.AnalyseResponse(request);
            return reponse.ParseData<bool>(quantity);
        }
        catch (Exception e)
        {
            return Result<bool[]>.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// 读线圈
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="quantity">读取位数</param>
    /// <returns>返回数据</returns>
    public Result<bool[]> ReadCoils(int address, int quantity)
    {
        try
        {
            Check(address, quantity, Extention.CoilLimit);
            var request = CreateBaseRequest(FunctionCode.ReadCoils, (ushort)address, (ushort)quantity);
            var reponse = Fetch(request);
            reponse.AnalyseResponse(request);
            return reponse.ParseData<bool>(quantity);
        }
        catch (Exception e)
        {
            return Result<bool[]>.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// 读取保持寄存器
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="quantity">读取位数</param>
    /// <returns>返回数据</returns>
    public Result<ushort[]> ReadHoldingRegisters(int address, int quantity)
    {
        try
        {
            Check(address, quantity, Extention.RegisterLimit);
            var request = CreateBaseRequest(FunctionCode.ReadHoldingRegisters, (ushort)address, (ushort)quantity);
            var reponse = Fetch(request);
            reponse.AnalyseResponse(request);
            return reponse.ParseData<ushort>(quantity);
        }
        catch (Exception e)
        {
            return Result<ushort[]>.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// 读取输入寄存器
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="quantity">读取位数</param>
    /// <returns>返回数据</returns>
    public Result<ushort[]> ReadInputRegisters(int address, int quantity)
    {
        try
        {
            Check(address, quantity, Extention.RegisterLimit);
            var request = CreateBaseRequest(FunctionCode.ReadInputRegisters, (ushort)address, (ushort)quantity);
            var reponse = Fetch(request);
            reponse.AnalyseResponse(request);
            return reponse.ParseData<ushort>(quantity);
        }
        catch (Exception e)
        {
            return Result<ushort[]>.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// 写单个线圈
    /// </summary>
    /// <param name="address">地址</param>
    /// <param name="value">数据</param>
    public Result WriteSingleCoil(int address, bool value)
    {
        try
        {
            Check(address, 1, Extention.CoilLimit);
            var request = CreateBaseRequest(FunctionCode.WriteSingleCoil, (ushort)address, (ushort)(value ? 0xFF00 : 0x0000));
            var reponse = Fetch(request);
            reponse.AnalyseResponse(request);
            return Result.CreateSuccessResult();
        }
        catch (Exception e)
        {
            return Result.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// 写单个寄存器
    /// </summary>
    /// <param name="address">地址</param>
    /// <param name="value">数据</param>
    public Result WriteSingleRegister(int address, ushort value)
    {
        try
        {
            Check(address, 1, Extention.RegisterLimit);
            var request = CreateBaseRequest(FunctionCode.WriteSingleRegister, (ushort)address, value);
            var reponse = Fetch(request);
            reponse.AnalyseResponse(request);
            return Result.CreateSuccessResult();
        }
        catch (Exception e)
        {
            return Result.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// 写多个线圈
    /// </summary>
    /// <param name="address">初始地址</param>
    /// <param name="values">数据</param>
    public Result WriteMultipleCoils(int address, bool[] values)
    {
        try
        {
            Check(address, values.Length, Extention.CoilLimit);
            var request = CreateBaseRequest(FunctionCode.WriteMultipleCoils, (ushort)address, (ushort)values.Length);
            request.ByteCount = (byte)(values.Length % 8 != 0 ? values.Length / 8 + 1 : values.Length / 8);
            request.Length = (ushort)(request.ByteCount + 7);
            request.Values = values;
            var reponse = Fetch(request);
            reponse.AnalyseResponse(request);
            return Result.CreateSuccessResult();
        }
        catch (Exception e)
        {
            return Result.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// 写多个寄存器
    /// </summary>
    /// <param name="address">初始地址</param>
    /// <param name="values">数据</param>
    public Result WriteMultipleRegisters(int address, ushort[] values)
    {
        try
        {
            Check(address, values.Length, Extention.RegisterLimit);
            var request = CreateBaseRequest(FunctionCode.WriteMultipleRegisters, (ushort)address, (ushort)values.Length);
            request.ByteCount = (byte)(values.Length * 2);
            request.Length = (ushort)(request.ByteCount + 7);
            request.Values = values;
            var reponse = Fetch(request);
            reponse.AnalyseResponse(request);
            return Result.CreateSuccessResult();
        }
        catch (Exception e)
        {
            return Result.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// 屏蔽写寄存器
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="mask">掩码</param>
    /// <param name="value">新值</param>
    /// <returns></returns>
    public Result MaskWriteRegister(int address, ushort mask, ushort value)
    {
        try
        {
            Check(address, 1, Extention.RegisterLimit);
            var request = CreateBaseRequest(FunctionCode.MaskWriteRegister, (ushort)address, mask);
            request.Length = 8;
            request.Values = value;
            var reponse = Fetch(request);
            reponse.AnalyseResponse(request);
            return Result.CreateSuccessResult();
        }
        catch (Exception e)
        {
            return Result.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// 屏蔽写寄存器
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="mask">掩码</param>
    /// <param name="value">新值</param>
    /// <returns></returns>
    public Result MaskWriteRegister(int address, BitArray mask, BitArray value)
    {
        try
        {
            if (Config.EnableMask)
            {
                var maskBytes = new byte[2] { 0x00, 0x00 };
                var valueBytes = new byte[2] { 0x00, 0x00 };
                mask.CopyTo(maskBytes, 0);
                value.CopyTo(valueBytes, 0);
                return MaskWriteRegister(address, BitConverter.ToUInt16(maskBytes), BitConverter.ToUInt16(valueBytes));
            }
            else   //不支持MaskWriteRegister(0x16)的Modbus'Server的特殊处理，先读，然后组包，再发送
            {
                var res = Read<ushort>(address);
                res.ThrowExceptionIfError();
                var bytes = BitConverter.GetBytes(res.Content);
                var ba = new BitArray(bytes);
                for (var i = 0; i < 16; i++)
                {
                    if (!mask[i])
                    {
                        ba[i] = value[i];
                    }
                }
                var newValue = new byte[2];
                ba.CopyTo(newValue, 0);
                return Write(address, newValue.UnPackup());
            }
        }
        catch (Exception e)
        {
            return Result.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// 读/写多个寄存器
    /// </summary>
    /// <param name="addressRead">读起始地址</param>
    /// <param name="quantityRead">读数量</param>
    /// <param name="addressWrite">写起始地址</param>
    /// <param name="values">写的数据</param>
    /// <returns></returns>
    public Result<ushort[]> ReadWriteMultipleRegisters(int addressRead, int quantityRead, int addressWrite, ushort[] values)
    {
        try
        {
            Check(addressRead, quantityRead, Extention.RegisterLimit);
            Check(addressWrite, values.Length, Extention.RegisterLimit);
            var request = CreateBaseRequest(FunctionCode.ReadWriteMultipleRegisters, (ushort)addressRead, (ushort)quantityRead);
            request.ByteCount = (byte)(values.Length * 2);
            request.Length = (ushort)(11 + values.Length * 2);
            request.Values = values;
            request.Address2 = (ushort)addressWrite;
            request.Quantity2 = (ushort)values.Length;
            var reponse = Fetch(request);
            reponse.AnalyseResponse(request);
            return reponse.ParseData<ushort>(quantityRead);
        }
        catch (Exception e)
        {
            return Result<ushort[]>.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// 读(bool为线圈或离散输入，其他为寄存器)
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="address">起始地址</param>
    /// <param name="quantity">数量</param>
    /// <param name="Input">是否为输入寄存器或离散输入</param>
    /// <returns></returns>
    public Result<T[]> Read<T>(int address, int quantity, bool Input = false) where T : struct
    {
        try
        {
            if (typeof(char).IsAssignableFrom(typeof(T)))
            {
                var res = Read<ushort>(address, quantity / 2, Input);
                res.ThrowExceptionIfError();
                return (res.Content.ConvertChar() as Result<T[]>)!;
            }
            T[] list = new T[quantity];
            int limit = Extention.RegisterLimit;
            if (typeof(bool).IsAssignableFrom(typeof(T)))
                limit = Extention.CoilLimit;
            else
            {
                int size = Marshal.SizeOf<T>() / 2;
                quantity *= size;
            }
            int times = quantity / limit;
            int left = quantity % limit;
            for (int i = 0; i < times; i++)
            {
                read(list, limit * i, address, limit, Input);
            }
            if (left > 0)//处理余数
            {
                read(list, limit * times, address, left, Input);
            }
            return Result<T[]>.CreateSuccessResult(list);
        }
        catch (Exception e)
        {
            return Result<T[]>.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// 读(bool为线圈或离散输入，其他为寄存器)
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="address">起始地址</param>
    /// <param name="Input">是否为输入寄存器或离散输入</param>
    /// <returns></returns>
    public Result<T> Read<T>(int address, bool Input = false) where T : struct
    {
        try
        {
            var res = Read<T>(address, 1, Input);
            res.ThrowExceptionIfError();
            return Result<T>.CreateSuccessResult(res.Content[0]);
        }
        catch (Exception e)
        {
            return Result<T>.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// 写(bool为线圈，其他为寄存器)
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="address">起始地址</param>
    /// <param name="values">数据</param>
    /// <returns></returns>
    public Result Write<T>(int address, T[] values) where T : struct
    {
        try
        {
            int limit = Extention.RegisterLimit, step = Extention.RegisterLimit;
            if (typeof(bool).IsAssignableFrom(typeof(T)))
                limit = Extention.CoilLimit;
            else if (typeof(char).IsAssignableFrom(typeof(T)))
            {
                step = limit * 2;
            }
            else
            {
                int size = Marshal.SizeOf<T>() / 2;
                step = limit / size;
            }
            int times = values.Length / step;
            int left = values.Length % step;
            for (int i = 0; i < times; i++)
            {
                var tmp = new T[step];
                Array.Copy(values, i * step, tmp, 0, step);
                write(address + limit * i, tmp);
            }
            if (left > 0)//处理余数
            {
                var tmp = new T[left];
                Array.Copy(values, step * times, tmp, 0, left);
                write(address + limit * times, tmp);
            }
            return Result.CreateSuccessResult();
        }
        catch (Exception e)
        {
            return Result.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// 写(bool为线圈，其他为寄存器)
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="address">起始地址</param>
    /// <param name="value">数据</param>
    /// <returns></returns>
    public Result Write<T>(int address, T value) where T : struct => Write(address, new T[] { value });

    /// <summary>
    /// 保持寄存器读Int16(short)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="quantity">读取位数</param>
    /// <returns>返回数据</returns>
    public Result<short[]> ReadShort(int address, int quantity) => Read<short>(address, quantity);

    /// <summary>
    /// 保持寄存器读Int16(short)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <returns>返回数据</returns>
    public Result<short> ReadShort(int address) => Read<short>(address);

    /// <summary>
    /// 保持寄存器读UInt16(ushort)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="quantity">读取位数</param>
    /// <returns>返回数据</returns>
    public Result<ushort[]> ReadUShort(int address, int quantity) => Read<ushort>(address, quantity);

    /// <summary>
    /// 保持寄存器读UInt16(ushort)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <returns>返回数据</returns>
    public Result<ushort> ReadUShort(int address) => Read<ushort>(address);

    /// <summary>
    /// 保持寄存器读Int32(int)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="quantity">读取位数</param>
    /// <returns>返回数据</returns>
    public Result<int[]> ReadInt(int address, int quantity) => Read<int>(address, quantity);

    /// <summary>
    /// 保持寄存器读Int32(int)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <returns>返回数据</returns>
    public Result<int> ReadInt(int address) => Read<int>(address);

    /// <summary>
    /// 保持寄存器读UInt32(uint)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="quantity">读取位数</param>
    /// <returns>返回数据</returns>
    public Result<uint[]> ReadUInt(int address, int quantity) => Read<uint>(address, quantity);

    /// <summary>
    /// 保持寄存器读UInt32(uint)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <returns>返回数据</returns>
    public Result<uint> ReadUInt(int address) => Read<uint>(address);

    /// <summary>
    /// 保持寄存器读Int64(long)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="quantity">读取位数</param>
    /// <returns>返回数据</returns>
    public Result<long[]> ReadLong(int address, int quantity) => Read<long>(address, quantity);

    /// <summary>
    /// 保持寄存器读Int64(long)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <returns>返回数据</returns>
    public Result<long> ReadLong(int address) => Read<long>(address);

    /// <summary>
    /// 保持寄存器读UInt64(ulong)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="quantity">读取位数</param>
    /// <returns>返回数据</returns>
    public Result<ulong[]> ReadULong(int address, int quantity) => Read<ulong>(address, quantity);

    /// <summary>
    /// 保持寄存器读UInt64(ulong)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <returns>返回数据</returns>
    public Result<ulong> ReadULong(int address) => Read<ulong>(address);

    /// <summary>
    /// 保持寄存器读float
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="quantity">读取位数</param>
    /// <returns>返回数据</returns> 
    public Result<float[]> ReadFloat(int address, int quantity) => Read<float>(address, quantity);

    /// <summary>
    /// 保持寄存器读float
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <returns>返回数据</returns>
    public Result<float> ReadFloat(int address) => Read<float>(address);

    /// <summary>
    /// 保持寄存器读double
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="quantity">读取位数</param>
    /// <returns>返回数据</returns>
    public Result<double[]> ReadDouble(int address, int quantity) => Read<double>(address, quantity);

    /// <summary>
    /// 保持寄存器读double
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <returns>返回数据</returns>
    public Result<double> ReadDouble(int address) => Read<double>(address);

    /// <summary>
    /// 保持寄存器读string
    /// </summary>
    /// <param name="address">起始地址</param>
    /// /// <param name="quantity">读取位数</param>
    /// <returns>返回数据</returns>
    public Result<string> ReadString(int address, int quantity)
    {
        try
        {
            var res = Read<char>(address, quantity);
            res.ThrowExceptionIfError();
            return Result<string>.CreateSuccessResult(new string(res.Content));
        }
        catch (Exception e)
        {
            return Result<string>.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// 保持寄存器写Int16(short)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="values">数据</param>
    /// <returns>是否成功</returns>
    public Result WriteShort(int address, short[] values) => Write(address, values);

    /// <summary>
    /// 保持寄存器写Int16(short)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="value">数据</param>
    /// <returns>是否成功</returns>
    public Result WriteShort(int address, short value) => WriteSingleRegister(address, (ushort)value);

    /// <summary>
    /// 保持寄存器写UInt16(ushort)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="values">数据</param>
    /// <returns>是否成功</returns>
    public Result WriteUShort(int address, ushort[] values) => Write(address, values);

    /// <summary>
    /// 保持寄存器写UInt16(ushort)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="value">数据</param>
    /// <returns>是否成功</returns>
    public Result WriteUShort(int address, ushort value) => WriteSingleRegister(address, value);

    /// <summary>
    /// 保持寄存器写Int32(int)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="values">数据</param>
    /// <returns>是否成功</returns>
    public Result WriteInt(int address, int[] values) => Write(address, values);

    /// <summary>
    /// 保持寄存器写Int32(int)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="value">数据</param>
    /// <returns>是否成功</returns>
    public Result WriteInt(int address, int value) => Write(address, value);

    /// <summary>
    /// 保持寄存器写UInt32(uint)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="values">数据</param>
    /// <returns>是否成功</returns>
    public Result WriteUInt(int address, uint[] values) => Write(address, values);

    /// <summary>
    /// 保持寄存器写UInt32(uint)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="value">数据</param>
    /// <returns>是否成功</returns>
    public Result WriteUInt(int address, uint value) => Write(address, value);

    /// <summary>
    /// 保持寄存器写Int64(long)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="values">数据</param>
    /// <returns>是否成功</returns>
    public Result WriteLong(int address, long[] values) => Write(address, values);

    /// <summary>
    /// 保持寄存器写Int64(long)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="value">数据</param>
    /// <returns>是否成功</returns>
    public Result WriteLong(int address, long value) => Write(address, value);

    /// <summary>
    /// 保持寄存器写UInt64(ulong)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="values">数据</param>
    /// <returns>是否成功</returns>
    public Result WriteULong(int address, ulong[] values) => Write(address, values);

    /// <summary>
    /// 保持寄存器写UInt64(ulong)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="value">数据</param>
    /// <returns>是否成功</returns>
    public Result WriteULong(int address, ulong value) => Write(address, value);

    /// <summary>
    /// 保持寄存器写float
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="values">数据</param>
    /// <returns>是否成功</returns>
    public Result WriteFloat(int address, float[] values) => Write(address, values);

    /// <summary>
    /// 保持寄存器写float
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="value">数据</param>
    /// <returns>是否成功</returns>
    public Result WriteFloat(int address, float value) => Write(address, value);

    /// <summary>
    /// 保持寄存器写double
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="values">数据</param>
    /// <returns>是否成功</returns>
    public Result WriteDouble(int address, double[] values) => Write(address, values);

    /// <summary>
    /// 保持寄存器写double
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="value">数据</param>
    /// <returns>是否成功</returns>
    public Result WriteDouble(int address, double value) => Write(address, value);

    /// <summary>
    /// 保持寄存器写string
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="value">数据</param>
    /// <returns>是否成功</returns>
    public Result WriteString(int address, string value) => Write(address, value.ToCharArray());

    /// <summary>
    /// 寄存器读bit位(bool)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="bitIndex">起始位地址</param>
    /// <param name="quantity">数量</param>
    /// <param name="Input">是否为输入寄存器</param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public Result<bool[]> ReadBit(int address, int bitIndex, int quantity, bool Input = false)
    {
        try
        {
            if (bitIndex >= 16 || bitIndex < 0)
            {
                throw new ArgumentOutOfRangeException($"bitIndex({bitIndex})不合法(0~15)");
            }
            var ret = new bool[quantity];
            var sum = quantity + bitIndex;
            var count = sum / 16;
            if (sum % 16 > 0)
                count++;
            var res = Read<ushort>(address, count, Input);
            res.ThrowExceptionIfError();
            var ba = new BitArray(res.Content.Packup());
            var tmp = new bool[ba.Length];
            ba.CopyTo(tmp, 0);
            Array.Copy(tmp, bitIndex, ret, 0, quantity);
            return Result<bool[]>.CreateSuccessResult(ret);
        }
        catch (Exception ex)
        {
            return Result<bool[]>.CreateFailedResult(ex.Message);
        }
    }

    /// <summary>
    /// 寄存器读bit位(bool)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="bitIndex">起始位地址</param>
    /// <param name="Input">是否为输入寄存器</param>
    /// <returns></returns>
    public Result<bool> ReadBit(int address, int bitIndex, bool Input = false)
    {
        try
        {
            var res = ReadBit(address, bitIndex, 1, Input);
            res.ThrowExceptionIfError();
            return Result<bool>.CreateSuccessResult(res.Content[0]);
        }
        catch (Exception ex)
        {
            return Result<bool>.CreateFailedResult(ex.Message);
        }
    }

    /// <summary>
    /// 寄存器写bit位(bool)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="bitIndex">起始位地址</param>
    /// <param name="values">数据</param>
    /// <returns></returns>
    public Result WriteBit(int address, int bitIndex, bool[] values)
    {
        try
        {
            if (bitIndex >= 16 || bitIndex < 0)
            {
                throw new ArgumentOutOfRangeException($"bitIndex({bitIndex})不合法(0~15)");
            }
            int quantity = values.Length;
            var head = 16 - bitIndex;
            head = Math.Min(head, quantity);
            var maskBit = new BitArray(16, true);
            var metaBit = new BitArray(16, false);
            for (int i = bitIndex; i < bitIndex + head; i++)
            {
                maskBit[i] = false;
                metaBit[i] = values[i - bitIndex];
            }
            var res = MaskWriteRegister(address, maskBit, metaBit);
            res.ThrowExceptionIfError();
            var left = quantity - head;
            var times = left / 16;
            left %= 16;
            if (times > 0)
            {
                var tmp = new bool[times * 16];
                Array.Copy(values, head, tmp, 0, tmp.Length);
                var ba = new BitArray(tmp);
                var bytes = new byte[times * 2];
                ba.CopyTo(bytes, 0);
                var shorts = bytes.UnPackup();
                var res1 = Write(address + 1, shorts);
                res1.ThrowExceptionIfError();
            }
            if (left > 0)
            {
                maskBit = new BitArray(16, true);
                metaBit = new BitArray(16, false);
                for (int i = 0; i < left; i++)
                {
                    maskBit[i] = false;
                    metaBit[i] = values[head + times * 16 + i];
                }
                var res1 = MaskWriteRegister(address + 1 + times, maskBit, metaBit);
                res1.ThrowExceptionIfError();
            }
            return Result.CreateSuccessResult();
        }
        catch (Exception ex)
        {
            return Result.CreateFailedResult(ex.Message);
        }
    }

    /// <summary>
    /// 寄存器写bit位(bool)
    /// </summary>
    /// <param name="address">起始地址</param>
    /// <param name="bitIndex">起始位地址</param>
    /// <param name="value">数据</param>
    /// <returns></returns>
    public Result WriteBit(int address, int bitIndex, bool value) => WriteBit(address, bitIndex, new bool[] { value });
}
