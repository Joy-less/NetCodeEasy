using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Linq;

public class ClientBase : Shared
{
    public ConnectionStatus Connected {get; private set;} = ConnectionStatus.NotConnected;
    public enum ConnectionStatus {
        NotConnected,
        TryingToConnect,
        ConnectionPending,
        Connected,
    }

    private TcpConnection TcpClient;

    private const float ServerTimeoutDuration = 5f;

    async void OnApplicationQuit() {
        await StopClient();
    }

    protected async Task<bool> ConnectToServer(float Timeout = -1) {
        Connected = ConnectionStatus.TryingToConnect;
        TcpClient Client = null;
        try {
            // Start client
            Client = new TcpClient();
            Client.SendTimeout =
            Client.ReceiveTimeout = SecondsToMilliseconds(ServerTimeoutDuration);
            // Connect to the server
            bool Success = await WaitForTask(Client.ConnectAsync(ServerIpAddress, ServerPort), Timeout);
            if (Success) {
                // Output
                ClientLog($"Client started at {System.DateTime.Now.ToString("HH:mm:ss")}");
                // Create the connection
                TcpClient = new TcpConnection((IPEndPoint)Client.Client.LocalEndPoint, Client, Client.GetStream());
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
            if (Client != null) {
                Client.Close();
            }
            TcpClient = null;
            return false;
        }
    }
    protected async Task StopClient() {
        // Send close message
        await SendToServer(MessageCodes.Close);
        // Stop the client
        TcpClient.TcpClient.Close();
        TcpClient = null;
        // Mark as disconnected
        Connected = ConnectionStatus.NotConnected;
        // OnDisconnected();
    }
    protected async Task<bool> SendToServer(string Message) {
        if (TcpClient != null) {
            // Send bytes
            await TcpClient.NetworkStream.WriteAsync(CreatePacket(Message));
            return true;
        }
        return false;
    }
}
