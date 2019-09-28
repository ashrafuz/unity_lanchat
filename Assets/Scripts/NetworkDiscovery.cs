using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace RB_LANCHAT {
    public class NetworkUtil {
        public static byte[] GetBytes (string msg) {
            return Encoding.UTF8.GetBytes (msg);
        }

        public static string GetRandomRoomName () {
            return "ROOM_ID_" + UnityEngine.Random.Range (100, 100000);
        }
    }

    public class NetworkDiscovery : MonoBehaviour {
        public static event Action<JoinReqPacket> onHostFound;
        public static event Action<RoomBroadcastPacket> onRoomFound;

        private BroadcastMGR mBroadcastManager = null;
        private byte[] pingData = null;

        private RoomBroadcastPacket smp = null;

        private float pingDelayInterval = 1;
        private float currentDelayPassed = 0;

        private string myIP = "";

        private bool isServerOn = false;

        private void Awake () {
            mBroadcastManager = new BroadcastMGR ();
            mBroadcastManager.ScanHost ();
        }

        public string GetIP () {
            if (string.IsNullOrEmpty (myIP.ToString ())) {
                myIP = mBroadcastManager.GetLocalIP ();
            }
            return myIP;
        }

        public void StartBroadCastPing (string _name) {
            if (string.IsNullOrEmpty (mBroadcastManager.GetLocalIP ())) {
                isServerOn = false;
            } else {
                myIP = mBroadcastManager.GetLocalIP ();
                mBroadcastManager.StartUDPServer (_name, Server_OnJoinReqReceived);

                smp = new RoomBroadcastPacket ();
                smp.serverName = _name;
                smp.ip = (mBroadcastManager.GetServer ().LocalEndPoint as IPEndPoint).Address.ToString ();
                smp.port = (mBroadcastManager.GetServer ().LocalEndPoint as IPEndPoint).Port;
                pingData = NetworkUtil.GetBytes (JsonUtility.ToJson (smp));

                isServerOn = true;
            }
        }

        public bool IsConnectionValid () {
            return !string.IsNullOrEmpty (mBroadcastManager.GetLocalIP ());
        }

        public Socket GetNetworkSocket () {
            return mBroadcastManager.GetClient ();
        }

        public void StartLocalClient () {
            mBroadcastManager.StartClientLocal (Client_OnReceiveFromServer);
        }

        public bool IsActive () {
            //is server or client, anything active
            return isServerOn || (mBroadcastManager.GetClient () != null);
        }

        public void CloseServer () {
            isServerOn = false;
            mBroadcastManager?.Close ();
            this.gameObject.SetActive (false);
        }

        void Update () {
            if (isServerOn == false) {
                return;
            }

            //server is on
            currentDelayPassed += Time.deltaTime;
            if (currentDelayPassed > pingDelayInterval) {
                mBroadcastManager.SendBroadcast (pingData);
                currentDelayPassed = 0;
            }
        }

        private void Server_OnJoinReqReceived (IBroadcastPacket _imp) {
            switch ((BroadCastType) _imp.packType) {
                case BroadCastType.JOIN_REQUEST_WITH_CLIENT:
                    {
                        Debug.Log ("successfully received joined request ");
                        JoinReqPacket _joinReq = (JoinReqPacket) _imp;
                        if (onHostFound != null) onHostFound (_joinReq);
                    }
                    break;
                default:
                    break;
            }
        }

        private void Client_OnReceiveFromServer (IBroadcastPacket _imp) {
            switch ((BroadCastType) _imp.packType) {
                case BroadCastType.SERVER_BROADCAST:
                    {
                        RoomBroadcastPacket _smp = (RoomBroadcastPacket) _imp;
                        Debug.Log ("SERVER FOUND ON MONO: " + _smp.ip + " , " + _smp.serverName);
                        if (onRoomFound == null) {
                            Debug.Log ("no server found function added");
                        } else {
                            onRoomFound (_smp);
                        }

                    }
                    break;
                default:
                    break;
            }
        }
    }

    public class BroadcastMGR {
        public const int BROADCAST_PORT = 56782;
        public const int BUFF_SIZE = 1024;
        public bool LogFlag = true;

        /* PRIVATE VARIABLES */
        private Socket mUDPServer;
        private byte[] mClientBuffer;
        private byte[] mServerBuffer;
        private Socket mPrivateClient;
        private List<RoomBroadcastPacket> mAllHosts;
        private Action<IBroadcastPacket> sendToClientCallback;
        private Action<IBroadcastPacket> sendToServerCallback;

        private List<string> localAddressArray;
        private List<string> localSubs;
        private IPEndPoint broadcastEndPoint = null;

        public BroadcastMGR () {
            mAllHosts = new List<RoomBroadcastPacket> ();
            localSubs = new List<string> ();
            localAddressArray = new List<string> ();
            broadcastEndPoint = new IPEndPoint (IPAddress.Broadcast, BroadcastMGR.BROADCAST_PORT);

            mServerBuffer = new byte[BUFF_SIZE];
            mClientBuffer = new byte[BUFF_SIZE];
        }

        public void SetLogFlag (bool _flag) {
            LogFlag = _flag;
        }

        public string GetLocalIP () {
            if (localAddressArray == null || localAddressArray.Count == 0) {
                ScanHost ();
            }

            if (localAddressArray.Count == 0) {
                return "";
            }
            return localAddressArray[0];
        }

        public void Log (string _msg) {
            //return;
            Debug.Log (_msg);
        }

        private void InitClient () {
            mPrivateClient = new Socket (AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint localEnd = new IPEndPoint (IPAddress.Any, BROADCAST_PORT);
            mPrivateClient.Bind (localEnd);
        }

        public Socket GetClient () {
            if (mPrivateClient != null) {
                return mPrivateClient;
            }
            return null;
        }

        public void StartClientLocal (Action<IBroadcastPacket> _onHostFound) {
            try {
                sendToClientCallback = _onHostFound;
                InitClient ();
                mPrivateClient.BeginReceive (mClientBuffer, 0, BUFF_SIZE, SocketFlags.None, new AsyncCallback (OnClientReceive), mPrivateClient);
            } catch (System.Exception _exc) {
                Log ("ERR: creating client:: " + _exc.Message + " STACK: " + _exc.StackTrace);
            }
        }

        private bool IsHostAdded (RoomBroadcastPacket _mp) {
            foreach (var item in mAllHosts) {
                if (item.ip == _mp.ip && string.Equals (item.serverName, _mp.serverName)) {
                    return true;
                }
            }
            return false;
        }

        private void OnClientReceive (IAsyncResult _ar) {
            try {
                Socket clientSock = (Socket) _ar.AsyncState;
                int receivedLength = clientSock.EndReceive (_ar);

                byte[] receivedData = new byte[receivedLength];
                Array.Copy (mClientBuffer, receivedData, receivedLength);
                string jsString = Encoding.UTF8.GetString (receivedData);
                //Debug.Log(jsString);

                IBroadcastPacket imp = JsonUtility.FromJson<IBroadcastPacket> (jsString);

                switch ((BroadCastType) imp.packType) {
                    case BroadCastType.SERVER_BROADCAST:
                        {
                            RoomBroadcastPacket servBroadPack = JsonUtility.FromJson<RoomBroadcastPacket> (jsString);
                            if (IsHostAdded (servBroadPack) == false) {
                                Log ("FOUND A NEW SERVER:: " + jsString);
                                //we found a new game server
                                mAllHosts.Add (servBroadPack);
                                if (sendToClientCallback != null) {
                                    sendToClientCallback (servBroadPack);
                                }

                                // IPEndPoint iep = new IPEndPoint(IPAddress.Parse(mPacket.ip), mPacket.port);
                                // byte[] data = Encoding.ASCII.GetBytes("JOIN_REQ_IM");
                                // clientSock.SendTo(data, (EndPoint)iep);
                            }
                        }
                        break;
                }

                //KEEP THE CONNECTION ALIVE FOR OTHER INCOMING MESSAGES
                mPrivateClient.BeginReceive (mClientBuffer, 0, BUFF_SIZE, SocketFlags.None, new AsyncCallback (OnClientReceive), mPrivateClient);
            } catch (Exception ex) {
                Log ("ERR: Client REceive:: " + ex.Message);
            }
        }

        private void CloseSocket (Socket _sock) {
            try {
                _sock?.Close ();
            } catch (Exception _exc) {
                Log ("ERR: CLOSING SOCKET:: " + _exc.Message);
                _sock = null;
            }
        }

        public void Close () {
            CloseSocket (GetClient ());
            CloseSocket (GetServer ());
        }

        public Socket GetServer () {
            return mUDPServer;
        }

        public void StartUDPServer (string _name, Action<IBroadcastPacket> _onJoinReq) {
            try {
                mUDPServer = new Socket (AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                mUDPServer.Bind (new IPEndPoint (IPAddress.Parse (localAddressArray[0]), 0)); // ANY PORT THATS AVAILABLE
                mUDPServer.SetSocketOption (SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1); //enable broadcast

                mUDPServer.BeginReceive (mServerBuffer, 0, BUFF_SIZE, SocketFlags.None, new AsyncCallback (OnServerAcceptMsg), mUDPServer);

                Log ("Server started:: LOCAL IP OF SERVER:: " + mUDPServer.LocalEndPoint);
                sendToServerCallback = _onJoinReq;
            } catch (Exception _exc) {
                Log ("ERR: ONSERVER: " + _exc.Message + " :: STACK " + _exc.StackTrace);
            }
        }

        private void OnServerAcceptMsg (IAsyncResult _ar) {
            Socket servSock = (Socket) _ar.AsyncState;
            int receivedLength = servSock.EndReceive (_ar);

            byte[] receivedData = new byte[receivedLength];
            Array.Copy (mServerBuffer, receivedData, receivedLength);

            string jsString = Encoding.ASCII.GetString (receivedData);
            IBroadcastPacket mPack = JsonUtility.FromJson<IBroadcastPacket> (jsString);

            switch ((BroadCastType) mPack.packType) {
                case BroadCastType.JOIN_REQUEST_WITH_CLIENT:
                    Log ("NEW JOIN REQUEST RECEIVED BY SERVER::" + jsString);
                    JoinReqPacket joinnPack = JsonUtility.FromJson<JoinReqPacket> (jsString);
                    if (sendToServerCallback != null) {
                        Debug.Log ("inside send to server callback :: ");
                        sendToServerCallback (joinnPack);
                    }
                    break;
                default:
                    //Debug.Log("UNRECOGNIZED PACKET!!");
                    break;
            }

            //be ready for other connections
            mUDPServer.BeginReceive (mServerBuffer, 0, BUFF_SIZE, SocketFlags.None, new AsyncCallback (OnServerAcceptMsg), servSock);
        }

        public void SendBroadcast (byte[] data) {
            try {
                //Log("broadcasting :: data");
                GetServer ().SendTo (data, broadcastEndPoint);
            } catch (Exception _ex) {
                Log ("SendBroadcast ERR:: " + _ex.Message);
                return;
            }
        }

        public void ScanHost () {
            localAddressArray.Clear ();
            // Debug.Log("Application platform :: " + Application.platform);
            if (Application.platform == RuntimePlatform.Android) {
                var host = Dns.GetHostEntry (Dns.GetHostName ());
                foreach (var ip in host.AddressList) {
                    if (ip.AddressFamily == AddressFamily.InterNetwork) {
                        string address = ip.ToString ();
                        string subAddress = address.Remove (address.LastIndexOf ('.'));
                        localAddressArray.Add (address);
                        Log ("DNS Local Address:: " + address + " :::: SUB: [" + subAddress + "]");

                        if (!localSubs.Contains (subAddress)) {
                            localSubs.Add (subAddress);
                        }
                    }
                }
            }

            if (localAddressArray.Count == 0) //TRY AGAIN WITH IPGLOBALPROPERTIES
            {
                IPGlobalProperties comProperties = IPGlobalProperties.GetIPGlobalProperties ();
                NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces ();

                if (adapters == null || adapters.Length < 1) {
                    Log ("No network interfaces found.");
                    return;
                }

                Log ("Number of interfaces :: " + adapters.Length);
                foreach (var item in adapters) {
                    if (item.NetworkInterfaceType == NetworkInterfaceType.Loopback) {
                        continue; //LOCALHOST, we dont care about localhost
                    }
                    if (item.OperationalStatus == OperationalStatus.Up) {
                        foreach (var ip in item.GetIPProperties ().UnicastAddresses) {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork) {
                                string address = ip.Address.ToString ();
                                string subAddress = address.Remove (address.LastIndexOf ('.'));
                                localAddressArray.Add (address);
                                Log ("Local Address :: " + address + " :::: SUB: [" + subAddress + "]");

                                if (!localSubs.Contains (subAddress)) {
                                    localSubs.Add (subAddress);
                                }
                            }
                            //Log(ip.Address.ToString());
                        }
                    }
                }
            }

            Log ("valid Address found :: " + localAddressArray.Count);
        }

    }

} //namespace