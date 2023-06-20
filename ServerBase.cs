using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

public abstract class ServerBase : Shared
{
    public bool Ready {get; private set;}

    private TcpListener TcpServer;
    private readonly List<TcpConnection> TcpConnections = new();
    private readonly List<TcpConnection> BlockedTcpConnections = new();

    async void OnApplicationQuit() {
        await StopServer();
    }
    
    protected async Task StartServer() {
        try {
            // Start TCP server
            TcpServer = new TcpListener(IPAddress.Any, ServerPort);
            TcpServer.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
            TcpServer.Start();
            // Accept clients
            _ = Task.Run(() => AcceptClients());
            // Output
            ServerLog($"Server started at {System.DateTime.Now.ToString("HH:mm:ss")}");
            // Mark as ready
            Ready = true;
            // Run custom function
            _ = RunInMainThread(OnServerStarted);
        }
        catch (System.Exception E) {
            ServerLog("Server could not start: " + E.Message);
            await StopServer();
        }
    }
    protected async Task StopServer() {
        // Mark as not ready
        Ready = false;
        // Close client connections
        while (TcpConnections.Count > 0) {
            await DisconnectClient(TcpConnections[0]);
        }
		// Close server connection
		if (TcpServer != null) {
			TcpServer.Stop();
			TcpServer = null;
		}
        // Output
        ServerLog("Stopped server");
        // Run custom function
        _ = RunInMainThread(OnServerStopped);
    }
    private async Task<bool> AcceptClients(float Timeout = -1) {
        // Await clients
        Task<TcpClient> TcpClientAwaiter = TcpServer.AcceptTcpClientAsync();
        bool Success = await WaitForTask(TcpClientAwaiter, Timeout);
        if (Success) {
            // Get the network client
            TcpClient Client = TcpClientAwaiter.Result;
            TcpConnection TcpConnection = new TcpConnection((IPEndPoint)Client.Client.RemoteEndPoint, Client, Client.GetStream());
            // Welcome the client
            if (TcpConnections.Count + 1 <= MaxClientCount) {
                TcpConnections.Add(TcpConnection);
                // Tell the client that they are welcome
                await RunInMainThread(async () => await SendToClient(TcpConnection, MessageCodes.Welcome));
                // Output
                ServerLog("Client {" + TcpConnection.EndPoint + "} connected");
                // Listen for packets from the client
                double TimeLastReceivedData = GetUnixTimeStamp();
                _ = ListenForPackets(TcpConnection, () => TcpConnections.Contains(TcpConnection), ReceivedFromClient, async ReceivedData => {
                    if (ReceivedData) {
                        TimeLastReceivedData = GetUnixTimeStamp();
                    }
                    else if (GetUnixTimeStamp() - TimeLastReceivedData >= ClientTimeoutDuration) {
                        ServerLog("Client timed out");
                        await DisconnectClient(TcpConnection);
                    }
                });
                // Run custom function
                _ = RunInMainThread(() => OnClientConnected(TcpConnection));
            }
            // Reject the client
            else {
                BlockedTcpConnections.Add(TcpConnection);
                // Tell the client that the server is full
                await RunInMainThread(async () => await SendToClient(TcpConnection, MessageCodes.ServerFull));
                // Output
                ServerLog("Client {" + TcpConnection.EndPoint + "} tried to join but the server was full");
                // Disconnect them
                await DisconnectClient(TcpConnection);
            }
        }
        return Success;
    }
    protected async Task DisconnectClient(TcpConnection TcpConnection) {
        // Send close message
        await SendToClient(TcpConnection, Shared.MessageCodes.Close);
        // Disconnect the client
        TcpConnections.Remove(TcpConnection);
        BlockedTcpConnections.Remove(TcpConnection);
        TcpConnection.TcpClient.Close();
        TcpConnection.NetworkStream.Close();
        // Output
        ServerLog("Client {" + TcpConnection.EndPoint + "} disconnected");
        // Run custom function
        _ = RunInMainThread(() => OnClientDisconnected(TcpConnection));
    }
    protected async Task<bool> SendToClient(TcpConnection TcpConnection, string Message, bool NoDelay = false, float Timeout = -1) {
        if (TcpConnections.Contains(TcpConnection) || BlockedTcpConnections.Contains(TcpConnection)) {
            // Encrypt message
            Message = EncryptMessages ? Encryption.SimpleEncryptWithPassword(Message, EncryptionKey) : Message;
            // Send bytes
            TcpConnection.TcpClient.NoDelay = NoDelay;
            await WaitForTask(TcpConnection.NetworkStream.WriteAsync(CreatePacket(Message)).AsTask(), Timeout);
            return true;
        }
        return false;
    }
    protected async Task SendToAllClients(string Message, bool NoDelay = false, float Timeout = -1) {
        HashSet<Task> Tasks = new();
        foreach (TcpConnection TcpConnection in TcpConnections) {
            Tasks.Add(SendToClient(TcpConnection, Message, NoDelay, Timeout));
        }
        foreach (Task Task in Tasks) {
            await Task;
        }
    }
    protected void ReceivedFromClient(TcpConnection TcpConnection, byte[] Bytes) {
        // Get message as string
        string Message = MessageEncoding.GetString(Bytes);
        // Decrypt message
        Message = EncryptMessages ? Encryption.SimpleDecryptWithPassword(Message, EncryptionKey) : Message;
        // Output
        ServerLog("Received message from {" + TcpConnection.EndPoint + "}: " + Message);
        // Run custom function
        _ = RunInMainThread(() => OnReceivedFromClient(TcpConnection, Message));
    }
    protected TcpConnection[] GetClients() {
        return TcpConnections.ToArray();
    }

    protected abstract void OnReceivedFromClient(TcpConnection Client, string Message);
    protected abstract void OnClientConnected(TcpConnection Client);
    protected abstract void OnClientDisconnected(TcpConnection Client);
    protected abstract void OnServerStarted();
    protected abstract void OnServerStopped();
}
