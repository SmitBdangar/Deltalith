using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using VoxelModeler.Runtime;
using System.IO;

namespace VoxelModeler.Editor {
    public class VoxelEditorWindow : EditorWindow {
        VoxelChunk currentChunk;
        Color paintColor = Color.white;
        int paintMaterialId = 1;
        int brushSize = 1;
        bool showAdvancedOptions = false;
        Vector2 scrollPosition;
        
        string exportFolderName = "Voxel Model";
        bool exportAsOBJ = true;
        bool exportAsFBX = true;
        bool exportAsPrefab = true;

        [MenuItem("Window/Voxel Creator")]
        public static void OpenWindow() {
            VoxelEditorWindow window = GetWindow<VoxelEditorWindow>("Voxel Creator");
            window.minSize = new Vector2(320, 500);
        }

        void OnGUI() {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            GUILayout.Space(10);
            GUILayout.Label("Voxel Creator", EditorStyles.largeLabel);
            GUILayout.Space(10);
            
            DrawSeparator();
            
            GUILayout.Label("Chunk Management", EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            if (GUILayout.Button("Create New Voxel Chunk", GUILayout.Height(30))) {
                CreateChunk();
            }
            
            GUILayout.Space(5);
            currentChunk = EditorGUILayout.ObjectField("Active Chunk", currentChunk, typeof(VoxelChunk), true) as VoxelChunk;
            
            if (currentChunk == null) {
                EditorGUILayout.HelpBox("Create or select a Voxel Chunk to start modeling.", MessageType.Info);
            }
            
            GUILayout.Space(10);
            DrawSeparator();
            
            GUILayout.Label("Painting Tools", EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            paintColor = EditorGUILayout.ColorField("Brush Color", paintColor);
            paintMaterialId = EditorGUILayout.IntSlider("Material ID", paintMaterialId, 1, 10);
            brushSize = EditorGUILayout.IntSlider("Brush Size", brushSize, 1, 5);
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(5);
            EditorGUILayout.HelpBox("Toggle 'Voxel Brush' in SceneView:\n• Left-click to paint voxels\n• Right-click to erase voxels", MessageType.Info);
            
            if (currentChunk != null) {
                GUILayout.Space(10);
                DrawSeparator();
                
                GUILayout.Label("Quick Actions", EditorStyles.boldLabel);
                GUILayout.Space(5);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Clear All", GUILayout.Height(25))) {
                    if (EditorUtility.DisplayDialog("Clear Chunk", "Clear all voxels?", "Yes", "Cancel")) {
                        Undo.RecordObject(currentChunk, "Clear Voxel Chunk");
                        ClearChunk(currentChunk);
                        EditorSceneManager.MarkSceneDirty(currentChunk.gameObject.scene);
                    }
                }
                
                if (GUILayout.Button("Generate Mesh", GUILayout.Height(25))) {
                    Mesh mesh = VoxelMeshGenerator.GenerateMesh(currentChunk);
                    currentChunk.ApplyMesh(mesh);
                    EditorSceneManager.MarkSceneDirty(currentChunk.gameObject.scene);
                    Debug.Log("Mesh generated successfully!");
                }
                EditorGUILayout.EndHorizontal();
                
                GUILayout.Space(5);
                
                showAdvancedOptions = EditorGUILayout.Foldout(showAdvancedOptions, "Advanced Options", true);
                if (showAdvancedOptions) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    
                    if (GUILayout.Button("Random Fill (Test)", GUILayout.Height(22))) {
                        Undo.RecordObject(currentChunk, "Random Fill");
                        RandomFill(currentChunk);
                        EditorSceneManager.MarkSceneDirty(currentChunk.gameObject.scene);
                    }
                    
                    if (GUILayout.Button("Fill Box (5x5x5)", GUILayout.Height(22))) {
                        Undo.RecordObject(currentChunk, "Fill Box");
                        FillBox(currentChunk, 0, 0, 0, 5, 5, 5);
                        EditorSceneManager.MarkSceneDirty(currentChunk.gameObject.scene);
                    }
                    
                    EditorGUILayout.EndVertical();
                    EditorGUI.indentLevel--;
                }
                
                GUILayout.Space(10);
                DrawSeparator();
                
                GUILayout.Label("Export Options", EditorStyles.boldLabel);
                GUILayout.Space(5);
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                exportFolderName = EditorGUILayout.TextField("Export Folder", exportFolderName);
                GUILayout.Space(3);
                exportAsFBX = EditorGUILayout.ToggleLeft("Export as FBX", exportAsFBX);
                exportAsOBJ = EditorGUILayout.ToggleLeft("Export as OBJ", exportAsOBJ);
                exportAsPrefab = EditorGUILayout.ToggleLeft("Save as Prefab", exportAsPrefab);
                EditorGUILayout.EndVertical();
                
                GUILayout.Space(5);
                
                if (GUILayout.Button("EXPORT VOXEL MODEL", GUILayout.Height(35))) {
                    ExportVoxelModel(currentChunk);
                }
                
                GUILayout.Space(5);
                
                string exportPath = Path.Combine("Assets", exportFolderName);
                EditorGUILayout.HelpBox($"Files will be saved to: {exportPath}", MessageType.Info);
            }
            
            GUILayout.Space(10);
            
            EditorGUILayout.EndScrollView();
        }

        void DrawSeparator() {
            GUILayout.Space(5);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Space(5);
        }

        void CreateChunk() {
            GameObject go = new GameObject("VoxelChunk");
            var vc = go.AddComponent<VoxelChunk>();
            var mf = go.GetComponent<MeshFilter>();
            var mr = go.GetComponent<MeshRenderer>();
            var mc = go.GetComponent<MeshCollider>();
            
            string materialPath = "Assets/VoxelModeler/Materials";
            string matFile = Path.Combine(materialPath, "VoxelMaterial.mat");
            
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matFile);
            if (mat == null) {
                if (!AssetDatabase.IsValidFolder("Assets/VoxelModeler")) {
                    AssetDatabase.CreateFolder("Assets", "VoxelModeler");
                }
                if (!AssetDatabase.IsValidFolder(materialPath)) {
                    AssetDatabase.CreateFolder("Assets/VoxelModeler", "Materials");
                }
                
                mat = new Material(Shader.Find("Standard"));
                mat.color = Color.white;
                AssetDatabase.CreateAsset(mat, matFile);
                AssetDatabase.SaveAssets();
            }
            
            mr.sharedMaterial = mat;
            
            Selection.activeGameObject = go;
            currentChunk = vc;
            Undo.RegisterCreatedObjectUndo(go, "Create Voxel Chunk");
            
            Debug.Log("Voxel Chunk created successfully!");
        }

        void ClearChunk(VoxelChunk chunk) {
            chunk.EnsureArray();
            for (int i = 0; i < chunk.voxels.Length; i++) {
                chunk.voxels[i] = Voxel.Empty;
            }
            chunk.ApplyMesh(null);
        }

        void RandomFill(VoxelChunk chunk) {
            chunk.EnsureArray();
            System.Random r = new System.Random();
            for (int x = 0; x < VoxelChunk.ChunkSize; x++) {
                for (int y = 0; y < VoxelChunk.ChunkSize; y++) {
                    for (int z = 0; z < VoxelChunk.ChunkSize; z++) {
                        if (r.NextDouble() < 0.12) {
                            Voxel v = new Voxel { 
                                id = (byte)Random.Range(1, 4), 
                                color = paintColor 
                            };
                            chunk.SetVoxel(x, y, z, v);
                        }
                    }
                }
            }
            Mesh mesh = VoxelMeshGenerator.GenerateMesh(chunk);
            chunk.ApplyMesh(mesh);
        }

        void FillBox(VoxelChunk chunk, int startX, int startY, int startZ, int width, int height, int depth) {
            chunk.EnsureArray();
            for (int x = startX; x < startX + width && x < VoxelChunk.ChunkSize; x++) {
                for (int y = startY; y < startY + height && y < VoxelChunk.ChunkSize; y++) {
                    for (int z = startZ; z < startZ + depth && z < VoxelChunk.ChunkSize; z++) {
                        Voxel v = new Voxel { 
                            id = (byte)paintMaterialId, 
                            color = paintColor 
                        };
                        chunk.SetVoxel(x, y, z, v);
                    }
                }
            }
            Mesh mesh = VoxelMeshGenerator.GenerateMesh(chunk);
            chunk.ApplyMesh(mesh);
        }

        void ExportVoxelModel(VoxelChunk chunk) {
            MeshFilter mf = chunk.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) {
                EditorUtility.DisplayDialog("Export Error", "No mesh found! Generate mesh first.", "OK");
                return;
            }

            string folderPath = Path.Combine("Assets", exportFolderName);
            if (!AssetDatabase.IsValidFolder(folderPath)) {
                AssetDatabase.CreateFolder("Assets", exportFolderName);
                Debug.Log($"Created folder: {folderPath}");
            }

            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string baseName = $"{chunk.name}_{timestamp}";
            bool exported = false;

            if (exportAsFBX) {
                string fbxPath = Path.Combine(folderPath, baseName + ".fbx");
                if (ExportAsFBX(chunk, fbxPath)) {
                    Debug.Log($"Exported FBX: {fbxPath}");
                    exported = true;
                }
            }

            if (exportAsOBJ) {
                string objPath = Path.Combine(folderPath, baseName + ".obj");
                if (ExportAsOBJ(chunk, objPath)) {
                    Debug.Log($"Exported OBJ: {objPath}");
                    exported = true;
                }
            }

            if (exportAsPrefab) {
                string prefabPath = Path.Combine(folderPath, baseName + ".prefab");
                PrefabUtility.SaveAsPrefabAsset(chunk.gameObject, prefabPath);
                Debug.Log($"Saved Prefab: {prefabPath}");
                exported = true;
            }

            AssetDatabase.Refresh();

            if (exported) {
                EditorUtility.DisplayDialog("Export Successful", 
                    $"Voxel model exported to:\n{folderPath}", "OK");
                EditorUtility.RevealInFinder(folderPath);
            } else {
                EditorUtility.DisplayDialog("Export Error", 
                    "No export format selected!", "OK");
            }
        }

        bool ExportAsFBX(VoxelChunk chunk, string path) {
            MeshFilter mf = chunk.GetComponent<MeshFilter>();
            MeshRenderer mr = chunk.GetComponent<MeshRenderer>();

            GameObject tmp = new GameObject(chunk.name + "_FBXExport");
            var mf2 = tmp.AddComponent<MeshFilter>();
            var mr2 = tmp.AddComponent<MeshRenderer>();
            mf2.sharedMesh = mf.sharedMesh;
            mr2.sharedMaterials = mr != null ? mr.sharedMaterials : new Material[] { };

#if UNITY_2018_3_OR_NEWER
            var type = System.Type.GetType("UnityEditor.Formats.Fbx.Exporter.ModelExporter, Unity.Formats.Fbx.Editor");
            if (type != null) {
                try {
                    var method = type.GetMethod("ExportObject", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    method.Invoke(null, new object[] { path, tmp });
                    GameObject.DestroyImmediate(tmp);
                    return true;
                } catch (System.Exception e) {
                    Debug.LogError("FBX export failed: " + e.Message);
                    GameObject.DestroyImmediate(tmp);
                    return false;
                }
            } else {
                Debug.LogWarning("FBX Exporter not found. Install 'com.unity.formats.fbx' for FBX export.");
                GameObject.DestroyImmediate(tmp);
                return false;
            }
#else
            Debug.LogError("FBX Export requires Unity 2018.3+");
            GameObject.DestroyImmediate(tmp);
            return false;
#endif
        }

        bool ExportAsOBJ(VoxelChunk chunk, string path) {
            MeshFilter mf = chunk.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return false;

            Mesh mesh = mf.sharedMesh;
            
            using (StreamWriter sw = new StreamWriter(path)) {
                sw.WriteLine("# Voxel Model OBJ Export");
                sw.WriteLine($"# Generated: {System.DateTime.Now}");
                sw.WriteLine();

                foreach (Vector3 v in mesh.vertices) {
                    sw.WriteLine($"v {v.x} {v.y} {v.z}");
                }

                foreach (Vector3 n in mesh.normals) {
                    sw.WriteLine($"vn {n.x} {n.y} {n.z}");
                }

                foreach (Vector2 uv in mesh.uv) {
                    sw.WriteLine($"vt {uv.x} {uv.y}");
                }

                for (int s = 0; s < mesh.subMeshCount; s++) {
                    sw.WriteLine($"\n# Submesh {s}");
                    int[] triangles = mesh.GetTriangles(s);
                    for (int i = 0; i < triangles.Length; i += 3) {
                        int v1 = triangles[i] + 1;
                        int v2 = triangles[i + 1] + 1;
                        int v3 = triangles[i + 2] + 1;
                        sw.WriteLine($"f {v1}/{v1}/{v1} {v2}/{v2}/{v2} {v3}/{v3}/{v3}");
                    }
                }
            }

            return true;
        }
    }
}
