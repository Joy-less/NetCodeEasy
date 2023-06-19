using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

public class Client : ClientBase
{
    async void Start() {
        await ConnectToServer(5f);
    }
    protected override async void Update() {
        base.Update();

        if (Input.GetKeyDown(KeyCode.A)) {
            await SendToServer("Hi");
        }
    }
}
