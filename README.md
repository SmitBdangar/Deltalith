# Deltalith - Unity Voxel Studio

A high-performance, in-editor voxel modeling tool for Unity with optimized mesh generation and seamless workflow integration.

## Overview

Deltalith provides a complete voxel creation environment directly inside Unity Editor. Build cube-based 3D models, props, characters, and environments without leaving your workflow. The tool features intelligent mesh optimization, vertex color support, and instant asset generation.

## Key Features

### Editor Integration
- Dedicated 3D viewport with smooth camera controls
- Orbit, pan, and zoom navigation
- Real-time preview with lighting
- Interactive grid display for precise placement
- Isolated rendering system independent of Scene view

### Painting Tools
- Brush tool with adjustable size (1-8 voxels)
- Color picker with RGB support
- Material ID assignment for multi-material meshes
- Vertex color baking directly into mesh
- Eraser tool for quick removal
- Undo/redo support via Unity's undo system

### Color Management
- 16-slot customizable palette with persistence
- 12-slot recent colors history
- 16 preset colors for quick access
- Hex color input support
- Click-to-select from any palette
- Automatic color saving between sessions

### Mesh Generation
- Greedy meshing algorithm for optimal polygon count
- Automatic face culling removes hidden internal faces
- Submesh generation for material ID separation
- Vertex color support for per-voxel coloring
- Mesh collider generation for raycasting
- 32x32x32 chunk size (32,768 voxels per chunk)

### Smart Placement System
- First block must be placed on ground (y=0)
- Subsequent blocks require adjacency to existing voxels
- Face, edge, and corner adjacency detection
- Automatic snapping to 1x1x1 grid
- Build area constrained to chunk bounds
- Hover preview shows placement location

### Export Options
- Unity mesh asset (.asset) generation
- Ready for MeshFilter assignment
- Preserves vertex colors and materials
- Timestamped export naming
- Automatic folder creation
- Organized export directory structure

## Installation

### Via Unity Package Manager

1. Open Unity Package Manager (Window > Package Manager)
2. Click the '+' button and select "Add package from git URL"
3. Enter: `https://github.com/SmitBdangar/Deltalith.git`
4. Click 'Add'

### Manual Installation

1. Download or clone this repository
2. Copy the `Deltalith` folder into your project's `Packages` directory
3. Unity will automatically detect and import the package

## Requirements

- Unity 2020.3 or later
- .NET Standard 2.0 or higher
- No external dependencies required

## Getting Started

### Opening the Editor

1. Go to menu: `Deltalith > Voxel Creator`
2. The Deltalith Creator window will open
3. Click "New Chunk (Clear)" to start fresh

### Basic Workflow

1. Select a color from the palette or color picker
2. Adjust brush size if needed (default: 1)
3. Left-click in the viewport to place voxels
4. Right-click to erase voxels
5. Use right-drag to rotate camera
6. Use middle-drag to pan camera
7. Use scroll wheel to zoom
8. Click "Generate Mesh" to update preview
9. Click "Export Selected Chunk as Mesh Asset" to save

### Controls

| Action | Input |
|--------|-------|
| Place voxel | Left-click |
| Erase voxel | Right-click |
| Rotate camera | Right-drag |
| Pan camera | Middle-drag |
| Zoom camera | Scroll wheel |
| Undo | Ctrl+Z (Unity default) |

## Project Structure

```
Deltalith/
├── Runtime/
│   ├── Deltalith.Runtime.asmdef
│   ├── Voxel.cs                    # Voxel data structure
│   ├── VoxelChunk.cs               # Chunk container (32x32x32)
│   └── VoxelMeshGenerator.cs       # Greedy meshing algorithm
│
├── Editor/
│   ├── Deltalith.Editor.asmdef
│   ├── DeltalithCreatorWindow.cs   # Main editor window
│   └── DependencyChecker.cs        # Package validation
│
├── CHANGELOG.md
├── LICENSE
├── README.md
├── package.json
└── icon.png
```

## Technical Details

### Voxel Data Structure

Each voxel contains:
- Material ID (byte): 0 = empty, 1-255 = material
- Color (Color32): RGBA vertex color
- Position: Implicit from array index

### Chunk System

- Fixed size: 32x32x32 voxels
- 1D array storage for memory efficiency
- Index calculation: `x + size * (y + size * z)`
- Bounds checking on all operations

### Mesh Optimization

The greedy meshing algorithm:
1. Analyzes voxel grid in six directions
2. Groups adjacent faces with same material
3. Extends quads horizontally and vertically
4. Culls hidden internal faces
5. Generates submeshes per material ID
6. Bakes vertex colors into mesh

This typically reduces polygon count by 70-90% compared to naive per-voxel cubes.

### Rendering Pipeline

- Isolated preview world with dedicated camera
- RenderTexture-based viewport
- Standard shader with vertex color support
- Directional lighting for depth perception
- Transparent hover preview cube
- Grid mesh rendered with line topology

## Advanced Usage

### Custom Materials

The exported mesh uses Unity's built-in vertex color shader. To use custom materials:

1. Create a material with a shader that supports vertex colors
2. Assign to the MeshRenderer after export
3. Recommended shaders:
   - Legacy Shaders/VertexLit
   - Custom vertex color shader
   - Standard (with vertex color support)

### Batch Operations

For creating multiple chunks:

1. Build and export first chunk
2. Click "New Chunk (Clear)" to reset
3. Build next chunk
4. Export with unique timestamped name
5. Combine chunks in scene as needed

### Performance Tips

- Keep chunk size at 32x32x32 for optimal performance
- Use material IDs to organize submeshes
- Export frequently to avoid data loss
- Clear unused chunks to free memory
- Use the grid toggle to improve viewport FPS

## Limitations

- Maximum chunk size: 32x32x32 voxels
- No multi-chunk editing in single session
- First block must be placed at y=0
- All blocks must be connected (no floating voxels)
- Export supports Unity mesh assets only

## Troubleshooting

### Mesh not visible after export
- Ensure material supports vertex colors
- Check MeshRenderer has valid material assigned
- Verify mesh has non-zero vertex count

### Camera controls not working
- Check mouse is inside viewport area
- Ensure window has focus
- Try resetting camera with New Chunk

### Colors not showing correctly
- Use Legacy Shaders/VertexLit or compatible shader
- Ensure material base color is white
- Verify vertex colors are enabled in shader

### Build restrictions too limiting
- First block establishes ground level at y=0
- All subsequent blocks need neighbor connection
- This prevents floating geometry and ensures structural integrity

## Roadmap

### Planned Features
- Multi-chunk editing and management
- Layer system for complex models
- Selection and copy/paste tools
- Symmetry mode for mirrored editing
- Import existing meshes as voxels
- Animation support for voxel models
- Texture atlas generation
- FBX export support (requires com.unity.formats.fbx)
- OBJ export support

## Contributing

Contributions are welcome! Please follow these guidelines:

1. Fork the repository
2. Create a feature branch
3. Follow Unity C# coding conventions
4. Add XML documentation for public APIs
5. Test thoroughly in Unity 2020.3+
6. Submit a pull request with clear description

## License

MIT License - See LICENSE file for details

## Support

- Issues: [GitHub Issues](https://github.com/SmitBdangar/Deltalith/issues)
- Documentation: [Wiki](https://github.com/SmitBdangar/Deltalith/wiki)
- Author: Smit B Dangar

## Acknowledgments

Built for Unity Engine with performance and usability in mind. Inspired by voxel editors like MagicaVoxel and Qubicle, optimized for seamless Unity integration.
