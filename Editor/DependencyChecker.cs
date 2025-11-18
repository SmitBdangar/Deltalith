using UnityEditor;
using UnityEngine;
using System.Linq;
using System;

namespace Deltalith.Editor
{
    public static class DependencyChecker
    {
        [InitializeOnLoadMethod]
        static void CheckDependencies()
        {
            bool found = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch
                    {
                        return new Type[] { };
                    }
                })
                .Any(t =>
                    t.FullName != null &&
                    t.FullName.Contains("UnityEditor.Formats.Fbx.Exporter.ModelExporter"));

            if (!found)
            {
                Debug.LogWarning("[Deltalith] 'com.unity.formats.fbx' not detected. " +
                                 "FBX export will be unavailable. Install via Package Manager to enable FBX export.");
            }
        }
    }
}
