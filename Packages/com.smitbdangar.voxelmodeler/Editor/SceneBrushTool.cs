// Assets/Editor/SceneBrushTool.cs
using UnityEditor;
using UnityEngine;
using VoxelModeler.Runtime;

namespace VoxelModeler.Editor {
    [InitializeOnLoad]
    public static class SceneBrushTool {
        static bool toolActive = false;
        static Color brushColor = Color.white;
        static int brushMaterialId = 1;

        static SceneBrushTool() {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        static void OnSceneGUI(SceneView sv) {
            Handles.BeginGUI();
            if (GUI.Button(new Rect(10, 40, 120, 22), "Voxel Brush Toggle")) {
                toolActive = !toolActive;
            }
            Handles.EndGUI();

            if (!toolActive) return;

            Event e = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f)) {
                var chunk = hit.collider.GetComponentInParent<VoxelChunk>();
                if (chunk != null) {
                    Vector3 localHit = chunk.transform.InverseTransformPoint(hit.point);
                    Vector3 localNormal = chunk.transform.InverseTransformDirection(hit.normal);

                    // FIXED: Use normal to determine correct voxel position
                    // For painting: add voxel adjacent to hit surface
                    Vector3 paintPoint = localHit + localNormal * 0.1f;
                    int px = Mathf.FloorToInt(paintPoint.x);
                    int py = Mathf.FloorToInt(paintPoint.y);
                    int pz = Mathf.FloorToInt(paintPoint.z);

                    // For erasing: remove the voxel we actually hit
                    Vector3 erasePoint = localHit - localNormal * 0.1f;
                    int ex = Mathf.FloorToInt(erasePoint.x);
                    int ey = Mathf.FloorToInt(erasePoint.y);
                    int ez = Mathf.FloorToInt(erasePoint.z);

                    // Visualize paint position
                    Vector3 cubeCenter = chunk.transform.TransformPoint(new Vector3(px + 0.5f, py + 0.5f, pz + 0.5f));
                    Handles.color = new Color(0f, 1f, 0f, 0.3f);
                    Handles.DrawWireCube(cubeCenter, Vector3.one);

                    // Left click: paint
                    if (e.type == EventType.MouseDown && e.button == 0) {
                        Undo.RecordObject(chunk, "Paint Voxel");
                        Voxel v = new Voxel { id = (byte)brushMaterialId, color = brushColor };
                        chunk.SetVoxel(px, py, pz, v);
                        Mesh m = VoxelMeshGenerator.GenerateMesh(chunk);
                        chunk.ApplyMesh(m);
                        EditorUtility.SetDirty(chunk);
                        e.Use();
                    } 
                    // Right click: erase
                    else if (e.type == EventType.MouseDown && e.button == 1) {
                        Undo.RecordObject(chunk, "Erase Voxel");
                        chunk.SetVoxel(ex, ey, ez, Voxel.Empty);
                        Mesh m = VoxelMeshGenerator.GenerateMesh(chunk);
                        chunk.ApplyMesh(m);
                        EditorUtility.SetDirty(chunk);
                        e.Use();
                    }
                }
            }
        }
    }
}