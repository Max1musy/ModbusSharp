namespace ModbusSharp;

/// <summary>
/// 操作通用返回结果
/// </summary>
public class Result
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; protected set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string ErrorMsg { get; protected set; } = string.Empty;

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static Result CreateSuccessResult() => new() { Success = true };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static Result CreateFailedResult(string msg) => new() { Success = false, ErrorMsg = msg };

    /// <summary>
    /// 抛异常
    /// </summary>
    /// <exception cref="Exception"></exception>
    public void ThrowExceptionIfError()
    {
        if (!Success) throw new Exception(ErrorMsg);
    }
}

/// <summary>
/// 操作返回结果
/// </summary>
/// <typeparam name="T"></typeparam>
public class Result<T> : Result
{
    /// <summary>
    /// 具体值
    /// </summary>
#pragma warning disable CS8601 // 引用类型赋值可能为 null。
    public T Content { get; protected set; } = default;
#pragma warning restore CS8601 // 引用类型赋值可能为 null。

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static Result<T> CreateSuccessResult(T value) => new() { Success = true, Content = value };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static new Result<T> CreateFailedResult(string msg) => new() { Success = false, ErrorMsg = msg };
}
