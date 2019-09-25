using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RB_LANCHAT {
    public class LobbyManager : MonoBehaviour {
        public int PLAYER_MAX_COUNT;

        public static string HOST_NAME = "";
        public static string HOST_IP = "";

        //Editor Values
        [SerializeField] private Button m_CreateBtn, m_Joinbtn, m_RefreshNetBtn;
        [SerializeField] private TextMeshProUGUI m_NormalMsgText;
        [SerializeField] private TextMeshProUGUI m_ConveyMsgText;
        [SerializeField] private TMP_InputField m_RoomInputField;
        [SerializeField] private RectTransform m_HostListHolder;
        [SerializeField] private GameObject m_JoinBtnPrefab;
        [SerializeField] private GameObject m_JoinRoomPanel;
        [SerializeField] private GameObject m_LobbyBox;
        [SerializeField] private GameObject m_BlockBox;
        [SerializeField] private ChatBox m_ChatBox;
        [SerializeField] private NetworkDiscovery m_NetworkDiscovery = null;

        private const int GAME_PORT = 56798;

        private RoomBroadcastPacket mCacheRoomPacket = null;
        private MainServer mMainServer;
        private NetworkPlayer mMainStream;
        private List<string> mPlayerPackIPS;
        //private Queue<IMessagePacket> mPacketStack;
        private Queue<MethodPacket> mMethodStack;

        private bool mIsGameStarted = false;
        private float mPacketProcessInterval = 0.5f;
        private float mCurrentTimeProgress = 0;

        void Start () {
            //Debug.Log("Bluetooth name :: " + SystemInfo.deviceName + ", Model Name: " + SystemInfo.deviceModel + " :: onnanno::  " + SystemInfo.deviceType);
            mPlayerPackIPS = new List<string> ();
            //mPacketStack = new Queue<IMessagePacket> ();
            mMethodStack = new Queue<MethodPacket> ();

            NetworkDiscovery.onHostFound += OnRoomJoinReq;
            NetworkDiscovery.onRoomFound += OnBroadcastHostFound;

            AddBtnListeners ();
            m_RefreshNetBtn.onClick.Invoke ();
        }

        private void OnDestroy () {
            NetworkDiscovery.onHostFound -= OnRoomJoinReq;
            NetworkDiscovery.onRoomFound -= OnBroadcastHostFound;
        }

        private void Update () {
            //Before connection
            CheckBroadcastUpdate ();

            //After connection
            ServerUpdate ();
            ClientUpdate ();
        }

        #region  SETUPS

        private void ConveyMessage (string msg) {
            m_BlockBox.SetActive (true);
            m_ConveyMsgText.text = msg;
        }

        private void AddBtnListeners () {
            m_CreateBtn.onClick.AddListener (() => {
                m_NetworkDiscovery.StartBroadCastPing (m_RoomInputField.text);
                HOST_NAME = m_RoomInputField.text;

                m_JoinRoomPanel.gameObject.SetActive (true);
                m_Joinbtn.gameObject.SetActive (false);
                m_CreateBtn.gameObject.SetActive (false);
                m_NormalMsgText.text = "Waiting for other players to join!!\n";

                RoomBroadcastPacket roomPack = new RoomBroadcastPacket ();
                roomPack.ip = HOST_IP;
                roomPack.serverName = HOST_NAME;

                mCacheRoomPacket = roomPack;

                // JoinReqPacket _joinReqPack = new JoinReqPacket();
                // _joinReqPack.clientIP = HOST_IP;
                // _joinReqPack.clientName = HOST_NAME;
                // OnBroadcastJoinReq(_joinReqPack);
                //mNetworkDiscovery.StartLocalClient(); // dont need broadcast client for the host itself.
                StartMainServer ();
            });

            m_Joinbtn.onClick.AddListener (() => {
                m_JoinRoomPanel.gameObject.SetActive (true);
                m_Joinbtn.gameObject.SetActive (false);
                m_CreateBtn.gameObject.SetActive (false);

                m_NormalMsgText.text = "Please wait...Finding Game Servers to join. \n";
                m_NetworkDiscovery.StartLocalClient ();
            });

            m_RoomInputField.onValueChanged.AddListener (delegate {
                SaveMyName ();
            });

            m_RefreshNetBtn.onClick.AddListener (() => {
                if (m_NetworkDiscovery.IsConnectionValid ()) {
                    HOST_IP = m_NetworkDiscovery.GetIP ();
                    m_RefreshNetBtn.gameObject.SetActive (false);
                    SetupLobby ();
                } else {
                    ConveyMessage ("Please Make sure your internet is on & connect to same wifi/hotspot.");
                }
            });
        }
        private void SaveMyName () {
            HOST_NAME = string.IsNullOrEmpty (m_RoomInputField.text.Trim ()) ? SystemInfo.deviceName : m_RoomInputField.text.Trim ();
        }

        private void SetupLobby () {
            HOST_IP = m_NetworkDiscovery?.GetIP ();
            HOST_NAME = SystemInfo.deviceName;

            m_BlockBox.SetActive (false);
            m_LobbyBox.gameObject.SetActive (true);
            m_ChatBox.gameObject.SetActive (false);

            m_RoomInputField.text = SystemInfo.deviceName;

            m_CreateBtn.gameObject.SetActive (true);
            m_Joinbtn.gameObject.SetActive (true);
        }

        // private void StartChat () {
        //     //Debug.Log("starting chat..");
        //     m_ChatBox.gameObject.SetActive (true);
        //     m_LobbyBox.gameObject.SetActive (false);
        // }
        #endregion

        #region  BEFORE_CONNECTION i.e. network discovery stuff
        private void AddNewHost () {
            RoomBroadcastPacket serverInfo = mCacheRoomPacket;
            GameObject go = Instantiate (m_JoinBtnPrefab, m_HostListHolder);

            Button goBtn = go.GetComponent<Button> ();
            TextMeshProUGUI goText = go.GetComponentInChildren<TextMeshProUGUI> ();
            JoinReqPacket joinReqPack = new JoinReqPacket ();

            //Debug.Log("Adding new host");
            // if i am the host, i dont need to add myself, just showing button to explicitly say that we are online searching for other players/users.
            if (string.Equals (HOST_IP, serverInfo.ip)) {
                goBtn.interactable = false;
                goText.text = "(YOU) " + HOST_NAME;

                joinReqPack.clientIP = HOST_IP;
                joinReqPack.clientName = HOST_NAME;
                OnRoomJoinReq (joinReqPack); //to update the ui
                StartAsPlayer (HOST_IP, GAME_PORT); // If i am the server, i can also start as player as well.
            } else {
                goBtn.onClick.AddListener (() => {
                    Debug.Log ("sending join request to ::" + serverInfo.ip + " : " + serverInfo.port);
                    try {
                        //TODO check if we need local end point here
                        Socket sock = m_NetworkDiscovery.GetNetworkSocket ();
                        joinReqPack.clientIP = (sock.LocalEndPoint as IPEndPoint).Address.ToString ();
                        joinReqPack.clientName = HOST_NAME;

                        IPEndPoint remoteEndPoint = new IPEndPoint (IPAddress.Parse (serverInfo.ip), serverInfo.port);
                        sock.SendTo (NetworkUtil.GetBytes (JsonUtility.ToJson (joinReqPack)), (EndPoint) remoteEndPoint);
                        //TODO, improvement :: ACKNOWLEDGEMENT
                        StartAsPlayer (serverInfo.ip, GAME_PORT);
                        goBtn.interactable = false;
                        goText.text = "Joining...";
                    } catch (Exception _exc) {
                        Debug.Log ("EXC: " + _exc.Message);
                        goBtn.interactable = true;
                    }
                });
                goText.text = "JOIN : " + serverInfo.serverName;
            }
            m_HostListHolder.gameObject.SetActive (true);
        }

        private void CheckBroadcastUpdate () {
            if (mCacheRoomPacket != null) {
                AddNewHost ();
                mCacheRoomPacket = null;
            }

        }
        #endregion

        #region  AFTER_CONNECTION

        private void StartMainServer () {
            //Debug.Log("STARTING AS SERVER");
            NetworkPlayer.OnMessageReceivedByServerAction += OnMsgReceivedByServer;
            mMainServer = new MainServer (HOST_IP, GAME_PORT);
        }

        private void StartAsPlayer (string _sip, int _port) {
            //RUNNING FOR CLIENTS
            NetworkPlayer.OnMessageReceivedByClientAction += OnMsgReceivedByClient;
            mMainStream = new NetworkPlayer (_sip, _port);
        }

        #endregion

        #region  SERVER
        private void ServerUpdate () {
            //SERVER
            if (!mIsGameStarted && mMainServer != null && mMainServer.GetClientCount () == PLAYER_MAX_COUNT) {
                Debug.Log ("Sending start game to all");
                //mMainServer.RpcAll(new StartGamePacket());
                //MethodPacket mp = new MethodPacket("RPCStartGame", 209, "hello");
                //MethodPacket mp = new MethodPacket("RPCStartGame");
                //MethodPacket mp = new MethodPacket ("RPCStartGameWithMult", 512, "hello!232");
                //mMainServer.RpcAll (mp.GetObject ());

                mMainServer.RpcAll (new MethodPacket ("StartGame").GetObject ());
                mIsGameStarted = true;
            }
        }
        #endregion

        #region  Client

        private void ClientUpdate () {
            //PROCESS STACK
            mCurrentTimeProgress += Time.deltaTime;
            //Debug.Log ("method stack count:: " + mMethodStack.Count);
            if (mMethodStack.Count > 0) {
                if (mCurrentTimeProgress > mPacketProcessInterval) {
                    Debug.Log ("processing method:::" + mCurrentTimeProgress);
                    ProcessMethod ();
                    mCurrentTimeProgress = 0;
                }
            }
        }
        #endregion

        // private void ProcessStack () {
        //     IMessagePacket imp = mPacketStack.Dequeue ();
        //     Debug.Log ("Processing stack :: " + imp.msgType);
        //     switch ((MessageType) imp.msgType) {
        //         case MessageType.START_GAME:
        //             m_ChatBox.OnSendBtnClick (mMainStream);
        //             StartChat ();
        //             break;
        //         case MessageType.SIMPLE_MSG:
        //             SimpleMessagePacket sm = (SimpleMessagePacket) imp;
        //             m_ChatBox.AddToBox (sm.fromIP + " said : " + sm.msg);
        //             break;
        //         default:
        //             break;
        //     }
        // }

        private void ProcessMethod () {
            MethodPacket crntPack = mMethodStack.Dequeue ();
            Debug.Log ("method parameters:: " + crntPack.methodName + ", " + ", TOTAL PARAMS: " + crntPack.allParams.Length);
            Type t = this.GetType ();
            MethodInfo theMethodToCall = t.GetMethod (crntPack.methodName);
            if (theMethodToCall == null) {
                Debug.LogError ("No Such Method exists:: " + crntPack.methodName);
            } else {
                Debug.Log ("total number of args :: " + theMethodToCall.GetParameters ().Count ());
                Debug.Log ("incoming args count :: " + crntPack.allParams.Length);
                if (theMethodToCall.GetParameters ().Count () != crntPack.allParams.Length) {
                    Debug.LogError ("ARGUMENT COUNT MISMATCH ::");
                } else {
                    if (crntPack.allParams.Length > 0) {
                        theMethodToCall.Invoke (this, crntPack.allParams);
                    } else {
                        theMethodToCall.Invoke (this, null);
                    }
                }
            }
        }

        #region  AsyncCallbacks_SecondaryThread
        /// <summary>
        /// These functions are running on a different thread, not in the main thread, 
        /// so if any error happens, i wont get anything on console unless used with try/catch Debug.Log function
        /// </summary>
        private void OnMsgReceivedByClient (string _msg) {
            mIsGameStarted = true;
            byte[] receivedBytes = NetworkUtil.GetBytes (_msg);
            Debug.Log (_msg);
            Debug.Log ("received by mono " + Encoding.ASCII.GetString (receivedBytes));

            try {
                MemoryStream memStream = new MemoryStream ();
                BinaryFormatter binForm = new BinaryFormatter ();
                memStream.Write (receivedBytes, 0, receivedBytes.Length);
                memStream.Seek (0, SeekOrigin.Begin);
                MethodPacket obj = (MethodPacket) binForm.Deserialize (memStream);
                mMethodStack.Enqueue (obj);
                Debug.Log ("Method successfully enqued...");
            } catch (Exception _exc) {
                Debug.LogError ("ERROR in processing method packet: " + _exc.Message);
            }

            //====Right below, it was the initial prototype without using C# reflection, it was becoming difficult to maintain with packets,
            //====so genuinly felt the need to use a more robust way to implement rpc calls

            // IMessagePacket mp = JsonUtility.FromJson<IMessagePacket>(_msg);
            // Debug.Log("Client received :: " + mp);
            // switch ((MessageType)mp.msgType)
            // {
            //     case MessageType.START_GAME:
            //         isGameStarted = true;
            //         packetStack.Enqueue(mp);
            //         break;
            //     case MessageType.SIMPLE_MSG:
            //         SimpleMessagePacket smp = JsonUtility.FromJson<SimpleMessagePacket>(_msg);
            //         packetStack.Enqueue(smp);
            //         break;
            //     default:
            //         break;
            // }
        }

        private void OnMsgReceivedByServer (string _imp) {
            Debug.Log ("Server received :: " + _imp);
            //TODO:: here we can do all sorts of stuff like routing, security, p2p talks etc
            IMessagePacket imp = JsonUtility.FromJson<IMessagePacket> (_imp);

            //here we are routing only via msg type bcz it wont be a good idea to let the client "execute" command of server.
            //the idea here is that client can only request something, the processing of the request is upto server
            if (imp.msgType == MessageType.SIMPLE_MSG) {
                Debug.Log ("SIMPLE_MSG passing to all clients");
                mMainServer.RpcAll (new MethodPacket ("PassMessage", _imp).GetObject ());
            } else {
                mMainServer.RpcAll (_imp); // NOT A GOOD IDEA OFCOURSE, me just being lazy ?:?
            }

        }

        // Some example methods to be called by the server
        public void StartGame () {
            Debug.Log ("Starting chat...with " + PLAYER_MAX_COUNT + " users.");
            m_ChatBox.gameObject.SetActive (true);
            //m_LobbyBox.gameObject.SetActive (false);

            m_ChatBox.InitializeChat (mMainStream);
        }

        public void PassMessage (string _smpStr) {
            SimpleMessagePacket smp = JsonUtility.FromJson<SimpleMessagePacket> (_smpStr);
            m_ChatBox.AddToBox ("<size=20><color=#874B80>" + smp.fromName + " said : </size></color> <b>" + smp.msg + "</b>");
        }

        public void RPCStartGameWith (int data) {
            Debug.Log ("Starting game... with:: " + data);
        }

        public void RPCStartGameWithMult (int data, string name) {
            Debug.Log ("Starting game...MULTI:: " + data + ", " + name);
        }

        #region BroadcastCallbacks
        // this section is used only for broadcasting i.e. when players trying to discover each other...

        //ONLY FOR CLIENTS
        public void OnBroadcastHostFound (RoomBroadcastPacket _smp) {
            //Debug.Log("Caching server broadcast mP" + _smp);
            mCacheRoomPacket = _smp;
        }

        //ONLY FOR SERVER
        public void OnRoomJoinReq (JoinReqPacket _imp) {
            // TODO 
            // UI UPDATE
            // SEND ACK
            if (mPlayerPackIPS.Contains (_imp.clientIP) == false) {
                mPlayerPackIPS.Add (_imp.clientIP);
                if (PLAYER_MAX_COUNT > mPlayerPackIPS.Count) {
                    Debug.Log ("TOTAL JOINED::  " + mPlayerPackIPS.Count);
                } else {
                    Debug.Log ("Closing network discovery");
                    m_NetworkDiscovery.CloseServer ();
                }
            }
        }
        #endregion

        #endregion
    }
}