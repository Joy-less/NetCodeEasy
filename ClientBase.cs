using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
        await StopClient();
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
                var _ = ListenForPackets(TcpClient, () => TcpClient != null, (Connection, Bytes) => ReceivedFromServer(Bytes), null);
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
    protected async Task StopClient() {
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
        RunInMainThread(OnClientDisconnected);
    }
    protected async Task<bool> SendToServer(string Message, float Timeout = -1) {
        if (TcpClient != null) {
            // Encrypt message
            string EncryptedMessage = EncryptMessages ? Encryption.SimpleEncryptWithPassword(Message, EncryptionKey) : Message;
            // Send bytes
            await WaitForTask(TcpClient.NetworkStream.WriteAsync(CreatePacket(EncryptedMessage)).AsTask(), Timeout);
            return true;
        }
        return false;
    }
    protected async void ReceivedFromServer(byte[] Bytes) {
        // Get message as string
        string EncryptedMessage = MessageEncoding.GetString(Bytes);
        // Decrypt message
        string Message = EncryptMessages ? Encryption.SimpleDecryptWithPassword(EncryptedMessage, EncryptionKey) : EncryptedMessage;
        // Check message
        if (Message == MessageCodes.Close || Message == MessageCodes.ServerFull) {
            await StopClient();
            return;
        }
        else if (Message == MessageCodes.Welcome) {
            // Mark as connected
            Connected = ConnectionStatus.Connected;
            // Output
            ClientLog("Client connected");
            // Run custom function
            RunInMainThread(OnClientConnected);
            return;
        }
        // Output
        ClientLog("Received message from {" + TcpClient.EndPoint + "}: " + Message);
        // Run custom function
        RunInMainThread(() => OnReceivedFromServer(Message));
    }

    protected abstract void OnReceivedFromServer(string Message);
    protected abstract void OnClientConnected();
    protected abstract void OnClientDisconnected();
}
