using UnityEngine;

namespace VoxelModeler.Runtime {
    [System.Serializable]
    public struct Voxel {
        public byte id;          // 0 = empty, >0 = material index
        public Color32 color;    // vertex color / tint

        public bool IsEmpty => id == 0;
        public static Voxel Empty => new Voxel { id = 0, color = new Color32(0,0,0,0) };
    }
}
