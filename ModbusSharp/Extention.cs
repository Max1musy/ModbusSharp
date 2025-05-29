using System.Runtime.InteropServices;

namespace ModbusSharp;

/// <summary>
/// Modbus功能码
/// </summary>
public enum FunctionCode
{
    /// <summary>
    /// 读线圈
    /// </summary>
    ReadCoils = 0x01,
    /// <summary>
    /// 读离散输入
    /// </summary>
    ReadDiscreteInputs = 0x02,
    /// <summary>
    /// 读保持寄存器
    /// </summary>
    ReadHoldingRegisters = 0x03,
    /// <summary>
    /// 读输入寄存器
    /// </summary>
    ReadInputRegisters = 0x04,
    /// <summary>
    /// 写单个线圈
    /// </summary>
    WriteSingleCoil = 0x05,
    /// <summary>
    /// 写单个保持寄存器
    /// </summary>
    WriteSingleRegister = 0x06,
    /// <summary>
    /// 写多个线圈
    /// </summary>
    WriteMultipleCoils = 0x0F,
    /// <summary>
    /// 写多个保持寄存器
    /// </summary>
    WriteMultipleRegisters = 0x10,
    /// <summary>
    /// 屏蔽写寄存器
    /// </summary>
    MaskWriteRegister = 0x16,
    /// <summary>
    /// 读/写多个寄存器
    /// </summary>
    ReadWriteMultipleRegisters = 0x17,
    /// <summary>
    /// 文件记录操作
    /// </summary>
    FileRecord = 0x14,
    /// <summary>
    /// 设备诊断
    /// </summary>
    Diagnostic = 0x08,
    /// <summary>
    /// 设备操作
    /// </summary>
    DeviceIdentification = 0x2B
}

/// <summary>
/// Modbus类型
/// </summary>
public enum ModbusType
{ 
    /// <summary>
    /// Tcp
    /// </summary>
    Tcp,
    /// <summary>
    /// Udp
    /// </summary>
    Udp,
    /// <summary>
    /// Rtu
    /// </summary>
    Rtu,
    /// <summary>
    /// ASCII
    /// </summary>
    Ascii
}

/// <summary>
/// 排列方式
/// </summary>
public enum RegisterOrder
{
    /// <summary>
    /// 高位在前
    /// </summary>
    LowHigh = 0,
    /// <summary>
    /// 低位在前
    /// </summary>
    HighLow = 1
};

/// <summary>
/// 扩展
/// </summary>
public static class Extention
{
    /// <summary>
    /// 事务ID号
    /// </summary>
    public static ushort TransactionIdentifier { get; set; } = 0;
    /// <summary>
    /// 寄存器一次读写限制
    /// </summary>
    public static readonly int RegisterLimit = 124;
    /// <summary>
    /// 线圈一次读写限制
    /// </summary>
    public static readonly int CoilLimit = 2000;
    /// <summary>
    /// 校验码校验计算(CRC-16/MODBUS)
    /// x16+x15+x2+1(0x8005)
    /// </summary>
    /// <param name="buffer">数据</param>
    /// /// <param name="offset">起始位置</param>
    /// <param name="Reverse">true=高字节在前、false=高字节在后</param>
    /// <returns></returns>
    public static byte[] Crc16(this byte[] buffer, int offset = 0, bool Reverse = true)
    {
        int len = buffer.Length;
        if (len > 0)
        {
            ushort crc = 0xFFFF;

            for (var i = offset; i < len; i++)
            {
                crc = (ushort)(crc ^ (buffer[i]));
                for (int j = 0; j < 8; j++)
                {
                    crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
                }
            }
            byte hi = (byte)((crc & 0xFF00) >> 8);  //高位置
            byte lo = (byte)(crc & 0x00FF);         //低位置

            if (!Reverse)
                return new byte[] { lo, hi };
            else
                return new byte[] { hi, lo };
        }
        return new byte[] { 0, 0 };
    }

    /// <summary>
    /// 校验码校验计算(Lrc)
    /// </summary>
    /// <param name="buffer">数据</param>
    /// <returns></returns>
    public static byte Lrc(this byte[] buffer)
    {
        byte lrc = 0;
        foreach (byte b in buffer)
        {
            lrc += b;
        }
        lrc = (byte)(-((sbyte)lrc));
        return lrc;
    }

    /// <summary>
    /// ushortarray封装为bytearray
    /// </summary>
    /// <param name="data"></param>
    /// /// <param name="str"></param>
    /// <returns></returns>
    public static byte[] Packup(this ushort[] data, bool str = false)
    {
        var result = new List<byte>();
        foreach (var value in data)
        {
            var bytes = BitConverter.GetBytes(value);
            if (str)
                Array.Reverse(bytes);
            result.AddRange(bytes);
        }
        return result.ToArray();
    }

    /// <summary>
    /// bytearray封装为ushortarray
    /// </summary>
    /// <param name="data"></param>
    /// /// /// <param name="str"></param>
    /// <returns></returns>
    public static ushort[] UnPackup(this byte[] data, bool str = false)
    {
        var result = new List<ushort>();
        for (int i = 0; i < data.Length - 1; i += 2)
        {
            if (str)
                result.Add(BitConverter.ToUInt16(new byte[] { data[i + 1], data[i] }, 0));
            else
                result.Add(BitConverter.ToUInt16(data, i));
        }
        return result.ToArray();
    }

    /// /// <summary>
    /// 将sizeof(T)/2个寄存器数据转换为T
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="registers">寄存器数据</param>
    /// <param name="registerOrder">排列方式 默认LowHigh</param>
    /// <returns>转换后的数据</returns>
    public static Result<T> To<T>(this ushort[] registers, RegisterOrder registerOrder = RegisterOrder.LowHigh) where T : struct
    {
        try
        {
            int size = Marshal.SizeOf<T>() / 2;
            if (registers.Length < size)
                throw new ArgumentException($"输入长度异常，寄存器转{typeof(T).Name}需要{size}个寄存器值");
            if (registerOrder == RegisterOrder.HighLow)
                Array.Reverse(registers);
            var tmp = new ushort[size];
            Array.Copy(registers, 0, tmp, 0, tmp.Length);
            var bytes = tmp.Packup();
            T value;
            var type = typeof(T);
            var converters = new Dictionary<Type, Func<byte[], T>>()
            {
                { typeof(short), b => (T)Convert.ChangeType(BitConverter.ToInt16(b), type) },
                { typeof(ushort), b => (T)Convert.ChangeType(BitConverter.ToUInt16(b), type) },
                { typeof(int), b => (T)Convert.ChangeType(BitConverter.ToInt32(b), type) },
                { typeof(uint), b => (T)Convert.ChangeType(BitConverter.ToUInt32(b), type) },
                { typeof(long), b => (T)Convert.ChangeType(BitConverter.ToInt64(b), type) },
                { typeof(ulong), b => (T)Convert.ChangeType(BitConverter.ToUInt64(b), type) },
                { typeof(float), b => (T)Convert.ChangeType(BitConverter.ToSingle(b), type) },
                { typeof(double), b => (T)Convert.ChangeType(BitConverter.ToDouble(b), type) }
            };
            if (converters.TryGetValue(type, out var converter))
            {
                value = converter(bytes);
            }
            else
            {
                throw new Exception($"{typeof(T).Name}类型错误");
            }
            return Result<T>.CreateSuccessResult(value);
        }
        catch (Exception e)
        {
            return Result<T>.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// 将寄存器数据转换为string
    /// </summary>
    /// <param name="registers">寄存器数据</param>
    /// <param name="offset">包含要转换的字符串的第一个寄存器</param>
    /// <returns>Converted String</returns>
    public static Result<string> ConvertString(this ushort[] registers, int offset = 0)
    {
        try
        {
            if (offset > registers.Length)
                throw new Exception("offset必须小于registers的长度");
            var tmp = new ushort[registers.Length - offset];
            Array.Copy(registers, offset, tmp, 0, tmp.Length);
            var bytes = tmp.Packup(true);
            return Result<string>.CreateSuccessResult(System.Text.Encoding.Default.GetString(bytes));
        }
        catch (Exception e)
        {
            return Result<string>.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// 将寄存器数据转换为string
    /// </summary>
    /// <param name="registers">寄存器数据</param>
    /// <param name="offset">包含要转换的字符串的第一个寄存器</param>
    /// <returns>Converted String</returns>
    public static Result<char[]> ConvertChar(this ushort[] registers, int offset = 0)
    {
        try
        {
            var res = ConvertString(registers, offset);
            res.ThrowExceptionIfError();
            return Result<char[]>.CreateSuccessResult(res.Content.ToCharArray());
        }
        catch (Exception e)
        {
            return Result<char[]>.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// 将T转化为sizeof(T)/2个寄存器数据
    /// </summary>
    /// <param name="value">float数据</param>
    /// /// <param name="registerOrder">排列方式 默认LowHigh</param>
    /// <returns>转换后的寄存器数据short[sizeof(T)/2]</returns>
    public static Result<ushort[]> ToRegisters<T>(this T value, RegisterOrder registerOrder = RegisterOrder.LowHigh) where T : struct
    {
        try
        {
            byte[] bytes;
            var type = typeof(T);
            var converters = new Dictionary<Type, Func<T, byte[]>>()
            {
                { typeof(short), v => BitConverter.GetBytes(Convert.ToInt16(v)) },
                { typeof(ushort), v => BitConverter.GetBytes(Convert.ToUInt16(v)) },
                { typeof(int), v => BitConverter.GetBytes(Convert.ToInt32(v)) },
                { typeof(uint), v => BitConverter.GetBytes(Convert.ToUInt32(v)) },
                { typeof(long), v => BitConverter.GetBytes(Convert.ToInt64(v)) },
                { typeof(ulong), v => BitConverter.GetBytes(Convert.ToUInt64(v)) },
                { typeof(float), v => BitConverter.GetBytes(Convert.ToSingle(v)) },
                { typeof(double), v => BitConverter.GetBytes(Convert.ToDouble(v)) }
            };
            if (converters.TryGetValue(type, out var converter))
            {
                bytes = converter(value);
            }
            else
            {
                throw new Exception($"{typeof(T).Name}类型错误");
            }
            var tmp = bytes.UnPackup();
            if (registerOrder == RegisterOrder.HighLow)
            {
                Array.Reverse(tmp);
            }
            return Result<ushort[]>.CreateSuccessResult(tmp);
        }
        catch (Exception e)
        {
            return Result<ushort[]>.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// 将string转换为寄存器数据
    /// </summary>
    /// <param name="stringToConvert">寄存器数据</param>
    /// <returns>转换后的String</returns>
    public static Result<ushort[]> ToRegisters(this string stringToConvert)
    {
        try
        {
            var array = System.Text.Encoding.Default.GetBytes(stringToConvert);
            var tmp = array.UnPackup(true);
            return Result<ushort[]>.CreateSuccessResult(tmp);
        }
        catch (Exception e)
        {
            return Result<ushort[]>.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// 将char[]转换为寄存器数据
    /// </summary>
    /// <param name="stringToConvert">寄存器数据</param>
    /// <returns>转换后的String</returns>
    public static Result<ushort[]> ToRegisters(this char[] stringToConvert)
    {
        try
        {
            var array = System.Text.Encoding.Default.GetBytes(stringToConvert);
            var tmp = array.UnPackup(true);
            return Result<ushort[]>.CreateSuccessResult(tmp);
        }
        catch (Exception e)
        {
            return Result<ushort[]>.CreateFailedResult(e.Message);
        }
    }

    /// <summary>
    /// ushort转bytearray
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static byte[] ToByte(this ushort value)
    {
        var bytes = BitConverter.GetBytes(value);
        return new byte[] { bytes[1], bytes[0] };
    }
}
