using System.Net.Sockets;
using System.Net;

namespace ModbusSharp.Server;

/// <summary>
/// modbus客户端 只支持tcp协议
/// </summary>
public class ModbusServer
{
    public IPAddress IP { get; private set; } = IPAddress.Any;
    public int Port { get; private set; } = 502;
    public HoldingRegisterServer holdingRegisters;
    public InputRegisterServer inputRegisters;
    public CoilServer coils;
    public DiscreteInputServer discreteInputs;
    public int numberOfConnections { get => tcpHandler.NumberOfConnectedClients; }
    private byte unitIdentifier = 0x01;
    private TcpHandler tcpHandler;
    private Thread listenerThread;
    object lockCoils = new();
    object lockHoldingRegisters = new();
    public ModbusServer(IPAddress IP, int Port)
    {
        this.Port = Port;
        this.IP = IP;
        holdingRegisters = new HoldingRegisterServer() { modbusServer = this };
        inputRegisters = new InputRegisterServer() { modbusServer = this };
        coils = new CoilServer() { modbusServer = this };
        discreteInputs = new DiscreteInputServer() { modbusServer = this };
    }

    public delegate void CoilsChangedHandler(int coil, int numberOfCoils);
    public event CoilsChangedHandler CoilsChanged;

    public delegate void HoldingRegistersChangedHandler(int register, int numberOfRegisters);
    public event HoldingRegistersChangedHandler HoldingRegistersChanged;

    public delegate void NumberOfConnectedClientsChangedHandler();
    public event NumberOfConnectedClientsChangedHandler NumberOfConnectedClientsChanged;
    public void Listen()
    {
        coils.Build();
        discreteInputs.Build();
        inputRegisters.Build();
        holdingRegisters.Build();
        listenerThread = new Thread(ListenerThread);
        listenerThread.Start();
    }

    public void StopListening()
    {
        try
        {
            tcpHandler.Disconnect();
            listenerThread.Abort();

        }
        catch (Exception) { }
        listenerThread.Join();
    }

    private void ListenerThread()
    {
        tcpHandler = new TcpHandler(IP, Port);

        tcpHandler.dataChanged += new TcpHandler.DataChanged(ProcessReceivedData);
        tcpHandler.numberOfClientsChanged += new TcpHandler.NumberOfClientsChanged(numberOfClientsChanged);
    }

    private void numberOfClientsChanged()
    {
        if (NumberOfConnectedClientsChanged != null)
            NumberOfConnectedClientsChanged();
    }
   
    object lockProcessReceivedData = new();
 
    private void ProcessReceivedData(object networkConnectionParameter)
    {
        lock (lockProcessReceivedData)
        {
            var param = (NetworkConnectionParameter)networkConnectionParameter;
            byte[] bytes = new byte[param.bytes.Length];
            NetworkStream stream = param.stream;
            int portIn = param.portIn;
            IPAddress ipAddressIn = param.ipAddressIn;
            Array.Copy(param.bytes, 0, bytes, 0, param.bytes.Length);

            ModbusProtocol receiveDataThread = new ModbusProtocol();
            ModbusProtocol sendDataThread = new ModbusProtocol();

            try
            {
                ushort[] wordData = new ushort[1];
                byte[] byteData = new byte[2];
                receiveDataThread.timeStamp = DateTime.Now;
                receiveDataThread.request = true;


                //Lese Transaction identifier
                byteData[1] = bytes[0];
                byteData[0] = bytes[1];
                Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                receiveDataThread.transactionIdentifier = wordData[0];

                //Lese Protocol identifier
                byteData[1] = bytes[2];
                byteData[0] = bytes[3];
                Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                receiveDataThread.protocolIdentifier = wordData[0];

                //Lese length
                byteData[1] = bytes[4];
                byteData[0] = bytes[5];
                Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                receiveDataThread.length = wordData[0];


                //Lese unit identifier
                receiveDataThread.unitIdentifier = bytes[6];
                //Check UnitIdentifier
                if (receiveDataThread.unitIdentifier != unitIdentifier & receiveDataThread.unitIdentifier != 0)
                    return;

                // Lese function code
                receiveDataThread.functionCode = bytes[7];

                // Lese starting address 
                byteData[1] = bytes[8];
                byteData[0] = bytes[9];
                Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                receiveDataThread.startingAdress = wordData[0];

                if (receiveDataThread.functionCode <= 4)
                {
                    // Lese quantity
                    byteData[1] = bytes[10];
                    byteData[0] = bytes[11];
                    Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                    receiveDataThread.quantity = wordData[0];
                }
                if (receiveDataThread.functionCode == 5)
                {
                    receiveDataThread.receiveCoilValues = new ushort[1];
                    // Lese Value
                    byteData[1] = bytes[10];
                    byteData[0] = bytes[11];
                    Buffer.BlockCopy(byteData, 0, receiveDataThread.receiveCoilValues, 0, 2);
                }
                if (receiveDataThread.functionCode == 6)
                {
                    receiveDataThread.receiveRegisterValues = new ushort[1];
                    // Lese Value
                    byteData[1] = bytes[10];
                    byteData[0] = bytes[11];
                    Buffer.BlockCopy(byteData, 0, receiveDataThread.receiveRegisterValues, 0, 2);
                }
                if (receiveDataThread.functionCode == 15)
                {
                    // Lese quantity
                    byteData[1] = bytes[10];
                    byteData[0] = bytes[11];
                    Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                    receiveDataThread.quantity = wordData[0];

                    receiveDataThread.byteCount = bytes[12];

                    if (receiveDataThread.byteCount % 2 != 0)
                        receiveDataThread.receiveCoilValues = new ushort[receiveDataThread.byteCount / 2 + 1];
                    else
                        receiveDataThread.receiveCoilValues = new ushort[receiveDataThread.byteCount / 2];
                    // Lese Value
                    Buffer.BlockCopy(bytes, 13, receiveDataThread.receiveCoilValues, 0, receiveDataThread.byteCount);
                }
                if (receiveDataThread.functionCode == 16)
                {
                    // Lese quantity
                    byteData[1] = bytes[10];
                    byteData[0] = bytes[11];
                    Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                    receiveDataThread.quantity = wordData[0];

                    receiveDataThread.byteCount = bytes[12];
                    receiveDataThread.receiveRegisterValues = new ushort[receiveDataThread.quantity];
                    for (int i = 0; i < receiveDataThread.quantity; i++)
                    {
                        // Lese Value
                        byteData[1] = bytes[13 + i * 2];
                        byteData[0] = bytes[14 + i * 2];
                        Buffer.BlockCopy(byteData, 0, receiveDataThread.receiveRegisterValues, i * 2, 2);
                    }

                }
                if (receiveDataThread.functionCode == 23)
                {
                    // Lese starting Address Read
                    byteData[1] = bytes[8];
                    byteData[0] = bytes[9];
                    Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                    receiveDataThread.startingAddressRead = wordData[0];
                    // Lese quantity Read
                    byteData[1] = bytes[10];
                    byteData[0] = bytes[11];
                    Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                    receiveDataThread.quantityRead = wordData[0];
                    // Lese starting Address Write
                    byteData[1] = bytes[12];
                    byteData[0] = bytes[13];
                    Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                    receiveDataThread.startingAddressWrite = wordData[0];
                    // Lese quantity Write
                    byteData[1] = bytes[14];
                    byteData[0] = bytes[15];
                    Buffer.BlockCopy(byteData, 0, wordData, 0, 2);
                    receiveDataThread.quantityWrite = wordData[0];

                    receiveDataThread.byteCount = bytes[16];
                    receiveDataThread.receiveRegisterValues = new ushort[receiveDataThread.quantityWrite];
                    for (int i = 0; i < receiveDataThread.quantityWrite; i++)
                    {
                        // Lese Value
                        byteData[1] = bytes[17 + i * 2];
                        byteData[0] = bytes[18 + i * 2];
                        Buffer.BlockCopy(byteData, 0, receiveDataThread.receiveRegisterValues, i * 2, 2);
                    }
                }
            }
            catch (Exception exc)
            { }
            CreateAnswer(receiveDataThread, sendDataThread, stream, portIn, ipAddressIn);
        }
    }
    private void CreateAnswer(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
    {
        try
        {
            switch (receiveData.functionCode)
            {
                // Read Coils
                case 1:
                    if (!coils.enable)
                        throw new Exception();
                    ReadCoils(receiveData, sendData, stream, portIn, ipAddressIn);
                    break;
                // Read Input Registers
                case 2:
                    if (!discreteInputs.enable)
                        throw new Exception();
                    ReadDiscreteInputs(receiveData, sendData, stream, portIn, ipAddressIn);
                    break;
                // Read Holding Registers
                case 3:
                    if (!holdingRegisters.enable)
                        throw new Exception();
                    ReadHoldingRegisters(receiveData, sendData, stream, portIn, ipAddressIn);
                    break;
                // Read Input Registers
                case 4:
                    if (!inputRegisters.enable)
                        throw new Exception();
                    ReadInputRegisters(receiveData, sendData, stream, portIn, ipAddressIn);
                    break;
                // Write single coil
                case 5:
                    if (!coils.enable)
                        throw new Exception();
                    WriteSingleCoil(receiveData, sendData, stream, portIn, ipAddressIn);
                    break;
                // Write single register
                case 6:
                    if (!holdingRegisters.enable)
                        throw new Exception();
                    WriteSingleRegister(receiveData, sendData, stream, portIn, ipAddressIn);
                    break;
                // Write Multiple coils
                case 15:
                    if (!coils.enable)
                        throw new Exception();
                    WriteMultipleCoils(receiveData, sendData, stream, portIn, ipAddressIn);
                    break;
                // Write Multiple registers
                case 16:
                    if (!holdingRegisters.enable)
                        throw new Exception();
                    WriteMultipleRegisters(receiveData, sendData, stream, portIn, ipAddressIn);
                    break;
                // Error: Function Code not supported
                case 23:
                    if (!holdingRegisters.enable || !inputRegisters.enable)
                        throw new Exception();
                    ReadWriteMultipleRegisters(receiveData, sendData, stream, portIn, ipAddressIn);
                    break;
                // Error: Function Code not supported
                default:
                    throw new Exception();
            }
        }
        catch
        {
            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
            sendData.exceptionCode = 1;
            sendException(sendData.errorCode, sendData.exceptionCode, receiveData, sendData, stream, portIn, ipAddressIn);
        }
        
        sendData.timeStamp = DateTime.Now;
    }

    private void ReadCoils(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
    {
        sendData.response = true;

        sendData.transactionIdentifier = receiveData.transactionIdentifier;
        sendData.protocolIdentifier = receiveData.protocolIdentifier;

        sendData.unitIdentifier = unitIdentifier;
        sendData.functionCode = receiveData.functionCode;
        if (receiveData.quantity < 0 | receiveData.quantity > 0x07D0)  //Invalid quantity
        {
            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
            sendData.exceptionCode = 3;
        }
        if (receiveData.startingAdress + receiveData.quantity > 65535 | receiveData.startingAdress < 0)     //Invalid Starting adress or Starting address + quantity
        {
            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
            sendData.exceptionCode = 2;
        }
        if (sendData.exceptionCode == 0)
        {
            if (receiveData.quantity % 8 == 0)
                sendData.byteCount = (byte)(receiveData.quantity / 8);
            else
                sendData.byteCount = (byte)(receiveData.quantity / 8 + 1);

            sendData.sendCoilValues = new bool[receiveData.quantity];
            lock (lockCoils)
                Array.Copy(coils.localArray, receiveData.startingAdress, sendData.sendCoilValues, 0, receiveData.quantity);
        }
        byte[] data;

        if (sendData.exceptionCode > 0)
            data = new byte[9];
        else
            data = new byte[9 + sendData.byteCount];

        byte[] byteData = new byte[2];

        sendData.length = (byte)(data.Length - 6);

        //Send Transaction identifier
        byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
        data[0] = byteData[1];
        data[1] = byteData[0];

        //Send Protocol identifier
        byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
        data[2] = byteData[1];
        data[3] = byteData[0];

        //Send length
        byteData = BitConverter.GetBytes((int)sendData.length);
        data[4] = byteData[1];
        data[5] = byteData[0];
        //Unit Identifier
        data[6] = sendData.unitIdentifier;

        //Function Code
        data[7] = sendData.functionCode;

        //ByteCount
        data[8] = sendData.byteCount;

        if (sendData.exceptionCode > 0)
        {
            data[7] = sendData.errorCode;
            data[8] = sendData.exceptionCode;
            sendData.sendCoilValues = null;
        }

        if (sendData.sendCoilValues != null)
            for (int i = 0; i < sendData.byteCount; i++)
            {
                byteData = new byte[2];
                for (int j = 0; j < 8; j++)
                {
                    byte boolValue;
                    if (sendData.sendCoilValues[i * 8 + j] == true)
                        boolValue = 1;
                    else
                        boolValue = 0;
                    byteData[1] = (byte)(byteData[1] | boolValue << j);
                    if (i * 8 + j + 1 >= sendData.sendCoilValues.Length)
                        break;
                }
                data[9 + i] = byteData[1];
            }
        try
        {
            stream.Write(data, 0, data.Length);
        }
        catch (Exception) { }
        
    }

    private void ReadDiscreteInputs(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
    {
        sendData.response = true;

        sendData.transactionIdentifier = receiveData.transactionIdentifier;
        sendData.protocolIdentifier = receiveData.protocolIdentifier;

        sendData.unitIdentifier = unitIdentifier;
        sendData.functionCode = receiveData.functionCode;
        if (receiveData.quantity < 0 | receiveData.quantity > 0x07D0)  //Invalid quantity
        {
            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
            sendData.exceptionCode = 3;
        }
        if (receiveData.startingAdress + receiveData.quantity > 65535 | receiveData.startingAdress < 0)   //Invalid Starting adress or Starting address + quantity
        {
            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
            sendData.exceptionCode = 2;
        }
        if (sendData.exceptionCode == 0)
        {
            if (receiveData.quantity % 8 == 0)
                sendData.byteCount = (byte)(receiveData.quantity / 8);
            else
                sendData.byteCount = (byte)(receiveData.quantity / 8 + 1);

            sendData.sendCoilValues = new bool[receiveData.quantity];
            Array.Copy(discreteInputs.localArray, receiveData.startingAdress, sendData.sendCoilValues, 0, receiveData.quantity);
        }
        
        byte[] data;
        if (sendData.exceptionCode > 0)
            data = new byte[9];
        else
            data = new byte[9 + sendData.byteCount];
        byte[] byteData = new byte[2];
        sendData.length = (byte)(data.Length - 6);

        //Send Transaction identifier
        byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
        data[0] = byteData[1];
        data[1] = byteData[0];

        //Send Protocol identifier
        byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
        data[2] = byteData[1];
        data[3] = byteData[0];

        //Send length
        byteData = BitConverter.GetBytes((int)sendData.length);
        data[4] = byteData[1];
        data[5] = byteData[0];

        //Unit Identifier
        data[6] = sendData.unitIdentifier;

        //Function Code
        data[7] = sendData.functionCode;

        //ByteCount
        data[8] = sendData.byteCount;


        if (sendData.exceptionCode > 0)
        {
            data[7] = sendData.errorCode;
            data[8] = sendData.exceptionCode;
            sendData.sendCoilValues = null;
        }

        if (sendData.sendCoilValues != null)
            for (int i = 0; i < sendData.byteCount; i++)
            {
                byteData = new byte[2];
                for (int j = 0; j < 8; j++)
                {

                    byte boolValue;
                    if (sendData.sendCoilValues[i * 8 + j] == true)
                        boolValue = 1;
                    else
                        boolValue = 0;
                    byteData[1] = (byte)(byteData[1] | boolValue << j);
                    if (i * 8 + j + 1 >= sendData.sendCoilValues.Length)
                        break;
                }
                data[9 + i] = byteData[1];
            }

        try
        {
            stream.Write(data, 0, data.Length);
        }
        catch (Exception) { }
    }

    private void ReadHoldingRegisters(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
    {
        sendData.response = true;

        sendData.transactionIdentifier = receiveData.transactionIdentifier;
        sendData.protocolIdentifier = receiveData.protocolIdentifier;

        sendData.unitIdentifier = unitIdentifier;
        sendData.functionCode = receiveData.functionCode;
        if (receiveData.quantity < 0 | receiveData.quantity > 0x007D)  //Invalid quantity
        {
            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
            sendData.exceptionCode = 3;
        }
        if (receiveData.startingAdress + receiveData.quantity > 65535 | receiveData.startingAdress < 0)   //Invalid Starting adress or Starting address + quantity
        {
            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
            sendData.exceptionCode = 2;
        }
        if (sendData.exceptionCode == 0)
        {
            sendData.byteCount = (byte)(2 * receiveData.quantity);
            sendData.sendRegisterValues = new short[receiveData.quantity];
            lock (lockHoldingRegisters)
                Buffer.BlockCopy(holdingRegisters.localArray, receiveData.startingAdress * 2, sendData.sendRegisterValues, 0, receiveData.quantity * 2);
        }
        if (sendData.exceptionCode > 0)
            sendData.length = 0x03;
        else
            sendData.length = (ushort)(0x03 + sendData.byteCount);
        byte[] data;
        if (sendData.exceptionCode > 0)
            data = new byte[9];
        else
            data = new byte[9 + sendData.byteCount];
        byte[] byteData = new byte[2];
        sendData.length = (byte)(data.Length - 6);

        //Send Transaction identifier
        byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
        data[0] = byteData[1];
        data[1] = byteData[0];

        //Send Protocol identifier
        byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
        data[2] = byteData[1];
        data[3] = byteData[0];

        //Send length
        byteData = BitConverter.GetBytes((int)sendData.length);
        data[4] = byteData[1];
        data[5] = byteData[0];

        //Unit Identifier
        data[6] = sendData.unitIdentifier;

        //Function Code
        data[7] = sendData.functionCode;

        //ByteCount
        data[8] = sendData.byteCount;

        if (sendData.exceptionCode > 0)
        {
            data[7] = sendData.errorCode;
            data[8] = sendData.exceptionCode;
            sendData.sendRegisterValues = null;
        }


        if (sendData.sendRegisterValues != null)
            for (int i = 0; i < sendData.byteCount / 2; i++)
            {
                byteData = BitConverter.GetBytes(sendData.sendRegisterValues[i]);
                data[9 + i * 2] = byteData[1];
                data[10 + i * 2] = byteData[0];
            }
        try
        {
            stream.Write(data, 0, data.Length);
        }
        catch (Exception) { }
    }

    private void ReadInputRegisters(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
    {
        sendData.response = true;

        sendData.transactionIdentifier = receiveData.transactionIdentifier;
        sendData.protocolIdentifier = receiveData.protocolIdentifier;

        sendData.unitIdentifier = unitIdentifier;
        sendData.functionCode = receiveData.functionCode;
        if (receiveData.quantity < 0 | receiveData.quantity > 0x007D)  //Invalid quantity
        {
            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
            sendData.exceptionCode = 3;
        }
        if (receiveData.startingAdress + receiveData.quantity > 65535 | receiveData.startingAdress < 0)   //Invalid Starting adress or Starting address + quantity
        {
            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
            sendData.exceptionCode = 2;
        }
        if (sendData.exceptionCode == 0)
        {
            sendData.byteCount = (byte)(2 * receiveData.quantity);
            sendData.sendRegisterValues = new short[receiveData.quantity];
            Buffer.BlockCopy(inputRegisters.localArray, receiveData.startingAdress * 2, sendData.sendRegisterValues, 0, receiveData.quantity * 2);
        }
        if (sendData.exceptionCode > 0)
            sendData.length = 0x03;
        else
            sendData.length = (ushort)(0x03 + sendData.byteCount);

        byte[] data;
        if (sendData.exceptionCode > 0)
            data = new byte[9];
        else
            data = new byte[9 + sendData.byteCount];
        byte[] byteData = new byte[2];
        sendData.length = (byte)(data.Length - 6);

        //Send Transaction identifier
        byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
        data[0] = byteData[1];
        data[1] = byteData[0];

        //Send Protocol identifier
        byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
        data[2] = byteData[1];
        data[3] = byteData[0];

        //Send length
        byteData = BitConverter.GetBytes((int)sendData.length);
        data[4] = byteData[1];
        data[5] = byteData[0];

        //Unit Identifier
        data[6] = sendData.unitIdentifier;

        //Function Code
        data[7] = sendData.functionCode;

        //ByteCount
        data[8] = sendData.byteCount;


        if (sendData.exceptionCode > 0)
        {
            data[7] = sendData.errorCode;
            data[8] = sendData.exceptionCode;
            sendData.sendRegisterValues = null;
        }
        if (sendData.sendRegisterValues != null)
            for (int i = 0; i < sendData.byteCount / 2; i++)
            {
                byteData = BitConverter.GetBytes(sendData.sendRegisterValues[i]);
                data[9 + i * 2] = byteData[1];
                data[10 + i * 2] = byteData[0];
            }
        try
        {
            stream.Write(data, 0, data.Length);
        }
        catch (Exception) { }
    }

    private void WriteSingleCoil(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
    {
        sendData.response = true;

        sendData.transactionIdentifier = receiveData.transactionIdentifier;
        sendData.protocolIdentifier = receiveData.protocolIdentifier;

        sendData.unitIdentifier = unitIdentifier;
        sendData.functionCode = receiveData.functionCode;
        sendData.startingAdress = receiveData.startingAdress;
        sendData.receiveCoilValues = receiveData.receiveCoilValues;
        if (receiveData.receiveCoilValues[0] != 0x0000 & receiveData.receiveCoilValues[0] != 0xFF00)  //Invalid Value
        {
            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
            sendData.exceptionCode = 3;
        }
        if (receiveData.startingAdress > 65535 | receiveData.startingAdress < 0)    //Invalid Starting adress or Starting address + quantity
        {
            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
            sendData.exceptionCode = 2;
        }
        if (sendData.exceptionCode == 0)
        {
            if (receiveData.receiveCoilValues[0] == 0xFF00)
            {
                lock (lockCoils)
                    coils[receiveData.startingAdress] = true;
            }
            if (receiveData.receiveCoilValues[0] == 0x0000)
            {
                lock (lockCoils)
                    coils[receiveData.startingAdress] = false;
            }
        }
        if (sendData.exceptionCode > 0)
            sendData.length = 0x03;
        else
            sendData.length = 0x06;

        byte[] data;
        if (sendData.exceptionCode > 0)
            data = new byte[9];
        else
            data = new byte[12];

        byte[] byteData = new byte[2];
        sendData.length = (byte)(data.Length - 6);

        //Send Transaction identifier
        byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
        data[0] = byteData[1];
        data[1] = byteData[0];

        //Send Protocol identifier
        byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
        data[2] = byteData[1];
        data[3] = byteData[0];

        //Send length
        byteData = BitConverter.GetBytes((int)sendData.length);
        data[4] = byteData[1];
        data[5] = byteData[0];

        //Unit Identifier
        data[6] = sendData.unitIdentifier;

        //Function Code
        data[7] = sendData.functionCode;
        if (sendData.exceptionCode > 0)
        {
            data[7] = sendData.errorCode;
            data[8] = sendData.exceptionCode;
            sendData.sendRegisterValues = null;
        }
        else
        {
            byteData = BitConverter.GetBytes((int)receiveData.startingAdress);
            data[8] = byteData[1];
            data[9] = byteData[0];
            byteData = BitConverter.GetBytes((int)receiveData.receiveCoilValues[0]);
            data[10] = byteData[1];
            data[11] = byteData[0];
        }


        try
        {
            stream.Write(data, 0, data.Length);
        }
        catch (Exception) { }
        if (CoilsChanged != null)
            CoilsChanged(receiveData.startingAdress, 1);
    }

    private void WriteSingleRegister(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
    {
        sendData.response = true;

        sendData.transactionIdentifier = receiveData.transactionIdentifier;
        sendData.protocolIdentifier = receiveData.protocolIdentifier;

        sendData.unitIdentifier = unitIdentifier;
        sendData.functionCode = receiveData.functionCode;
        sendData.startingAdress = receiveData.startingAdress;
        sendData.receiveRegisterValues = receiveData.receiveRegisterValues;

        if (receiveData.receiveRegisterValues[0] < 0x0000 | receiveData.receiveRegisterValues[0] > 0xFFFF)  //Invalid Value
        {
            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
            sendData.exceptionCode = 3;
        }
        if (receiveData.startingAdress > 65535 | receiveData.startingAdress < 0)    //Invalid Starting adress or Starting address + quantity
        {
            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
            sendData.exceptionCode = 2;
        }
        if (sendData.exceptionCode == 0)
        {
            lock (lockHoldingRegisters)
                holdingRegisters[receiveData.startingAdress] = unchecked((short)receiveData.receiveRegisterValues[0]);
        }
        if (sendData.exceptionCode > 0)
            sendData.length = 0x03;
        else
            sendData.length = 0x06;

        byte[] data;
        if (sendData.exceptionCode > 0)
            data = new byte[9];
        else
            data = new byte[12];

        byte[] byteData = new byte[2];
        sendData.length = (byte)(data.Length - 6);


        //Send Transaction identifier
        byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
        data[0] = byteData[1];
        data[1] = byteData[0];

        //Send Protocol identifier
        byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
        data[2] = byteData[1];
        data[3] = byteData[0];

        //Send length
        byteData = BitConverter.GetBytes((int)sendData.length);
        data[4] = byteData[1];
        data[5] = byteData[0];

        //Unit Identifier
        data[6] = sendData.unitIdentifier;

        //Function Code
        data[7] = sendData.functionCode;



        if (sendData.exceptionCode > 0)
        {
            data[7] = sendData.errorCode;
            data[8] = sendData.exceptionCode;
            sendData.sendRegisterValues = null;
        }
        else
        {
            byteData = BitConverter.GetBytes((int)receiveData.startingAdress);
            data[8] = byteData[1];
            data[9] = byteData[0];
            byteData = BitConverter.GetBytes((int)receiveData.receiveRegisterValues[0]);
            data[10] = byteData[1];
            data[11] = byteData[0];
        }


        try
        {
            stream.Write(data, 0, data.Length);
        }
        catch (Exception) { }
        if (HoldingRegistersChanged != null)
            HoldingRegistersChanged(receiveData.startingAdress, 1);
    }

    private void WriteMultipleCoils(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
    {
        sendData.response = true;

        sendData.transactionIdentifier = receiveData.transactionIdentifier;
        sendData.protocolIdentifier = receiveData.protocolIdentifier;

        sendData.unitIdentifier = unitIdentifier;
        sendData.functionCode = receiveData.functionCode;
        sendData.startingAdress = receiveData.startingAdress;
        sendData.quantity = receiveData.quantity;

        if (receiveData.quantity == 0x0000 | receiveData.quantity > 0x07B0)  //Invalid Quantity
        {
            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
            sendData.exceptionCode = 3;
        }
        if (receiveData.startingAdress + receiveData.quantity > 65535 | receiveData.startingAdress < 0)    //Invalid Starting adress or Starting address + quantity
        {
            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
            sendData.exceptionCode = 2;
        }
        if (sendData.exceptionCode == 0)
        {
            lock (lockCoils)
                for (int i = 0; i < receiveData.quantity; i++)
                {
                    int shift = i % 16;
                    /*                if ((i == receiveData.quantity - 1) & (receiveData.quantity % 2 != 0))
                                    {
                                        if (shift < 8)
                                            shift = shift + 8;
                                        else
                                            shift = shift - 8;
                                    }*/
                    int mask = 0x1;
                    mask = mask << shift;
                    if ((receiveData.receiveCoilValues[i / 16] & (ushort)mask) == 0)

                        coils[receiveData.startingAdress + i] = false;
                    else

                        coils[receiveData.startingAdress + i] = true;

                }
        }
        if (sendData.exceptionCode > 0)
            sendData.length = 0x03;
        else
            sendData.length = 0x06;
        byte[] data;
        if (sendData.exceptionCode > 0)
            data = new byte[9];
        else
            data = new byte[12];

        byte[] byteData = new byte[2];
        sendData.length = (byte)(data.Length - 6);

        //Send Transaction identifier
        byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
        data[0] = byteData[1];
        data[1] = byteData[0];

        //Send Protocol identifier
        byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
        data[2] = byteData[1];
        data[3] = byteData[0];

        //Send length
        byteData = BitConverter.GetBytes((int)sendData.length);
        data[4] = byteData[1];
        data[5] = byteData[0];

        //Unit Identifier
        data[6] = sendData.unitIdentifier;

        //Function Code
        data[7] = sendData.functionCode;

        if (sendData.exceptionCode > 0)
        {
            data[7] = sendData.errorCode;
            data[8] = sendData.exceptionCode;
            sendData.sendRegisterValues = null;
        }
        else
        {
            byteData = BitConverter.GetBytes((int)receiveData.startingAdress);
            data[8] = byteData[1];
            data[9] = byteData[0];
            byteData = BitConverter.GetBytes((int)receiveData.quantity);
            data[10] = byteData[1];
            data[11] = byteData[0];
        }

        try
        {
            stream.Write(data, 0, data.Length);
        }
        catch (Exception) { }
        if (CoilsChanged != null)
            CoilsChanged(receiveData.startingAdress, receiveData.quantity);
    }

    private void WriteMultipleRegisters(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
    {
        sendData.response = true;

        sendData.transactionIdentifier = receiveData.transactionIdentifier;
        sendData.protocolIdentifier = receiveData.protocolIdentifier;

        sendData.unitIdentifier = unitIdentifier;
        sendData.functionCode = receiveData.functionCode;
        sendData.startingAdress = receiveData.startingAdress;
        sendData.quantity = receiveData.quantity;

        if (receiveData.quantity == 0x0000 | receiveData.quantity > 0x07B0)  //Invalid Quantity
        {
            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
            sendData.exceptionCode = 3;
        }
        if (receiveData.startingAdress + receiveData.quantity > 65535 | receiveData.startingAdress < 0)   //Invalid Starting adress or Starting address + quantity
        {
            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
            sendData.exceptionCode = 2;
        }
        if (sendData.exceptionCode == 0)
        {
            lock (lockHoldingRegisters)
                for (int i = 0; i < receiveData.quantity; i++)
                {
                    holdingRegisters[receiveData.startingAdress + i] = unchecked((short)receiveData.receiveRegisterValues[i]);
                }
        }
        if (sendData.exceptionCode > 0)
            sendData.length = 0x03;
        else
            sendData.length = 0x06;
        byte[] data;
        if (sendData.exceptionCode > 0)
            data = new byte[9];
        else
            data = new byte[12];

        byte[] byteData = new byte[2];
        sendData.length = (byte)(data.Length - 6);

        //Send Transaction identifier
        byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
        data[0] = byteData[1];
        data[1] = byteData[0];

        //Send Protocol identifier
        byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
        data[2] = byteData[1];
        data[3] = byteData[0];

        //Send length
        byteData = BitConverter.GetBytes((int)sendData.length);
        data[4] = byteData[1];
        data[5] = byteData[0];

        //Unit Identifier
        data[6] = sendData.unitIdentifier;

        //Function Code
        data[7] = sendData.functionCode;



        if (sendData.exceptionCode > 0)
        {
            data[7] = sendData.errorCode;
            data[8] = sendData.exceptionCode;
            sendData.sendRegisterValues = null;
        }
        else
        {
            byteData = BitConverter.GetBytes((int)receiveData.startingAdress);
            data[8] = byteData[1];
            data[9] = byteData[0];
            byteData = BitConverter.GetBytes((int)receiveData.quantity);
            data[10] = byteData[1];
            data[11] = byteData[0];
        }

        try
        {
            stream.Write(data, 0, data.Length);
        }
        catch (Exception) { }
        if (HoldingRegistersChanged != null)
            HoldingRegistersChanged(receiveData.startingAdress, receiveData.quantity);
        
    }

    private void ReadWriteMultipleRegisters(ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
    {
        sendData.response = true;

        sendData.transactionIdentifier = receiveData.transactionIdentifier;
        sendData.protocolIdentifier = receiveData.protocolIdentifier;

        sendData.unitIdentifier = unitIdentifier;
        sendData.functionCode = receiveData.functionCode;


        if (receiveData.quantityRead < 0x0001 | receiveData.quantityRead > 0x007D | receiveData.quantityWrite < 0x0001 | receiveData.quantityWrite > 0x0079 | receiveData.byteCount != receiveData.quantityWrite * 2)  //Invalid Quantity
        {
            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
            sendData.exceptionCode = 3;
        }
        if (receiveData.startingAddressRead + 1 + receiveData.quantityRead > 65535 | receiveData.startingAddressWrite + 1 + receiveData.quantityWrite > 65535 | receiveData.quantityWrite < 0 | receiveData.quantityRead < 0)    //Invalid Starting adress or Starting address + quantity
        {
            sendData.errorCode = (byte)(receiveData.functionCode + 0x80);
            sendData.exceptionCode = 2;
        }
        if (sendData.exceptionCode == 0)
        {
            sendData.sendRegisterValues = new short[receiveData.quantityRead];
            lock (lockHoldingRegisters)
                Buffer.BlockCopy(holdingRegisters.localArray, receiveData.startingAddressRead * 2 + 2, sendData.sendRegisterValues, 0, receiveData.quantityRead * 2);

            lock (holdingRegisters)
                for (int i = 0; i < receiveData.quantityWrite; i++)
                {
                    holdingRegisters[receiveData.startingAddressWrite + i + 1] = unchecked((short)receiveData.receiveRegisterValues[i]);
                }
            sendData.byteCount = (byte)(2 * receiveData.quantityRead);
        }
        if (sendData.exceptionCode > 0)
            sendData.length = 0x03;
        else
            sendData.length = Convert.ToUInt16(3 + 2 * receiveData.quantityRead);
        byte[] data;
        if (sendData.exceptionCode > 0)
            data = new byte[9];
        else
            data = new byte[9 + sendData.byteCount];

        byte[] byteData = new byte[2];

        //Send Transaction identifier
        byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
        data[0] = byteData[1];
        data[1] = byteData[0];

        //Send Protocol identifier
        byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
        data[2] = byteData[1];
        data[3] = byteData[0];

        //Send length
        byteData = BitConverter.GetBytes((int)sendData.length);
        data[4] = byteData[1];
        data[5] = byteData[0];

        //Unit Identifier
        data[6] = sendData.unitIdentifier;

        //Function Code
        data[7] = sendData.functionCode;

        //ByteCount
        data[8] = sendData.byteCount;


        if (sendData.exceptionCode > 0)
        {
            data[7] = sendData.errorCode;
            data[8] = sendData.exceptionCode;
            sendData.sendRegisterValues = null;
        }
        else
        {
            if (sendData.sendRegisterValues != null)
                for (int i = 0; i < sendData.byteCount / 2; i++)
                {
                    byteData = BitConverter.GetBytes(sendData.sendRegisterValues[i]);
                    data[9 + i * 2] = byteData[1];
                    data[10 + i * 2] = byteData[0];
                }
        }
        try
        {
            stream.Write(data, 0, data.Length);
        }
        catch (Exception) { }
        if (HoldingRegistersChanged != null)
            HoldingRegistersChanged(receiveData.startingAddressWrite + 1, receiveData.quantityWrite);
        
    }

    private void sendException(int errorCode, int exceptionCode, ModbusProtocol receiveData, ModbusProtocol sendData, NetworkStream stream, int portIn, IPAddress ipAddressIn)
    {
        sendData.response = true;

        sendData.transactionIdentifier = receiveData.transactionIdentifier;
        sendData.protocolIdentifier = receiveData.protocolIdentifier;

        sendData.unitIdentifier = receiveData.unitIdentifier;
        sendData.errorCode = (byte)errorCode;
        sendData.exceptionCode = (byte)exceptionCode;

        if (sendData.exceptionCode > 0)
            sendData.length = 0x03;
        else
            sendData.length = (ushort)(0x03 + sendData.byteCount);

        if (true)
        {
            byte[] data;
            if (sendData.exceptionCode > 0)
                data = new byte[9];
            else
                data = new byte[9 + sendData.byteCount];
            byte[] byteData = new byte[2];
            sendData.length = (byte)(data.Length - 6);

            //Send Transaction identifier
            byteData = BitConverter.GetBytes((int)sendData.transactionIdentifier);
            data[0] = byteData[1];
            data[1] = byteData[0];

            //Send Protocol identifier
            byteData = BitConverter.GetBytes((int)sendData.protocolIdentifier);
            data[2] = byteData[1];
            data[3] = byteData[0];

            //Send length
            byteData = BitConverter.GetBytes((int)sendData.length);
            data[4] = byteData[1];
            data[5] = byteData[0];

            //Unit Identifier
            data[6] = sendData.unitIdentifier;


            data[7] = sendData.errorCode;
            data[8] = sendData.exceptionCode;


            try
            {
                stream.Write(data, 0, data.Length);
            }
            catch (Exception) { }
        }
    }

}
