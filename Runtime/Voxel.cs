using UnityEngine;

namespace Deltalith.Runtime
{
    [System.Serializable]
    public struct Voxel
    {
        public byte id;       
        public Color32 color;  

        public bool IsEmpty => id == 0;

        public static Voxel Empty => new Voxel
        {
            id = 0,
            color = new Color32(0, 0, 0, 0)
        };
    }
}
