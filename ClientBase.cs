using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

public abstract class ClientBase : Shared
{
    public ConnectionStatus Connected {get; private set;} = ConnectionStatus.NotConnected;
    public enum ConnectionStatus {
        NotConnected,
        TryingToConnect,
        ConnectionPending,
        Connected,
    }

    private TcpConnection TcpClient;

    async void OnApplicationQuit() {
        await DisconnectFromServer();
    }

    protected async Task<bool> ConnectToServer(float Timeout = -1) {
        Connected = ConnectionStatus.TryingToConnect;
        TcpClient Client = null;
        try {
            // Start client
            Client = new TcpClient();
            // Connect to the server
            bool Success = await WaitForTask(Client.ConnectAsync(ServerIpAddress, ServerPort), Timeout);
            if (Success) {
                // Output
                ClientLog($"Client started at {System.DateTime.Now.ToString("HH:mm:ss")}");
                // Create the connection
                TcpClient = new TcpConnection((IPEndPoint)Client.Client.LocalEndPoint, Client, Client.GetStream());
                // Listen for packets from the server
                _ = ListenForPackets(TcpClient, () => TcpClient != null, (Connection, Bytes) => ReceivedFromServer(Bytes), null);
                // Mark connection as pending
                Connected = ConnectionStatus.ConnectionPending;
            }
            else {
                // Output
                ClientLog($"Client could not connect: Server timed out");
            }
            return Success;
        }
        catch (System.Exception E) {
            ClientLog("Client could not start: " + E.Message);
            Connected = ConnectionStatus.NotConnected;
            if (Client != null) Client.Close();
            TcpClient = null;
            return false;
        }
    }
    protected async Task DisconnectFromServer() {
        // Send close message
        await SendToServer(MessageCodes.Close);
        // Stop the client
        if (TcpClient != null) TcpClient.TcpClient.Close();
        TcpClient = null;
        // Mark as disconnected
        Connected = ConnectionStatus.NotConnected;
        // Output
        ClientLog("Client disconnected");
        // Run custom function
        _ = RunInMainThread(OnClientDisconnected);
    }
    protected async Task<bool> SendToServer(string Message, bool NoDelay = false, float Timeout = -1) {
        if (Connected == ConnectionStatus.Connected) {
            // Encrypt message
            Message = EncryptMessages ? Encryption.SimpleEncryptWithPassword(Message, EncryptionKey) : Message;
            // Send bytes
            TcpClient.TcpClient.NoDelay = NoDelay;
            await WaitForTask(TcpClient.NetworkStream.WriteAsync(CreatePacket(Message)).AsTask(), Timeout);
            return true;
        }
        return false;
    }
    protected async void ReceivedFromServer(byte[] Bytes) {
        // Get message as string
        string Message = MessageEncoding.GetString(Bytes);
        // Decrypt message
        Message = EncryptMessages ? Encryption.SimpleDecryptWithPassword(Message, EncryptionKey) : Message;
        // Check message
        if (Message == MessageCodes.Close || Message == MessageCodes.ServerFull) {
            await DisconnectFromServer();
            return;
        }
        else if (Message == MessageCodes.Welcome) {
            // Mark as connected
            Connected = ConnectionStatus.Connected;
            // Output
            ClientLog("Client connected");
            // Run custom function
            _ = RunInMainThread(OnClientConnected);
            return;
        }
        // Output
        ClientLog("Received message from {" + TcpClient.EndPoint + "}: " + Message);
        // Run custom function
        _ = RunInMainThread(() => OnReceivedFromServer(Message));
    }

    protected abstract void OnReceivedFromServer(string Message);
    protected abstract void OnClientConnected();
    protected abstract void OnClientDisconnected();
}
