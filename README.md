# ModbusSharp
Modbus TCP, Modbus UDP, Modbus Ascii and Modbus RTU client/server library for .NET implementations
## usage1
```
var client = new ModbusTcp();//ModbusUdp ModbusRtu ModbusAscii
var res = client.ReadDiscreteInputs(2, 2);
var res = client.ReadCoils(2, 2);
var res = client.ReadHoldingRegisters(2, 2);
var res = client.ReadInputRegisters(2, 2);
var res = client.WriteSingleCoil(2, true);
var res = client.WriteSingleRegister(2, 2);
var res = client.WriteMultipleCoils(2, new []);
var res = client.WriteMultipleRegisters(2, new []);
var res = client.MaskWriteRegister(2, 2);
var res = client.ReadWriteMultipleRegisters(2, 2);
var res = client.Read<ushort>(2, 2);
var res = client.Write(2, 2);
var res = client.ReadShort(2, 2);
var res = client.ReadUInt(2, 2);
var res = client.ReadFloat(2, 2);
var res = client.ReadString(2, 2);
var res = client.WriteInt(2, 2);
var res = client.WriteLong(2, 2);
var res = client.WriteString(2, 2);
var res = client.ReadBit(2, 2);
var res = client.WriteBit(2, 2);
```
## usage2
```
public class MyPlc : ModbusTcp
{
    [ModbusProperty(Address = 1)]
    public Coil 线圈测试 { get; set; } = new();

    [ModbusProperty(Address = 1)]
    public DiscreteInput 离散输入测试;

    [ModbusProperty(Address = 1)]
    public HoldingRegister<float> 保持寄存器测试;

    [ModbusProperty(Address = 1)]
    public InputRegister<double> 输入寄存器测试;

    public M(SocketConfig config) : base(config)
    {
    }
}

public static class StaticPlc
{
    [ModbusProperty(Address = 1)]
    public static Coil 线圈测试 = new();

    [ModbusProperty(Address = 1)]
    public static DiscreteInput 离散输入测试;

    [ModbusProperty(Address = 1)]
    public static HoldingRegister<float> 保持寄存器测试;

    [ModbusProperty(Address = 1)]
    public static InputRegister<double> 输入寄存器测试;

}

var plc = newMyPlc(new() { IP = "172.29.196.124",Port=1003, AutoRead = true});
plc.BindIOAttribute(typeof(StaticPlc));
plc.线圈测试.DataChanged += (from, to) => 
{
    //
};
StaticPlc.保持寄存器测试.DataChanged += (from, to) => 
{
    //
};
float aa = plc.保持寄存器测试 + master.保持寄存器测试 + 5.0f;
bool a = plc.线圈测试;
bool b = plc.保持寄存器测试 > 5;
plc.保持寄存器测试.InvokeChange(7);
var json = Newtonsoft.Json.JsonConvert.SerializeObject(plc);
```
