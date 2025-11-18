using UnityEngine;

namespace Deltalith.Runtime
{
    public class VoxelChunk : MonoBehaviour
    {
        public const int ChunkSize = 32;

        public Voxel[] voxels = new Voxel[ChunkSize * ChunkSize * ChunkSize];

        [System.NonSerialized] MeshFilter meshFilter;
        [System.NonSerialized] MeshRenderer meshRenderer;
        [System.NonSerialized] MeshCollider meshCollider;

        void Awake() => InitializeComponents();

        public void InitializeComponents()
        {
            if (!meshFilter) meshFilter = GetComponent<MeshFilter>();
            if (!meshRenderer) meshRenderer = GetComponent<MeshRenderer>();
            if (!meshCollider) meshCollider = GetComponent<MeshCollider>();
        }

        public void EnsureArray()
        {
            if (voxels == null || voxels.Length != ChunkSize * ChunkSize * ChunkSize)
                voxels = new Voxel[ChunkSize * ChunkSize * ChunkSize];
        }

        public void SetVoxel(int x, int y, int z, Voxel v)
        {
            if (x < 0 || y < 0 || z < 0 ||
                x >= ChunkSize || y >= ChunkSize || z >= ChunkSize)
                return;

            int index = x + ChunkSize * (y + ChunkSize * z);
            voxels[index] = v;
        }

        public Voxel GetVoxel(int x, int y, int z)
        {
            if (x < 0 || y < 0 || z < 0 ||
                x >= ChunkSize || y >= ChunkSize || z >= ChunkSize)
                return Voxel.Empty;

            int index = x + ChunkSize * (y + ChunkSize * z);
            return voxels[index];
        }
        
        public void ApplyMesh(Mesh mesh)
        {
            InitializeComponents();
            meshFilter.sharedMesh = mesh;
            meshCollider.sharedMesh = mesh;
        }
    }
}
