namespace ModbusSharp;

/// <summary>
/// 操作通用返回结果
/// </summary>
public record Result
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
    public static Result Ok() => new() { Success = true };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static Result Failed(string msg) => new() { Success = false, ErrorMsg = msg };

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static Result<T> Ok<T>(T value) => new() { Success = true, Content = value };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static Result<T> Failed<T>(string msg) => new() { Success = false, ErrorMsg = msg };

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
public record Result<T> : Result
{
    /// <summary>
    /// 具体值
    /// </summary>
#pragma warning disable CS8601 // 引用类型赋值可能为 null。
    public T Content { get; internal set; } = default;
#pragma warning restore CS8601 // 引用类型赋值可能为 null。
}
