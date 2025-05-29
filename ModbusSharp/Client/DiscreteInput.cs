namespace ModbusSharp.Client;

/// <summary>
/// 离散输入(只有bool量)
/// </summary>
public record DiscreteInput : IOReadBase<bool>
{

    /// <summary>
    /// 类型
    /// </summary>
    public override IOType Type => IOType.DiscreteInput;

    /// <summary>
    /// 读
    /// </summary>
    /// <returns></returns>
    public override Result<bool> Read()
    {
        try
        {
            var res = Client.Read<bool>(Address, true);
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
    /// 外部设置LastDta
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
