using System.Diagnostics.Metrics;
using ModbusSharp.Client;
using ModbusSharp.Server;
namespace test;

public partial class Form1 : Form
{
    public Form1()
    {
        InitializeComponent();
        DataGridViewTextBoxColumn columnName = new DataGridViewTextBoxColumn();
        columnName.HeaderText = "offset";
        DataGridViewTextBoxColumn columnAge = new DataGridViewTextBoxColumn();
        columnAge.HeaderText = "value";
        dataGridView1.Columns.Add(columnName);
        dataGridView1.Columns.Add(columnAge);
        DataGridViewTextBoxColumn columnName1 = new DataGridViewTextBoxColumn();
        columnName1.HeaderText = "offset";
        DataGridViewTextBoxColumn columnAge1 = new DataGridViewTextBoxColumn();
        columnAge1.HeaderText = "value";
        dataGridView2.Columns.Add(columnName1);
        dataGridView2.Columns.Add(columnAge1);
        DataGridViewTextBoxColumn columnName2 = new DataGridViewTextBoxColumn();
        columnName2.HeaderText = "offset";
        DataGridViewTextBoxColumn columnAge2 = new DataGridViewTextBoxColumn();
        columnAge2.HeaderText = "value";
        dataGridView3.Columns.Add(columnName2);
        dataGridView3.Columns.Add(columnAge2);
        DataGridViewTextBoxColumn columnName3 = new DataGridViewTextBoxColumn();
        columnName3.HeaderText = "offset";
        DataGridViewTextBoxColumn columnAge3 = new DataGridViewTextBoxColumn();
        columnAge3.HeaderText = "value";
        dataGridView4.Columns.Add(columnName3);
        dataGridView4.Columns.Add(columnAge3);

        //ModbusServer server = new ModbusServer(IPAddress.Any, 502);
        //server.coils.count = 30;
        //server.coils.enable = true;
        //server.Listen();
    }

    M master;
    private void button1_Click(object sender, EventArgs e)
    {
        string ip = textBox1.Text;
        master = new(new() { IP = "172.29.196.124",Port=1003, AutoRead = true});
        master.BindIOAttribute(typeof(F));
        new Thread(new ThreadStart(() =>
        {
            while (true)
            {
                Thread.Sleep(100);
                master.ReadCoils(0, 2);
                label1.SafeSet(master.Connected ? "1" : "0");
            }
        }))
        { IsBackground = true }.Start();
        Task.Run(() => {
            while (true)
            {
                Thread.Sleep(100);
                master.ReConnect();
            }
        });
        button1.Enabled = false;
        master.œﬂ»¶≤‚ ‘.DataChanged += (from, to) => 
        {
            int aaa = 00;
        };
        master.±£≥÷ºƒ¥Ê∆˜≤‚ ‘.DataChanged += (from, to) => 
        {
            int aaa = 00;
        };
        float aa = master.±£≥÷ºƒ¥Ê∆˜≤‚ ‘ + master.±£≥÷ºƒ¥Ê∆˜≤‚ ‘ + 5.0f;
        bool a = master.œﬂ»¶≤‚ ‘;
        bool b = master.±£≥÷ºƒ¥Ê∆˜≤‚ ‘ > 5;
        master.±£≥÷ºƒ¥Ê∆˜≤‚ ‘.InvokeChange(7);
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(master);
    }

    private async void button2_Click(object sender, EventArgs e)
    {
        if (master == null) return;
        button2.Enabled = false;
        dataGridView1.Rows.Clear();
        await Task.Run(() =>
        {
            try
            {
                                    var aa = master.ReadCoils(0, 2);
                                    if (aa.Success)
                                    {
                                        for (ushort i = 0; i < 2; i++)
                                        {
                                            dataGridView1.SafeAdd(i.ToString(), aa.Content[i]);
                                        }
                                    }

                //foreach (var item in master.IOBases)
                //{
                //    string v = (item as dynamic).LastData.ToString();
                //    dataGridView1.SafeAdd(item.Name, v);
                //}
            }
            catch (Exception ex) { }
        });
        button2.Enabled = true;
    }

    private async void button3_Click(object sender, EventArgs e)
    {
        if (master == null) return;
        button3.Enabled = false;
        dataGridView2.Rows.Clear();
        await Task.Run(() =>
        {
            try
            {
                var aa = master.ReadDiscreteInputs(0, 10);
                if (aa.Success)
                {
                    for (ushort i = 0; i < 10; i++)
                    {
                        dataGridView2.SafeAdd(i.ToString(), aa.Content[i]);
                    }
                }
            }
            catch (Exception ex) { }
        });
        bool œﬂ»¶≤‚ ‘ = master.œﬂ»¶≤‚ ‘;
        bool b = master.±£≥÷ºƒ¥Ê∆˜≤‚ ‘ == 0;
        var a = master.±£≥÷ºƒ¥Ê∆˜≤‚ ‘ + 3;
        button3.Enabled = true;
    }

    private async void button4_Click(object sender, EventArgs e)
    {
        if (master == null) return;
        button4.Enabled = false;
        try
        {
            master.WriteSingleCoil(int.Parse(textBox2.Text), textBox3.Text == "1" || textBox3.Text == "true" || textBox3.Text == "True");
        }
        catch { }
        button4.Enabled = true;
    }

    private async void button5_Click(object sender, EventArgs e)
    {
        if (master == null) return;
        button5.Enabled = false;
        dataGridView3.Rows.Clear();
        await Task.Run(() =>
        {
            try
            {
                var aa = master.Read<short>(0, 10);
                if (aa.Success)
                {
                    for (ushort i = 0; i < 10; i++)
                    {
                        dataGridView3.SafeAdd(i.ToString(), aa.Content[i]);
                    }
                }
            }
            catch (Exception ex) { }
        });
        button5.Enabled = true;
    }

    private async void button7_Click(object sender, EventArgs e)
    {
        if (master == null) return;
        button7.Enabled = false;
        dataGridView4.Rows.Clear();
        await Task.Run(() =>
        {
            try
            {
                var aa = master.Read<short>(0, 10, true);
                if (aa.Success)
                {
                    for (ushort i = 0; i < 10; i++)
                    {
                        dataGridView4.SafeAdd(i.ToString(), aa.Content[i]);
                    }
                }
            }
            catch (Exception ex) { }
        });
        button7.Enabled = true;
    }

    private void button6_Click(object sender, EventArgs e)
    {
        if (master == null) return;
        button6.Enabled = false;
        try
        {
            master.Write(int.Parse(textBox2.Text), short.Parse(textBox3.Text));
        }
        catch { }
        button6.Enabled = true;
    }
}


public class M : ModbusTcp
{
    [ModbusProperty(Address = 1)]
    public Coil œﬂ»¶≤‚ ‘ { get; set; } = new();

    [ModbusProperty(Address = 1)]
    public DiscreteInput ¿Î…¢ ‰»Î≤‚ ‘;

    [ModbusProperty(Address = 1)]
    public HoldingRegister<float> ±£≥÷ºƒ¥Ê∆˜≤‚ ‘;

    [ModbusProperty(Address = 1)]
    public InputRegister<double>  ‰»Îºƒ¥Ê∆˜≤‚ ‘;

    public M(SocketConfig config) : base(config)
    {
    }
}

public static class F
{
    [ModbusProperty(Address = 1)]
    public static Coil œﬂ»¶≤‚ ‘ = new();

    [ModbusProperty(Address = 1)]
    public static DiscreteInput ¿Î…¢ ‰»Î≤‚ ‘;

    [ModbusProperty(Address = 1)]
    public static HoldingRegister<float> ±£≥÷ºƒ¥Ê∆˜≤‚ ‘;

    [ModbusProperty(Address = 1)]
    public static InputRegister<double>  ‰»Îºƒ¥Ê∆˜≤‚ ‘;

}



public static class SafeStatic
{
    public static void SafeAdd(this DataGridView dv, string pos, object value)
    {
        if (dv.InvokeRequired)
        {
            Action safe = delegate { dv.SafeAdd(pos, value); };
            dv.Invoke(safe);
        }
        else
        {
            dv.Rows.Add(pos, value);
        }
    }

    public static void SafeSet(this Label lb, string txt)
    {
        if (lb.InvokeRequired)
        {
            Action safe = delegate { lb.SafeSet(txt); };
            lb.Invoke(safe);
        }
        else
        {
            lb.Text = txt;
        }
    }
}