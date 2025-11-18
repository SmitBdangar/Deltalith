using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deltalith.Runtime
{
    public static class VoxelMeshGenerator
    {
        static readonly Vector3Int[] DIRS = new Vector3Int[]
        {
            new Vector3Int(1,0,0),   // +X
            new Vector3Int(-1,0,0),  // -X
            new Vector3Int(0,1,0),   // +Y
            new Vector3Int(0,-1,0),  // -Y
            new Vector3Int(0,0,1),   // +Z
            new Vector3Int(0,0,-1)   // -Z
        };

        static readonly Vector3[,] FACE_CORNERS = new Vector3[6, 4]
        {
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

        public static Mesh GenerateMesh(VoxelChunk chunk)
        {
            chunk.EnsureArray();
            int size = VoxelChunk.ChunkSize;
            Voxel[] vox = chunk.voxels;

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
                            Vector3 normalVec = GetNormalVector(cur.faceDir);

                            for (int q = 0; q < 4; q++)
                            {
                                vertices.Add(quad[q]);
                                normals.Add(normalVec);
                                uvs.Add(new Vector2(q == 0 || q == 3 ? 0 : 1, q < 2 ? 0 : 1));
                                colors.Add(cur.color);
                            }

                            int mat = Math.Max(0, cur.material);
                            if (!submeshTris.TryGetValue(mat, out var triList))
                            {
                                triList = new List<int>();
                                submeshTris[mat] = triList;
                            }

                            if (IsNormalPositive(cur.faceDir))
                            {
                                triList.Add(vertStart + 0);
                                triList.Add(vertStart + 1);
                                triList.Add(vertStart + 2);
                                triList.Add(vertStart + 0);
                                triList.Add(vertStart + 2);
                                triList.Add(vertStart + 3);
                            }
                            else
                            {
                                triList.Add(vertStart + 0);
                                triList.Add(vertStart + 2);
                                triList.Add(vertStart + 1);
                                triList.Add(vertStart + 0);
                                triList.Add(vertStart + 3);
                                triList.Add(vertStart + 2);
                            }

                            for (int hh = 0; hh < height; hh++)
                                for (int ww = 0; ww < width; ww++)
                                    mask[i + ww + hh * sizeU] = FaceMaskItem.Empty;

                            u += width;
                            i += width;
                        }
                    }
                }
            }

            Mesh mesh = new Mesh();
            mesh.indexFormat = (vertices.Count > 65535)
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);

            var keys = new List<int>(submeshTris.Keys);
            keys.Sort();

            mesh.subMeshCount = keys.Count;
            for (int s = 0; s < keys.Count; s++)
                mesh.SetTriangles(submeshTris[keys[s]].ToArray(), s);

            mesh.RecalculateBounds();
            return mesh;
        }

        static Voxel GetVoxelSafe(Voxel[] vox, int size, int x, int y, int z)
        {
            if (x < 0 || y < 0 || z < 0 ||
                x >= size || y >= size || z >= size)
                return Voxel.Empty;

            return vox[(y * size + z) * size + x];
        }

        struct FaceMaskItem
        {
            public int material;
            public Color32 color;
            public int faceDir;
            public bool exists;

            public static FaceMaskItem Empty => new FaceMaskItem
            {
                exists = false,
                material = 0,
                color = new Color32(0, 0, 0, 0),
                faceDir = 0
            };

            public bool Equals(FaceMaskItem other)
            {
                return exists == other.exists &&
                       material == other.material &&
                       faceDir == other.faceDir &&
                       color.Equals(other.color);
            }
        }

        static Vector3 GetNormalVector(int faceDir)
        {
            switch (faceDir)
            {
                case 0: return Vector3.right;
                case 1: return Vector3.left;
                case 2: return Vector3.up;
                case 3: return Vector3.down;
                case 4: return Vector3.forward;
                default: return Vector3.back;
            }
        }

        static bool IsNormalPositive(int faceDir)
        {
            return faceDir == 0 || faceDir == 2 || faceDir == 4;
        }
    }
}
