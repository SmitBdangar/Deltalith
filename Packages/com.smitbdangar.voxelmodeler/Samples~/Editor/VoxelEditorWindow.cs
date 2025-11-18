// Assets/Editor/VoxelEditorWindow.cs
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
        string defaultMaterialPath = "Assets/VoxelModeler/Materials/VoxelMaterial.mat";

        [MenuItem("Window/Voxel Modeler")]
        public static void OpenWindow() {
            GetWindow<VoxelEditorWindow>("Voxel Modeler");
        }

        void OnGUI() {
            GUILayout.Label("Voxel Modeler", EditorStyles.boldLabel);

            if (GUILayout.Button("Create New Chunk")) CreateChunk();

            currentChunk = EditorGUILayout.ObjectField("Chunk", currentChunk, typeof(VoxelChunk), true) as VoxelChunk;
            paintColor = EditorGUILayout.ColorField("Paint Color", paintColor);
            paintMaterialId = EditorGUILayout.IntField("Material ID", paintMaterialId);

            if (currentChunk != null) {
                if (GUILayout.Button("Clear Chunk")) {
                    Undo.RecordObject(currentChunk, "Clear Voxel Chunk");
                    ClearChunk(currentChunk);
                    EditorSceneManager.MarkSceneDirty(currentChunk.gameObject.scene);
                }

                if (GUILayout.Button("Random Fill (test)")) {
                    Undo.RecordObject(currentChunk, "Random Fill");
                    RandomFill(currentChunk);
                    EditorSceneManager.MarkSceneDirty(currentChunk.gameObject.scene);
                }

                if (GUILayout.Button("Generate Mesh (sync)")) {
                    Mesh mesh = VoxelMeshGenerator.GenerateMesh(currentChunk);
                    currentChunk.ApplyMesh(mesh);
                    EditorSceneManager.MarkSceneDirty(currentChunk.gameObject.scene);
                }

                if (GUILayout.Button("Export FBX")) {
                    ExportChunkToFbx(currentChunk);
                }
            }

            GUILayout.Space(10);
            EditorGUILayout.HelpBox("Use the Scene toolbar 'Voxel Brush' button to paint voxels in SceneView.", MessageType.Info);
        }

        void CreateChunk() {
            GameObject go = new GameObject("VoxelChunk");
            var vc = go.AddComponent<VoxelChunk>();
            var mf = go.GetComponent<MeshFilter>();
            var mr = go.GetComponent<MeshRenderer>();
            // try to assign default material if exists
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(defaultMaterialPath);
            if (mat != null) mr.sharedMaterial = mat;
            Selection.activeGameObject = go;
        }

        void ClearChunk(VoxelChunk chunk) {
            chunk.EnsureArray();
            for (int i = 0; i < chunk.voxels.Length; i++) chunk.voxels[i] = Voxel.Empty;
            chunk.ApplyMesh(null);
        }

        void RandomFill(VoxelChunk chunk) {
            chunk.EnsureArray();
            System.Random r = new System.Random();
            for (int x = 0; x < VoxelChunk.ChunkSize; x++) {
                for (int y = 0; y < VoxelChunk.ChunkSize; y++) {
                    for (int z = 0; z < VoxelChunk.ChunkSize; z++) {
                        if (r.NextDouble() < 0.12) {
                            Voxel v = new Voxel { id = (byte)UnityEngine.Random.Range(1, 4), color = paintColor };
                            chunk.SetVoxel(x, y, z, v);
                        } else {
                            chunk.SetVoxel(x, y, z, Voxel.Empty);
                        }
                    }
                }
            }
        }

        void ExportChunkToFbx(VoxelChunk chunk) {
            string path = EditorUtility.SaveFilePanel("Export FBX", Application.dataPath, chunk.name + ".fbx", "fbx");
            if (string.IsNullOrEmpty(path)) return;

            MeshFilter mf = chunk.GetComponent<MeshFilter>();
            MeshRenderer mr = chunk.GetComponent<MeshRenderer>();
            if (mf == null || mf.sharedMesh == null) {
                Debug.LogError("Chunk has no mesh. Generate mesh first.");
                return;
            }

            // create a temporary GameObject to hold mesh + renderer for export
            GameObject tmp = new GameObject(chunk.name + "_FBXExport");
            var mf2 = tmp.AddComponent<MeshFilter>();
            var mr2 = tmp.AddComponent<MeshRenderer>();
            mf2.sharedMesh = mf.sharedMesh;
            mr2.sharedMaterials = mr != null ? mr.sharedMaterials : new Material[] { };

#if UNITY_2018_3_OR_NEWER
            // Use FBX Exporter if available
            var type = System.Type.GetType("UnityEditor.Formats.Fbx.Exporter.ModelExporter, Unity.Formats.Fbx.Editor");
            if (type != null) {
                try {
                    var method = type.GetMethod("ExportObject", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    method.Invoke(null, new object[] { path, tmp });
                    Debug.Log($"Exported FBX to {path}");
                } catch (System.Exception e) {
                    Debug.LogError("FBX export failed: " + e.Message);
                }
            } else {
                Debug.LogError("FBX Exporter package not found. Install 'com.unity.formats.fbx' to enable FBX export.");
            }
#else
            Debug.LogError("FBX Export is supported on Unity 2018.3+ with the FBX package.");
#endif
            GameObject.DestroyImmediate(tmp);
        }
    }
}
