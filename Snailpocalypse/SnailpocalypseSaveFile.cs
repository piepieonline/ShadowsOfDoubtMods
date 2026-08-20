using System;
using System.Collections.Generic;
using UnityEngine;

namespace Snailpocalypse;

[Serializable]
public class SnailpocalypseSaveFile
{
    [Serializable]
    public class SnailSaveData
    {
        public SerializedVector3 pos;
        public SerializedQuaternion rot;
        public bool inAirVent;
        public int duct;

        [Serializable]
        public class SerializedVector3(Vector3 v)
        {
            public float x = v.x;
            public float y = v.y;
            public float z = v.z;

            public Vector3 ToVector3()
            {
                return new Vector3(x, y, z);
            }
        }
        
        [Serializable]
        public class SerializedQuaternion(Quaternion q)
        {
            public float x = q.x;
            public float y = q.y;
            public float z = q.z;
            public float w = q.w;
            
            public Quaternion ToQuaternion()
            {
                return new Quaternion(x, y, z, w);
            }
        }
    }
    
    public int minutesTillNextSnail { get; set; }
    public List<SnailSaveData> snails { get; set; }
}