// Assets/Editor/DependencyChecker.cs
using UnityEditor;
using UnityEngine;
using System.Linq;
using System;

namespace VoxelModeler.Editor {
    public static class DependencyChecker {
        [InitializeOnLoadMethod]
        static void CheckDependencies() {
            // check for FBX exporter type
            bool found = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => {
                    try { return a.GetTypes(); } catch { return new Type[] { }; }
                })
                .Any(t => t.FullName != null && t.FullName.Contains("UnityEditor.Formats.Fbx.Exporter.ModelExporter"));

            if (!found) {
                Debug.LogWarning("[Voxel Modeler] 'com.unity.formats.fbx' not detected. Install via Package Manager to enable FBX export.");
            }
        }
    }
}
