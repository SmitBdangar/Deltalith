using UnityEditor;
using UnityEngine;

namespace Deltalith.Editor
{
    [InitializeOnLoad]
    public static class VoxelToolbar
    {
        static VoxelToolbar()
        {
            SceneView.duringSceneGui += OnGUI;
        }

        static void OnGUI(SceneView view)
        {
            Handles.BeginGUI();

            if (GUI.Button(new Rect(10, 10, 140, 25), "Open Voxel Creator"))
            {
                VoxelEditorWindow.OpenWindow();
            }

            Handles.EndGUI();
        }
    }
}
