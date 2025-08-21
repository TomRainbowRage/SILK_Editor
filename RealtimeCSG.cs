using System.Drawing;
using System.Net.Sockets;
using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using static Gizmo;

public class RealtimeCSG
{
    private static GL Gl { get { return MainScript.Gl; } }

    static List<BillboardRenderer.Billboard> PointerBillboards = new List<BillboardRenderer.Billboard>();
    static List<BillboardRenderer.Billboard> EditModeBillboards = new List<BillboardRenderer.Billboard>();
    static List<(Vector3 start, Vector3 end, Vector3 color, float thickness)> EditModeLines = new List<(Vector3 start, Vector3 end, Vector3 color, float thickness)>();
    private static List<(Vector3[] vertices, Vector4 color, ActiveAxis axis, bool useDepthTest)> PointerQuads =
    new List<(Vector3[] vertices, Vector4 color, ActiveAxis axis, bool useDepthTest)>();

    private static BillboardRenderer.Billboard pointer1Billboard;
    private static bool showPointer1;
    private static BillboardRenderer.Billboard pointer2Billboard;
    private static bool showPointer2;

    private static BillboardRenderer.Billboard verticBillboard;
    private static BillboardRenderer.Billboard sideBillboard;

    private static Vector3 pointer1Normal;

    private static float generationAxisStart;

    private const float MIN_BRUSH_DIMENSION = 0.1f;


    //private static Vector3[] draggingVerts = new Vector3[0];
    private static List<(int vertIndex, Vector3 vertOffset)> draggingVerts = new List<(int vertIndex, Vector3 vertOffset)>();


    static (Vector3[] vertices, Vector4 color, ActiveAxis axis, bool useDepthTest) SelectQuad =
    new(new Vector3[4], new Vector4(0, 0, 1, 0.5f), ActiveAxis.None, true);

    static (Vector3 start, Vector3 end, Vector3 color, float thickness) EditModeWireframeLine =
    new(Vector3.Zero, Vector3.Zero, new Vector3(0.588f, 0.937f, 1), 1);


    static Brush previewBrush = null;

    public static List<(int, int, int, int)> selectedSurfacesIndices = new List<(int, int, int, int)>();

    static (Vector3[] vertices, Vector4 color, ActiveAxis axis, bool useDepthTest) surfaceHoverQuad =
    new(null, new Vector4(1, 1, 1, 0.25f), ActiveAxis.None, false);
    private static List<(Vector3[] vertices, Vector4 color, ActiveAxis axis, bool useDepthTest)> surfaceSelectQuads =
    new List<(Vector3[] vertices, Vector4 color, ActiveAxis axis, bool useDepthTest)>();


    public static GenerateionState generationState = GenerateionState.HoverStart;

    public enum GenerateionState { HoverStart, DraggingPoints, Extrude }

    public static void Load()
    {
        pointer1Billboard = new BillboardRenderer.Billboard(Vector3.Zero, Vector2.One * 20, new Vector4(1, 1, 0, 1));
        pointer2Billboard = new BillboardRenderer.Billboard(Vector3.Zero, Vector2.One * 20, new Vector4(1, 1, 0, 1));

        verticBillboard = new BillboardRenderer.Billboard(Vector3.Zero, Vector2.One * 20, new Vector4(0, 0, 1, 1), 0, false);
        sideBillboard = new BillboardRenderer.Billboard(Vector3.Zero, Vector2.One * 20, new Vector4(1, 1, 0, 1), 0, false);

        PointerBillboards.Clear();

        PointerBillboards.Add(pointer1Billboard);
        PointerBillboards.Add(pointer2Billboard);

        PointerQuads.Clear();
        PointerQuads.Add(SelectQuad);

        MainScript.mouse.MouseMove += MouseMove;
        MainScript.mouse.MouseDown += MouseDown;
        MainScript.mouse.MouseUp += MouseUp;

        MainScript.window.Update += Update;
        MainScript.keyboard.KeyDown += KeyDown;
    }

    public static void Render(double delta, Matrix4x4 projection, Matrix4x4 view)
    {
        if (EditorMain.Mode == EditMode.Generate)
        {
            UpdatePointerLines();


            PointerBillboards.Clear();
            if (showPointer1) { PointerBillboards.Add(pointer1Billboard); }
            if (showPointer2) { PointerBillboards.Add(pointer2Billboard); }

            PointerQuads.Clear();
            PointerQuads.Add(SelectQuad);

            if (generationState != GenerateionState.HoverStart)
            {
                QuadRenderer.RenderQuads(projection, view, PointerQuads);
            }
        }
        else if (EditorMain.Mode == EditMode.Edit)
        {
            UpdateEditModeVisuals();
        }
        else if (EditorMain.Mode == EditMode.Surface)
        {
            if (surfaceHoverQuad.vertices != null)
            {
                QuadRenderer.RenderQuads(projection, view, new List<(Vector3[] vertices, Vector4 color, ActiveAxis axis, bool useDepthTest)>() { surfaceHoverQuad });
            }

            QuadRenderer.RenderQuads(projection, view, surfaceSelectQuads);
        }
    }

    public static void RenderAfterGizmo(double delta, Matrix4x4 projection, Matrix4x4 view)
    {
        if (EditorMain.Mode == EditMode.Generate)
        {
            BillboardRenderer.Render(projection, view, PointerBillboards);
        }
        else if (EditorMain.Mode == EditMode.Edit)
        {
            ThickLineRenderer.Render(projection, view, EditModeLines);
            BillboardRenderer.Render(projection, view, EditModeBillboards);
        }
    }

    public static void Update(double delta)
    {
        if (EditorMain.Mode == EditMode.Generate)
        {
            MainScript.debugText["GenerationMode"] = "GenerationMode: " + generationState;

            if (Gizmo.DraggingAxis != ActiveAxis.None)
            {
                Vector3 unsnappedPosition = Gizmo.virtualPosition;

                // Only snap along the currently active axis
                Vector3 snappedPos = unsnappedPosition;

                if (generationState == GenerateionState.Extrude)
                {
                    // Determine which axis we're moving along
                    if (Gizmo.DraggingAxis == ActiveAxis.X)
                    {
                        // Snap only the X coordinate to grid corner
                        float snappedX = SnapAxisToGridCorner(unsnappedPosition.X, EditorMain.snappingTranslate);
                        snappedPos.X = snappedX;
                    }
                    else if (Gizmo.DraggingAxis == ActiveAxis.Y)
                    {
                        // Snap only the Y coordinate to grid corner
                        float snappedY = SnapAxisToGridCorner(unsnappedPosition.Y, EditorMain.snappingTranslate);
                        snappedPos.Y = snappedY;
                    }
                    else if (Gizmo.DraggingAxis == ActiveAxis.Z)
                    {
                        // Snap only the Z coordinate to grid corner
                        float snappedZ = SnapAxisToGridCorner(unsnappedPosition.Z, EditorMain.snappingTranslate);
                        snappedPos.Z = snappedZ;
                    }

                    UpdatePreviewBrush(snappedPos);
                }
                else
                {
                    // Standard grid snapping for other cases
                    snappedPos = EditorMain.SnapToGrid(unsnappedPosition, EditorMain.snappingTranslate, EditorMain.snapThreshold);
                }

                Gizmo.virtualPosition = snappedPos;
            }
        }
        else if (EditorMain.Mode == EditMode.Edit)
        {
            if (EditorMain.selectedBrush != null && draggingVerts.Count != 0)
            {
                if (Gizmo.DraggingAxis != Gizmo.ActiveAxis.None)
                {
                    // Gizmo.virtualPosition is already in world space
                    Vector3 unsnappedWorldPosition = Gizmo.virtualPosition;

                    if (EditorMain.gridSnapEnabled && draggingVerts.Count != 0)
                    {
                        // Get the first vertex we're dragging
                        int firstVertIndex = draggingVerts[0].vertIndex;

                        // The offset is in local space, but we need it in world space for proper snapping
                        Vector3 firstVertOffset = draggingVerts[0].vertOffset;

                        // Calculate where this vertex would be in world space without snapping
                        Vector3 unsnappedFirstVertexWorldPos = unsnappedWorldPosition + firstVertOffset;

                        // Snap that position to the grid - handle both axis and plane dragging
                        Vector3 snappedFirstVertexWorldPos;

                        // Check which type of gizmo dragging we're doing (axis or plane)
                        if (Gizmo.DraggingAxis == ActiveAxis.X ||
                            Gizmo.DraggingAxis == ActiveAxis.Y ||
                            Gizmo.DraggingAxis == ActiveAxis.Z)
                        {
                            // Single axis snapping - use SnapToGridCorner as before
                            snappedFirstVertexWorldPos = EditorMain.SnapToGridCorner(unsnappedFirstVertexWorldPos);
                        }
                        else
                        {
                            // Plane dragging (XY, XZ, YZ) - we need to snap the individual components
                            snappedFirstVertexWorldPos = unsnappedFirstVertexWorldPos;

                            // For XY plane (Z is locked)
                            if (Gizmo.DraggingAxis == ActiveAxis.XY)
                            {
                                float snappedX = SnapAxisToGridCorner(unsnappedFirstVertexWorldPos.X, EditorMain.snappingTranslate);
                                float snappedY = SnapAxisToGridCorner(unsnappedFirstVertexWorldPos.Y, EditorMain.snappingTranslate);
                                snappedFirstVertexWorldPos.X = snappedX;
                                snappedFirstVertexWorldPos.Y = snappedY;
                            }
                            // For XZ plane (Y is locked)
                            else if (Gizmo.DraggingAxis == ActiveAxis.XZ)
                            {
                                float snappedX = SnapAxisToGridCorner(unsnappedFirstVertexWorldPos.X, EditorMain.snappingTranslate);
                                float snappedZ = SnapAxisToGridCorner(unsnappedFirstVertexWorldPos.Z, EditorMain.snappingTranslate);
                                snappedFirstVertexWorldPos.X = snappedX;
                                snappedFirstVertexWorldPos.Z = snappedZ;
                            }
                            // For YZ plane (X is locked)
                            else if (Gizmo.DraggingAxis == ActiveAxis.YZ)
                            {
                                float snappedY = SnapAxisToGridCorner(unsnappedFirstVertexWorldPos.Y, EditorMain.snappingTranslate);
                                float snappedZ = SnapAxisToGridCorner(unsnappedFirstVertexWorldPos.Z, EditorMain.snappingTranslate);
                                snappedFirstVertexWorldPos.Y = snappedY;
                                snappedFirstVertexWorldPos.Z = snappedZ;
                            }
                        }

                        // Recalculate what the gizmo position should be
                        Vector3 newGizmoWorldPos = snappedFirstVertexWorldPos - firstVertOffset;

                        // Update the gizmo position
                        Gizmo.virtualPosition = newGizmoWorldPos;

                        // Now update all vertices based on the new gizmo position
                        foreach ((int vertIndex, Vector3 vertOffset) draggingVert in draggingVerts)
                        {
                            EditorMain.selectedBrush.Vertices[draggingVert.vertIndex] = newGizmoWorldPos - EditorMain.selectedBrush.Pos + draggingVert.vertOffset;
                        }

                        // Recalculate the matrix and update GPU buffers

                        EditorMain.selectedBrush.CenterBrushPosFromMesh();
                        EditorMain.selectedBrush.RecalculateMatrix();
                    }
                    else
                    {
                        // For non-snapped movement, apply the same transformation
                        foreach ((int vertIndex, Vector3 vertOffset) draggingVert in draggingVerts)
                        {
                            EditorMain.selectedBrush.Vertices[draggingVert.vertIndex] = unsnappedWorldPosition - EditorMain.selectedBrush.Pos + draggingVert.vertOffset;
                        }

                        EditorMain.selectedBrush.CenterBrushPosFromMesh();
                        EditorMain.selectedBrush.RecalculateMatrix();
                    }
                }
            }
        }
    }

    private static void UpdatePreviewBrush(Vector3 gizmoPos)
    {
        if (previewBrush != null)
        {
            WorldManager.brushes.Remove(previewBrush);
            previewBrush = null;
        }

        if (!showPointer1 || !showPointer2) return;

        Vector3 p1 = pointer1Billboard.Position;
        Vector3 p2 = pointer2Billboard.Position;
        Vector3 normal = Vector3.Normalize(pointer1Normal);

        // Calculate brush dimensions
        Vector3 min = new Vector3();
        Vector3 max = new Vector3();
        float extrusionDepth = 0;

        if (Math.Abs(normal.X) > 0.9f) // X-normal plane
        {
            // Determine min/max for Y and Z from pointers
            float minY = Math.Min(p1.Y, p2.Y);
            float maxY = Math.Max(p1.Y, p2.Y);
            float minZ = Math.Min(p1.Z, p2.Z);
            float maxZ = Math.Max(p1.Z, p2.Z);

            // Calculate extrusion depth - now considering direction
            extrusionDepth = gizmoPos.X - p1.X; // Can be positive or negative

            // Apply minimum size constraint while preserving sign
            if (Math.Abs(extrusionDepth) < MIN_BRUSH_DIMENSION)
                extrusionDepth = Math.Sign(extrusionDepth) * MIN_BRUSH_DIMENSION;

            if (maxY - minY < MIN_BRUSH_DIMENSION) maxY = minY + MIN_BRUSH_DIMENSION;
            if (maxZ - minZ < MIN_BRUSH_DIMENSION) maxZ = minZ + MIN_BRUSH_DIMENSION;

            // Set the dimensions based on extrusion direction
            if (extrusionDepth >= 0)
            {
                // Positive direction (forward extrusion)
                min = new Vector3(p1.X, minY, minZ);
                max = new Vector3(p1.X + extrusionDepth, maxY, maxZ);
            }
            else
            {
                // Negative direction (backward extrusion)
                min = new Vector3(p1.X + extrusionDepth, minY, minZ);
                max = new Vector3(p1.X, maxY, maxZ);
            }
        }
        else if (Math.Abs(normal.Y) > 0.9f) // Y-normal plane
        {
            // Determine min/max for X and Z from pointers
            float minX = Math.Min(p1.X, p2.X);
            float maxX = Math.Max(p1.X, p2.X);
            float minZ = Math.Min(p1.Z, p2.Z);
            float maxZ = Math.Max(p1.Z, p2.Z);

            // Calculate extrusion depth - considering direction
            extrusionDepth = gizmoPos.Y - p1.Y; // Can be positive or negative

            // Apply minimum size constraint while preserving sign
            if (Math.Abs(extrusionDepth) < MIN_BRUSH_DIMENSION)
                extrusionDepth = Math.Sign(extrusionDepth) * MIN_BRUSH_DIMENSION;

            if (maxX - minX < MIN_BRUSH_DIMENSION) maxX = minX + MIN_BRUSH_DIMENSION;
            if (maxZ - minZ < MIN_BRUSH_DIMENSION) maxZ = minZ + MIN_BRUSH_DIMENSION;

            // Set the dimensions based on extrusion direction
            if (extrusionDepth >= 0)
            {
                // Positive direction (forward extrusion)
                min = new Vector3(minX, p1.Y, minZ);
                max = new Vector3(maxX, p1.Y + extrusionDepth, maxZ);
            }
            else
            {
                // Negative direction (backward extrusion)
                min = new Vector3(minX, p1.Y + extrusionDepth, minZ);
                max = new Vector3(maxX, p1.Y, maxZ);
            }
        }
        else // Z-normal plane
        {
            // Determine min/max for X and Y from pointers
            float minX = Math.Min(p1.X, p2.X);
            float maxX = Math.Max(p1.X, p2.X);
            float minY = Math.Min(p1.Y, p2.Y);
            float maxY = Math.Max(p1.Y, p2.Y);

            // Calculate extrusion depth - considering direction
            extrusionDepth = gizmoPos.Z - p1.Z; // Can be positive or negative

            // Apply minimum size constraint while preserving sign
            if (Math.Abs(extrusionDepth) < MIN_BRUSH_DIMENSION)
                extrusionDepth = Math.Sign(extrusionDepth) * MIN_BRUSH_DIMENSION;

            if (maxX - minX < MIN_BRUSH_DIMENSION) maxX = minX + MIN_BRUSH_DIMENSION;
            if (maxY - minY < MIN_BRUSH_DIMENSION) maxY = minY + MIN_BRUSH_DIMENSION;

            // Set the dimensions based on extrusion direction
            if (extrusionDepth >= 0)
            {
                // Positive direction (forward extrusion)
                min = new Vector3(minX, minY, p1.Z);
                max = new Vector3(maxX, maxY, p1.Z + extrusionDepth);
            }
            else
            {
                // Negative direction (backward extrusion)
                min = new Vector3(minX, minY, p1.Z + extrusionDepth);
                max = new Vector3(maxX, maxY, p1.Z);
            }
        }

        // Calculate true center point and size
        Vector3 center = (min + max) * 0.5f;
        Vector3 size = max - min;

        MainScript.debugText["MinMax"] = "Min:" + min + " Max: " + max;
        MainScript.debugText["Center"] = "Center: " + center;

        // Now we need to transform our unit cube vertices into the actual brush shape
        Vector3[] vertices = new Vector3[WorldManager.MeshCube.verts.Count];
        for (int i = 0; i < vertices.Length; i++)
        {
            // Unit cube vertices are in range [-0.5, 0.5]
            Vector3 unitVertex = WorldManager.MeshCube.verts[i];

            // Transform unit vertices to match our desired shape
            // Scale around center, then translate to world position
            vertices[i] = new Vector3(
                (unitVertex.X * size.X),// + (center.X / 2),
                (unitVertex.Y * size.Y),// + (center.Y / 2),
                (unitVertex.Z * size.Z) // + (center.Z / 2)
            );
        }

        // Create the brush with position = center of vertices, scale = Vector3.One
        previewBrush = new Brush(
            center,             // Position at the true center
            Vector3.One,        // Keep scale at Vector3.One as requested
            vertices, //WorldManager.MeshCube.verts.ToArray(),           // Vertices contain the correct shape
            WorldManager.MeshCube.uvs.ToArray(),
            WorldManager.MeshCube.normals.ToArray(),
            WorldManager.MeshCube.indices,
            new Face[] {
                new Face(ResourceManager.GetTexture("dev/dev_light_checker"), Vector2.One, Vector2.Zero, 0),
                new Face(ResourceManager.GetTexture("dev/dev_light_checker"), Vector2.One, Vector2.Zero, 0),
                new Face(ResourceManager.GetTexture("dev/dev_light_checker"), Vector2.One, Vector2.Zero, 0),
                new Face(ResourceManager.GetTexture("dev/dev_light_checker"), Vector2.One, Vector2.Zero, 0),
                new Face(ResourceManager.GetTexture("dev/dev_light_checker"), Vector2.One, Vector2.Zero, 0),
                new Face(ResourceManager.GetTexture("dev/dev_light_checker"), Vector2.One, Vector2.Zero, 0),
            }
        );

        // Add the preview brush to the world
        WorldManager.brushes.Add(previewBrush);

        previewBrush.RecalculateMatrix();
    }

    // Helper method to snap a single axis value to the nearest grid corner
    private static float SnapAxisToGridCorner(float value, float gridSize)
    {
        // Offset by half a grid cell to target corners
        float offsetValue = value - (gridSize * 0.5f);

        // Find the nearest grid line
        float gridLine = MathF.Round(offsetValue / gridSize) * gridSize;

        // Shift back to the corner coordinate
        float cornerCoordinate = gridLine + (gridSize * 0.5f);

        return cornerCoordinate;
    }

    private static void CreateFinalBrush()
    {
        // If we already have a preview brush, just make it permanent
        if (previewBrush != null)
        {
            // Remove it from brush list to avoid duplication
            WorldManager.brushes.Remove(previewBrush);

            // Create a new permanent brush with the same properties
            WorldManager.brushes.Add(new Brush(
                previewBrush.Pos,           // Position at the true center of all vertices
                Vector3.One,            // Always use Vector3.One for scale
                previewBrush.Vertices,               // Vertices already contain the correct shape
                previewBrush.Uvs,
                previewBrush.Normals,
                previewBrush.Indices,
                previewBrush.Faces
            ));

            previewBrush = null;
        }
    }

    public static void DisposeGeneration()
    {
        pointer1Billboard.Position = Vector3.Zero;
        pointer2Billboard.Position = Vector3.Zero;

        SelectQuad.vertices = new Vector3[4];
        SelectQuad.axis = ActiveAxis.None;

        showPointer1 = false;
        showPointer2 = false;

        pointer1Normal = Vector3.Zero;

        generationState = GenerateionState.HoverStart;
        Gizmo.VisibleAxes = Gizmo.ActiveAxis.None;
        generationAxisStart = 0;

        if (previewBrush != null) { WorldManager.brushes.Remove(previewBrush); previewBrush = null; }
    }


    private static void UpdatePointerLines()
    {
        /*
        if(generationState == GenerateionState.Extrude)
        {

            PointerLines.Add((pointerLine1Pos + (pointerLine1Normal * (pointerLine1Length / 2)), pointerLine1Pos - (pointerLine1Normal * (pointerLine1Length / 2)), pointerColor, pointerThickness));
            PointerLines.Add((pointerLine2Pos + (pointerLine2Normal * (pointerLine2Length / 2)), pointerLine2Pos - (pointerLine2Normal * (pointerLine2Length / 2)), pointerColor, pointerThickness));
        }
        else if(generationState == GenerateionState.DraggingPoints)
        {
            PointerLines.Add((pointerLine1Pos + (pointerLine1Normal * (pointerLine1Length / 2)), pointerLine1Pos - (pointerLine1Normal * (pointerLine1Length / 2)), pointerColor, pointerThickness));
            PointerLines.Add((pointerLine2Pos + (pointerLine2Normal * (pointerLine2Length / 2)), pointerLine2Pos - (pointerLine2Normal * (pointerLine2Length / 2)), pointerColor, pointerThickness));
        }
        else if(generationState == GenerateionState.HoverStart)
        {
            PointerLines.Add((pointerLine1Pos + (pointerLine1Normal * (pointerLine1Length / 2)), pointerLine1Pos - (pointerLine1Normal * (pointerLine1Length / 2)), pointerColor, pointerThickness));
        }
        */

        if (generationState == GenerateionState.HoverStart)
        {
            pointer2Billboard.Position = pointer1Billboard.Position;
        }

        if (generationState != GenerateionState.HoverStart)
        {
            UpdateSelectionQuad();
        }
    }

    private static void UpdateEditModeVisuals()
    {
        EditModeBillboards.Clear();
        EditModeLines.Clear();

        if (EditorMain.selectedBrush == null) { return; }

        /*
        // Draw face center billboards using the pre-calculated data
        for (int face = 0; face < EditorMain.selectedBrush.FaceCenters.Length; face++)
        {
            // Use the stored face center
            Vector3 faceCenter = EditorMain.selectedBrush.GetFaceCenterWorld(face);
            
            // Add billboard for face center
            EditModeBillboards.Add(new BillboardRenderer.Billboard(
                faceCenter,  // No need to add Pos, already in world space
                sideBillboard
            ));
        }
        */

        // Draw wireframe edges
        for (int face = 0; face < 6; face++)
        {
            if (EditorMain.selectedBrush.FaceEdges.ContainsKey(face))
            {
                foreach (var edge in EditorMain.selectedBrush.FaceEdges[face])
                {
                    // Get the vertices for this edge in world space
                    Vector3 v1 = EditorMain.selectedBrush.Vertices[edge.Item1] + EditorMain.selectedBrush.Pos;
                    Vector3 v2 = EditorMain.selectedBrush.Vertices[edge.Item2] + EditorMain.selectedBrush.Pos;

                    // Add the line
                    EditModeLines.Add((v1, v2, EditModeWireframeLine.color, EditModeWireframeLine.thickness));
                }
            }
        }


        for (int i = 0; i < EditorMain.selectedBrush.Vertices.Length; i++)
        {
            EditModeBillboards.Add(new BillboardRenderer.Billboard(EditorMain.selectedBrush.Vertices[i] + EditorMain.selectedBrush.Pos, verticBillboard));
        }

    }

    private static void UpdateSelectionQuad()
    {
        if (!showPointer1 || !showPointer2) return;

        // Get the positions of the two pointers
        Vector3 p1 = pointer1Billboard.Position;
        Vector3 p2 = pointer2Billboard.Position;

        // Get the normal (already stored in pointer1Normal)
        Vector3 normal = Vector3.Normalize(pointer1Normal);

        // Find two perpendicular vectors to create the quad
        Vector3 tangent;
        if (Math.Abs(normal.Y) < 0.99f)
            tangent = Vector3.Normalize(Vector3.Cross(normal, Vector3.UnitY));
        else
            tangent = Vector3.Normalize(Vector3.Cross(normal, Vector3.UnitZ));

        Vector3 bitangent = Vector3.Normalize(Vector3.Cross(normal, tangent));

        // IMPORTANT: Move the quad SLIGHTLY IN FRONT of the surface, not behind
        // This ensures it's visible when flush against a brush face
        Vector3 offset = normal * 0.002f; // Small positive offset pushes it in front

        // Calculate the difference vector between points
        Vector3 diff = p2 - p1;

        // Project this vector onto our tangent and bitangent to get the quad dimensions
        float width = Vector3.Dot(diff, tangent);
        float height = Vector3.Dot(diff, bitangent);

        // Create four corner vertices
        Vector3[] vertices = new Vector3[4];

        vertices[0] = p1 + offset;                      // Bottom-left
        vertices[1] = p1 + tangent * width + offset;    // Bottom-right
        vertices[2] = p1 + tangent * width + bitangent * height + offset;  // Top-right
        vertices[3] = p1 + bitangent * height + offset; // Top-left

        // Update the SelectQuad data
        SelectQuad.vertices = vertices;
        SelectQuad.color = new Vector4(0.2f, 0.6f, 1.0f, 0.5f);
        SelectQuad.useDepthTest = true;  // Still use depth test for other cases
    }

    // Orders vertices to form a proper quad based on the face normal
    private static Vector3[] OrderVerticesForQuad(Vector3[] vertices, Vector3 normal)
    {
        if (vertices.Length != 4)
            return vertices;

        // Find the center point
        Vector3 center = Vector3.Zero;
        foreach (var v in vertices)
            center += v;
        center /= 4;

        // Determine the principal plane based on the normal
        int principalAxis = 0;
        float maxAbs = Math.Abs(normal.X);
        
        if (Math.Abs(normal.Y) > maxAbs)
        {
            principalAxis = 1;
            maxAbs = Math.Abs(normal.Y);
        }
        
        if (Math.Abs(normal.Z) > maxAbs)
        {
            principalAxis = 2;
        }

        // Sort vertices based on their angle around the center in the principal plane
        Array.Sort(vertices, (a, b) => 
        {
            float angleA = 0, angleB = 0;
            
            switch (principalAxis)
            {
                case 0: // YZ plane
                    angleA = (float)Math.Atan2(a.Z - center.Z, a.Y - center.Y);
                    angleB = (float)Math.Atan2(b.Z - center.Z, b.Y - center.Y);
                    break;
                case 1: // XZ plane
                    angleA = (float)Math.Atan2(a.Z - center.Z, a.X - center.X);
                    angleB = (float)Math.Atan2(b.Z - center.Z, b.X - center.X);
                    break;
                case 2: // XY plane
                    angleA = (float)Math.Atan2(a.Y - center.Y, a.X - center.X);
                    angleB = (float)Math.Atan2(b.Y - center.Y, b.X - center.X);
                    break;
            }
            
            return angleA.CompareTo(angleB);
        });

        return vertices;
    }


    // Input

    public static void MouseMove(IMouse mouse, Vector2 position)
    {
        if (EditorMain.Mode == EditMode.Generate)
        {
            Ray ray = RaycastPhysics.ScreenPointToRay(mouse.Position);

            RaycastHit brushRaycast = RaycastPhysics.RaycastBrushes(ray);

            if (generationState == GenerateionState.HoverStart)
            {
                if (!brushRaycast.HasHit || brushRaycast.Distance > 100)
                {
                    RaycastHit planeRaycast = RaycastPhysics.RaycastPlane(ray, 1, -0.5f);

                    if (planeRaycast.HasHit && planeRaycast.Distance < 100)
                    {
                        pointer1Billboard.Position = EditorMain.SnapToGridCorner(planeRaycast.Point, EditorMain.snappingTranslate);
                        pointer1Normal = planeRaycast.Normal;

                        showPointer1 = planeRaycast.Distance < 100;
                        showPointer2 = planeRaycast.Distance < 100;
                    }
                    else
                    {
                        showPointer1 = brushRaycast.Distance < 100;
                        showPointer2 = brushRaycast.Distance < 100;
                    }
                }
                else
                {
                    showPointer1 = brushRaycast.Distance < 100;
                    showPointer2 = brushRaycast.Distance < 100;

                    pointer1Billboard.Position = EditorMain.SnapToGridCorner(brushRaycast.Point, EditorMain.snappingTranslate);
                    pointer1Normal = brushRaycast.Normal;
                }
            }
            else if (generationState == GenerateionState.DraggingPoints)
            {

                RaycastHit planeRaycast = RaycastHit.Miss;

                if (Vector3.Abs(pointer1Normal) == Vector3.UnitX)
                {
                    planeRaycast = RaycastPhysics.RaycastPlane(ray, 0, pointer1Billboard.Position.X);
                }
                else if (Vector3.Abs(pointer1Normal) == Vector3.UnitY)
                {
                    planeRaycast = RaycastPhysics.RaycastPlane(ray, 1, pointer1Billboard.Position.Y);
                }
                else if (Vector3.Abs(pointer1Normal) == Vector3.UnitZ)
                {
                    planeRaycast = RaycastPhysics.RaycastPlane(ray, 2, pointer1Billboard.Position.Z);
                }

                if (!planeRaycast.HasHit || planeRaycast.Distance > 100)
                {
                    showPointer2 = false;
                }
                else
                {
                    showPointer2 = true;

                    pointer2Billboard.Position = EditorMain.SnapToGridCorner(planeRaycast.Point, EditorMain.snappingTranslate);
                }
            }
        }
        else if (EditorMain.Mode == EditMode.Surface)
        {
            Ray ray = RaycastPhysics.ScreenPointToRay(mouse.Position);
            RaycastHit brushRaycast = RaycastPhysics.RaycastBrushes(ray);

            if (brushRaycast.HasHit)
            {
                Brush brush = (Brush)brushRaycast.HitObject;



                if (brushRaycast.HasHit && brushRaycast.Distance < 100)
                {
                    Vector3 normal = new Vector3(
                        (float)Math.Round(brushRaycast.Normal.X, 5),
                        (float)Math.Round(brushRaycast.Normal.Y, 5),
                        (float)Math.Round(brushRaycast.Normal.Z, 5)
                    );

                    if (!brush.Normals.Contains(normal))
                    {
                        MainScript.debugText["HitType"] = "Normal isnt in brush declaration";
                        return;
                    }

                    MainScript.debugText["SurfacesNormalHit"] = "Surfaces Normal Hit: " + normal;
                    // NORMAL IS THE ISSUE

                    Vector3[] result = new Vector3[4];
                    List<int> normalIndexes = MainScript.GetAllIndexes(brush.Normals, normal);

                    result[0] = brush.Vertices[normalIndexes[0]] + brush.Pos;
                    result[1] = brush.Vertices[normalIndexes[1]] + brush.Pos;
                    result[2] = brush.Vertices[normalIndexes[2]] + brush.Pos;
                    result[3] = brush.Vertices[normalIndexes[3]] + brush.Pos;

                    MainScript.debugText["HitType"] = "Face hit";

                    surfaceHoverQuad.vertices = result;


                }
                
                
            }
            else { surfaceHoverQuad.vertices = null; }
        }
    }

    public static void MouseDown(IMouse mouse, MouseButton button)
    {
        if (UIManager.isMouseOverUI) { return; }

        if (EditorMain.Mode == EditMode.Generate && button == MouseButton.Left)
        {
            if (generationState == GenerateionState.HoverStart)
            {
                generationState = GenerateionState.DraggingPoints;
            }
            else if (generationState == GenerateionState.DraggingPoints)
            {
                generationState = GenerateionState.Extrude;

                if (Vector3.Abs(pointer1Normal) == Vector3.UnitX)
                {
                    Gizmo.CurrentMode = GizmoMode.Translate;
                    Gizmo.VisibleAxes = ActiveAxis.X;
                    Gizmo.virtualPosition = Vector3.Lerp(pointer1Billboard.Position, pointer2Billboard.Position, 0.5f);

                    generationAxisStart = pointer1Billboard.Position.X;
                }
                else if (Vector3.Abs(pointer1Normal) == Vector3.UnitY)
                {
                    Gizmo.CurrentMode = GizmoMode.Translate;
                    Gizmo.VisibleAxes = ActiveAxis.Y;
                    Gizmo.virtualPosition = Vector3.Lerp(pointer1Billboard.Position, pointer2Billboard.Position, 0.5f);

                    generationAxisStart = pointer1Billboard.Position.Y;
                }
                else if (Vector3.Abs(pointer1Normal) == Vector3.UnitZ)
                {
                    Gizmo.CurrentMode = GizmoMode.Translate;
                    Gizmo.VisibleAxes = ActiveAxis.Z;
                    Gizmo.virtualPosition = Vector3.Lerp(pointer1Billboard.Position, pointer2Billboard.Position, 0.5f);

                    generationAxisStart = pointer1Billboard.Position.Z;
                }
            }
        }
        else if (EditorMain.Mode == EditMode.Surface && button == MouseButton.Left)
        {
            Ray ray = RaycastPhysics.ScreenPointToRay(mouse.Position);
            RaycastHit brushRaycast = RaycastPhysics.RaycastBrushes(ray);

            if (brushRaycast.HasHit)
            {
                if (selectedSurfacesIndices.Count > 0 && (Brush)brushRaycast.HitObject != EditorMain.selectedBrush && MainScript.keyboard.IsKeyPressed(Key.ControlLeft)) { return; }
                EditorMain.selectedBrush = (Brush)brushRaycast.HitObject;

                if (!MainScript.keyboard.IsKeyPressed(Key.ControlLeft)) { selectedSurfacesIndices.Clear(); surfaceSelectQuads.Clear(); }

                Vector3 normal = new Vector3(
                    (float)Math.Round(brushRaycast.Normal.X, 5),
                    (float)Math.Round(brushRaycast.Normal.Y, 5),
                    (float)Math.Round(brushRaycast.Normal.Z, 5)
                );

                if (!EditorMain.selectedBrush.Normals.Contains(normal))
                {
                    MainScript.debugText["HitType"] = "Normal isnt in brush declaration";
                    return;
                }

                Vector3[] result = new Vector3[4];
                List<int> normalIndexes = MainScript.GetAllIndexes(EditorMain.selectedBrush.Normals, normal);
                selectedSurfacesIndices.Add((normalIndexes[0], normalIndexes[1], normalIndexes[2], normalIndexes[3]));

                result[0] = EditorMain.selectedBrush.Vertices[normalIndexes[0]] + EditorMain.selectedBrush.Pos;
                result[1] = EditorMain.selectedBrush.Vertices[normalIndexes[1]] + EditorMain.selectedBrush.Pos;
                result[2] = EditorMain.selectedBrush.Vertices[normalIndexes[2]] + EditorMain.selectedBrush.Pos;
                result[3] = EditorMain.selectedBrush.Vertices[normalIndexes[3]] + EditorMain.selectedBrush.Pos;

                surfaceSelectQuads.Add((result, new Vector4(1, 1, 1, 0.4f), ActiveAxis.None, false));
            }
            else { EditorMain.selectedBrush = null; selectedSurfacesIndices.Clear(); surfaceSelectQuads.Clear(); }

            /*
            Ray ray = RaycastPhysics.ScreenPointToRay(mouse.Position);
            RaycastHit brushRaycast = RaycastPhysics.RaycastBrushes(ray);

            if (brushRaycast.HasHit)
            {
                Brush brush = (Brush)brushRaycast.HitObject;



                if (brushRaycast.HasHit && brushRaycast.Distance < 100)
                {
                    Vector3 normal = new Vector3(
                        (float)Math.Round(brushRaycast.Normal.X, 5),
                        (float)Math.Round(brushRaycast.Normal.Y, 5),
                        (float)Math.Round(brushRaycast.Normal.Z, 5)
                    );

                    if (!brush.Normals.Contains(normal))
                    {
                        MainScript.debugText["HitType"] = "Normal isnt in brush declaration";
                        return;
                    }

                    Vector3[] result = new Vector3[4];
                    List<int> normalIndexes = MainScript.GetAllIndexes(brush.Normals, normal);

                    result[0] = brush.Vertices[normalIndexes[0]] + brush.Pos;
                    result[1] = brush.Vertices[normalIndexes[1]] + brush.Pos;
                    result[2] = brush.Vertices[normalIndexes[2]] + brush.Pos;
                    result[3] = brush.Vertices[normalIndexes[3]] + brush.Pos;

                    MainScript.debugText["HitType"] = "Face hit";

                    surfaceHoverQuad.vertices = result;


                }


            }
            else { surfaceHoverQuad.vertices = null; }
            */
        }
    }

    public static void MouseUp(IMouse mouse, MouseButton button)
    {
        if (UIManager.isMouseOverUI) { return; }

        if (EditorMain.Mode == EditMode.Edit)
        {
            if (button == MouseButton.Left)
            {
                if (EditorMain.selectedBrush != null && !EditorMain.wasDragging)
                {
                    Vector3[] clickedParts = GetClickedPart(mouse);
                    draggingVerts.Clear();

                    if (clickedParts.Length == 1) // Vertic
                    {
                        //MainScript.debugText["ClickedPart"] = "Clicked Part: Vertic";
                        Gizmo.virtualPosition = clickedParts[0] + EditorMain.selectedBrush.Pos;
                        Gizmo.VisibleAxes = Gizmo.ActiveAxis.All;

                        // All Vertices of the selected vertic Vector3 int indexes
                        List<int> indexesOfVert = MainScript.GetAllIndexes(EditorMain.selectedBrush.Vertices, clickedParts[0]);

                        foreach (int index in indexesOfVert)
                        {
                            // Store the offset from gizmo position to original vertex position
                            // This should be a fixed offset that doesn't change during dragging
                            Vector3 vertexWorldPos = EditorMain.selectedBrush.Vertices[index] + EditorMain.selectedBrush.Pos;
                            Vector3 offset = EditorMain.selectedBrush.Vertices[index] - clickedParts[0];
                            draggingVerts.Add((index, offset));
                        }
                    }
                    else if (clickedParts.Length == 2) // Edge
                    {
                        //MainScript.debugText["ClickedPart"] = "Clicked Part: Edge";

                        // Set gizmo to the midpoint of the edge
                        Vector3 midpoint = Vector3.Lerp(clickedParts[0], clickedParts[1], 0.5f);
                        Gizmo.virtualPosition = midpoint + EditorMain.selectedBrush.Pos;
                        Gizmo.VisibleAxes = Gizmo.ActiveAxis.All;

                        // Process vertices for the first endpoint
                        List<int> indexesOfVert = MainScript.GetAllIndexes(EditorMain.selectedBrush.Vertices, clickedParts[0]);
                        foreach (int index in indexesOfVert)
                        {
                            // Calculate offset from the gizmo position (midpoint)
                            Vector3 offset = EditorMain.selectedBrush.Vertices[index] - midpoint;
                            draggingVerts.Add((index, offset));
                        }

                        // Process vertices for the second endpoint
                        List<int> indexesOfVert2 = MainScript.GetAllIndexes(EditorMain.selectedBrush.Vertices, clickedParts[1]);
                        foreach (int index in indexesOfVert2)
                        {
                            // Calculate offset from the gizmo position (midpoint)
                            Vector3 offset = EditorMain.selectedBrush.Vertices[index] - midpoint;
                            draggingVerts.Add((index, offset));
                        }
                    }
                    else if (clickedParts.Length != 0) // Face
                    {
                        // Calculate face center
                        Vector3 sum = Vector3.Zero;
                        foreach (var pos in clickedParts) { sum += pos; }
                        Vector3 center = sum / clickedParts.Length;

                        // Position gizmo at face center
                        Gizmo.virtualPosition = center + EditorMain.selectedBrush.Pos;
                        Gizmo.VisibleAxes = Gizmo.ActiveAxis.All;

                        // Process each vertex in the face
                        foreach (Vector3 pos in clickedParts)
                        {
                            List<int> indexesOfVert = MainScript.GetAllIndexes(EditorMain.selectedBrush.Vertices, pos);
                            foreach (int index in indexesOfVert)
                            {
                                // Calculate offset from face center
                                Vector3 offset = EditorMain.selectedBrush.Vertices[index] - center;
                                draggingVerts.Add((index, offset));
                            }
                        }
                    }
                    else if (!EditorMain.wasDragging && draggingVerts.Count == 0) { Gizmo.VisibleAxes = Gizmo.ActiveAxis.None; }
                }
                else if (!EditorMain.wasDragging && draggingVerts.Count == 0)
                {
                    Gizmo.VisibleAxes = Gizmo.ActiveAxis.None;
                }


                if (draggingVerts.Count == 0 || EditorMain.selectedBrush == null)
                {
                    if (EditorMain.selectedBrush != null && EditorMain.wasDragging) { return; }

                    Ray ray = RaycastPhysics.ScreenPointToRay(mouse.Position);
                    //Brush raycastBrush = RaycastPhysics.RaycastBrushes(ray);
                    RaycastHit raycastBrush = RaycastPhysics.RaycastBrushes(ray);

                    EditorMain.selectedBrush = (Brush?)raycastBrush.HitObject;

                    Gizmo.VisibleAxes = Gizmo.ActiveAxis.None;
                }
            }
        }
    }


    public static Vector3[] GetClickedPart(IMouse mouse)
    {
        // Early exit if no brush is selected
        if (EditorMain.selectedBrush == null) return new Vector3[0];

        Ray ray = RaycastPhysics.ScreenPointToRay(mouse.Position);

        float pixelSelectionRadius = 15.0f;

        int verticIndexSmallest = -1;
        float smallestDistance = float.PositiveInfinity;

        for (int i = 0; i < EditorMain.selectedBrush.Vertices.Length; i++)
        {
            Vector3 vertexWorldPos = EditorMain.selectedBrush.Vertices[i] + EditorMain.selectedBrush.Pos;
            Vector2 screenPos = MainScript.cam.WorldToScreen(vertexWorldPos);

            float screenDistance = Vector2.Distance(mouse.Position, screenPos);

            if (!MainScript.cam.IsInView(vertexWorldPos)) { Console.WriteLine("Is Not In View"); continue; }

            Ray vertexRay = RaycastPhysics.ScreenPointToRay(mouse.Position);
            RaycastHit depthHit = RaycastPhysics.RaycastBrushes(vertexRay);

            // Calculate actual distance from camera to vertex
            float vertexDistance = Vector3.Distance(MainScript.cam.Pos, vertexWorldPos);

            // Only consider this vertex if it's not occluded by another brush
            // (or it's the closest thing hit by the ray)
            bool isVisible = !depthHit.HasHit ||
                            (depthHit.HitObject == EditorMain.selectedBrush &&
                            Math.Abs(depthHit.Distance - vertexDistance) < 0.1f);

            if (screenDistance < smallestDistance && isVisible)
            {
                smallestDistance = screenDistance;
                verticIndexSmallest = i;
            }

        }

        if (smallestDistance < pixelSelectionRadius && verticIndexSmallest != -1)
        {
            MainScript.debugText["HitType"] = "Vertex hit: " + verticIndexSmallest + " (index)";
            Vector3[] result = new Vector3[1];
            result[0] = EditorMain.selectedBrush.Vertices[verticIndexSmallest];
            return result;
        }






        // 2. Try to hit edges
        float closestDistance = float.MaxValue;
        (int, int) closestEdge = (-1, -1);
        Vector3 closestEdgePoint = Vector3.Zero;

        // Check all face edges
        for (int face = 0; face < 6; face++)
        {
            if (EditorMain.selectedBrush.FaceEdges.ContainsKey(face))
            {
                foreach (var edge in EditorMain.selectedBrush.FaceEdges[face])
                {
                    Vector3 v1 = EditorMain.selectedBrush.Vertices[edge.Item1] + EditorMain.selectedBrush.Pos;
                    Vector3 v2 = EditorMain.selectedBrush.Vertices[edge.Item2] + EditorMain.selectedBrush.Pos;

                    // Use line segment distance calculation
                    Vector3 closestPoint;
                    float distance = DistanceFromRayToLineSegment(ray, v1, v2, out closestPoint);

                    // INCREASED THRESHOLD - try 10.0f instead of 2
                    float threshold = EditModeWireframeLine.thickness / 20; // * 10.0f;

                    // Check if we're close enough to the edge
                    if (distance < threshold && distance < closestDistance)
                    {
                        // Perform depth test - check if the closest point on the edge is visible
                        float edgePointDistance = Vector3.Distance(MainScript.cam.Pos, closestPoint);

                        // Cast ray from camera through mouse position
                        Ray edgeRay = RaycastPhysics.ScreenPointToRay(mouse.Position);
                        RaycastHit depthHit = RaycastPhysics.RaycastBrushes(edgeRay);

                        // Edge is visible if nothing was hit, or if what was hit is very close to our edge point
                        bool isVisible = !depthHit.HasHit ||
                                        (depthHit.HitObject == EditorMain.selectedBrush &&
                                        Math.Abs(depthHit.Distance - edgePointDistance) < 0.1f);

                        if (isVisible)
                        {
                            closestDistance = distance;
                            closestEdge = edge;
                            closestEdgePoint = closestPoint;
                        }
                    }
                }
            }
        }

        // If we hit an edge, return its vertices
        if (closestEdge.Item1 >= 0 && closestDistance < 100)
        {
            MainScript.debugText["HitType"] = "Edge hit: (" + closestEdge.Item1 + "," + closestEdge.Item2 +
                                            ") at distance " + closestDistance +
                                            ", point: " + closestEdgePoint;

            // Return two vertices that make up the edge
            Vector3[] result = new Vector3[2];
            result[0] = EditorMain.selectedBrush.Vertices[closestEdge.Item1];
            result[1] = EditorMain.selectedBrush.Vertices[closestEdge.Item2];
            return result;
        }



        // 3. Try to hit faces
        RaycastHit brushRaycast = RaycastPhysics.RaycastBrushes(ray);



        if (brushRaycast.HasHit && brushRaycast.Distance < 100 && brushRaycast.HitObject == EditorMain.selectedBrush)
        {
            Vector3 normal = new Vector3(
                (float)Math.Round(brushRaycast.Normal.X, 5),
                (float)Math.Round(brushRaycast.Normal.Y, 5),
                (float)Math.Round(brushRaycast.Normal.Z, 5)
            );

            if (!EditorMain.selectedBrush.Normals.Contains(normal))
            {
                MainScript.debugText["HitType"] = "Normal isnt in brush declaration";
                return new Vector3[0];
            }

            Vector3[] result = new Vector3[4];
            List<int> normalIndexes = MainScript.GetAllIndexes(EditorMain.selectedBrush.Normals, normal);

            result[0] = EditorMain.selectedBrush.Vertices[normalIndexes[0]];
            result[1] = EditorMain.selectedBrush.Vertices[normalIndexes[1]];
            result[2] = EditorMain.selectedBrush.Vertices[normalIndexes[2]];
            result[3] = EditorMain.selectedBrush.Vertices[normalIndexes[3]];

            MainScript.debugText["HitType"] = "Face hit";

            return result;
        }

        // Nothing was hit
        MainScript.debugText["HitType"] = "No hit detected";
        return new Vector3[0];
    }

    // Helper method to find the closest distance from a ray to a line segment
    private static float DistanceFromRayToLineSegment(Ray ray, Vector3 lineStart, Vector3 lineEnd, out Vector3 closestPoint)
    {
        Vector3 v = ray.Direction;
        Vector3 w = lineEnd - lineStart;
        Vector3 p = ray.Origin;
        Vector3 q = lineStart;

        float vDotw = Vector3.Dot(v, w);
        float vDotv = Vector3.Dot(v, v);
        float wDotw = Vector3.Dot(w, w);

        // Compute parametric points on both lines
        Vector3 pMinusQ = p - q;
        float vDotPMinusQ = Vector3.Dot(v, pMinusQ);
        float wDotPMinusQ = Vector3.Dot(w, pMinusQ);

        float denom = vDotv * wDotw - vDotw * vDotw;

        // If lines are not parallel
        float s, t;
        if (MathF.Abs(denom) > 0.0001f)
        {
            s = (vDotw * wDotPMinusQ - wDotw * vDotPMinusQ) / denom;
            t = (vDotv * wDotPMinusQ - vDotw * vDotPMinusQ) / denom;
        }
        else
        {
            // Lines are parallel, use projection
            s = 0;
            t = wDotPMinusQ / wDotw;
        }

        // Clamp t to ensure we're on the line segment
        t = MathF.Min(1, MathF.Max(0, t));

        // Clamp s to ensure we're on the positive part of the ray
        s = MathF.Max(0, s);

        // Find closest points on the two lines
        Vector3 pointOnRay = p + s * v;
        Vector3 pointOnSegment = q + t * w;

        // Return the closest point on the line segment
        closestPoint = pointOnSegment;

        // Return the distance between the two closest points
        return Vector3.Distance(pointOnRay, pointOnSegment);
    }

    public static void KeyDown(IKeyboard keyboard, Key key, int arg3)
    {
        if (key == Key.Enter && generationState == GenerateionState.Extrude && EditorMain.Mode == EditMode.Generate)
        {
            // Actually place the brush
            CreateFinalBrush();
            DisposeGeneration();
        }
    }
    

    private class VectorComparer : IEqualityComparer<Vector3>
    {
        private readonly float _tolerance;

        public VectorComparer(float tolerance)
        {
            _tolerance = tolerance;
        }

        public bool Equals(Vector3 x, Vector3 y)
        {
            return Math.Abs(x.X - y.X) < _tolerance &&
                Math.Abs(x.Y - y.Y) < _tolerance &&
                Math.Abs(x.Z - y.Z) < _tolerance;
        }

        public int GetHashCode(Vector3 obj)
        {
            // Round values to reduce floating point precision issues
            return HashCode.Combine(
                Math.Round(obj.X, 3),
                Math.Round(obj.Y, 3),
                Math.Round(obj.Z, 3)
            );
        }
    }
}