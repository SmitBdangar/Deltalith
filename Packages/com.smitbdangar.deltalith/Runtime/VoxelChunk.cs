using UnityEngine;
using System;

namespace VoxelModeler.Runtime {
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class VoxelChunk : MonoBehaviour {
        public const int ChunkSize = 32;
        public Voxel[] voxels;

        [NonSerialized] public MeshFilter meshFilter;
        [NonSerialized] public MeshRenderer meshRenderer;
        [NonSerialized] public MeshCollider meshCollider;

        void Awake() {
            InitializeComponents();
            EnsureArray();
        }

        void Reset() {
            InitializeComponents();
            EnsureArray();
        }

        void InitializeComponents() {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshCollider = GetComponent<MeshCollider>();
            
            if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
            if (meshCollider == null) meshCollider = gameObject.AddComponent<MeshCollider>();
        }

        public void EnsureArray() {
            int len = ChunkSize * ChunkSize * ChunkSize;
            if (voxels == null || voxels.Length != len) {
                voxels = new Voxel[len];
                for (int i = 0; i < len; i++) {
                    voxels[i] = Voxel.Empty;
                }
            }
        }

        public int Index(int x, int y, int z) {
            return (y * ChunkSize + z) * ChunkSize + x;
        }

        public Voxel GetVoxel(int x, int y, int z) {
            if (x < 0 || y < 0 || z < 0 || x >= ChunkSize || y >= ChunkSize || z >= ChunkSize) {
                return Voxel.Empty;
            }
            return voxels[Index(x, y, z)];
        }

        public void SetVoxel(int x, int y, int z, Voxel v) {
            if (x < 0 || y < 0 || z < 0 || x >= ChunkSize || y >= ChunkSize || z >= ChunkSize) {
                return;
            }
            voxels[Index(x, y, z)] = v;
        }

        public void ApplyMesh(Mesh mesh) {
            InitializeComponents();
            
            meshFilter.sharedMesh = mesh;
            meshCollider.sharedMesh = mesh;
        }
    }
}
