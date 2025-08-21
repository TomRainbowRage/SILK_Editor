using System.Numerics;
using Silk.NET.Input;

public class EditorMain
{
    public static EditMode Mode;
    public static Brush selectedBrush = null;


    private static Vector3 lastSelectedRot;
    private static Vector3 lastSelectedPos = Vector3.Zero;


    // Grid Snapping

    public static float snappingTranslate = 1f; // Grid size to snap to
    private static Vector3 unsnappedPosition = Vector3.Zero; // Tracks unsnapped position
    private static bool snapOnRelease = true; // Whether to snap only when releasing mouse
    public static float snapThreshold = 0.5f; // How close to next grid line before snapping during drag
    public static bool gridSnapEnabled = true;

    public static bool wasDragging = false;
    

    public static void Load()
    {
        MainScript.window.Update += Update;
        MainScript.mouse.MouseUp += MouseUp;
        MainScript.keyboard.KeyDown += KeyDown;
    }

    public static void Update(double deltaTime)
    {
        if(selectedBrush != null && Mode == EditMode.Place)
        {
            if (Gizmo.DraggingAxis != Gizmo.ActiveAxis.None)
            {
                // Store the unsnapped position from gizmo
                unsnappedPosition = Gizmo.virtualPosition;
                
                if (gridSnapEnabled)
                {
                    // Check if we've crossed a snap threshold during dragging
                    //Vector3 snappedPos = SnapToGrid(unsnappedPosition, snappingTranslate, snapThreshold);
                    // Update the brush position with the snapped position
                    //selectedBrush.Pos = snappedPos;

                    /*
                    selectedBrush.Pos = unsnappedPosition;

                    Vector3 minVert = selectedBrush.GetMinVert();
                    Vector3 brushMinVertOffset = selectedBrush.Pos - minVert;

                    Vector3 snappedVert = SnapToGridCorner(minVert);
                    selectedBrush.Pos = snappedVert + brushMinVertOffset;
                    */

                    // First, store the previous brush position
                    Vector3 oldBrushPos = selectedBrush.Pos;
                    
                    // Temporarily update the brush position to unsnapped position
                    selectedBrush.Pos = unsnappedPosition;
                    
                    // Get the minimum vertex in world space
                    Vector3 minVert = selectedBrush.GetMinVert();  // Make sure this returns the world-space vertex
                    
                    // Calculate offset from min vertex to brush center (not the other way around)
                    Vector3 brushOffsetFromMinVert = selectedBrush.Pos - minVert;
                    
                    // Snap min vertex to grid corner
                    Vector3 snappedMinVert = SnapToGridCorner(minVert);
                    
                    // Set brush position = snapped min vertex + offset
                    selectedBrush.Pos = snappedMinVert + brushOffsetFromMinVert;
                    

                    
                    
                    // IMPORTANT: Update the gizmo's visual position to match the brush
                    // This keeps them visually aligned
                    Gizmo.virtualPosition = selectedBrush.Pos;
                    
                    if (lastSelectedPos != selectedBrush.Pos)
                    {
                        lastSelectedPos = selectedBrush.Pos;
                        selectedBrush.RecalculateMatrix();
                    }
                }
                else
                {
                    // No snapping, just update normally
                    selectedBrush.Pos = unsnappedPosition;
                    
                    if (lastSelectedPos != selectedBrush.Pos)
                    {
                        lastSelectedPos = selectedBrush.Pos;
                        selectedBrush.RecalculateMatrix();
                    }
                }
            }
            else
            {
                // Not dragging - keep brush and gizmo in sync
                selectedBrush.Pos = Gizmo.virtualPosition;
                
                if(lastSelectedPos != selectedBrush.Pos)
                {
                    lastSelectedPos = selectedBrush.Pos;
                    selectedBrush.RecalculateMatrix();
                }
            }
            
            // Rotation handling stays the same
            selectedBrush.Rot = -Gizmo.virtualRotation;
            if(lastSelectedRot != selectedBrush.Rot)
            {
                lastSelectedRot = selectedBrush.Rot;
                selectedBrush.RecalculateMatrix();
            }
        }
        
        wasDragging = Gizmo.DraggingAxis != Gizmo.ActiveAxis.None;
    }

    public static Vector3 SnapToGrid(Vector3 position, float gridSize, float threshold)
    {
        Vector3 result = new Vector3();
        
        // For each axis (X, Y, Z)
        for (int i = 0; i < 3; i++)
        {
            // Find the nearest grid line
            float gridLine = MathF.Round(position[i] / gridSize) * gridSize;
            
            // Calculate distance to nearest grid line
            float distanceToGridLine = MathF.Abs(position[i] - gridLine);
            
            // If we're within threshold of the grid line, snap to it
            if (distanceToGridLine <= threshold * gridSize)
                result[i] = gridLine;
            else
                result[i] = position[i]; // Keep unsnapped position
        }
        
        return result;
    }

    public static Vector3 SnapPositionToGrid(Vector3 position)
    {
        return SnapToGrid(position, snappingTranslate, snapThreshold);
    }

    public static Vector3 SnapToGridCorner(Vector3 position, float gridSize = 1.0f, float threshold = 0.5f)
    {
        Vector3 result = new Vector3();
        
        // For each axis (X, Y, Z)
        for (int i = 0; i < 3; i++)
        {
            // Offset the position by half a grid cell so we're targeting corners
            float offsetPosition = position[i] - (gridSize * 0.5f);
            
            // Find the nearest grid line
            float gridLine = MathF.Round(offsetPosition / gridSize) * gridSize;
            
            // Shift back to the corner coordinate
            float cornerCoordinate = gridLine + (gridSize * 0.5f);
            
            // Calculate distance to nearest corner
            float distanceToCorner = MathF.Abs(position[i] - cornerCoordinate);
            
            // If we're within threshold, snap to the corner
            if (distanceToCorner <= threshold * gridSize)
                result[i] = cornerCoordinate;
            else
                result[i] = position[i]; // Keep unsnapped position
        }
        
        return result;
    }

    public static void ChangeEditMode(EditMode editMode)
    {
        if(Mode == EditMode.Generate && editMode != EditMode.Generate) { RealtimeCSG.DisposeGeneration(); Gizmo.useCardinalOrientation = true; } // Leaving
        if(Mode != EditMode.Generate && editMode == EditMode.Generate) { RealtimeCSG.DisposeGeneration(); Gizmo.useCardinalOrientation = false; } // Entering

        if(Mode == EditMode.Place && editMode != EditMode.Place) { Gizmo.VisibleAxes = Gizmo.ActiveAxis.None; } // Leaving
        if(Mode != EditMode.Place && editMode == EditMode.Place)
        {
            Gizmo.VisibleAxes = selectedBrush == null ? Gizmo.ActiveAxis.None : Gizmo.ActiveAxis.All;

            if(selectedBrush != null)
            {
                Gizmo.virtualPosition = selectedBrush.Pos;
                Gizmo.virtualRotation = selectedBrush.Rot;
            }
        } // Entering

        if(Mode == EditMode.Edit && editMode != EditMode.Edit) { Gizmo.VisibleAxes = Gizmo.ActiveAxis.None; } // Leaving


        Mode = editMode;
    }

    public static void Render()
    {
        
    }


    // Input

    public static void MouseUp(IMouse mouse, MouseButton button)
    {
        if(UIManager.isMouseOverUI) { return; }

        if(button == MouseButton.Left)
        {
            if(Mode == EditMode.Place)
            {
                /*
                // If we were dragging and released, snap to grid
                if (wasDragging && selectedBrush != null && gridSnapEnabled && snapOnRelease)
                {
                    // When releasing, snap directly to grid without threshold
                    Vector3 finalSnappedPos = new Vector3(
                        MathF.Round(unsnappedPosition.X / snappingTranslate) * snappingTranslate,
                        MathF.Round(unsnappedPosition.Y / snappingTranslate) * snappingTranslate,
                        MathF.Round(unsnappedPosition.Z / snappingTranslate) * snappingTranslate
                    );
                    
                    // Update both the brush and the gizmo position
                    selectedBrush.Pos = finalSnappedPos;
                    Gizmo.virtualPosition = finalSnappedPos;
                    
                    selectedBrush.RecalculateMatrix();
                    lastSelectedPos = selectedBrush.Pos;
                    return;
                }
                */
                
                // Rest of your existing MouseUp code
                if(selectedBrush != null && wasDragging) { return; }

                Ray ray = RaycastPhysics.ScreenPointToRay(mouse.Position);
                //Brush raycastBrush = RaycastPhysics.RaycastBrushes(ray);
                RaycastHit raycastBrush = RaycastPhysics.RaycastBrushes(ray);

                selectedBrush = (Brush?)raycastBrush.HitObject;

                Gizmo.VisibleAxes = selectedBrush == null ? Gizmo.ActiveAxis.None : Gizmo.ActiveAxis.All;

                if(selectedBrush != null)
                {
                    Gizmo.virtualPosition = selectedBrush.Pos;
                    Gizmo.virtualRotation = selectedBrush.Rot;
                }
            }
        }
    }

    public static void KeyDown(IKeyboard keyboard, Key key, int arg3)
    {
        if(key == Key.Delete && Mode == EditMode.Place)
        {
            if(WorldManager.brushes.Contains(selectedBrush))
            {
                WorldManager.brushes.Remove(selectedBrush);
                selectedBrush = null;
                Gizmo.VisibleAxes = Gizmo.ActiveAxis.None;
            }
        }
    }
}

public enum EditMode
{
    Place,
    Generate,
    Edit,
    Surface,
    // Entity
}