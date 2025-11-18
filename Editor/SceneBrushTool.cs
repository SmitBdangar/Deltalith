using UnityEditor;
using UnityEngine;
using Deltalith.Runtime;

namespace Deltalith.Editor
{
    [InitializeOnLoad]
    public static class SceneBrushTool
    {
        static bool isEnabled = false;
        static Color brushColor = Color.white;
        static int brushMaterialId = 1;
        static int brushSize = 1;

        static SceneBrushTool()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        public static void SetBrushSettings(Color color, int materialId, int size)
        {
            brushColor = color;
            brushMaterialId = materialId;
            brushSize = Mathf.Max(1, size);
        }

        public static void Enable() => isEnabled = true;
        public static void Disable() => isEnabled = false;

        static void OnSceneGUI(SceneView sceneView)
        {
            if (!isEnabled) return;

            Event e = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            if (!Physics.Raycast(ray, out RaycastHit hit)) return;

            Handles.color = brushColor;
            Handles.DrawWireCube(hit.point, Vector3.one * brushSize);

            if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
            {
                TryPaintVoxel(hit, e.button == 0);
                e.Use();
            }
        }

        static void TryPaintVoxel(RaycastHit hit, bool paint)
        {
            VoxelChunk chunk = hit.collider.GetComponent<VoxelChunk>();
            if (!chunk) return;

            Vector3 local = chunk.transform.InverseTransformPoint(hit.point);
            int vx = Mathf.FloorToInt(local.x);
            int vy = Mathf.FloorToInt(local.y);
            int vz = Mathf.FloorToInt(local.z);

            Undo.RegisterCompleteObjectUndo(chunk, paint ? "Paint Voxel" : "Erase Voxel");

            for (int x = 0; x < brushSize; x++)
                for (int y = 0; y < brushSize; y++)
                    for (int z = 0; z < brushSize; z++)
                        chunk.SetVoxel(
                            vx + x,
                            vy + y,
                            vz + z,
                            paint ? new Voxel { id = (byte)brushMaterialId, color = brushColor } : Voxel.Empty
                        );

            Mesh mesh = VoxelMeshGenerator.GenerateMesh(chunk);
            chunk.ApplyMesh(mesh);
        }
    }
}
