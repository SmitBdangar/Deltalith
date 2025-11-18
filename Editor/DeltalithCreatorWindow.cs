// DeltalithCreatorWindow.cs
// Place in: DELTALITH/Editor/DeltalithCreatorWindow.cs

using UnityEditor;
using UnityEngine;
using Deltalith.Runtime;
using System;

namespace Deltalith.Editor
{
    public class DeltalithCreatorWindow : EditorWindow
    {
        const int DefaultRTSize = 1024;

        // viewport render
        RenderTexture rt;
        Camera previewCamera;
        Light previewLight;
        GameObject previewRoot;
        VoxelChunk previewChunk;
        MeshFilter previewMeshFilter;
        MeshRenderer previewMeshRenderer;
        MeshCollider previewMeshCollider;

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

        // --- Color palette system (Option A: 16 fixed slots) ---
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

        // lifecycle
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
            CreatePreviewObjects(DefaultRTSize, DefaultRTSize);
            CreatePreviewCube();
        }

        void OnDisable()
        {
            SavePalette();
            SaveRecentColors();
            DestroyPreviewObjects();
            DestroyPreviewCube();
            if (rt != null) { rt.Release(); rt = null; }
        }

        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            // Left panel: controls
            DrawLeftPanel();

            // Right panel: viewport
            DrawViewportPanel();

            EditorGUILayout.EndHorizontal();

            // repaint continuously while interacting
            if (Event.current.type == EventType.Repaint) Repaint();
        }

        void DrawLeftPanel()
        {
            GUILayout.BeginVertical(GUILayout.Width(300));
            leftPanelScroll = EditorGUILayout.BeginScrollView(leftPanelScroll);

            EditorGUILayout.LabelField("Deltalith Creator", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Viewport controls: Right-drag rotate, Middle-drag pan, Scroll zoom");
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
            // Painting Tools
            EditorGUILayout.LabelField("Painting Tools", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            paintColor = EditorGUILayout.ColorField("Brush Color", paintColor);
            if (EditorGUI.EndChangeCheck())
            {
                AddRecentColor(paintColor);
            }

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

            // Draw Palette (16 slots)
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
                Mesh mesh = previewMeshFilter.sharedMesh;
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
            EditorGUILayout.HelpBox("Left-click in the viewport to paint. Right-click to erase.", MessageType.Info);

            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();
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
                        continue;
                    }

                    var c = colors[index];
                    if (DrawColorSwatchClickable(c, 28))
                    {
                        paintColor = c;
                        AddRecentColor(c);
                    }

                    // Ctrl+click to edit palette slot (only for palette array)
                    if (label == "Palette" && Event.current.type == EventType.MouseDown && Event.current.control)
                    {
                        // get rect for last control? Simpler: expose Edit Palette button below instead of complex ctrl-click
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

            // border
            Color border = Color.black;
            Handles.BeginGUI();
            Handles.color = border;
            Vector3 p1 = new Vector3(r.xMin, r.yMin);
            Vector3 p2 = new Vector3(r.xMax, r.yMin);
            Vector3 p3 = new Vector3(r.xMax, r.yMax);
            Vector3 p4 = new Vector3(r.xMin, r.yMax);
            Handles.DrawLine(p1, p2);
            Handles.DrawLine(p2, p3);
            Handles.DrawLine(p3, p4);
            Handles.DrawLine(p4, p1);
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
                    // initialize sensible default palette spread
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
            // avoid duplicates: shift only if different from last
            if (recentColors.Length > 0 && recentColors[0] == c) return;

            for (int i = recentColors.Length - 1; i > 0; i--)
                recentColors[i] = recentColors[i - 1];

            recentColors[0] = c;
            SaveRecentColors();
        }

        void DrawViewportPanel()
        {
            Rect r = GUILayoutUtility.GetRect(position.width - 300, position.height, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type == EventType.Repaint && (rt == null || rt.width != (int)r.width || rt.height != (int)r.height))
            {
                int w = Mathf.Max(256, (int)r.width);
                int h = Mathf.Max(256, (int)r.height);
                CreatePreviewObjects(w, h);
            }

            if (rt != null)
            {
                GUI.DrawTexture(r, rt, ScaleMode.ScaleToFit, false);
            }

            ProcessViewportInput(r);
            RenderPreview(r);
        }

        void ProcessViewportInput(Rect viewportRect)
        {
            Event e = Event.current;
            Vector2 mouse = e.mousePosition;

            // Mouse inside viewport?
            if (!viewportRect.Contains(mouse)) return;

            // transform GUI pos to RenderTexture pixel coords
            Vector2 local = mouse - viewportRect.position;
            float px = (local.x / viewportRect.width) * rt.width;
            float py = ((viewportRect.height - local.y) / viewportRect.height) * rt.height; // invert y for RT

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
                TryEditVoxelAtPixel((int)px, (int)py, paint);
                e.Use();
            }

            // continuous painting while dragging with left button
            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                bool paint = true;
                TryEditVoxelAtPixel((int)px, (int)py, paint);
                e.Use();
            }
        }

        void RenderPreview(Rect viewportRect)
        {
            if (previewCamera == null || rt == null) return;

            // update camera transform from orbit parameters
            Quaternion rot = Quaternion.Euler(camEuler);
            Vector3 dir = rot * Vector3.forward;
            previewCamera.transform.position = camTarget - dir * camDistance;
            previewCamera.transform.rotation = rot;

            // set camera properties
            previewCamera.targetTexture = rt;
            previewCamera.aspect = (float)rt.width / rt.height;
            previewCamera.Render();

            // draw preview cube overlay using Graphics.DrawMesh in preview camera
            if (showPreviewCube)
            {
                if (ComputeHoverVoxelWorld(out Vector3 hoverPos))
                {
                    Matrix4x4 mat = Matrix4x4.TRS(previewRoot.transform.TransformPoint(hoverPos + Vector3.one * 0.0f), Quaternion.identity, Vector3.one * 1.0f);
                    Color c = paintColor;
                    c.a = 0.45f;
                    previewCubeMaterial.SetColor("_Color", c);
                    Graphics.DrawMesh(previewCubeMesh, mat, previewCubeMaterial, 0, previewCamera);
                }
            }
        }

        bool ComputeHoverVoxelWorld(out Vector3 hoverPos)
        {
            hoverPos = Vector3.zero;
            Vector2 windowMouse = Event.current.mousePosition;
            Rect vpRect = new Rect(300, 0, position.width - 300, position.height); // approximate viewport position relative to window
            if (!vpRect.Contains(windowMouse)) return false;
            Vector2 local = windowMouse - vpRect.position;
            float px = (local.x / vpRect.width) * rt.width;
            float py = ((vpRect.height - local.y) / vpRect.height) * rt.height;
            Ray ray = previewCamera.ScreenPointToRay(new Vector3(px, py, 0));
            Ray localRay = new Ray(previewRoot.transform.InverseTransformPoint(ray.origin), previewRoot.transform.InverseTransformDirection(ray.direction));
            if (!RayAABBIntersection(localRay, Vector3.zero, Vector3.one * VoxelChunk.ChunkSize, out float enter, out float exit)) return false;
            Vector3 hitPoint = localRay.origin + localRay.direction * enter;
            int vx = Mathf.FloorToInt(hitPoint.x);
            int vy = Mathf.FloorToInt(hitPoint.y);
            int vz = Mathf.FloorToInt(hitPoint.z);
            if (vx < 0 || vy < 0 || vz < 0 || vx >= VoxelChunk.ChunkSize || vy >= VoxelChunk.ChunkSize || vz >= VoxelChunk.ChunkSize) return false;
            hoverPos = new Vector3(vx, vy, vz);
            return true;
        }

        void TryEditVoxelAtPixel(int px, int py, bool paint)
        {
            EnsurePreviewChunk();
            Vector3 pixel = new Vector3(px, py, 0);
            Ray ray = previewCamera.ScreenPointToRay(pixel);

            Ray localRay = new Ray(previewRoot.transform.InverseTransformPoint(ray.origin), previewRoot.transform.InverseTransformDirection(ray.direction));

            if (!RayAABBIntersection(localRay, Vector3.zero, Vector3.one * VoxelChunk.ChunkSize, out float enter, out float exit)) return;

            Vector3 hitPoint = localRay.origin + localRay.direction * enter;
            int vx = Mathf.FloorToInt(hitPoint.x);
            int vy = Mathf.FloorToInt(hitPoint.y);
            int vz = Mathf.FloorToInt(hitPoint.z);

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
            if (previewChunk == null) return;
            Mesh m = VoxelMeshGenerator.GenerateMesh(previewChunk);
            if (previewMeshFilter != null)
            {
                previewMeshFilter.sharedMesh = m;
            }
            if (previewMeshCollider != null)
            {
                previewMeshCollider.sharedMesh = m;
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
            chunk.EnsureArray();
            for (int i = 0; i < chunk.voxels.Length; i++) chunk.voxels[i] = Voxel.Empty;
            RegeneratePreviewMesh();
        }

        void CreatePreviewObjects(int width, int height)
        {
            if (rt != null)
            {
                rt.Release();
                DestroyImmediate(rt);
            }

            rt = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR);
            rt.Create();

            if (previewRoot == null)
            {
                previewRoot = new GameObject("DeltalithPreviewRoot");
                previewRoot.hideFlags = HideFlags.HideAndDontSave;
            }

            if (previewChunk == null)
            {
                previewChunk = previewRoot.GetComponent<VoxelChunk>();
                if (previewChunk == null) previewChunk = previewRoot.AddComponent<VoxelChunk>();
            }

            previewMeshFilter = previewRoot.GetComponent<MeshFilter>();
            if (previewMeshFilter == null) previewMeshFilter = previewRoot.AddComponent<MeshFilter>();
            previewMeshRenderer = previewRoot.GetComponent<MeshRenderer>();
            if (previewMeshRenderer == null) previewMeshRenderer = previewRoot.AddComponent<MeshRenderer>();
            previewMeshCollider = previewRoot.GetComponent<MeshCollider>();
            if (previewMeshCollider == null) previewMeshCollider = previewRoot.AddComponent<MeshCollider>();

            if (previewMeshRenderer.sharedMaterial == null)
            {
                var mat = new Material(Shader.Find("Standard"));
                mat.enableInstancing = true;
                previewMeshRenderer.sharedMaterial = mat;
            }

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

            if (previewLight == null)
            {
                GameObject lightGo = new GameObject("DeltalithPreviewLight");
                lightGo.hideFlags = HideFlags.HideAndDontSave;
                previewLight = lightGo.AddComponent<Light>();
                previewLight.type = LightType.Directional;
                previewLight.intensity = 1.0f;
                previewLight.transform.rotation = Quaternion.Euler(50, -30, 0);
            }

            previewRoot.transform.position = Vector3.zero;
            previewRoot.transform.rotation = Quaternion.identity;
            previewRoot.transform.localScale = Vector3.one;

            camDistance = Mathf.Max(30f, VoxelChunk.ChunkSize * 2f);
            camTarget = new Vector3(VoxelChunk.ChunkSize * 0.5f, VoxelChunk.ChunkSize * 0.5f, VoxelChunk.ChunkSize * 0.5f);
            camEuler = new Vector3(30f, -45f, 0f);

            previewCamera.targetTexture = rt;
            previewCamera.aspect = (float)rt.width / rt.height;
        }

        void DestroyPreviewObjects()
        {
            if (previewCamera != null) DestroyImmediate(previewCamera.gameObject);
            if (previewLight != null) DestroyImmediate(previewLight.gameObject);
            if (previewRoot != null) DestroyImmediate(previewRoot);
            previewCamera = null;
            previewLight = null;
            previewRoot = null;
            previewChunk = null;
            previewMeshFilter = null;
            previewMeshRenderer = null;
            previewMeshCollider = null;
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
