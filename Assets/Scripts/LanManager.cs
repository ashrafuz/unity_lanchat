using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class LanManager {
    public const int APPLICAION_PORT = 56789;
    public const int BUFF_SIZE = 1024;
    private Socket mServerSocket;

    private List<Socket> mClientSockets;
    private byte[] mServerBuffer;
    private byte[] mClientBuffer;

    private Socket mPrivateClient;

    public LanManager () {
        mClientSockets = new List<Socket> ();
        mServerBuffer = new byte[BUFF_SIZE];
        mClientBuffer = new byte[BUFF_SIZE];
    }

    public void Log (string _msg) {
        Debug.Log (_msg);
    }

    public Socket GetPrivateClientSocket () {
        return mPrivateClient;
    }

    public void StartClientLocal () {
        try {
            mPrivateClient = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint localEnd = new IPEndPoint (IPAddress.Any, 0);
            mPrivateClient.Connect (localEnd);
        } catch (System.Exception _exc) {
            Log ("ERR: creating client " + _exc.Message + " STACK: " + _exc.StackTrace);
        }
    }

    public void SendMessageToServer (Socket _from, string _msg = "") {
        if (_from == null || !_from.Connected) {
            Debug.Log ("client is not alive any more");
            return;
        }

        try {
            _from.Send (Encoding.ASCII.GetBytes (string.IsNullOrEmpty (_msg) ? "PING" : _msg));
            //AddReceiveCallbackTo(_from, mClientBuffer);
            GetPendingMessage (_from);
        } catch (Exception ex) {
            Log ("ERR: SENDING MSGG" + ex.Message);
        }
    }

    private void AddReceiveCallbackTo (Socket _sock, byte[] _buffer) {
        _sock.BeginReceive (_buffer, 0, _buffer.Length, SocketFlags.None, new AsyncCallback (ReceiveCallback), _sock);
    }

    public void GetPendingMessage (Socket _from) {
        try {
            byte[] buffData = new byte[BUFF_SIZE];
            int receivedSize = _from.Receive (buffData);

            byte[] receivedData = new byte[receivedSize];
            Array.Copy (buffData, receivedData, receivedSize);
            Log ("Received FROM:: " + receivedSize + " :: msg:: " + Encoding.ASCII.GetString (receivedData));
        } catch (System.Exception ex) {
            Debug.Log ("ERR: PENDING MSG :: " + ex.Message);
            throw;
        }
    }

    public void StartServer () {
        try {
            mServerSocket = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint localEnd = new IPEndPoint (IPAddress.Any, APPLICAION_PORT);
            mServerSocket.Bind (localEnd);

            mServerSocket.Listen (2); // 5 is backlog, how many requests can this server handle at a time
            mServerSocket.BeginAccept (OnServerAccet, null);
            Log ("Server is listening::: ");
        } catch (Exception _exc) {
            Log ("ERR: " + _exc.Message + " :: STACK " + _exc.StackTrace);
            throw;
        }
    }

    public void OnServerAccet (IAsyncResult _ar) {
        try {
            Socket currClientSocket = mServerSocket.EndAccept (_ar);
            if (!mClientSockets.Contains (currClientSocket)) {
                mClientSockets.Add (currClientSocket);
                Debug.Log ("adding new client");
            }
            IPEndPoint clientIP = (IPEndPoint) currClientSocket.RemoteEndPoint;
            Debug.Log ("Server accepted a client :: " + clientIP);

            // string dataToSend = "CONNECTED";
            // byte[] dataToSendBuff = Encoding.ASCII.GetBytes(dataToSend);
            // currClientSocket.Send(dataToSendBuff);

            mServerBuffer = new byte[BUFF_SIZE];
            currClientSocket.BeginReceive (mServerBuffer, 0, mServerBuffer.Length, SocketFlags.None, new AsyncCallback (ReceiveCallback), currClientSocket);
            mServerSocket.BeginAccept (OnServerAccet, null);
            Debug.Log ("Server is listening again");

        } catch (System.Exception ex) {
            Debug.Log ("ERR: " + ex.Message);
        }

    }

    private void ReceiveCallback (IAsyncResult _ar) {
        Socket clientSock = (Socket) _ar.AsyncState;
        int receivedLength = clientSock.EndReceive (_ar);

        byte[] receivedData = new byte[receivedLength];
        Array.Copy (mServerBuffer, receivedData, receivedLength);

        string msg = Encoding.ASCII.GetString (receivedData);
        IPEndPoint clientIP = (IPEndPoint) clientSock.RemoteEndPoint;
        Debug.Log ("Server/Client accept a message " + msg + " from a client :: " + clientIP);
        //Debug.Log("Server accept a message from a client :: ");

        string dataToSend = "GOT_DATA";
        byte[] dataToSendBuff = Encoding.ASCII.GetBytes (dataToSend);
        clientSock.Send (dataToSendBuff);

        mServerBuffer = new byte[BUFF_SIZE];
        clientSock.BeginReceive (mServerBuffer, 0, BUFF_SIZE, SocketFlags.None, new AsyncCallback (ReceiveCallback), clientSock);
    }

    public void BroadcastMsg (string _msg, Socket _skip = null) {
        for (int i = 0; i < mClientSockets.Count; i++) {
            Debug.Log ("broadcasting msg " + _msg + " to " + mClientSockets[i].RemoteEndPoint);
            mClientSockets[i].Send (Encoding.ASCII.GetBytes (string.IsNullOrEmpty (_msg) ? "PING" : _msg));

            // byte[] buffData = new byte[BUFF_SIZE];
            // int receivedSize = mClientSockets[i].Receive(buffData);

            // byte[] receivedData = new byte[receivedSize];
            // Array.Copy(buffData, receivedData, receivedSize);
            // Log("BROADCAST:: " + receivedSize + " :: msg:: " + Encoding.ASCII.GetString(receivedData));

            AddReceiveCallbackTo (mClientSockets[i], mServerBuffer);
        }
    }

    private List<string> LocalAddresses = new List<string> ();
    private List<string> LocalSubAdds = new List<string> ();
    private List<string> AddressFoundOnLan = new List<string> ();
    public bool IsSearching = false;

    public void ScanHost () {
        LocalAddresses.Clear ();
        IPGlobalProperties comProperties = IPGlobalProperties.GetIPGlobalProperties ();
        NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces ();

        if (adapters == null || adapters.Length < 1) {
            Log ("No network interfaces found.");
            return;
        }

        Log ("Number of interfaces :: " + adapters.Length);
        foreach (var item in adapters) {
            if (item.NetworkInterfaceType == NetworkInterfaceType.Loopback) {
                continue;
            }
            if (item.OperationalStatus == OperationalStatus.Up) {
                foreach (var ip in item.GetIPProperties ().UnicastAddresses) {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork) {
                        string address = ip.Address.ToString ();
                        string subAddress = address.Remove (address.LastIndexOf ('.'));
                        LocalAddresses.Add (address);
                        Log ("Local Address :: " + address + " :::: SUB: [" + subAddress + "]");

                        if (!LocalSubAdds.Contains (subAddress)) {
                            LocalSubAdds.Add (subAddress);
                        }
                    }
                }
            }
        }

        Log ("TOTAL valid Address found :: " + LocalAddresses.Count);
    }

    public IEnumerator SendPing () {
        int totalAttempts = 0;
        while (true) {
            totalAttempts++;
            if (totalAttempts > 10) {
                break;
            }
            if (GetPrivateClientSocket () != null && GetPrivateClientSocket ().Connected) {
                break;
            }

            Log ("ATTEMPTING CONNECTION :: " + totalAttempts);
            yield return new WaitForSeconds (0.2f);
        }

        AddressFoundOnLan.Clear ();
        IsSearching = true;
        foreach (var subAddress in LocalSubAdds) {
            IPEndPoint destinationEndPoint = new IPEndPoint (IPAddress.Parse (subAddress + ".255"), APPLICAION_PORT);
            byte[] str = Encoding.ASCII.GetBytes ("ping12345");
            try {
                GetPrivateClientSocket ().SendTo (str, destinationEndPoint);
            } catch (System.Exception _ex) {
                Log ("ERR:: " + _ex.Message + " :: STACK " + _ex);
            }

            Log ("Sending ping :: " + subAddress);
            yield return new WaitForSeconds (0.1f);
        }

        IsSearching = false;

        Log ("Send Ping ended.");
    } //SendPing

}