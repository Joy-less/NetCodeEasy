using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

public class Client : ClientBase
{
    async void Start() {
        bool Success = await ConnectToServer(5f);
        if (Success) {
            print("Connection established; waiting to be accepted or rejected");
        }
        else {
            print("Couldn't connect to server; timed out");
        }
    }
    protected override async void Update() {
        base.Update();

        if (Input.GetKeyDown(KeyCode.E)) {
            await SendToServer("Hi");
        }
    }

    protected override void OnClientConnected() {
        print("Client connected!");
    }
    protected override void OnClientDisconnected() {
        print("Client disconnected!");
    }
    protected override void OnReceivedFromServer(string Message) {
        print($"Message received from server: '{Message}'");
    }
}
