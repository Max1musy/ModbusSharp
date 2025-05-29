namespace ModbusSharp.Client;

/// <summary>
/// 线圈(只有bool量)
/// </summary>
public record Coil : IOWriteBase<bool>
{
    /// <summary>
    /// 类型
    /// </summary>
    public override IOType Type => IOType.Coil;

    /// <summary>
    /// 读
    /// </summary>
    /// <returns></returns>
    public override Result<bool> Read()
    {
        try 
        {
            var res = Client.Read<bool>(Address);
            res.ThrowExceptionIfError();
            InvokeChange(res.Content);
            return Result<bool>.CreateSuccessResult(LastData);
        }
        catch (Exception ex)
        {
            return Result<bool>.CreateFailedResult(ex.Message);
        }
    }

    /// <summary>
    /// 写
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public override Result Write(bool value)
    {
        try
        {
            var res = Client.Write(Address, value);
            res.ThrowExceptionIfError();
            LastData = value;
            return Result.CreateSuccessResult();
        }
        catch (Exception ex)
        {
            return Result.CreateFailedResult(ex.Message);
        }
    }

    /// <summary>
    /// 外部设置LastData
    /// </summary>
    /// <param name="values">一组数据</param>
    /// <param name="start">数据起点地址</param>
    public void SetValue(bool[] values, int start = 0)
    {
        if (Address >= start && Address - start < values.Length)
        {
            InvokeChange(values[Address - start]);
        }
    }
}
