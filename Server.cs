using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Server : ServerBase
{
    async void Start() {
        await StartServer();
    }
}
