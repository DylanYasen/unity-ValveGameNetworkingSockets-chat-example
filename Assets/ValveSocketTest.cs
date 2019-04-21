using AOT;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Valve.Sockets;

public class ValveSocketTest : MonoBehaviour
{
    private StatusCallback status;
    private NetworkingSockets networkClient;
    private uint listenSocket;
    private bool isServer;

    private uint connection;

    public static ValveSocketTest Instance;

    private List<string> messages = new List<string>();

    void Start()
    {
        Valve.Sockets.Library.Initialize();

        Instance = this;

        NetworkingUtils utils = new NetworkingUtils();
        utils.SetDebugCallback(DebugType.Everything, DebugMessageCallback);
    }

    void OnApplicationQuit()
    {
        Valve.Sockets.Library.Deinitialize();
    }

    void StartServer()
    {
        isServer = true;
        networkClient = new NetworkingSockets();
        Address address = new Address();
        address.SetAddress("::0", 7777);
        listenSocket = networkClient.CreateListenSocket(address);

        status = OnServerStatusUpdate;
    }

    [MonoPInvokeCallback(typeof(DebugCallback))]
    static void DebugMessageCallback(DebugType type, string message)
    {
        Debug.Log("Debug - Type: " + type + ", Message: " + message);
    }

    [MonoPInvokeCallback(typeof(StatusCallback))]
    static void OnServerStatusUpdate(StatusInfo info, System.IntPtr context)
    {
        switch (info.connectionInfo.state)
        {
            case ConnectionState.None:
                break;

            case ConnectionState.Connecting:
                Instance.networkClient.AcceptConnection(info.connection);
                break;

            case ConnectionState.Connected:
                Debug.Log("Client connected - ID: " + info.connection + ", IP: " + info.connectionInfo.address.GetIP());
                Instance.connection = info.connection;
                break;

            case ConnectionState.ClosedByPeer:
                Instance.networkClient.CloseConnection(info.connection);
                Debug.Log("Client disconnected - ID: " + info.connection + ", IP: " + info.connectionInfo.address.GetIP());
                break;
        }
    }

    void StartClient()
    {
        networkClient = new NetworkingSockets();
        Address address = new Address();
        address.SetAddress("::1", 7777);

        connection = networkClient.Connect(address);

        status = OnClientStatusUpdate;
    }

    [MonoPInvokeCallback(typeof(StatusCallback))]
    static void OnClientStatusUpdate(StatusInfo info, System.IntPtr context)
    {
        switch (info.connectionInfo.state)
        {
            case ConnectionState.None:
                break;

            case ConnectionState.Connected:
                Debug.Log("Client connected to server - ID: " + Instance.connection);
                break;

            case ConnectionState.ClosedByPeer:
                Instance.networkClient.CloseConnection(Instance.connection);
                Debug.Log("Client disconnected from server");
                break;

            case ConnectionState.ProblemDetectedLocally:
                Instance.networkClient.CloseConnection(Instance.connection);
                Debug.Log("Client unable to connect");
                break;
        }
    }

    const int maxMessages = 20;
    NetworkingMessage[] netMessages = new NetworkingMessage[maxMessages];

    byte[] messageDataBuffer = new byte[256];

    void Update()
    {
        if (networkClient != null)
        {
            networkClient.DispatchCallback(status);

            int netMessagesCount = isServer ? networkClient.ReceiveMessagesOnListenSocket(listenSocket, netMessages, maxMessages) : networkClient.ReceiveMessagesOnConnection(connection, netMessages, maxMessages);
            if (netMessagesCount > 0)
            {
                for (int i = 0; i < netMessagesCount; i++)
                {
                    ref NetworkingMessage netMessage = ref netMessages[i];

                    Debug.Log("Message received from server - Channel ID: " + netMessage.channel + ", Data length: " + netMessage.length);

                    netMessage.CopyTo(messageDataBuffer);
                    netMessage.Destroy();

                    string result = Encoding.ASCII.GetString(messageDataBuffer);
                    messages.Add(result);
                }
            }
        }
    }

    private string inputString;
    private void OnGUI()
    {
        if (GUI.Button(new Rect(10, 10, 100, 40), "Server"))
        {
            StartServer();
        }
        else if (GUI.Button(new Rect(10, 60, 100, 40), "Client"))
        {
            StartClient();
        }

        for (int i = 0; i < messages.Count; i++)
        {
            GUI.TextArea(new Rect(200, 10 + 20 * i, 300, 20), messages[i]);
        }

        inputString = GUI.TextField(new Rect(200, 200, 400, 50), inputString);
        if (GUI.Button(new Rect(200, 280, 100, 50), "send"))
        {
            SendChatMessage(inputString);
            inputString = "";
        }
    }

    void SendChatMessage(string message)
    {
        if (networkClient != null)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(message);
            networkClient.SendMessageToConnection(connection, bytes);

            messages.Add(message);
        }
    }
}
