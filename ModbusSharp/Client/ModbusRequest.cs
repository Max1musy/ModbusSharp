using System.Collections;

namespace ModbusSharp.Client;

/// <summary>
/// Modbus请求体
/// </summary>
public class ModbusRequest : ContentBase
{
    /// <summary>
    /// 起始地址
    /// </summary>
    public ushort Address { get; set; }

    /// <summary>
    /// 数量
    /// </summary>
    public ushort Quantity { get; set; }

    /// <summary>
    /// 位个数，写数据才会用到
    /// </summary>
    public byte? ByteCount { get; set; } = null;

    /// <summary>
    /// 数据位，写多个数据时才会用到
    /// </summary>
    public object? Values { get; set; }

    /// <summary>
    /// 起始地址2,ReadWriteMultipleRegisters才会用到
    /// </summary>
    public ushort? Address2 { get; set; }

    /// <summary>
    /// 数量2,ReadWriteMultipleRegisters才会用到
    /// </summary>
    public ushort? Quantity2 { get; set; }

    /// <summary>
    /// 建立报文
    /// </summary>
    /// <returns></returns>
    public byte[] Build()
    {
        var ret = new List<byte>();
        if (ModbusType == ModbusType.Tcp || ModbusType == ModbusType.Udp)
        {
            ret.AddRange(TransactionIdentifier.ToByte());//事务   占2位 0. 1
            ret.AddRange(ProtocolIdentifier.ToByte());   //协议   占2位 2. 3
            ret.AddRange(Length.ToByte());               //长度   占2位 4. 5
        }
        ret.Add(UnitIdentifier);                        //从站id 0x01   占1位 6
        ret.Add((byte)FunctionCode);                    //功能码 占1位 7
        ret.AddRange(Address.ToByte());                 //地址   占2位 8. 9
        ret.AddRange(Quantity.ToByte());                //数量   占2位 10.11
        if (Address2 != null)
        {
            ret.AddRange(Address2.Value.ToByte());    //地址2   占2位 
        }
        if (Quantity2 != null)
        {
            ret.AddRange(Quantity2.Value.ToByte());   //数量2   占2位 
        }
        if (ByteCount != null)
        {
            ret.Add(ByteCount.Value);                //bc     占1位 
        }
        switch (FunctionCode)                           //数据   占N位 
        {
            case FunctionCode.WriteMultipleCoils:
                byte singleCoilValue = 0;
                var bools = (bool[])Values!;
                for (int i = 0; i < bools.Length; i++)
                {
                    if (i % 8 == 0)
                        singleCoilValue = 0;
                    byte CoilValue = bools[i] ? (byte)1 : (byte)0;
                    singleCoilValue = (byte)(CoilValue << i % 8 | singleCoilValue);
                    if (ret.Count <= 13 + i / 8)
                        ret.Add(singleCoilValue);
                    ret[13 + i / 8] = singleCoilValue;
                }
                break;
            case FunctionCode.WriteMultipleRegisters:
            case FunctionCode.ReadWriteMultipleRegisters:
                var shorts = (ushort[])Values!;
                for (int i = 0; i < shorts.Length; i++)
                {
                    ret.AddRange(shorts[i].ToByte());
                }
                break;
            case FunctionCode.MaskWriteRegister:
                var newValue = (ushort)Values!;
                ret.AddRange(newValue.ToByte());
                break;
            default:
                break;
        }
        if (ModbusType == ModbusType.Rtu)
        {
            ret.AddRange(ret.ToArray().Crc16(0, false));      //CRC
        }
        if (ModbusType == ModbusType.Ascii)
        {
            ret.Add(ret.ToArray().Lrc());
            var body = ":" + BitConverter.ToString(ret.ToArray()).Replace("-", "") + "\r\n";
            return System.Text.Encoding.ASCII.GetBytes(body);
        }
        return ret.ToArray();
    }

    /// <summary>
    /// 需要读取的长度
    /// </summary>
    /// <returns></returns>
    public int BytesToRead()
    {
        int count;
        switch (FunctionCode)
        {
            case FunctionCode.ReadCoils:
            case FunctionCode.ReadDiscreteInputs:
                count = 9 + Quantity / 8 + (Quantity % 8 == 0 ? 0 : 1);
                break;
            case FunctionCode.ReadHoldingRegisters:
            case FunctionCode.ReadInputRegisters:
                count = 9 + 2 * Quantity;
                break;
            case FunctionCode.WriteSingleCoil:
            case FunctionCode.WriteSingleRegister:
            case FunctionCode.WriteMultipleRegisters:
            case FunctionCode.WriteMultipleCoils:
            default:
                count = 12;
                break;
            case FunctionCode.MaskWriteRegister:
                count = 14;
                break;
        }
        if (ModbusType == ModbusType.Rtu)
            return count - 4;
        if (ModbusType == ModbusType.Ascii)
            return count * 2 - 7;
        return count;
    }
}

/// <summary>
/// Modbus响应
/// </summary>
public class ModbusResponse : ContentBase
{
    /// <summary>
    /// 错误码
    /// </summary>
    public byte Exception { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; protected set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string ErrorMsg { get; protected set; } = string.Empty;

    /// <summary>
    /// 内容
    /// </summary>
    public byte[] Content { get; protected set; } = new byte[] { };

    /// <summary>
    /// 分析响应
    /// </summary>
    /// <param name="request"></param>
    public void AnalyseResponse(ModbusRequest request)
    {
        if (!Success) throw new Exception(ErrorMsg);
        switch (request.ModbusType)
        {
            case ModbusType.Tcp:
            case ModbusType.Udp:
                TransactionIdentifier = BitConverter.ToUInt16(new byte[] { Content[1], Content[0] });
                if (TransactionIdentifier != request.TransactionIdentifier)
                {
                    throw new ArgumentException("响应事务号错误");
                }
                ProtocolIdentifier = BitConverter.ToUInt16(new byte[] { Content[3], Content[2] });
                if (ProtocolIdentifier != request.ProtocolIdentifier)
                {
                    throw new ArgumentException("响应协议标识符错误");
                }
                Length = BitConverter.ToUInt16(new byte[] { Content[5], Content[4] });
                UnitIdentifier = Content[6];
                FunctionCode = (FunctionCode)Content[7];
                Exception = Content[8];
                break;
            case ModbusType.Rtu:
                UnitIdentifier = Content[0];
                FunctionCode = (FunctionCode)Content[1];
                Exception = Content[2];
                break;
            case ModbusType.Ascii:
                if (Content[0] != 0x3A)
                {
                    throw new ArgumentException("0x3A错误");
                }
                var hexString = System.Text.Encoding.ASCII.GetString(Content).Substring(1).Replace("\r\n", "");
                var numberChars = hexString.Length;
                var hexBytes = new byte[numberChars / 2];
                for (int i = 0; i < numberChars; i += 2)
                {
                    hexBytes[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
                }
                Content = hexBytes;
                UnitIdentifier = Content[0];
                FunctionCode = (FunctionCode)Content[1];
                Exception = Content[2];
                break;
            default:
                break;
        }
        if (UnitIdentifier != request.UnitIdentifier)
        {
            throw new ArgumentException("响应从站ID错误");
        }
        ModbusType = request.ModbusType;
        if ((byte)FunctionCode == (byte)(request.FunctionCode + 0x80))
        {
            throw Exception switch
            {
                0x01 => new Exception($"主站不支持此功能码({request.FunctionCode})"),
                0x02 => new Exception("起始地址出错或起始地址加数量出错"),
                0x03 => new Exception("数量出错"),
                0x04 => new Exception("Modbus读取出错"),
                _ => new Exception("未知错误"),
            };
        }
        if (request.ModbusType == ModbusType.Rtu)
        {
            var tmp = new byte[Content.Length - 2];
            Array.Copy(Content, 0, tmp, 0, tmp.Length);
            var crc = tmp.Crc16(0, false);
            for (var i = 0; i < crc.Length; i++)
            {
                if (crc[i] != Content[i + tmp.Length])
                {
                    throw new Exception("CRC校验出错");
                }
            }
        }
        if (request.ModbusType == ModbusType.Ascii)
        {
            var tmp = Content.Take(Content.Length - 2).ToArray();
            var lrc = tmp.Lrc();
            if (Content[Content.Length - 1] != lrc)
            {
                throw new Exception("LRC校验出错");
            }
        }
    }

    /// <summary>
    /// 解析响应数据
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="quantity"></param>
    /// <returns></returns>
    public Result<T[]> ParseData<T>(int quantity) where T : struct
    {
        try
        {
            var head = (ModbusType == ModbusType.Rtu || ModbusType == ModbusType.Ascii) ? 3 : 9;
            if (Content.Length < head)
                throw new Exception($"{typeof(T).Name}解析数据失败");
            var tmp = Content.Skip(head).Take(Content.Length - head).ToArray();
            var data = new T[quantity];
            if (typeof(bool).IsAssignableFrom(typeof(T)))
            {
                var bitArray = new BitArray(tmp);
                for (int i = 0; i < quantity; i++)
                {
                    data[i] = (T)Convert.ChangeType(bitArray[i], typeof(T));
                }
            }
            else if (typeof(ushort).IsAssignableFrom(typeof(T)))
            {
                for (int i = 0; i < quantity; i++)
                {
                    var value = BitConverter.ToUInt16(new byte[] { tmp[i * 2 + 1], tmp[i * 2] });
                    data[i] = (T)Convert.ChangeType(value, typeof(T));
                }
            }
            else
            {
                throw new Exception($"{typeof(T).Name}类型错误");
            }
            return Result<T[]>.CreateSuccessResult(data);
        }
        catch (Exception e)
        {
            return Result<T[]>.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static ModbusResponse CreateSuccessResult(byte[] value) => new() { Success = true, Content = value };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static ModbusResponse CreateFailedResult(string msg) => new() { Success = false, ErrorMsg = msg };
}

/// <summary>
/// 基本体
/// </summary>
public class ContentBase
{
    /// <summary>
    /// 协议类型
    /// </summary>
    public ModbusType ModbusType { get; set; }
    /// <summary>
    /// 事务ID号
    /// </summary>
    public ushort TransactionIdentifier { get; set; } = 0;

    /// <summary>
    /// 协议标识符，用于标识Modbus协议，通常为 0
    /// </summary>
    public ushort ProtocolIdentifier { get; set; } = 0;

    /// <summary>
    /// 帧的长度，表示后续字段的字节数
    /// </summary>
    public ushort Length { get; set; } = 6;

    /// <summary>
    /// 从站地址，用于标识 Modbus 从站
    /// </summary>
    public byte UnitIdentifier { get; set; } = 0x01;

    /// <summary>
    /// 功能码，表示主站请求的操作类型
    /// </summary>
    public FunctionCode FunctionCode { get; set; }
}
