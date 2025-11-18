// Assets/Runtime/VoxelMeshGenerator.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelModeler.Runtime {
    // Greedy mesher with simple per-voxel material (id) grouping => submeshes per material id.
    public static class VoxelMeshGenerator {
        // Directions
        static readonly Vector3Int[] DIRS = new Vector3Int[] {
            new Vector3Int(1,0,0), // +X
            new Vector3Int(-1,0,0), // -X
            new Vector3Int(0,1,0), // +Y
            new Vector3Int(0,-1,0), // -Y
            new Vector3Int(0,0,1), // +Z
            new Vector3Int(0,0,-1) // -Z
        };

        // For each face direction define quad corner offsets (lower-left, lower-right, upper-right, upper-left)
        static readonly Vector3[,] FACE_CORNERS = new Vector3[6,4] {
            // +X (right)
            { new Vector3(1,0,0), new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(1,1,0) },
            // -X (left)
            { new Vector3(0,0,1), new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(0,1,1) },
            // +Y (top)
            { new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,1,0), new Vector3(0,1,0) },
            // -Y (bottom)
            { new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1), new Vector3(0,0,1) },
            // +Z (front)
            { new Vector3(0,0,1), new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1) },
            // -Z (back)
            { new Vector3(1,0,0), new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,1,0) }
        };

        // Main API: generate mesh for a chunk
        public static Mesh GenerateMesh(VoxelChunk chunk) {
            chunk.EnsureArray();
            int size = VoxelChunk.ChunkSize;
            Voxel[] vox = chunk.voxels;

            // Collect per-material vertex/triangle lists
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color32>();

            var submeshTris = new Dictionary<int, List<int>>(); // material id -> triangle indices

            // We'll perform simple greedy meshing per axis following classic algorithm.
            // Implementation notes: to keep code reasonably short we use object mask with small structs.
            for (int dir = 0; dir < 6; dir++) {
                Vector3Int normal = DIRS[dir];
                int axisU, axisV, axisW;
                // choose axes (w is along normal)
                if (Mathf.Abs(normal.x) == 1) { axisW = 0; axisU = 2; axisV = 1; } // X normal -> U=Z,V=Y
                else if (Mathf.Abs(normal.y) == 1) { axisW = 1; axisU = 0; axisV = 2; } // Y normal -> U=X,V=Z
                else { axisW = 2; axisU = 0; axisV = 1; } // Z normal -> U=X,V=Y

                int sizeU = size, sizeV = size, sizeW = size;

                // mask for one slice (U*V)
                FaceMaskItem[] mask = new FaceMaskItem[sizeU * sizeV];

                for (int w = -1; w < sizeW; w++) {
                    // build mask for slice w
                    int n = 0;
                    for (int v = 0; v < sizeV; v++) {
                        for (int u = 0; u < sizeU; u++) {
                            // map (u,v,w) into (x,y,z) for voxel A and B
                            int[] a = new int[3], b = new int[3];
                            a[axisU] = u; a[axisV] = v; a[axisW] = w;
                            b[axisU] = u; b[axisV] = v; b[axisW] = w + 1;

                            Voxel va = GetVoxelSafe(vox, size, a[0], a[1], a[2]);
                            Voxel vb = GetVoxelSafe(vox, size, b[0], b[1], b[2]);

                            bool aFull = !va.IsEmpty;
                            bool bFull = !vb.IsEmpty;

                            if (aFull == bFull) {
                                mask[n] = FaceMaskItem.Empty;
                            } else {
                                // face pointing towards the empty side; record material/color and which voxel to sample
                                if (aFull && !bFull) {
                                    // face belongs to A, normal points + direction of (w->w+1)
                                    mask[n] = new FaceMaskItem { material = va.id, color = va.color, faceDir = dir, exists = true };
                                } else {
                                    // face belongs to B, normal points - direction
                                    // flip direction for proper normal
                                    mask[n] = new FaceMaskItem { material = vb.id, color = vb.color, faceDir = dir, exists = true };
                                }
                            }
                            n++;
                        }
                    }

                    // greedy merge on mask
                    int i = 0;
                    for (int v = 0; v < sizeV; v++) {
                        for (int u = 0; u < sizeU;) {
                            FaceMaskItem cur = mask[i];
                            if (!cur.exists) { u++; i++; continue; }
                            // determine width
                            int width = 1;
                            while (u + width < sizeU && mask[i + width].exists && mask[i + width].Equals(cur)) width++;
                            // determine height
                            int height = 1;
                            bool done = false;
                            while (v + height < sizeV) {
                                for (int k = 0; k < width; k++) {
                                    if (!(mask[i + k + height * sizeU].exists && mask[i + k + height * sizeU].Equals(cur))) { done = true; break; }
                                }
                                if (done) break;
                                height++;
                            }

                            // compute quad corners in chunk local space
                            // base point p : (u, v, w)
                            Vector3 p = Vector3.zero;
                            int[] pos = new int[3];
                            pos[axisU] = u;
                            pos[axisV] = v;
                            pos[axisW] = w + (cur.faceDir == 0 || cur.faceDir == 2 || cur.faceDir == 4 ? 1 : 0); // push quad to correct side for + normals (heuristic)
                            p = new Vector3(pos[0], pos[1], pos[2]);

                            // du and dv vectors
                            Vector3 du = Vector3.zero, dv = Vector3.zero;
                            du[axisU] = width;
                            dv[axisV] = height;

                            // depending on face orientation we need specific corner ordering to maintain consistent winding
                            Vector3[] quad = new Vector3[4];
                            // For each faceDir we use FACE_CORNERS template scaled by width/height and offset p
                            Vector3 off = p;
                            // compute corners using FACE_CORNERS but scaled
                            for (int c = 0; c < 4; c++) {
                                Vector3 baseCorner = FACE_CORNERS[cur.faceDir, c];
                                // scale baseCorner around axes by width/height
                                // baseCorner components are 0 or 1: transform: x' = baseCorner.x * width in axisU etc.
                                Vector3 world = Vector3.zero;
                                world[axisU] = u + baseCorner[axisU] * width;
                                world[axisV] = v + baseCorner[axisV] * height;
                                // world[axisW] depends on whether baseCorner has 0/1 -> add w (if face at w+1 then +1)
                                world[axisW] = w + (baseCorner[axisW]);
                                quad[c] = world;
                            }

                            // add vertices and triangles into lists
                            int vertStart = vertices.Count;
                            Vector3 normalVec = GetNormalVector(cur.faceDir);
                            // order triangles so that normals point outward
                            // choose winding such that triangle order matches normal direction
                            // We will add in order 0,1,2 and 0,2,3 if normal is pointing positive along axisW; otherwise reverse.
                            for (int q = 0; q < 4; q++) {
                                vertices.Add(quad[q]);
                                normals.Add(normalVec);
                                // basic UV mapping scaled
                                uvs.Add(new Vector2(q == 0 || q == 3 ? 0 : 1, q < 2 ? 0 : 1));
                                colors.Add(cur.color);
                            }

                            // ensure list exists for material
                            int mat = Math.Max(0, cur.material); // 0 is allowed but we treat 0 as a material index (user can map material 0 to default)
                            if (!submeshTris.TryGetValue(mat, out var triList)) {
                                triList = new List<int>();
                                submeshTris[mat] = triList;
                            }

                            if (IsNormalPositive(cur.faceDir)) {
                                triList.Add(vertStart + 0); triList.Add(vertStart + 1); triList.Add(vertStart + 2);
                                triList.Add(vertStart + 0); triList.Add(vertStart + 2); triList.Add(vertStart + 3);
                            } else {
                                triList.Add(vertStart + 0); triList.Add(vertStart + 2); triList.Add(vertStart + 1);
                                triList.Add(vertStart + 0); triList.Add(vertStart + 3); triList.Add(vertStart + 2);
                            }

                            // zero out mask region
                            for (int hh = 0; hh < height; hh++) {
                                for (int ww = 0; ww < width; ww++) {
                                    mask[i + ww + hh * sizeU] = FaceMaskItem.Empty;
                                }
                            }

                            u += width;
                            i += width;
                        }
                    }
                }
            }

            // Build mesh
            Mesh mesh = new Mesh();
            mesh.indexFormat = (vertices.Count > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);

            // Build submeshes in deterministic order (sort keys)
            var keys = new List<int>(submeshTris.Keys);
            keys.Sort();
            mesh.subMeshCount = keys.Count;
            for (int s = 0; s < keys.Count; s++) {
                mesh.SetTriangles(submeshTris[keys[s]].ToArray(), s);
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        static Voxel GetVoxelSafe(Voxel[] vox, int size, int x, int y, int z) {
            if (x < 0 || y < 0 || z < 0 || x >= size || y >= size || z >= size) return Voxel.Empty;
            return vox[(y * size + z) * size + x];
        }

        struct FaceMaskItem {
            public int material;
            public Color32 color;
            public int faceDir;
            public bool exists;
            public static FaceMaskItem Empty => new FaceMaskItem { exists = false, material = 0, color = new Color32(0,0,0,0), faceDir = 0 };
            public bool Equals(FaceMaskItem other) {
                return exists == other.exists && material == other.material && faceDir == other.faceDir && color.Equals(other.color);
            }
        }

        static Vector3 GetNormalVector(int faceDir) {
            switch(faceDir) {
                case 0: return Vector3.right;
                case 1: return Vector3.left;
                case 2: return Vector3.up;
                case 3: return Vector3.down;
                case 4: return Vector3.forward;
                default: return Vector3.back;
            }
        }

        static bool IsNormalPositive(int faceDir) {
            // faceDirs 0(+X),2(+Y),4(+Z) are positive
            return faceDir == 0 || faceDir == 2 || faceDir == 4;
        }
    }
}
