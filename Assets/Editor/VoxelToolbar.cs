// Assets/Editor/VoxelToolbar.cs
using UnityEditor;
using UnityEngine;

namespace VoxelModeler.Editor {
    [InitializeOnLoad]
    public static class VoxelToolbar {
        static VoxelToolbar() {
            SceneView.duringSceneGui += OnGUI;
        }

        static void OnGUI(SceneView view) {
            Handles.BeginGUI();
            if (GUI.Button(new Rect(10, 10, 120, 22), "Voxel Brush")) {
                // toggle tool: the SceneBrushTool already has a toggle button; bring attention
                Debug.Log("Voxel Brush: toggle visible in SceneView (top-left).");
            }
            Handles.EndGUI();
        }
    }
}
