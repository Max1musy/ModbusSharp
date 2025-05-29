using System.Net.Sockets;
using System.Net;

namespace ModbusSharp.Server;

internal class TcpHandler
{

    public delegate void DataChanged(object networkConnectionParameter);
    public event DataChanged dataChanged;

    public delegate void NumberOfClientsChanged();
    public event NumberOfClientsChanged numberOfClientsChanged;

    TcpListener server = null;

    private List<Client> tcpClientLastRequestList = new List<Client>();

    public int NumberOfConnectedClients { get; set; }

    public IPAddress LocalIPAddress { get; private set; } = IPAddress.Any;

    /// <summary>
    /// 监听所有ip
    /// </summary>
    /// <param name="port">监听的端口</param>
    public TcpHandler(int port)
    {
        server = new TcpListener(LocalIPAddress, port);
        server.Start();
        server.BeginAcceptTcpClient(AcceptTcpClientCallback, null);
    }

    /// <summary>
    /// 监听所有某一个ip
    /// </summary>
    /// <param name="localIPAddress">监听的ip</param>
    /// <param name="port">监听的端口</param>
    public TcpHandler(IPAddress localIPAddress, int port)
    {
        LocalIPAddress = localIPAddress;
        server = new TcpListener(LocalIPAddress, port);
        server.Start();
        server.BeginAcceptTcpClient(AcceptTcpClientCallback, null);
    }


    private void AcceptTcpClientCallback(IAsyncResult asyncResult)
    {
        TcpClient tcpClient = new TcpClient();
        try
        {
            tcpClient = server.EndAcceptTcpClient(asyncResult);
            tcpClient.ReceiveTimeout = 4000;
        }
        catch (Exception) { }
        try
        {
            server.BeginAcceptTcpClient(AcceptTcpClientCallback, null);
            Client client = new Client(tcpClient);
            NetworkStream networkStream = client.NetworkStream;
            networkStream.ReadTimeout = 4000;
            networkStream.BeginRead(client.Buffer, 0, client.Buffer.Length, ReadCallback, client);
        }
        catch (Exception) { }
    }

    private int GetAndCleanNumberOfConnectedClients(Client client)
    {
        lock (this)
        {
            int i = 0;
            bool objetExists = false;
            foreach (Client clientLoop in tcpClientLastRequestList)
            {
                if (client.Equals(clientLoop))
                    objetExists = true;
            }
            try
            {
                tcpClientLastRequestList.RemoveAll(delegate (Client c)
                {
                    return DateTime.Now.Ticks - c.Ticks > 40000000;
                }

                    );
            }
            catch (Exception) { }
            if (!objetExists)
                tcpClientLastRequestList.Add(client);


            return tcpClientLastRequestList.Count;
        }
    }

    private void ReadCallback(IAsyncResult asyncResult)
    {
        NetworkConnectionParameter networkConnectionParameter = new NetworkConnectionParameter();
        Client client = asyncResult.AsyncState as Client;
        client.Ticks = DateTime.Now.Ticks;
        NumberOfConnectedClients = GetAndCleanNumberOfConnectedClients(client);
        if (numberOfClientsChanged != null)
            numberOfClientsChanged();
        if (client != null)
        {
            int read;
            NetworkStream networkStream = null;
            try
            {
                networkStream = client.NetworkStream;

                read = networkStream.EndRead(asyncResult);
            }
            catch (Exception ex)
            {
                return;
            }
            if (read == 0)
            {
                return;
            }
            byte[] data = new byte[read];
            Buffer.BlockCopy(client.Buffer, 0, data, 0, read);
            networkConnectionParameter.bytes = data;
            networkConnectionParameter.stream = networkStream;
            if (dataChanged != null)
                dataChanged(networkConnectionParameter);
            try
            {
                networkStream.BeginRead(client.Buffer, 0, client.Buffer.Length, ReadCallback, client);
            }
            catch (Exception)
            {
            }
        }
    }

    public void Disconnect()
    {
        try
        {
            foreach (Client clientLoop in tcpClientLastRequestList)
            {
                clientLoop.NetworkStream.Close(00);
            }
        }
        catch (Exception) { }
        server.Stop();

    }
}

internal class Client
{
    public TcpClient tcpClient { get; private set; }
    public byte[] Buffer { get; private set; }
    public long Ticks { get; set; }

    public Client(TcpClient tcpClient)
    {
        this.tcpClient = tcpClient;
        int bufferSize = tcpClient.ReceiveBufferSize;
        Buffer = new byte[bufferSize];
    }

    public NetworkStream NetworkStream { get => tcpClient.GetStream(); }
}
