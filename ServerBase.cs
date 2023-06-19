using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Linq;

public abstract class ServerBase : Shared
{
    public bool Ready {get; private set;}

    private TcpListener TcpServer;
    private readonly List<TcpConnection> TcpConnections = new();

    private const float ClientTimeoutDuration = 5f;

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
            var _ = Task.Run(() => AcceptClients());
            // Output
            ServerLog($"Server started at {System.DateTime.Now.ToString("HH:mm:ss")}");
            // Mark as ready
            Ready = true;
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
    }
    private async Task<bool> AcceptClients(int Timeout = -1) {
        // Await clients
        Task<TcpClient> TcpClientAwaiter = TcpServer.AcceptTcpClientAsync();
        bool Success = await WaitForTask(TcpClientAwaiter, Timeout);
        if (Success) {
            // Get the network client
            TcpClient Client = TcpClientAwaiter.Result;
            TcpConnection TcpConnection = new TcpConnection((IPEndPoint)Client.Client.RemoteEndPoint, Client, Client.GetStream());
            TcpConnections.Add(TcpConnection);
            // Welcome the client
            if (TcpConnections.Count <= MaxClientCount) {
                // Tell the client that they are welcome
                RunInMainThread(async () => await SendToClient(TcpConnection, MessageCodes.Welcome));
                // Output
                ServerLog("Client {" + TcpConnection.EndPoint + "} connected");
                //
                // RunInMainThread(() => StartCoroutine(ListenToClient(TcpConnection)));
                var _ = ListenToClient(TcpConnection);
            }
            // Reject the client
            else {
                // Tell the client that the server is full
                RunInMainThread(async () => await SendToClient(TcpConnection, MessageCodes.ServerFull));
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
        TcpConnection.TcpClient.Close();
        TcpConnection.NetworkStream.Close();
        TcpConnections.Remove(TcpConnection);
        // Output
        ServerLog("Client {" + TcpConnection.EndPoint + "} forcefully disconnected");
    }
    protected async Task<bool> SendToClient(TcpConnection TcpConnection, string Message) {
        if (TcpConnections.Contains(TcpConnection)) {
            // Send bytes
            await TcpConnection.NetworkStream.WriteAsync(CreatePacket(Message));
            return true;
        }
        return false;
    }
    protected async Task SendToAllClients(string Message) {
        foreach (TcpConnection TcpConnection in TcpConnections) {
            await SendToClient(TcpConnection, Message);
        }
    }
    private int GetMessageLength(IEnumerable<byte> Bytes) {
        System.Text.StringBuilder Digits = new();
        foreach (byte Byte in Bytes) {
            if (Byte >= 10) {
                throw new System.Exception("Invalid message length digit: " + Byte);
            }
            Digits.Append(Byte);
        }
        return int.Parse(Digits.ToString());
    }
    
    private static async Task<byte> ReadSingleByteAsync(NetworkStream Stream) {
        byte[] Buffer = new byte[1];
        await Stream.ReadAsync(Buffer, 0, 1);
        return Buffer[0];
    }
    protected async Task ListenToClient(TcpConnection TcpConnection) {
        // Output
        ServerLog("Starting listening for messages from client");
        // Read messages from the client
        bool BuildingMessageLength = true;
        int MessageLength = 0;
        List<byte> CurrentBytes = new();
        while (TcpConnections.Contains(TcpConnection)) {
            while (TcpConnection.NetworkStream.DataAvailable) {
                // Read message length
                if (BuildingMessageLength == true) {
                    while (TcpConnection.NetworkStream.DataAvailable) {
                        byte NextByte = await ReadSingleByteAsync(TcpConnection.NetworkStream);
                        if (NextByte == 10) {
                            // Got the message length
                            BuildingMessageLength = false;
                            MessageLength = int.Parse(string.Join("", CurrentBytes));
                            CurrentBytes.Clear();
                            break;
                        }
                        CurrentBytes.Add(NextByte);
                    }
                }
                // Read message
                else {
                    byte[] Buffer = new byte[BufferSize];
                    int BytesRead = await TcpConnection.NetworkStream.ReadAsync(Buffer, 0, Buffer.Length);
                    CurrentBytes.AddRange(Buffer.ToList().GetRange(0, BytesRead));
                    if (CurrentBytes.Count >= MessageLength) {
                        // Got the message
                        BuildingMessageLength = true;
                        MessageLength = 0;
                        ReceivedFromClient(TcpConnection, CurrentBytes.ToArray());
                        CurrentBytes.Clear();
                    }
                }
            }

            await Task.Delay(SecondsToMilliseconds(0.05f));
        }
        await DisconnectClient(TcpConnection);
    }
    protected void ReceivedFromClient(TcpConnection TcpConnection, byte[] Bytes) {
        // Get message as string
        string Message = MessageEncoding.GetString(Bytes);
        // Output
        ServerLog("Received message from {" + TcpConnection.EndPoint + "}: " + Message);
    }
}
