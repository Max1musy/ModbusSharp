using System.Net.Sockets;
using System.Net;

namespace ModbusSharp.Server;

/// <summary>
/// Modbus协议内容
/// </summary>
public class ModbusProtocol
{
    public DateTime timeStamp;
    public bool request;
    public bool response;
    public ushort transactionIdentifier;
    public ushort protocolIdentifier;
    public ushort length;
    public byte unitIdentifier;
    public byte functionCode;
    public ushort startingAdress;
    public ushort startingAddressRead;
    public ushort startingAddressWrite;
    public ushort quantity;
    public ushort quantityRead;
    public ushort quantityWrite;
    public byte byteCount;
    public byte exceptionCode;
    public byte errorCode;
    public ushort[] receiveCoilValues;
    public ushort[] receiveRegisterValues;
    public short[] sendRegisterValues;
    public bool[] sendCoilValues;
}

struct NetworkConnectionParameter
{
    public NetworkStream stream;
    public byte[] bytes;
    public int portIn;
    public IPAddress ipAddressIn;
}
