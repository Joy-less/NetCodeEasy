# Net Code Easy

Code to get you started making a TCP client and server in Unity!

The code is designed for use in games where players can connect to each other's servers.

## Layout

- `ClientBase.cs` and `ServerBase.cs` set up and process the TCP connections. You are recommended to leave them alone.
- `Client.cs` and `Server.cs` are where you should write your code. Some example code has been created for you.
- `Shared.cs` contains settings and configuration including the server IP address and port.

## Getting Started

ServerIpAddress should be set to the public (or private for LAN) IP address of the host.

You can connect using `StartServer()` and `ConnectToServer()`.

After connecting, you can send a message using `SendToServer()` and `SendToClient()` or `SendToAllClients()`. If the NoDelay parameter is set to false, it will wait up to 200ms before sending the data to increase efficiency.

You can get a list of connected clients using `GetClients()`.

You can disconnect using `StopServer()`, `DisconnectClient()` and `DisconnectFromServer()`.

The server will disconnect a client after 8 seconds of silence, so make sure to use `SendToServer()` regularly.

## Encryption

There is no SSL encryption (because you need a certificate) so this should **not** be used to send sensitive information such as passwords.

However, messages can be optionally encrypted using the key in `Shared.cs` before sending. This reduces performance.

## Serialisation

When sending data, it is recommended to create and serialise a class using Newtonsoft.Json.

#### Installing Newtonsoft.Json in Unity

1. Open Package Manager
2. Click "+" in top left
3. Click "add by git URL"
4. Enter "com.unity.nuget.newtonsoft-json"

Then you can serialise a class as a string like follows.
```C#
[System.Serializable]
class Test {
    public int Number;
}
string Json = Newtonsoft.Json.JsonConvert.SerializeObject(new Test() {
    Number = 27
});
```
