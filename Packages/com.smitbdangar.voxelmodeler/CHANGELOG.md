# Changelog

## [0.1.0] - 2025-01-XX

### Added
- Initial release
- Greedy mesh generation for voxel chunks (32x32x32)
- Scene view brush tool for painting and erasing voxels
- Editor window with chunk management tools
- FBX export support (requires com.unity.formats.fbx package)
- Per-voxel color and material ID support
- Submesh generation for different material types
- Undo/redo support for all voxel operations

### Technical Features
- Optimized greedy meshing algorithm
- Automatic mesh collider generation for raycasting
- Runtime and editor assembly definitions
- MIT License

### Requirements
- Unity 2020.3 or later
- Optional: com.unity.formats.fbx 4.1.0+ for FBX export

### Usage
1. Open Window > Voxel Modeler
2. Click "Create New Chunk" to add a voxel chunk to your scene
3. Click "Voxel Brush Toggle" in the SceneView
4. Left-click to paint voxels, right-click to erase
5. Generate mesh to update the visual representation