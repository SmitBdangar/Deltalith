using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deltalith.Runtime
{
    public static class VoxelMeshGenerator
    {
        // Cache the built-in cube mesh
        static Mesh s_cubeMesh;

        static Mesh GetCubeMesh()
        {
            if (s_cubeMesh == null)
            {
                // Try to get Unity's built-in cube mesh
                s_cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                if (s_cubeMesh == null)
                {
                    // Fallback: create a cube primitive and extract its mesh
                    GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    MeshFilter mf = tmp.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        // Copy the mesh so we own it
                        s_cubeMesh = UnityEngine.Object.Instantiate(mf.sharedMesh);
                    }
                    UnityEngine.Object.DestroyImmediate(tmp);
                }
            }
            return s_cubeMesh;
        }

        // Face directions (matching Unity cube normals)
        static readonly Vector3[] FaceNormals = new Vector3[]
        {
            new Vector3(1, 0, 0),   // Right (+X)
            new Vector3(-1, 0, 0),  // Left (-X)
            new Vector3(0, 1, 0),  // Up (+Y)
            new Vector3(0, -1, 0), // Down (-Y)
            new Vector3(0, 0, 1),  // Forward (+Z)
            new Vector3(0, 0, -1)  // Back (-Z)
        };

        // Neighbor offsets for each face direction
        static readonly Vector3Int[] FaceOffsets = new Vector3Int[]
        {
            new Vector3Int(1, 0, 0),   // Right
            new Vector3Int(-1, 0, 0),  // Left
            new Vector3Int(0, 1, 0),   // Up
            new Vector3Int(0, -1, 0),  // Down
            new Vector3Int(0, 0, 1),   // Forward
            new Vector3Int(0, 0, -1)   // Back
        };

        // Cache face data extracted from cube mesh
        static FaceData[] s_faceData;

        struct FaceData
        {
            public List<int> vertexIndices;  // Original vertex indices in cube mesh
            public List<int> triangleIndices; // Triangle indices for this face
            public Vector3 normal;
        }

        static void ExtractFaceDataFromCube(Mesh cubeMesh)
        {
            if (s_faceData != null) return;

            Vector3[] cubeVerts = cubeMesh.vertices;
            Vector3[] cubeNormals = cubeMesh.normals;
            Vector2[] cubeUVs = cubeMesh.uv;
            int[] cubeTris = cubeMesh.triangles;

            s_faceData = new FaceData[6];

            // Group triangles by their face normal
            for (int faceIdx = 0; faceIdx < 6; faceIdx++)
            {
                Vector3 targetNormal = FaceNormals[faceIdx];
                s_faceData[faceIdx] = new FaceData
                {
                    vertexIndices = new List<int>(),
                    triangleIndices = new List<int>(),
                    normal = targetNormal
                };

                // Find all triangles with this normal
                for (int tri = 0; tri < cubeTris.Length; tri += 3)
                {
                    int v0 = cubeTris[tri];
                    int v1 = cubeTris[tri + 1];
                    int v2 = cubeTris[tri + 2];

                    // Check if this triangle belongs to this face (by normal)
                    Vector3 n0 = cubeNormals[v0];
                    if (Vector3.Dot(n0, targetNormal) > 0.9f) // Threshold for matching
                    {
                        // Add vertices if not already present
                        if (!s_faceData[faceIdx].vertexIndices.Contains(v0))
                            s_faceData[faceIdx].vertexIndices.Add(v0);
                        if (!s_faceData[faceIdx].vertexIndices.Contains(v1))
                            s_faceData[faceIdx].vertexIndices.Add(v1);
                        if (!s_faceData[faceIdx].vertexIndices.Contains(v2))
                            s_faceData[faceIdx].vertexIndices.Add(v2);

                        // Add triangle indices (relative to face vertices)
                        s_faceData[faceIdx].triangleIndices.Add(v0);
                        s_faceData[faceIdx].triangleIndices.Add(v1);
                        s_faceData[faceIdx].triangleIndices.Add(v2);
                    }
                }
            }
        }

        public static Mesh GenerateMesh(VoxelChunk chunk)
        {
            chunk.EnsureArray();
            int size = VoxelChunk.ChunkSize;
            Voxel[] vox = chunk.voxels;

            // Get Unity's built-in cube mesh
            Mesh cubeMesh = GetCubeMesh();
            if (cubeMesh == null)
            {
                Debug.LogError("Deltalith: Could not get Unity's built-in cube mesh!");
                return new Mesh();
            }

            // Extract face data from cube mesh (cache it)
            ExtractFaceDataFromCube(cubeMesh);

            // Get cube mesh data
            Vector3[] cubeVerts = cubeMesh.vertices;
            Vector3[] cubeNormals = cubeMesh.normals;
            Vector2[] cubeUVs = cubeMesh.uv;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color32>();
            var submeshTris = new Dictionary<int, List<int>>();

            for (int dir = 0; dir < 6; dir++)
            {
                Vector3Int normal = DIRS[dir];
                int axisU, axisV, axisW;

                if (Mathf.Abs(normal.x) == 1)
                {
                    axisW = 0; axisU = 2; axisV = 1;
                }
                else if (Mathf.Abs(normal.y) == 1)
                {
                    axisW = 1; axisU = 0; axisV = 2;
                }
                else
                {
                    axisW = 2; axisU = 0; axisV = 1;
                }

                int sizeU = size, sizeV = size, sizeW = size;
                FaceMaskItem[] mask = new FaceMaskItem[sizeU * sizeV];

                for (int w = -1; w < sizeW; w++)
                {
                    int n = 0;
                    for (int v = 0; v < sizeV; v++)
                    {
                        for (int u = 0; u < sizeU; u++)
                        {
                            int[] a = new int[3];
                            int[] b = new int[3];

                            a[axisU] = u; a[axisV] = v; a[axisW] = w;
                            b[axisU] = u; b[axisV] = v; b[axisW] = w + 1;

                            Voxel va = GetVoxelSafe(vox, size, a[0], a[1], a[2]);
                            Voxel vb = GetVoxelSafe(vox, size, b[0], b[1], b[2]);

                            bool aFull = !va.IsEmpty;
                            bool bFull = !vb.IsEmpty;

                            if (aFull == bFull)
                            {
                                mask[n] = FaceMaskItem.Empty;
                            }
                            else
                            {
                                if (aFull)
                                {
                                    mask[n] = new FaceMaskItem
                                    {
                                        material = va.id,
                                        color = va.color,
                                        faceDir = dir,
                                        exists = true
                                    };
                                }
                                else
                                {
                                    mask[n] = new FaceMaskItem
                                    {
                                        material = vb.id,
                                        color = vb.color,
                                        faceDir = dir,
                                        exists = true
                                    };
                                }
                            }

                            n++;
                        }
                    }

                    int i = 0;
                    for (int v = 0; v < sizeV; v++)
                    {
                        for (int u = 0; u < sizeU;)
                        {
                            FaceMaskItem cur = mask[i];
                            if (!cur.exists)
                            {
                                u++;
                                i++;
                                continue;
                            }

                            int width = 1;
                            while (u + width < sizeU &&
                                   mask[i + width].exists &&
                                   mask[i + width].Equals(cur))
                            {
                                width++;
                            }

                            int height = 1;
                            bool done = false;

                            while (v + height < sizeV)
                            {
                                for (int k = 0; k < width; k++)
                                {
                                    if (!(mask[i + k + height * sizeU].exists &&
                                          mask[i + k + height * sizeU].Equals(cur)))
                                    {
                                        done = true;
                                        break;
                                    }
                                }

                                if (done) break;
                                height++;
                            }

                            Vector3[] quad = new Vector3[4];
                            int[] pos = new int[3];
                            pos[axisU] = u;
                            pos[axisV] = v;
                            pos[axisW] = w + 1;

                            for (int c = 0; c < 4; c++)
                            {
                                Vector3 baseCorner = FACE_CORNERS[cur.faceDir, c];
                                Vector3 world = Vector3.zero;

                                world[axisU] = u + baseCorner[axisU] * width;
                                world[axisV] = v + baseCorner[axisV] * height;
                                world[axisW] = pos[axisW] + (baseCorner[axisW] > 0.5f ? 0 : -1);

                                quad[c] = world;
                            }

                            int vertStart = vertices.Count;

                            // Add vertices for this face (in order of vertexIndices)
                            Dictionary<int, int> vertexRemap = new Dictionary<int, int>();
                            for (int i = 0; i < face.vertexIndices.Count; i++)
                            {
                                int origIdx = face.vertexIndices[i];
                                vertices.Add(cubeVerts[origIdx] + voxelPos);
                                normals.Add(cubeNormals[origIdx]);
                                uvs.Add(cubeUVs[origIdx]);
                                colors.Add(v.color);
                                vertexRemap[origIdx] = vertStart + i;
                            }

                            // Add triangles for this face (remap indices)
                            if (!submeshTris.TryGetValue(v.id, out var tri))
                            {
                                tri = new List<int>();
                                submeshTris[v.id] = tri;
                            }

                            for (int i = 0; i < face.triangleIndices.Count; i += 3)
                            {
                                int origV0 = face.triangleIndices[i];
                                int origV1 = face.triangleIndices[i + 1];
                                int origV2 = face.triangleIndices[i + 2];

                                // Remap to new vertex indices
                                int v0 = vertexRemap[origV0];
                                int v1 = vertexRemap[origV1];
                                int v2 = vertexRemap[origV2];

                                tri.Add(v0);
                                tri.Add(v1);
                                tri.Add(v2);
                            }
                        }
                    }
                }
            }

            // Early return if no geometry
            if (vertices.Count == 0)
            {
                Mesh emptyMesh = new Mesh();
                emptyMesh.name = "VoxelChunkMesh_Empty";
                return emptyMesh;
            }

            Mesh mesh = new Mesh();
            mesh.name = "VoxelChunkMesh";
            mesh.indexFormat = vertices.Count > 65535 ?
                UnityEngine.Rendering.IndexFormat.UInt32 :
                UnityEngine.Rendering.IndexFormat.UInt16;

            // Set all mesh data
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);

            // Set up submeshes
            var keys = new List<int>(submeshTris.Keys);
            keys.Sort();
            mesh.subMeshCount = keys.Count;

            for (int s = 0; s < keys.Count; s++)
            {
                int[] triangles = submeshTris[keys[s]].ToArray();
                if (triangles.Length > 0)
                {
                    mesh.SetTriangles(triangles, s);
                }
            }

            // Recalculate bounds and tangents (for proper rendering)
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            // Mark mesh as readable to ensure proper GPU upload
            mesh.UploadMeshData(false);

            return mesh;
        }

        static Voxel GetVoxelSafe(Voxel[] vox, int size, int x, int y, int z)
        {
            if (x < 0 || y < 0 || z < 0 ||
                x >= size || y >= size || z >= size)
                return Voxel.Empty;

            return vox[x + size * (y + size * z)];
        }
    }
}
