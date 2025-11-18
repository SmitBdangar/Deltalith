using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Deltalith.Runtime;
using System.IO;

namespace Deltalith.Editor {
    public class VoxelEditorWindow : EditorWindow {
        VoxelChunk currentChunk;
        Color paintColor = Color.white;
        int paintMaterialId = 1;
        int brushSize = 1;

        bool showAdvancedOptions = false;
        Vector2 scrollPosition;

        string exportFolderName = "Deltalith Models";
        bool exportAsOBJ = true;
        bool exportAsFBX = true;
        bool exportAsPrefab = true;

        [MenuItem("Deltalith/Voxel Creator")]
        public static void OpenWindow() {
            VoxelEditorWindow window = GetWindow<VoxelEditorWindow>("Deltalith Creator");
            window.minSize = new Vector2(320, 500);
        }

        void OnGUI() {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            GUILayout.Label("Deltalith Voxel Creator", EditorStyles.largeLabel);

            DrawSeparator();

            // Chunk tools
            GUILayout.Label("Chunk Management", EditorStyles.boldLabel);

            if (GUILayout.Button("Create New Voxel Chunk", GUILayout.Height(30)))
                CreateChunk();

            currentChunk = EditorGUILayout.ObjectField("Active Chunk", currentChunk, typeof(VoxelChunk), true) as VoxelChunk;

            DrawSeparator();

            // Painting
            GUILayout.Label("Painting Tools", EditorStyles.boldLabel);

            paintColor = EditorGUILayout.ColorField("Brush Color", paintColor);
            paintMaterialId = EditorGUILayout.IntSlider("Material ID", paintMaterialId, 1, 10);
            brushSize = EditorGUILayout.IntSlider("Brush Size", brushSize, 1, 5);

            SceneBrushTool.SetBrushSettings(paintColor, paintMaterialId, brushSize);

            DrawSeparator();

            if (currentChunk) DrawChunkTools();

            EditorGUILayout.EndScrollView();
        }

        void DrawChunkTools() {
            GUILayout.Label("Quick Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Clear All"))
                DoAndUndo(currentChunk, "Clear Chunk", () => ClearChunk(currentChunk));

            if (GUILayout.Button("Generate Mesh"))
                DoAndUndo(currentChunk, "Generate Mesh", () => {
                    Mesh mesh = VoxelMeshGenerator.GenerateMesh(currentChunk);
                    currentChunk.ApplyMesh(mesh);
                });

            EditorGUILayout.EndHorizontal();

            showAdvancedOptions = EditorGUILayout.Foldout(showAdvancedOptions, "Advanced Tools");

            if (showAdvancedOptions) {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                if (GUILayout.Button("Random Fill"))
                    DoAndUndo(currentChunk, "Random Fill", () => RandomFill(currentChunk));

                if (GUILayout.Button("Fill Box (5x5x5)"))
                    DoAndUndo(currentChunk, "Fill Box", () => FillBox(currentChunk, 0, 0, 0, 5, 5, 5));

                EditorGUILayout.EndVertical();
            }

            DrawSeparator();
            DrawExportSection();
        }

        void DrawExportSection() {
            GUILayout.Label("Export Options", EditorStyles.boldLabel);

            exportFolderName = EditorGUILayout.TextField("Export Folder", exportFolderName);
            exportAsFBX = EditorGUILayout.ToggleLeft("Export as FBX", exportAsFBX);
            exportAsOBJ = EditorGUILayout.ToggleLeft("Export as OBJ", exportAsOBJ);
            exportAsPrefab = EditorGUILayout.ToggleLeft("Save as Prefab", exportAsPrefab);

            if (GUILayout.Button("EXPORT VOXEL MODEL", GUILayout.Height(35)))
                ExportVoxelModel(currentChunk);
        }

        void DrawSeparator() {
            GUILayout.Space(5);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }

        // -------------------------
        // Chunk Creation
        // -------------------------
        void CreateChunk() {
            GameObject go = new GameObject("DeltalithChunk");
            var vc = go.AddComponent<VoxelChunk>();

            string matPath = "Assets/Deltalith/Materials/DefaultVoxel.mat";
            Directory.CreateDirectory("Assets/Deltalith/Materials");

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!mat) {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, matPath);
            }

            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshCollider>();

            Undo.RegisterCreatedObjectUndo(go, "Create Voxel Chunk");
            Selection.activeObject = go;
            currentChunk = vc;
        }

        // -------------------------
        // Chunk Editing Operations
        // -------------------------
        void ClearChunk(VoxelChunk chunk) {
            chunk.EnsureArray();
            for (int i = 0; i < chunk.voxels.Length; i++)
                chunk.voxels[i] = Voxel.Empty;

            chunk.ApplyMesh(null);
        }

        void RandomFill(VoxelChunk chunk) {
            chunk.EnsureArray();
            System.Random r = new System.Random();

            for (int x = 0; x < VoxelChunk.ChunkSize; x++)
                for (int y = 0; y < VoxelChunk.ChunkSize; y++)
                    for (int z = 0; z < VoxelChunk.ChunkSize; z++)
                        if (r.NextDouble() < 0.12)
                            chunk.SetVoxel(x, y, z, new Voxel {
                                id = (byte)Random.Range(1, 4),
                                color = paintColor
                            });

            Mesh mesh = VoxelMeshGenerator.GenerateMesh(chunk);
            chunk.ApplyMesh(mesh);
        }

        void FillBox(VoxelChunk chunk, int x0, int y0, int z0, int w, int h, int d) {
            chunk.EnsureArray();

            for (int x = x0; x < x0 + w; x++)
                for (int y = y0; y < y0 + h; y++)
                    for (int z = z0; z < z0 + d; z++)
                        chunk.SetVoxel(x, y, z, new Voxel {
                            id = (byte)paintMaterialId,
                            color = paintColor
                        });

            Mesh mesh = VoxelMeshGenerator.GenerateMesh(chunk);
            chunk.ApplyMesh(mesh);
        }

        // -------------------------
        // Undo Wrapper
        // -------------------------
        void DoAndUndo(VoxelChunk chunk, string name, System.Action action) {
            Undo.RegisterCompleteObjectUndo(chunk, name);

            var mf = chunk.GetComponent<MeshFilter>();
            var mc = chunk.GetComponent<MeshCollider>();
            var mr = chunk.GetComponent<MeshRenderer>();

            if (mf) Undo.RegisterCompleteObjectUndo(mf, name);
            if (mc) Undo.RegisterCompleteObjectUndo(mc, name);
            if (mr) Undo.RegisterCompleteObjectUndo(mr, name);

            action.Invoke();

            EditorSceneManager.MarkSceneDirty(chunk.gameObject.scene);
            EditorUtility.SetDirty(chunk);
        }

        // -------------------------
        // Export
        // -------------------------
        void ExportVoxelModel(VoxelChunk chunk) {
            if (!chunk || chunk.GetComponent<MeshFilter>().sharedMesh == null) {
                EditorUtility.DisplayDialog("Error", "Generate mesh first.", "OK");
                return;
            }

            // Folder validation
            char[] invalid = Path.GetInvalidFileNameChars();
            if (exportFolderName.IndexOfAny(invalid) >= 0) {
                EditorUtility.DisplayDialog("Invalid Name", "Folder name contains invalid characters.", "OK");
                return;
            }

            string folder = $"Assets/{exportFolderName}";
            if (!AssetDatabase.IsValidFolder(folder))
                Directory.CreateDirectory(folder);

            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string baseName = $"{chunk.name}_{timestamp}";

            if (exportAsOBJ)
                ExportAsOBJ(chunk, Path.Combine(folder, baseName + ".obj"));

            if (exportAsFBX)
                ExportAsFBX(chunk, Path.Combine(folder, baseName + ".fbx"));

            if (exportAsPrefab)
                PrefabUtility.SaveAsPrefabAsset(chunk.gameObject, Path.Combine(folder, baseName + ".prefab"));

            AssetDatabase.Refresh();
        }

        bool ExportAsOBJ(VoxelChunk chunk, string path) {
            Mesh mesh = chunk.GetComponent<MeshFilter>().sharedMesh;
            using (StreamWriter sw = new StreamWriter(path)) {
                sw.WriteLine("# Deltalith OBJ Export");
                foreach (var v in mesh.vertices) sw.WriteLine($"v {v.x} {v.y} {v.z}");
                foreach (var n in mesh.normals) sw.WriteLine($"vn {n.x} {n.y} {n.z}");
                foreach (var uv in mesh.uv) sw.WriteLine($"vt {uv.x} {uv.y}");
                int[] tri = mesh.triangles;
                for (int i = 0; i < tri.Length; i += 3)
                    sw.WriteLine($"f {tri[i]+1}/{tri[i]+1}/{tri[i]+1} {tri[i+1]+1}/{tri[i+1]+1}/{tri[i+1]+1} {tri[i+2]+1}/{tri[i+2]+1}/{tri[i+2]+1}");
            }
            return true;
        }

        bool ExportAsFBX(VoxelChunk chunk, string path) {
#if UNITY_2018_3_OR_NEWER
            var type = System.Type.GetType("UnityEditor.Formats.Fbx.Exporter.ModelExporter, Unity.Formats.Fbx.Editor");
            if (type == null) return false;

            GameObject tmp = GameObject.Instantiate(chunk.gameObject);

            try {
                var method = type.GetMethod("ExportObject");
                method.Invoke(null, new object[] { path, tmp });
                return true;
            }
            finally {
                GameObject.DestroyImmediate(tmp);
            }
#else
            return false;
#endif
        }
    }
}
