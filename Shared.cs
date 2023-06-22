using UnityEngine;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Linq;

public class Shared : MonoBehaviour
{
    protected static bool OutputDebugInfo = false; // Whether to output debug info
    protected static string ServerIpAddress = "127.0.0.1"; // The IP address of the server
    protected static int ServerPort = 7123; // An arbitrary number between 1024-65535 (see https://en.wikipedia.org/wiki/List_of_TCP_and_UDP_port_numbers)
    protected static int MaxClientCount = 2; // Maximum number of clients that can be connected to the server at any one time
    protected static float ReadFrequency = 30f; // How many times per second the client and server should read the network stream for new messages
    protected static float ClientTimeoutDuration = 8f; // How long before the server disconnects a client due to silence
    protected static bool EncryptMessages = false; // Whether to encrypt and decrypt sent messages (slows down connections)
    protected static string EncryptionKey = "KeyGoesHere"; // An application-level key used to encrypt messages

    protected readonly static System.Text.Encoding MessageEncoding = System.Text.Encoding.UTF8; // The text encoding of the sent messages
    protected static class MessageCodes { // Don't send these
        public const string Close = "CLOSE";
        public const string ServerFull = "SERVER_FULL";
        public const string Welcome = "WELCOME";
    }
    protected const int BufferSize = 8192; // The maximum number of bytes per ReadAsync; uses memory but more performant for long messages

    protected static void ClientLog(string Message) {
        if (OutputDebugInfo)
            Debug.Log($"<b>[Client]</b> {Message}");
    }
    protected static void ServerLog(string Message) {
        if (OutputDebugInfo)
            Debug.Log($"<b>[Server]</b> {Message}");
    }
    
    private Queue<System.Action> PendingActionsForMainThread = new();
    protected async Task RunInMainThread(System.Action Action) {
        lock (PendingActionsForMainThread) {
            PendingActionsForMainThread.Enqueue(Action);
        }
        do {
            await Task.Delay(10);
        } while (PendingActionsForMainThread.Contains(Action));
    }
    protected virtual void Update() {
        lock (PendingActionsForMainThread) {
            while (PendingActionsForMainThread.TryDequeue(out System.Action Action)) {
                Action();
            }
        }
    }

    protected class TcpConnection {
        public readonly IPEndPoint EndPoint;
        public readonly TcpClient TcpClient;
        public readonly NetworkStream NetworkStream;

        public TcpConnection(IPEndPoint EndPoint, TcpClient TcpClient, NetworkStream NetworkStream) {
            this.EndPoint = EndPoint;
            this.TcpClient = TcpClient;
            this.NetworkStream = NetworkStream;
        }
    }

    protected static async Task<bool> WaitForTask(Task Task, float Timeout = -1) {
        if (Timeout >= 0) {
            return await Task.Run(() => Task.Wait(SecondsToMilliseconds(Timeout)));
        }
        else {
            await Task.Run(() => Task.Wait());
            return true;
        }
    }
    protected static int SecondsToMilliseconds(float Seconds) {
        return (int)(Seconds * 1000);
    }
    protected static double GetUnixTimeStamp() {
        return System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
    }
    protected static byte[] CreatePacket(string Message) {
        // Get message as bytes
        byte[] Bytes = MessageEncoding.GetBytes(Message);
        // Insert message length bytes
        {
            List<byte> MessageLengthBytes = new();
            // Add the digits of the message length as bytes
            string NumberOfBytes = Bytes.Length.ToString();
            foreach (char NumberOfBytesDigit in NumberOfBytes) {
                MessageLengthBytes.Add(byte.Parse(NumberOfBytesDigit.ToString()));
            }
            // Add the end of message length byte
            MessageLengthBytes.Add(10);
            // Build the packet
            MessageLengthBytes.AddRange(Bytes);
            Bytes = MessageLengthBytes.ToArray();
        }
        return Bytes;
    }
    protected static async Task ListenForPackets(TcpConnection TcpConnection, System.Func<bool> CheckConnected, System.Action<TcpConnection, byte[]> OnReceived, System.Action<bool> OnRead) {
        try {
            // Initialise variables
            List<byte> CurrentBytes = new();
            byte[] Buffer = new byte[BufferSize];
            // Read messages while connected
            while (CheckConnected()) {
                // Read all the available data
                bool ReceivedData = false;
                while (TcpConnection.NetworkStream.DataAvailable) {
                    ReceivedData = true;
                    // Add the data to CurrentBytes
                    int BytesRead = await TcpConnection.NetworkStream.ReadAsync(Buffer, 0, Buffer.Length);
                    CurrentBytes.AddRange(Buffer.ToList().GetRange(0, BytesRead));
                }
                // Check for a complete message in the data
                if (CurrentBytes.Count > 0) {
                    for (int i = 0; i < CurrentBytes.Count; i++) {
                        // Check end of message length byte
                        if (CurrentBytes[i] == 10) {
                            // Get the message length
                            int MessageLength = int.Parse(string.Join("", CurrentBytes.GetRange(0, i)));
                            // Check if the message is complete
                            if (CurrentBytes.Count - (i + 1) >= MessageLength) {
                                // Take the message
                                byte[] Message = CurrentBytes.GetRange(i + 1, MessageLength).ToArray();
                                CurrentBytes.RemoveRange(0, (i + 1) + MessageLength);
                                // Handle the message
                                OnReceived(TcpConnection, Message);
                            }
                            // Break (the end message length byte has been reached)
                            break;
                        }
                    }
                }
                // Run if data was received this pass
                if (OnRead != null) OnRead(ReceivedData);
                // Wait until the next read
                await Task.Delay(SecondsToMilliseconds(1 / ReadFrequency));
            }
        }
        catch (System.ObjectDisposedException) {
        }
        catch (System.Exception Ex) {
            Debug.LogException(Ex);
        }
    }
}