using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Server : ServerBase
{
    async void Start() {
        await StartServer();
    }
    protected override async void Update() {
        base.Update();

        if (Input.GetKeyDown(KeyCode.X)) {
            await StopServer();
        }
    }

    protected override void OnServerStarted() {
        print("Server started!");
    }
    protected override void OnServerStopped() {
        print("Server stopped!");
    }
    protected override void OnClientConnected(TcpConnection Client) {
        print($"Client {Client.EndPoint} connected!");
    }
    protected override void OnClientDisconnected(TcpConnection Client) {
        print($"Client {Client.EndPoint} disconnected!");
    }
    protected override void OnReceivedFromClient(TcpConnection Client, string Message) {
        print($"Message received from {Client.EndPoint}: '{Message}'");
    }
}

public class SerializableVector2
{
    public float X;
    public float Y;

    public SerializableVector2(float X, float Y) {
        this.X = X;
        this.Y = Y;
    }
    public static implicit operator SerializableVector2(Vector2 Vector2) {
        return new SerializableVector2(Vector2.x, Vector2.y);
    }
    public Vector2 ToUnityVector() {
        return new Vector2(X, Y);
    }
}
public class SerializableVector3
{
    public float X;
    public float Y;
    public float Z;

    public SerializableVector3(float X, float Y, float Z) {
        this.X = X;
        this.Y = Y;
        this.Z = Z;
    }
    public static implicit operator SerializableVector3(Vector3 Vector3) {
        return new SerializableVector3(Vector3.x, Vector3.y, Vector3.z);
    }
    public Vector3 ToUnityVector() {
        return new Vector3(X, Y, Z);
    }
}
