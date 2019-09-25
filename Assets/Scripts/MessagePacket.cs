using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public enum BroadCastType {
    NONE = 0,
    SERVER_BROADCAST,
    JOIN_REQUEST_WITH_CLIENT,
}

[System.Serializable]
public class IBroadcastPacket {
    public int packType = (int) BroadCastType.NONE;
    protected virtual void SetPacketType (BroadCastType _pType) {
        packType = (int) _pType;
    }
}

[System.Serializable]
public class RoomBroadcastPacket : IBroadcastPacket {
    public string ip = "";
    public int port = 0;
    public string serverName = "";

    public RoomBroadcastPacket () {
        SetPacketType (BroadCastType.SERVER_BROADCAST);
    }
}

[System.Serializable]
public class JoinReqPacket : IBroadcastPacket {
    public string clientIP = "";
    public string clientName = "";
    public int port = 0;

    public JoinReqPacket () {
        SetPacketType (BroadCastType.JOIN_REQUEST_WITH_CLIENT);
    }
}

// ===============================================
public enum MessageType {
    NONE = 0,
    START_GAME,
    SIMPLE_MSG
}

[System.Serializable]
public class IMessagePacket {
    public string fromIP;
    public string fromName;
    public string toIP;
    public string toName;
    public MessageType msgType;
    protected virtual void SetMessageType (MessageType mType) {
        msgType = mType;
    }
}

// [System.Serializable]
// public class StartGamePacket : IMessagePacket {
//     public StartGamePacket () {
//         SetMessageType (MessageType.START_GAME);
//     }
// }

[System.Serializable]
public class SimpleMessagePacket : IMessagePacket {
    public string msg;
    public SimpleMessagePacket () {
        SetMessageType (MessageType.SIMPLE_MSG);
    }
}

//====

[System.Serializable]
public class MethodPacket {
    public string methodName;
    //public int methodParamsInt;
    public object[] allParams;

    public MethodPacket (string _mn, params object[] _args) {
        methodName = _mn;
        allParams = (_args.Length > 0) ? _args : new object[0];
        //methodParamsInt = 123;
        // UnityEngine.Debug.Log("all params legnth :: " + allParams.Length);
        // UnityEngine.Debug.Log("_args legnth :: " + _args.Length);
    }

    public byte[] GetObject () {
        BinaryFormatter bf = new BinaryFormatter ();
        using (var ms = new MemoryStream ()) {
            bf.Serialize (ms, this);
            return ms.ToArray ();
        }
    }
}