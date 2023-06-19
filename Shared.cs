using UnityEngine;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

public class Shared : MonoBehaviour
{
    protected static bool OutputDebugInfo = true; // Whether to output debug info
    protected static string ServerIpAddress = "127.0.0.1"; // The IP address of the server
    protected static int ServerPort = 7123; // An arbitrary number between 1024-65535 (see https://en.wikipedia.org/wiki/List_of_TCP_and_UDP_port_numbers)
    // protected static int UdpClientPort = 7124; // The UDP port number of the client (to allow one device to run a UDP client and server at the same time)
    protected static int MaxClientCount = 2; // Maximum number of clients that can be connected to the server at any one time

    protected readonly static System.Text.Encoding MessageEncoding = System.Text.Encoding.UTF8; // The text encoding of the sent messages
    protected const string EncryptionKey = "KeyGoesHere"; // An application-level key used to encrypt messages
    protected const int BufferSize = 8192; // Don't change this unless you need to

    protected static void ClientLog(string Message) {
        if (OutputDebugInfo)
            Debug.Log($"<b>[Client]</b> {Message}");
    }
    protected static void ServerLog(string Message) {
        if (OutputDebugInfo)
            Debug.Log($"<b>[Server]</b> {Message}");
    }
    
    private Queue<System.Action> PendingActionsForMainThread = new();
    protected void RunInMainThread(System.Action Action) {
        PendingActionsForMainThread.Enqueue(Action);
    }
    protected virtual void Update() {
        while (PendingActionsForMainThread.TryDequeue(out System.Action Action)) {
            Action();
        }
    }
    
    protected static class MessageCodes {
        public const string Close = "Close";
        public const string ServerFull = "SERVER_FULL";
        public const string Welcome = "WELCOME";
    }

    protected class TcpConnection {
        public readonly IPEndPoint EndPoint;
        public readonly TcpClient TcpClient;
        public readonly NetworkStream NetworkStream;
        // public readonly byte[] Buffer = new byte[BufferSize];

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
    /*protected static int GetLengthFromPacket(byte[] Packet) {
        
    }*/
}