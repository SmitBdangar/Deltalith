# Voxel Modeler for Unity

Simple Unity Editor extension to create voxel models (chunked) with greedy meshing and FBX export support.

## Features
- Chunked voxel storage (32³ default)
- Greedy meshing with submesh support by material ID
- SceneView brush for painting/removing voxels
- Export chunk(s) to FBX using Unity FBX Exporter package (com.unity.formats.fbx)
- Simple Editor window + toolbar button

## Install
1. In Unity: Window → Package Manager → "+" → Add package from git URL
2. Enter: `https://github.com/yourname/voxel-modeler.git`

Or copy `Runtime/` and `Editor/` into your project's `Assets/VoxelModeler/` folder.

## Usage
1. Window → Voxel Modeler to open the tool.
2. Create a new chunk, paint using SceneView brush (Voxel Brush button), or use the editor controls.
3. Click "Generate Mesh" then "Export FBX" (requires FBX Exporter package).

## Requirements
- Unity 2020.3 LTS or newer
- For FBX export: `com.unity.formats.fbx` (install via Package Manager)

## License
MIT
