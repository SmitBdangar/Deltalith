// Assets/Editor/SceneBrushTool.cs
using UnityEditor;
using UnityEngine;
using VoxelModeler.Runtime;

namespace VoxelModeler.Editor {
    // Simple SceneView brush to paint/remove single voxels by raycast to chunk surface.
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
            // small floating button in top-left
            if (GUI.Button(new Rect(10, 40, 120, 22), "Voxel Brush Toggle")) {
                toolActive = !toolActive;
            }
            Handles.EndGUI();

            if (!toolActive) return;

            Event e = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f)) {
                // try to find VoxelChunk on hit collider's gameobject or its parents
                var chunk = hit.collider.GetComponentInParent<VoxelChunk>();
                if (chunk != null) {
                    // visualize hovered voxel
                    Vector3 localHit = chunk.transform.InverseTransformPoint(hit.point);
                    // round to nearest voxel (assumes voxel size = 1 unit)
                    int hx = Mathf.FloorToInt(localHit.x + 0.5f);
                    int hy = Mathf.FloorToInt(localHit.y + 0.5f);
                    int hz = Mathf.FloorToInt(localHit.z + 0.5f);

                    Vector3 cubeCenter = chunk.transform.TransformPoint(new Vector3(hx, hy, hz));
                    Handles.color = new Color(1f, 1f, 1f, 0.25f);
                    Handles.DrawSolidRectangleWithOutline(new Vector3[] {
                        cubeCenter + new Vector3(-0.5f,-0.5f,-0.5f),
                        cubeCenter + new Vector3(0.5f,-0.5f,-0.5f),
                        cubeCenter + new Vector3(0.5f,0.5f,-0.5f),
                        cubeCenter + new Vector3(-0.5f,0.5f,-0.5f)
                    }, new Color(1,1,1,0.05f), Color.white);

                    // left click paint, right click erase
                    if (e.type == EventType.MouseDown && e.button == 0) {
                        Undo.RecordObject(chunk, "Paint Voxel");
                        Voxel v = new Voxel { id = (byte)brushMaterialId, color = brushColor };
                        chunk.SetVoxel(hx, hy, hz, v);
                        // regenerate mesh immediately for simplicity
                        Mesh m = VoxelMeshGenerator.GenerateMesh(chunk);
                        chunk.ApplyMesh(m);
                        EditorUtility.SetDirty(chunk);
                        e.Use();
                    } else if (e.type == EventType.MouseDown && e.button == 1) {
                        Undo.RecordObject(chunk, "Erase Voxel");
                        chunk.SetVoxel(hx, hy, hz, Voxel.Empty);
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
