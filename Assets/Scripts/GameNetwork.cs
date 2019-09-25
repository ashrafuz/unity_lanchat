using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class MainServer {
    private TcpListener mServer;
    private List<NetworkPlayer> mAllClients;

    public static event Action<string> onServerConnected;

    public MainServer (string _myIP, int _port) {
        mAllClients = new List<NetworkPlayer> ();
        mServer = new TcpListener (IPAddress.Parse (_myIP), _port);
        //mServer = new TcpListener(IPAddress.Any, _port);
        mServer.Start ();
        mServer.BeginAcceptTcpClient (OnServerConnect, null);
        onServerConnected = null;
        //Debug.Log("Server running at:: " + _port);
    }

    public void OnServerConnect (IAsyncResult _ar) {
        try {
            TcpClient newClient = mServer.EndAcceptTcpClient (_ar);
            NetworkPlayer registeredClient = new NetworkPlayer (newClient, this);
            mAllClients.Add (registeredClient);
            string clientRemote = ((IPEndPoint) (newClient.Client.RemoteEndPoint)).Address.ToString ();
            string clientLocal = ((IPEndPoint) (newClient.Client.LocalEndPoint)).Address.ToString ();

            if (onServerConnected != null) {
                onServerConnected (clientRemote + "#" + clientLocal);
            }

            mServer.BeginAcceptTcpClient (OnServerConnect, null);
        } catch (Exception _exc) {
            Debug.Log ("ERR_CONN_SERVER:" + _exc.Message + ", " + _exc.StackTrace);
        }
    }

    public void RpcAll (string _msg) {
        //Debug.Log("Sending message to all :: " + _msg);
        for (int i = 0; i < mAllClients.Count; i++) {
            mAllClients[i].Send (_msg);
        }
    }

    public void RpcAll (byte[] _msg) {
        for (int i = 0; i < mAllClients.Count; i++) {
            mAllClients[i].Send (_msg);
        }
    }

    public void RpcAll (IMessagePacket _imp) {
        //_imp.fromIP = "HOST";
        //_imp.toIP = "ALL";
        string data = JsonUtility.ToJson (_imp);
        for (int i = 0; i < mAllClients.Count; i++) {
            mAllClients[i].Send (data);
        }
    }

    public int GetClientCount () {
        return mAllClients == null ? 0 : mAllClients.Count;
    }
}

public class NetworkPlayer {
    public static event Action<string> OnMessageReceivedByServerAction;
    public static event Action<string> OnMessageReceivedByClientAction;

    private TcpClient tcpClient;
    private byte[] readBuffer = new byte[1024];
    NetworkStream stream {
        get {
            return tcpClient.GetStream ();
        }
    }

    private MainServer mainServer;

    private bool IsServerSide {
        get {
            return (mainServer != null);
        }
    }

    public NetworkPlayer (string _serverIP, int _port) //For client side initialization, we have to give it all
    {
        try {
            Debug.Log ("Client Side Initialization");
            TcpClient client = new TcpClient ();
            InitClient (client);
            tcpClient.BeginConnect (_serverIP, _port, OnConnectionComplete, null);
        } catch (Exception _ex) {
            Debug.Log ("CONS ERROR: " + _ex.Message);
        }
    }

    public NetworkPlayer (TcpClient _client, MainServer _m) { // for server side initialization, we already have the connection, dont need anything else
        InitClient (_client);
        //tcpClient.NoDelay = true;
        mainServer = _m;
        StartReading ();
    }

    private void InitClient (TcpClient _cl) {
        //OnMessageReceivedByClientAction = null; //reset
        //OnMessageReceivedByServerAction = null; //reset
        tcpClient = _cl;
    }

    public void Close () {
        tcpClient.Close ();
    }

    private void StartReading () {
        stream.BeginRead (readBuffer, 0, readBuffer.Length, OnRead, null);
    }

    private void OnRead (IAsyncResult _ar) {
        int length = stream.EndRead (_ar);
        if (length <= 0) {
            //Disconneced, TODO DISCONNECT
            Debug.Log ("CLIENT DISCONNECTED");
            return;
        }

        string newMsg = Encoding.ASCII.GetString (readBuffer, 0, length);
        Debug.Log ("read :: " + length + " :: msg :: " + newMsg);
        if (IsServerSide && OnMessageReceivedByServerAction != null) {
            //IMessagePacket imp = JsonUtility.FromJson<IMessagePacket>(newMsg);
            Debug.Log ("Server player received new message:: " + newMsg);
            OnMessageReceivedByServerAction (newMsg);
        } else if (!IsServerSide && OnMessageReceivedByClientAction != null) {
            Debug.Log ("Client player received new message:: " + newMsg);
            //IMessagePacket imp = JsonUtility.FromJson<IMessagePacket>(newMsg);
            OnMessageReceivedByClientAction (newMsg);
        }
        StartReading ();
    }

    public void Send (string _msg) {
        byte[] buffer = System.Text.Encoding.UTF8.GetBytes (_msg);
        stream.Write (buffer, 0, buffer.Length);
    }

    public void Send (byte[] _obj) {
        stream.Write (_obj, 0, _obj.Length);
    }

    #region  ONLY_CLIENT
    private void OnConnectionComplete (IAsyncResult _ar) {
        //Debug.Log("Connected with server as client!");
        StartReading ();
    }
    #endregion
}