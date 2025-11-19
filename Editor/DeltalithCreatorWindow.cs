// DeltalithCreatorWindow.cs
// Place in: DELTALITH/Editor/DeltalithCreatorWindow.cs
//
// This is the original file with changes to enforce:
// - 128x128x128 chunk (uses VoxelChunk.ChunkSize already changed)
// - Placement area and adjacency rules (can place in air only if connected by face/edge/corner)
// - First placed block must be on bottom (y == 0)
// - Grid generation uses VoxelChunk.ChunkSize so remains consistent

using UnityEditor;
using UnityEngine;
using Deltalith.Runtime;
using System;

namespace Deltalith.Editor
{
    public class DeltalithCreatorWindow : EditorWindow
    {
        const int DefaultRTSize = 1024;
        const float RAY_EPSILON = 0.001f;

        RenderTexture rt;
        Camera previewCamera;
        Light previewLight;
        GameObject previewRoot;
        VoxelChunk previewChunk;
        MeshFilter previewMeshFilter;
        MeshRenderer previewMeshRenderer;
        MeshCollider previewMeshCollider;

        // grid object (lines)
        GameObject gridGO;
        MeshFilter gridMeshFilter;
        MeshRenderer gridMeshRenderer;

        // camera orbit state
        Vector3 camEuler = new Vector3(30f, -45f, 0f);
        float camDistance = 60f;
        Vector3 camTarget = Vector3.one * (VoxelChunk.ChunkSize * 0.5f);

        // painting
        Color paintColor = Color.white;
        int paintMaterialId = 1;
        int brushSize = 1;
        bool showGrid = true;
        bool showPreviewCube = true;

        // preview cube mesh (for hover)
        Mesh previewCubeMesh;
        Material previewCubeMaterial;

        // UI scroll
        Vector2 leftPanelScroll;

        // palettes
        const int PaletteSlots = 16;
        Color[] palette = new Color[PaletteSlots];
        Color[] recentColors = new Color[12];
        Color[] presetColors = new Color[]
        {
            Color.white, Color.black, Color.gray,
            new Color(1,0,0), new Color(0,1,0), new Color(0,0,1),
            new Color(1,0.5f,0), new Color(1,1,0),
            new Color(0,1,1), new Color(1,0,1),
            new Color(0.4f,0.2f,0.1f),
            new Color(1.0f,0.8f,0.6f),
            new Color(0.9f,0.7f,0.5f),
            new Color(0.6f,0.3f,0.0f),
            new Color(0.2f,0.6f,0.2f),
            new Color(0.15f,0.3f,0.6f)
        };

        const string PaletteKey = "Deltalith_Palette";
        const string RecentKey = "Deltalith_Recent";

        // viewport rect computed during layout
        Rect viewportRect;

        // initialization guard
        bool initialized = false;

        [MenuItem("Deltalith/Voxel Creator")]
        public static void OpenWindow()
        {
            var w = GetWindow<DeltalithCreatorWindow>("Deltalith Creator");
            w.minSize = new Vector2(700, 480);
        }

        void OnEnable()
        {
            LoadPalette();
            LoadRecentColors();
            initialized = false; // lazy init
        }

        void OnDisable()
        {
            SavePalette();
            SaveRecentColors();
            DestroyPreviewObjects();
            DestroyPreviewCube();
            initialized = false;
        }

        void EnsureInitialized()
        {
            if (initialized) return;

            CreatePreviewObjects(DefaultRTSize, DefaultRTSize);
            CreatePreviewCube();
            initialized = true;
        }

        bool SafeToRender()
        {
            return initialized &&
                   previewCamera != null &&
                   previewRoot != null &&
                   rt != null &&
                   previewMeshFilter != null &&
                   previewCubeMaterial != null &&
                   previewCubeMesh != null;
        }

        void OnGUI()
        {
            // lazy init: ensures no GUI tries to access preview objects before they exist
            EnsureInitialized();

            EditorGUILayout.BeginHorizontal();
            DrawLeftPanel();
            DrawViewportPanel();
            EditorGUILayout.EndHorizontal();

            if (Event.current.type == EventType.Repaint) Repaint();
        }

        void DrawLeftPanel()
        {
            GUILayout.BeginVertical(GUILayout.Width(300));
            leftPanelScroll = EditorGUILayout.BeginScrollView(leftPanelScroll);
            try
            {
                EditorGUILayout.LabelField("Deltalith Creator", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Viewport: Right-drag rotate | Middle-drag pan | Scroll zoom");
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Canvas", EditorStyles.boldLabel);
                if (GUILayout.Button("New Chunk (Clear)"))
                {
                    EnsurePreviewChunk();
                    ClearChunk(previewChunk);
                    RegeneratePreviewMesh();
                }

                EditorGUILayout.LabelField($"Chunk Size: {VoxelChunk.ChunkSize}³");
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Painting Tools", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                paintColor = EditorGUILayout.ColorField("Brush Color", paintColor);
                if (EditorGUI.EndChangeCheck()) AddRecentColor(paintColor);

                paintMaterialId = EditorGUILayout.IntField("Material ID", Mathf.Max(1, paintMaterialId));
                brushSize = EditorGUILayout.IntSlider("Brush Size", brushSize, 1, 8);

                GUILayout.Space(6);
                if (GUILayout.Button("Clear All"))
                {
                    EnsurePreviewChunk();
                    Undo.RecordObject(previewChunk, "Clear Chunk");
                    ClearChunk(previewChunk);
                    RegeneratePreviewMesh();
                }

                if (GUILayout.Button("Generate Mesh"))
                {
                    EnsurePreviewChunk();
                    RegeneratePreviewMesh();
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);
                DrawColorSectionInline(palette, "Palette");
                GUILayout.Space(6);
                DrawColorSectionInline(recentColors, "Recent");
                GUILayout.Space(6);
                DrawColorSectionInline(presetColors, "Presets");

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("View", EditorStyles.boldLabel);
                showGrid = EditorGUILayout.ToggleLeft("Show Grid", showGrid);
                showPreviewCube = EditorGUILayout.ToggleLeft("Show Hover Preview", showPreviewCube);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);
                if (GUILayout.Button("Export Selected Chunk as Mesh Asset"))
                {
                    EnsurePreviewChunk();
                    string folder = "Assets/Deltalith/Exports";
                    if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);
                    string baseName = $"DeltalithChunk_{DateTime.Now:yyyyMMdd_HHmmss}";
                    Mesh mesh = (previewMeshFilter != null) ? previewMeshFilter.sharedMesh : null;
                    if (mesh != null)
                    {
                        string meshPath = $"{folder}/{baseName}.asset";
                        AssetDatabase.CreateAsset(Mesh.Instantiate(mesh), meshPath);
                        AssetDatabase.SaveAssets();
                        EditorUtility.DisplayDialog("Export", $"Saved mesh asset to {meshPath}", "OK");
                    }
                    else EditorUtility.DisplayDialog("Export", "No mesh to export. Generate mesh first.", "OK");
                }

                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Left-click in the viewport to paint. Right-click to erase. Blocks snap to a 1×1 grid. Placement requires adjacency (face/edge/corner) except the very first block which must be on the bottom (y=0). Build area is constrained to the chunk bounds.", MessageType.Info);
            }
            finally
            {
                EditorGUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
        }

        void DrawColorSectionInline(Color[] colors, string label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            int cols = 8;
            int index = 0;
            int rows = Mathf.CeilToInt(colors.Length / (float)cols);
            for (int y = 0; y < rows; y++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int x = 0; x < cols; x++)
                {
                    if (index >= colors.Length)
                    {
                        GUILayout.FlexibleSpace();
                        index++;
                        continue;
                    }

                    var c = colors[index];
                    if (DrawColorSwatchClickable(c, 28))
                    {
                        paintColor = c;
                        AddRecentColor(c);
                    }

                    index++;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        bool DrawColorSwatchClickable(Color c, int size)
        {
            Rect r = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            EditorGUI.DrawRect(r, c);

            Handles.BeginGUI();
            Handles.color = Color.black;
            Handles.DrawLine(new Vector3(r.xMin, r.yMin), new Vector3(r.xMax, r.yMin));
            Handles.DrawLine(new Vector3(r.xMax, r.yMin), new Vector3(r.xMax, r.yMax));
            Handles.DrawLine(new Vector3(r.xMax, r.yMax), new Vector3(r.xMin, r.yMax));
            Handles.DrawLine(new Vector3(r.xMin, r.yMax), new Vector3(r.xMin, r.yMin));
            Handles.EndGUI();

            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return true;
            }
            return false;
        }

        // Palette persistence
        void SavePalette()
        {
            for (int i = 0; i < palette.Length; i++)
            {
                string key = $"{PaletteKey}_{i}";
                EditorPrefs.SetString(key, ColorUtility.ToHtmlStringRGBA(palette[i]));
            }
        }

        void LoadPalette()
        {
            for (int i = 0; i < palette.Length; i++)
            {
                string key = $"{PaletteKey}_{i}";
                if (EditorPrefs.HasKey(key))
                {
                    string hex = EditorPrefs.GetString(key);
                    if (!string.IsNullOrEmpty(hex))
                        ColorUtility.TryParseHtmlString("#" + hex, out palette[i]);
                    else palette[i] = Color.white;
                }
                else
                {
                    palette[i] = presetColors[i % presetColors.Length];
                }
            }
        }

        void SaveRecentColors()
        {
            for (int i = 0; i < recentColors.Length; i++)
            {
                string key = $"{RecentKey}_{i}";
                EditorPrefs.SetString(key, ColorUtility.ToHtmlStringRGBA(recentColors[i]));
            }
        }

        void LoadRecentColors()
        {
            for (int i = 0; i < recentColors.Length; i++)
            {
                string key = $"{RecentKey}_{i}";
                if (EditorPrefs.HasKey(key))
                {
                    string hex = EditorPrefs.GetString(key);
                    if (!string.IsNullOrEmpty(hex))
                        ColorUtility.TryParseHtmlString("#" + hex, out recentColors[i]);
                    else recentColors[i] = Color.gray;
                }
                else recentColors[i] = Color.gray;
            }
        }

        void AddRecentColor(Color c)
        {
            if (recentColors.Length > 0 && recentColors[0] == c) return;

            for (int i = recentColors.Length - 1; i > 0; i--)
                recentColors[i] = recentColors[i - 1];

            recentColors[0] = c;
            SaveRecentColors();
        }

        void DrawViewportPanel()
        {
            // Reserve the viewportRect from layout
            viewportRect = GUILayoutUtility.GetRect(position.width - 300, position.height, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            // If RT size differs, recreate safely
            if (Event.current.type == EventType.Repaint && (rt == null || rt.width != (int)viewportRect.width || rt.height != (int)viewportRect.height))
            {
                int w = Mathf.Max(256, (int)viewportRect.width);
                int h = Mathf.Max(256, (int)viewportRect.height);
                CreatePreviewObjects(w, h);
            }

            if (rt != null)
            {
                GUI.DrawTexture(viewportRect, rt, ScaleMode.ScaleToFit, false);
            }

            ProcessViewportInput(viewportRect);
            RenderPreview(viewportRect);
        }

        void ProcessViewportInput(Rect viewportRect)
        {
            if (!SafeToRender()) return;

            Event e = Event.current;
            Vector2 mouse = e.mousePosition;

            // Mouse inside viewport?
            if (!viewportRect.Contains(mouse)) return;

            // transform GUI pos to RenderTexture pixel coords
            Vector2 local = mouse - viewportRect.position;
            int px = (int)((local.x / viewportRect.width) * rt.width);
            int py = (int)(((viewportRect.height - local.y) / viewportRect.height) * rt.height); // invert y for RT

            // camera control
            if (e.type == EventType.MouseDrag && e.button == 1) // right drag rotate
            {
                Vector2 delta = e.delta;
                camEuler.x = Mathf.Clamp(camEuler.x - delta.y * 0.2f, -89f, 89f);
                camEuler.y = camEuler.y + delta.x * 0.2f;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 2) // middle drag pan
            {
                Vector2 delta = e.delta;
                Vector3 right = previewCamera.transform.right;
                Vector3 up = previewCamera.transform.up;
                camTarget += (-right * delta.x + -up * delta.y) * (camDistance * 0.0025f);
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel)
            {
                float scroll = -e.delta.y;
                camDistance = Mathf.Clamp(camDistance * (1f - scroll * 0.05f), 10f, 500f);
                e.Use();
            }

            // painting (mouse down)
            if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
            {
                bool paint = (e.button == 0);
                TryEditVoxelAtPixel(px, py, paint);
                e.Use();
            }

            // continuous painting while dragging with left button
            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                TryEditVoxelAtPixel(px, py, true);
                e.Use();
            }
        }

        void RenderPreview(Rect viewportRect)
        {
            if (!SafeToRender()) return;

            // update camera transform from orbit parameters
            Quaternion rot = Quaternion.Euler(camEuler);
            Vector3 dir = rot * Vector3.forward;
            previewCamera.transform.position = camTarget - dir * camDistance;
            previewCamera.transform.rotation = rot;

            // set camera properties
            previewCamera.targetTexture = rt;
            previewCamera.aspect = (float)rt.width / rt.height;
            previewCamera.Render();

            // draw hover cube in preview camera (snapped)
            if (showPreviewCube)
            {
                if (ComputeHoverVoxelWorld(out Vector3 hoverPos))
                {
                    Matrix4x4 mat = Matrix4x4.TRS(previewRoot.transform.TransformPoint(hoverPos + Vector3.one * 0.5f), Quaternion.identity, Vector3.one);
                    Color c = paintColor;
                    c.a = 0.45f;
                    previewCubeMaterial.SetColor("_Color", c);
                    Graphics.DrawMesh(previewCubeMesh, mat, previewCubeMaterial, 0, previewCamera);
                }
            }
        }

        Ray RayFromViewportPixel(int px, int py)
        {
            // returns a ray in world space from camera for RT pixel coords (origin bottom-left)
            if (!SafeToRender())
                return new Ray(Vector3.zero, Vector3.forward);

            float nx = px / (float)rt.width;
            float ny = py / (float)rt.height;
            return previewCamera.ViewportPointToRay(new Vector3(nx, ny, 0f));
        }

        bool ComputeHoverVoxelWorld(out Vector3 hoverPos)
        {
            hoverPos = Vector3.zero;
            if (!SafeToRender()) return false;

            // Convert GUI position to RT pixel coords
            Vector2 windowMouse = Event.current.mousePosition;
            if (!viewportRect.Contains(windowMouse)) return false;

            Vector2 local = windowMouse - viewportRect.position;
            int px = (int)((local.x / viewportRect.width) * rt.width);
            int py = (int)(((viewportRect.height - local.y) / viewportRect.height) * rt.height);

            Ray ray = RayFromViewportPixel(px, py);
            Ray localRay = new Ray(
                previewRoot.transform.InverseTransformPoint(ray.origin),
                previewRoot.transform.InverseTransformDirection(ray.direction)
            );

            if (!RayAABBIntersection(localRay, Vector3.zero, Vector3.one * VoxelChunk.ChunkSize, out float enter, out _))
                return false;

            Vector3 hitPoint = localRay.origin + localRay.direction * (enter + RAY_EPSILON);

            // SNAP PERFECTLY TO GRID
            int vx = Mathf.FloorToInt(hitPoint.x + 0.5f);
            int vy = Mathf.FloorToInt(hitPoint.y + 0.5f);
            int vz = Mathf.FloorToInt(hitPoint.z + 0.5f);

            // OUT OF BOUNDS → NO HOVER
            if (vx < 0 || vy < 0 || vz < 0 ||
                vx >= VoxelChunk.ChunkSize ||
                vy >= VoxelChunk.ChunkSize ||
                vz >= VoxelChunk.ChunkSize)
                return false;

            bool isFirstBlock = previewChunk.IsEmpty();

            // FIRST BLOCK MUST BE ON FLOOR
            if (isFirstBlock)
            {
                if (vy != 0)
                    return false;
            }
            else
            {
                // REQUIRE ADJACENCY FOR HOVER (face, edge, corner)
                if (!HasAdjacentVoxel(vx, vy, vz))
                    return false;
            }

            hoverPos = new Vector3(vx, vy, vz);
            return true;
        }

        bool HasAdjacentVoxel(int x, int y, int z)
        {
            for (int nx = -1; nx <= 1; nx++)
            {
                for (int ny = -1; ny <= 1; ny++)
                {
                    for (int nz = -1; nz <= 1; nz++)
                    {
                        if (nx == 0 && ny == 0 && nz == 0) continue;

                        int cx = x + nx;
                        int cy = y + ny;
                        int cz = z + nz;

                        if (cx < 0 || cy < 0 || cz < 0 ||
                            cx >= VoxelChunk.ChunkSize ||
                            cy >= VoxelChunk.ChunkSize ||
                            cz >= VoxelChunk.ChunkSize)
                            continue;

                        if (!previewChunk.GetVoxel(cx, cy, cz).IsEmpty)
                            return true;
                    }
                }
            }
            return false;
        }


        void TryEditVoxelAtPixel(int px, int py, bool paint)
        {
            if (!SafeToRender()) return;
            EnsurePreviewChunk();

            Ray ray = RayFromViewportPixel(px, py);
            Ray localRay = new Ray(previewRoot.transform.InverseTransformPoint(ray.origin), previewRoot.transform.InverseTransformDirection(ray.direction));

            if (!RayAABBIntersection(localRay, Vector3.zero, Vector3.one * VoxelChunk.ChunkSize, out float enter, out float exit))
                return;

            Vector3 hitPoint = localRay.origin + localRay.direction * (enter + RAY_EPSILON);

            // Snap to exact grid cell
            int vx = Mathf.FloorToInt(hitPoint.x + 0.5f);
            int vy = Mathf.FloorToInt(hitPoint.y + 0.5f);
            int vz = Mathf.FloorToInt(hitPoint.z + 0.5f);

            // enforce chunk bounds (build area)
            if (vx < 0 || vz < 0 || vx >= VoxelChunk.ChunkSize || vz >= VoxelChunk.ChunkSize)
                return;
            if (vy < 0 || vy >= VoxelChunk.ChunkSize)
                return;

            // First block must be at bottom layer (y == 0).
            bool isFirstBlock = previewChunk.IsEmpty();

            if (isFirstBlock && paint)
            {
                if (vy != 0)
                    return; // only allow initial placement on bottom layer
            }

            // If not first block and painting, require connectivity (face/edge/corner) to existing voxel
            if (paint && !isFirstBlock)
            {
                bool hasNeighbor = false;
                for (int nx = -1; nx <= 1 && !hasNeighbor; nx++)
                {
                    for (int ny = -1; ny <= 1 && !hasNeighbor; ny++)
                    {
                        for (int nz = -1; nz <= 1 && !hasNeighbor; nz++)
                        {
                            if (nx == 0 && ny == 0 && nz == 0) continue;
                            int cx = vx + nx;
                            int cy = vy + ny;
                            int cz = vz + nz;
                            if (cx < 0 || cy < 0 || cz < 0 || cx >= VoxelChunk.ChunkSize || cy >= VoxelChunk.ChunkSize || cz >= VoxelChunk.ChunkSize) continue;
                            if (!previewChunk.GetVoxel(cx, cy, cz).IsEmpty)
                            {
                                hasNeighbor = true;
                                break;
                            }
                        }
                    }
                }

                if (!hasNeighbor) return; // placement not allowed
            }

            int half = brushSize / 2;
            Undo.RecordObject(previewChunk, paint ? "Paint Voxel" : "Erase Voxel");

            for (int dx = -half; dx <= half; dx++)
            {
                for (int dy = -half; dy <= half; dy++)
                {
                    for (int dz = -half; dz <= half; dz++)
                    {
                        int tx = vx + dx;
                        int ty = vy + dy;
                        int tz = vz + dz;
                        if (tx < 0 || ty < 0 || tz < 0 || tx >= VoxelChunk.ChunkSize || ty >= VoxelChunk.ChunkSize || tz >= VoxelChunk.ChunkSize) continue;
                        if (paint)
                        {
                            Voxel v = new Voxel { id = (byte)Mathf.Max(1, paintMaterialId), color = (Color32)paintColor };
                            previewChunk.SetVoxel(tx, ty, tz, v);
                        }
                        else
                        {
                            previewChunk.SetVoxel(tx, ty, tz, Voxel.Empty);
                        }
                    }
                }
            }

            RegeneratePreviewMesh();
        }

        void RegeneratePreviewMesh()
        {
            if (previewChunk == null || previewMeshFilter == null || previewMeshRenderer == null) return;
            
            Mesh m = VoxelMeshGenerator.GenerateMesh(previewChunk);
            
            if (m == null || m.vertexCount == 0)
            {
                previewMeshFilter.sharedMesh = null;
                if (previewMeshCollider != null) previewMeshCollider.sharedMesh = null;
                return;
            }
            
            previewMeshFilter.sharedMesh = m;
            if (previewMeshCollider != null) previewMeshCollider.sharedMesh = m;
            
            // CRITICAL: The mesh has submeshes and vertex colors - we need to assign materials properly
            UpdateMeshRendererMaterials(m);
        }
        
        void UpdateMeshRendererMaterials(Mesh mesh)
        {
            if (mesh == null || previewMeshRenderer == null) return;
            
            // Get or create a material that supports vertex colors
            Material baseMat = previewMeshRenderer.sharedMaterial;
            if (baseMat == null)
            {
                // Try to find a shader that supports vertex colors
                Shader shader = Shader.Find("Legacy Shaders/VertexLit");
                if (shader == null || !shader.isSupported)
                {
                    shader = Shader.Find("Legacy Shaders/Diffuse");
                    if (shader == null || !shader.isSupported)
                    {
                        shader = Shader.Find("Standard");
                        if (shader == null || !shader.isSupported)
                        {
                            shader = Shader.Find("Unlit/Color");
                        }
                    }
                }
                
                if (shader != null && shader.isSupported)
                {
                    baseMat = new Material(shader);
                    baseMat.color = Color.white; // White so vertex colors show through
                    previewMeshRenderer.sharedMaterial = baseMat;
                }
                else
                {
                    Debug.LogError("Deltalith: Could not find a suitable shader for vertex colors!");
                    return;
                }
            }
            
            // The mesh generator creates submeshes - we need to assign materials to all of them
            if (mesh.subMeshCount > 0)
            {
                Material[] materials = new Material[mesh.subMeshCount];
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = baseMat; // All submeshes use the same material (vertex colors differentiate)
                }
                previewMeshRenderer.sharedMaterials = materials;
            }
            else
            {
                previewMeshRenderer.sharedMaterial = baseMat;
            }
        }

        static bool RayAABBIntersection(Ray r, Vector3 boxMin, Vector3 boxMax, out float tmin, out float tmax)
        {
            tmin = 0f;
            tmax = float.MaxValue;

            for (int i = 0; i < 3; i++)
            {
                float origin = r.origin[i];
                float dir = r.direction[i];
                float min = boxMin[i];
                float max = boxMax[i];

                if (Mathf.Abs(dir) < 1e-6f)
                {
                    if (origin < min || origin > max) return false;
                }
                else
                {
                    float ood = 1f / dir;
                    float t1 = (min - origin) * ood;
                    float t2 = (max - origin) * ood;
                    if (t1 > t2) { var tmp = t1; t1 = t2; t2 = tmp; }
                    if (t1 > tmin) tmin = t1;
                    if (t2 < tmax) tmax = t2;
                    if (tmin > tmax) return false;
                }
            }
            return true;
        }

        void EnsurePreviewChunk()
        {
            if (previewChunk == null) CreatePreviewObjects(DefaultRTSize, DefaultRTSize);
        }

        void ClearChunk(VoxelChunk chunk)
        {
            if (chunk == null) return;
            chunk.EnsureArray();
            for (int i = 0; i < chunk.voxels.Length; i++) chunk.voxels[i] = Voxel.Empty;
            RegeneratePreviewMesh();
        }

        void CreatePreviewObjects(int width, int height)
        {
            // Detach camera from old RT before destroying
            if (previewCamera != null)
                previewCamera.targetTexture = null;

            if (rt != null)
            {
                rt.Release();
                DestroyImmediate(rt);
                rt = null;
            }

            rt = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR);
            rt.Create();

            // root object
            if (previewRoot == null)
            {
                previewRoot = new GameObject("DeltalithPreviewRoot");
                previewRoot.hideFlags = HideFlags.HideAndDontSave;
            }

            // ensure required components
            if (!previewRoot.TryGetComponent<VoxelChunk>(out previewChunk))
                previewChunk = previewRoot.AddComponent<VoxelChunk>();

            if (!previewRoot.TryGetComponent<MeshFilter>(out previewMeshFilter))
                previewMeshFilter = previewRoot.AddComponent<MeshFilter>();

            if (!previewRoot.TryGetComponent<MeshRenderer>(out previewMeshRenderer))
                previewMeshRenderer = previewRoot.AddComponent<MeshRenderer>();

            if (!previewRoot.TryGetComponent<MeshCollider>(out previewMeshCollider))
                previewMeshCollider = previewRoot.AddComponent<MeshCollider>();

            // make sure mesh renderer has a material that supports vertex colors
            if (previewMeshRenderer.sharedMaterial == null)
            {
                // Use VertexLit shader which properly supports vertex colors
                Shader shader = Shader.Find("Legacy Shaders/VertexLit");
                if (shader == null || !shader.isSupported)
                {
                    shader = Shader.Find("Legacy Shaders/Diffuse");
                    if (shader == null || !shader.isSupported)
                    {
                        shader = Shader.Find("Standard");
                    }
                }
                
                if (shader != null && shader.isSupported)
                {
                    var mat = new Material(shader);
                    mat.color = Color.white; // White base so vertex colors show through
                    previewMeshRenderer.sharedMaterial = mat;
                }
            }

            // camera
            if (previewCamera == null)
            {
                GameObject camGo = new GameObject("DeltalithPreviewCamera");
                camGo.hideFlags = HideFlags.HideAndDontSave;
                previewCamera = camGo.AddComponent<Camera>();
                previewCamera.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
                previewCamera.clearFlags = CameraClearFlags.Color;
                previewCamera.farClipPlane = 1000f;
                previewCamera.nearClipPlane = 0.01f;
            }

            // light
            if (previewLight == null)
            {
                GameObject lightGo = new GameObject("DeltalithPreviewLight");
                lightGo.hideFlags = HideFlags.HideAndDontSave;
                previewLight = lightGo.AddComponent<Light>();
                previewLight.type = LightType.Directional;
                previewLight.intensity = 1.0f;
                previewLight.transform.rotation = Quaternion.Euler(50, -30, 0);
            }

            // grid object (lines drawn on XZ plane)
            CreateOrUpdateGridMesh();

            // reset transforms / camera defaults
            previewRoot.transform.position = Vector3.zero;
            previewRoot.transform.rotation = Quaternion.identity;
            previewRoot.transform.localScale = Vector3.one;

            camDistance = Mathf.Max(30f, VoxelChunk.ChunkSize * 2f);
            camTarget = new Vector3(VoxelChunk.ChunkSize * 0.5f, VoxelChunk.ChunkSize * 0.5f, VoxelChunk.ChunkSize * 0.5f);
            camEuler = new Vector3(30f, -45f, 0f);

            previewCamera.targetTexture = rt;
            previewCamera.aspect = (float)rt.width / rt.height;

            // ensure chunk has array and mesh reflects it
            previewChunk.EnsureArray();
            RegeneratePreviewMesh();
        }

        void CreateOrUpdateGridMesh()
        {
            // grid GO is child of previewRoot so it follows the preview root transform
            if (gridGO == null)
            {
                gridGO = new GameObject("DeltalithGrid");
                gridGO.hideFlags = HideFlags.HideAndDontSave;
                gridGO.transform.parent = previewRoot.transform;
                gridGO.transform.localPosition = Vector3.zero;
                gridGO.transform.localRotation = Quaternion.identity;
            }

            if (!gridGO.TryGetComponent<MeshFilter>(out gridMeshFilter))
                gridMeshFilter = gridGO.AddComponent<MeshFilter>();

            if (!gridGO.TryGetComponent<MeshRenderer>(out gridMeshRenderer))
                gridMeshRenderer = gridGO.AddComponent<MeshRenderer>();

            // simple unlit color material for grid
            if (gridMeshRenderer.sharedMaterial == null)
            {
                var mat = new Material(Shader.Find("Unlit/Color"));
                if (mat == null)
                {
                    // fallback to Standard if Unlit/Color not available
                    mat = new Material(Shader.Find("Standard"));
                }
                mat.SetColor("_Color", new Color(0.6f, 0.6f, 0.6f, 1f));
                gridMeshRenderer.sharedMaterial = mat;
            }

            // Build a lines mesh on XZ plane from (0..ChunkSize)
            int size = VoxelChunk.ChunkSize;
            float spacing = 1f;
            int lineCount = (size + 1) * 2;
            int vertsCount = lineCount * 2;

            Vector3[] verts = new Vector3[vertsCount];
            int[] indices = new int[vertsCount];

            int idx = 0;
            // lines along X (vary Z)
            for (int z = 0; z <= size; z++)
            {
                verts[idx] = new Vector3(0f, 0f, z * spacing);
                indices[idx] = idx;
                idx++;
                verts[idx] = new Vector3(size * spacing, 0f, z * spacing);
                indices[idx] = idx;
                idx++;
            }
            // lines along Z (vary X)
            for (int x = 0; x <= size; x++)
            {
                verts[idx] = new Vector3(x * spacing, 0f, 0f);
                indices[idx] = idx;
                idx++;
                verts[idx] = new Vector3(x * spacing, 0f, size * spacing);
                indices[idx] = idx;
                idx++;
            }

            Mesh mesh = gridMeshFilter.sharedMesh;
            if (mesh == null) mesh = new Mesh();
            else mesh.Clear();

            mesh.name = "DeltalithGridMesh";
            mesh.vertices = verts;
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            gridMeshFilter.sharedMesh = mesh;

            // grid should not receive shadows etc
            gridMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gridMeshRenderer.receiveShadows = false;
        }

        void DestroyPreviewObjects()
        {
            // detach camera from RT before destroying
            if (previewCamera != null)
                previewCamera.targetTexture = null;

            if (previewCamera != null) DestroyImmediate(previewCamera.gameObject);
            if (previewLight != null) DestroyImmediate(previewLight.gameObject);

            if (gridGO != null) DestroyImmediate(gridGO);
            gridGO = null;
            gridMeshFilter = null;
            gridMeshRenderer = null;

            if (previewRoot != null) DestroyImmediate(previewRoot);
            previewCamera = null;
            previewLight = null;
            previewRoot = null;
            previewChunk = null;
            previewMeshFilter = null;
            previewMeshRenderer = null;
            previewMeshCollider = null;

            if (rt != null)
            {
                rt.Release();
                DestroyImmediate(rt);
                rt = null;
            }
        }

        void CreatePreviewCube()
        {
            // try builtin cube mesh, fallback to primitive
            previewCubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (previewCubeMesh == null)
            {
                GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                previewCubeMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(tmp);
            }

            previewCubeMaterial = new Material(Shader.Find("Standard"));
            // configure for transparency
            previewCubeMaterial.SetFloat("_Mode", 3f);
            previewCubeMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            previewCubeMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            previewCubeMaterial.SetInt("_ZWrite", 0);
            previewCubeMaterial.DisableKeyword("_ALPHATEST_ON");
            previewCubeMaterial.EnableKeyword("_ALPHABLEND_ON");
            previewCubeMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            previewCubeMaterial.renderQueue = 3000;
        }

        void DestroyPreviewCube()
        {
            if (previewCubeMaterial != null) DestroyImmediate(previewCubeMaterial);
            previewCubeMaterial = null;
            previewCubeMesh = null;
        }
    }
}
