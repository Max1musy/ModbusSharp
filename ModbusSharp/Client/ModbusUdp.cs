using System.Net.Sockets;
using System.Net;

namespace ModbusSharp.Client;

/// <summary>
/// Udp协议的Modbus
/// </summary>
public class ModbusUdp : ModbusClient<SocketConfig>
{
    /// <summary>
    /// ModbusUdp构造函数
    /// </summary>
    /// <param name="config">配置</param>
    public ModbusUdp(SocketConfig config) : base(config)
    {
    }

    /// <summary>
    /// 协议类型
    /// </summary>
    /// <returns></returns>
    public override ModbusType Type => ModbusType.Udp;

    /// <summary>
    /// 发送和读取
    /// </summary>
    /// <param name="request">发送的报文</param>
    /// <returns>接收的报文</returns>
    protected override ModbusResponse Fetch(ModbusRequest request)
    {
        lock (this)
        {
            try
            {
                using (var udpClient = new UdpClient())
                {
                    var endPoint = new IPEndPoint(IPAddress.Parse(Config.IP), Config.Port);
                    var sendData = request.Build();
                    udpClient.Send(sendData, sendData.Length, endPoint);
                    var PortOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
                    udpClient.Client.ReceiveTimeout = Config.ReceiveTimeout;
                    endPoint = new IPEndPoint(IPAddress.Parse(Config.IP), PortOut);
                    var data = udpClient.Receive(ref endPoint);
                    return ModbusResponse.CreateSuccessResult(data);
                }
            }
            catch (Exception ex)
            {
                return ModbusResponse.CreateFailedResult(ex.Message);
            }
        }
    }
}
