using System;
using System.Collections.Generic;
using System.Numerics;
using Silk.NET.Input;
using Silk.NET.OpenGL;

public class Gizmo
{
    private static GL Gl { get { return MainScript.Gl; } }

    public static Vector3 virtualPosition = Vector3.Zero;
    public static Vector3 virtualRotation = Vector3.Zero;

    private static float lineThickness = 1f;
    private static float lineLength = 2f;

    private static float coneHeight = 0.4f;
    private static float coneRadius = 0.125f;
    private static int coneSegments = 12;

    private static float planeSize = 0.25f; // Size of the plane quads relative to line length
    private static float planeDistance = 0.2f; // Distance from virtual position to plane
    private static float planeOpacity = 0.3f; // Base transparency of planes
    private static float planeHoverOpacity = 0.5f; // Transparency when hovered

    private static float rotationCircleRadius = 1.5f; // Size of rotation circles
    private static int rotationCircleSegments = 32; // Number of segments in rotation circles
    private static float rotationCircleThickness = 1.0f; // Thickness of rotation circles

    private static ActiveAxis hoveredAxis = ActiveAxis.None;
    private static float hoverDarkenFactor = 0.7f;  // Makes colors darker when hovered (30% darker)
    private static float inactiveAlpha = 0.3f;      // Alpha for inactive axes
    private static Vector3 activeColor = new Vector3(1.0f, 0.9f, 0.0f); // Yellow for active axis

    public static bool useCardinalOrientation = true; // Set to true to enable cardinal direction mode
    private static float cardinalRotationSnap = 90f; // Degrees between cardinal directions
    private static float cardinalYaw = 0f;

    public static ActiveAxis DraggingAxis = ActiveAxis.None;
    public static ActiveAxis VisibleAxes = ActiveAxis.None;

    [Flags]
    public enum ActiveAxis 
    { 
        None = 0,
        X = 1, 
        Y = 2, 
        Z = 4, 
        XY = 8, 
        XZ = 16, 
        YZ = 32,
        All = X | Y | Z | XY | XZ | YZ
    }

    public static GizmoMode CurrentMode = GizmoMode.Translate;
    public enum GizmoMode { Translate, Rotate }

    // New fields for dragging
    private static Vector3 dragStartPosition;
    private static Vector2 dragStartMousePosition;
    private static Vector3 dragClickOffset = Vector3.Zero;
    private static bool isDragging = false;
    private static Plane currentDragPlane;
    private static float rotationStartAngle; // Stores the starting angle for rotation
    private static Vector3 rotationStartValue; // Stores the starting rotation value
    private static float rotationActiveThickness = 2.5f;  // Thicker lines for active rotation circle
    private static float rotationHoverThickness = 1.5f;   // Medium thickness for hovered circle
    private static float rotationHitTolerance = 0.15f; // Thickness multiplier for hit detection (smaller = more precise)
    private static float edgeOnToleranceMultiplier = 1.5f; // Additional multiplier when circle is viewed edge-on
    private static float gizmoScaleMultiplier = 0.625f;

    private static List<(Vector3 start, Vector3 end, Vector3 color, float thickness)> GizmoLines = new List<(Vector3 start, Vector3 end, Vector3 color, float thickness)>
    {
        (Vector3.Zero, Vector3.Zero, Vector3.Zero, lineThickness),
        (Vector3.Zero, Vector3.Zero, Vector3.Zero, lineThickness),
        (Vector3.Zero, Vector3.Zero, Vector3.Zero, lineThickness)
    };

    private static List<(Vector3 position, Vector3 direction, float height, float radius, Vector3 color)> GizmoCones = new List<(Vector3 position, Vector3 direction, float height, float radius, Vector3 color)>
    {
        (Vector3.Zero, Vector3.UnitX, coneHeight, coneRadius, Vector3.UnitX),
        (Vector3.Zero, Vector3.UnitY, coneHeight, coneRadius, Vector3.UnitY),
        (Vector3.Zero, Vector3.UnitZ, coneHeight, coneRadius, Vector3.UnitZ)
    };

    private static List<(Vector3[] vertices, Vector4 color, ActiveAxis axis, bool useDepthTest)> GizmoPlanes = new List<(Vector3[] vertices, Vector4 color, ActiveAxis axis, bool useDepthTest)>
    {
        (new Vector3[4], new Vector4(0 * planeOpacity, 0 * planeOpacity, 1 * planeOpacity, 1), ActiveAxis.XY, false), // XY = Blue for Z
        (new Vector3[4], new Vector4(0 * planeOpacity, 1 * planeOpacity, 0 * planeOpacity, 1) * planeOpacity, ActiveAxis.XZ, false), // XZ = Green for Y
        (new Vector3[4], new Vector4(1 * planeOpacity, 0 * planeOpacity, 0 * planeOpacity, 1), ActiveAxis.YZ, false)  // YZ = Red for X
    };

    private static List<(Vector3[] points, Vector3 color, ActiveAxis axis)> rotationCircles = new List<(Vector3[] points, Vector3 color, ActiveAxis axis)>
    {
        (new Vector3[rotationCircleSegments], Vector3.UnitX, ActiveAxis.X),
        (new Vector3[rotationCircleSegments], Vector3.UnitY, ActiveAxis.Y),
        (new Vector3[rotationCircleSegments], Vector3.UnitZ, ActiveAxis.Z)
    };

    public static void Load()
    {
        MainScript.mouse.MouseDown += MouseDown;
        MainScript.mouse.MouseMove += MouseMove;
        MainScript.mouse.MouseUp += MouseUp;

        MainScript.window.Update += delegate(double delta)
        {
            MainScript.debugText["DraggingAxis"] = "DraggingAxis: " + DraggingAxis;
            MainScript.debugText["VirtualPosition"] = "Virtual Position: " + virtualPosition;
            MainScript.debugText["VirtualRotation"] = "Virtual Rotation: " + virtualRotation;
        };
    }

    public static void SetMode(GizmoMode mode)
    {
        if (CurrentMode != mode)
        {
            CurrentMode = mode;
            hoveredAxis = ActiveAxis.None;
            DraggingAxis = ActiveAxis.None;
            isDragging = false;
            
            MainScript.debugText["GizmoMode"] = $"Mode: {CurrentMode}";
        }
    }

    public static void Render(double delta, Matrix4x4 projection, Matrix4x4 view)
    {
        UpdateGizmoLines();
        UpdateGizmoCones();
        UpdateGizmoPlanes();
        UpdateRotationCircles();

        // Setup GL state
        bool depthTestEnabled = Gl.IsEnabled(EnableCap.DepthTest);
        if(depthTestEnabled)
            Gl.Disable(EnableCap.DepthTest);
        else
            Gl.Enable(EnableCap.DepthTest);
        
        // Force gizmos on top
        Gl.Clear(ClearBufferMask.DepthBufferBit);

        bool blendEnabled = Gl.IsEnabled(EnableCap.Blend);
        if (!blendEnabled)
            Gl.Enable(EnableCap.Blend);
        
        Gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        // Render based on current mode
        if (CurrentMode == GizmoMode.Translate)
        {
            // Render translation elements
            ThickLineRenderer.Render(projection, view, GizmoLines);
            ConeRenderer.RenderCones(projection, view, GizmoCones);
            QuadRenderer.RenderQuads(projection, view, GizmoPlanes);
            
            var planeOutlines = GetPlaneOutlines();
            if (planeOutlines.Count > 0)
            {
                ThickLineRenderer.Render(projection, view, planeOutlines);
            }
        }
        else // Rotation mode
        {
            // Only render rotation circles - no translation elements
            RenderRotationCircles(projection, view);
        }
        
        // Restore GL state
        if (depthTestEnabled)
            Gl.Enable(EnableCap.DepthTest);
        else
            Gl.Disable(EnableCap.DepthTest);

        if (!blendEnabled)
            Gl.Disable(EnableCap.Blend);
    }

    public static void RenderRotationCircles(Matrix4x4 projection, Matrix4x4 view)
    {
        if (CurrentMode != GizmoMode.Rotate)
            return;
            
        float scale = GetGizmoScale();
        
        foreach (var circle in rotationCircles)
        {
            // Convert points to lines
            List<(Vector3 start, Vector3 end, Vector3 color, float thickness)> lines = 
                new List<(Vector3 start, Vector3 end, Vector3 color, float thickness)>();
            
            // Determine thickness based on state
            float thickness = rotationCircleThickness;
            if (DraggingAxis == circle.axis)
                thickness = rotationActiveThickness;
            else if (hoveredAxis == circle.axis)
                thickness = rotationHoverThickness;
            
            // Apply scaling to line thickness
            thickness *= scale;
            
            for (int i = 0; i < circle.points.Length; i++)
            {
                int nextIndex = (i + 1) % circle.points.Length;
                lines.Add((circle.points[i], circle.points[nextIndex], 
                        circle.color, thickness));
            }
            
            // Render the lines
            ThickLineRenderer.Render(projection, view, lines);
        }
        
        // Add central axes as reference indicators (small, thin lines)
        if (!isDragging) // Only show when not dragging
        {
            var centerLines = new List<(Vector3 start, Vector3 end, Vector3 color, float thickness)>();
            float centerLineLength = rotationCircleRadius * 0.25f * scale;
            float thinThickness = rotationCircleThickness * 0.5f * scale;
            
            // Add X axis indicator
            centerLines.Add((
                virtualPosition - Vector3.UnitX * centerLineLength, 
                virtualPosition + Vector3.UnitX * centerLineLength, 
                Vector3.UnitX * 0.7f, thinThickness));
                
            // Add Y axis indicator
            centerLines.Add((
                virtualPosition - Vector3.UnitY * centerLineLength, 
                virtualPosition + Vector3.UnitY * centerLineLength, 
                Vector3.UnitY * 0.7f, thinThickness));
                
            // Add Z axis indicator
            centerLines.Add((
                virtualPosition - Vector3.UnitZ * centerLineLength, 
                virtualPosition + Vector3.UnitZ * centerLineLength, 
                Vector3.UnitZ * 0.7f, thinThickness));
                
            ThickLineRenderer.Render(projection, view, centerLines);
        }
    }


    private static Matrix4x4 GetGizmoOrientation()
    {
        if (!useCardinalOrientation)
        {
            // Default world-aligned orientation
            return Matrix4x4.Identity;
        }

        if(DraggingAxis != ActiveAxis.None)
        {
            return Matrix4x4.CreateRotationY(cardinalYaw);
        }

        // Get camera position relative to gizmo
        Vector3 cameraPos = MainScript.cam.Pos;
        Vector3 dirToCamera = cameraPos - virtualPosition;

        // Project onto XZ plane (horizontal plane)
        Vector2 horizontalDir = new Vector2(dirToCamera.X, dirToCamera.Z);

        // Normalize if not zero length
        if (horizontalDir.LengthSquared() > 0.0001f)
            horizontalDir = Vector2.Normalize(horizontalDir);
        else
            horizontalDir = new Vector2(0, 1); // Default direction if camera is directly above/below

        // Calculate yaw angle (in XZ plane)
        float yaw = -MathF.Atan2(horizontalDir.Y, horizontalDir.X);

        // Adjust to nearest 90-degree cardinal direction
        //float cardinalSnap = MathF.PI / 2; // 90 degrees in radians
        //float snappedYaw = MathF.Floor((yaw + cardinalSnap / 2) / cardinalSnap) * cardinalSnap;

        float yawDegrees = yaw * 180 / MathF.PI;
        yawDegrees = (float)(Math.Floor(yawDegrees / 90f) * 90f) + 90;

        yaw = yawDegrees * (MathF.PI / 180f);


        float snappedYawDegrees = 0;//snappedYaw * 180 / MathF.PI;
        MainScript.debugText["GizmoYaw"] = $"Camera: {yawDegrees:F0}°, Gizmo: {snappedYawDegrees:F0}°";

        cardinalYaw = yaw;

        // Create rotation matrix for the snapped yaw
        return Matrix4x4.CreateRotationY(yaw);
    }

    private static float GetGizmoScale()
    {
        Vector3 cameraPos = MainScript.cam.Pos;
        float distanceToCamera = (virtualPosition - cameraPos).Length();

        // Adjust the scale factor based on distance
        float baseScale = 0.1f; // Base size of the gizmo (when very close)
        float scaleFactor = distanceToCamera * 0.1f * gizmoScaleMultiplier; // Scale proportionally to distance
        
        // Ensure a minimum size
        return MathF.Max(baseScale, scaleFactor);
    }


    // This method calculates the drag position based on the mouse position
    // Update the CalculateDragPosition method to use the offset
    public static Vector3 CalculateDragPosition(Vector2 currentMousePosition)
    {
        if (DraggingAxis == ActiveAxis.None)
            return virtualPosition;

        if (CurrentMode == GizmoMode.Rotate)
            return virtualPosition;
        
        // Get current scale - important for scaling the offset
        float currentScale = GetGizmoScale();
        
        // Use the stored drag plane
        Ray ray = RaycastPhysics.ScreenPointToRay(currentMousePosition);
        float? intersection = RayPlaneIntersection(ray, currentDragPlane);
        
        if (!intersection.HasValue)
            return virtualPosition;
        
        // Get intersection point in world space
        Vector3 hitPoint = ray.Origin + ray.Direction * intersection.Value;
        
        // Apply the scaled offset to get an adjusted position that maintains
        // the relative position of the mouse cursor to the gizmo
        Vector3 scaledOffset = dragClickOffset * currentScale;
        Vector3 adjustedPosition = hitPoint - scaledOffset;
        
        // Get axis directions based on current orientation
        Matrix4x4 orientation = GetGizmoOrientation();
        Vector3 xDir = Vector3.Transform(Vector3.UnitX, orientation);
        Vector3 yDir = Vector3.UnitY;
        Vector3 zDir = Vector3.Transform(Vector3.UnitZ, orientation);
        
        // Calculate offset from virtual position
        Vector3 offset = adjustedPosition - virtualPosition;
        
        // Handle different drag modes with projected movements onto oriented axes
        switch (DraggingAxis)
        {
            case ActiveAxis.X:
                float xDot = Vector3.Dot(offset, xDir);
                return virtualPosition + xDir * xDot;
                
            case ActiveAxis.Y:
                float yDot = Vector3.Dot(offset, yDir);
                return virtualPosition + yDir * yDot;
                
            case ActiveAxis.Z:
                float zDot = Vector3.Dot(offset, zDir);
                return virtualPosition + zDir * zDot;
                
            case ActiveAxis.XY:
                float xyDotX = Vector3.Dot(offset, xDir);
                float xyDotY = Vector3.Dot(offset, yDir);
                return virtualPosition + xDir * xyDotX + yDir * xyDotY;
                
            case ActiveAxis.XZ:
                float xzDotX = Vector3.Dot(offset, xDir);
                float xzDotZ = Vector3.Dot(offset, zDir);
                return virtualPosition + xDir * xzDotX + zDir * xzDotZ;
                
            case ActiveAxis.YZ:
                float yzDotY = Vector3.Dot(offset, yDir);
                float yzDotZ = Vector3.Dot(offset, zDir);
                return virtualPosition + yDir * yzDotY + zDir * yzDotZ;
                
            default:
                return virtualPosition;
        }
    }

    public static Vector3 CalculateRotation(Vector2 currentMousePosition)
    {
        if (DraggingAxis == ActiveAxis.None || CurrentMode != GizmoMode.Rotate)
            return virtualRotation;
        
        // Get screen position of gizmo center
        Vector2 centerPos = MainScript.cam.WorldToScreen(virtualPosition);
        
        // Calculate current angle from center to mouse
        Vector2 mouseOffset = currentMousePosition - centerPos;
        float currentAngle = MathF.Atan2(mouseOffset.Y, mouseOffset.X);
        
        // Calculate angle difference
        float angleDelta = currentAngle - rotationStartAngle;
        
        // Make the rotation amount proportional to how far the cursor is from the center
        float sensitivity = 1.0f; // Adjust as needed
        
        // Update rotation based on axis
        Vector3 newRotation = rotationStartValue;
        switch (DraggingAxis)
        {
            case ActiveAxis.X:
                newRotation.X = rotationStartValue.X + angleDelta * sensitivity;
                break;
            case ActiveAxis.Y:
                newRotation.Y = rotationStartValue.Y + angleDelta * sensitivity;
                break;
            case ActiveAxis.Z:
                newRotation.Z = rotationStartValue.Z + angleDelta * sensitivity;
                break;
        }
        
        MainScript.debugText["CurrentAngle"] = $"Angle: {angleDelta * 180 / MathF.PI:F1} degrees";
        
        return newRotation * 180 / MathF.PI;
    }

    private static Plane CreateDragPlane(ActiveAxis axis)
    {
        Matrix4x4 orientation = GetGizmoOrientation();
        Vector3 normal;
        
        // For plane axes, use a fixed plane normal based on the plane type and current orientation
        switch (axis)
        {
            case ActiveAxis.XY:
                normal = Vector3.Transform(Vector3.UnitZ, orientation); // XY plane normal
                break;
            case ActiveAxis.XZ:
                normal = Vector3.UnitY; // XZ plane normal still points up
                break;
            case ActiveAxis.YZ:
                normal = Vector3.Transform(Vector3.UnitX, orientation); // YZ plane normal
                break;
            default:
                // For single axes, use modified algorithm with rotated axes
                Vector3 axisDirection = GetAxisDirection(axis);
                Vector3 cameraForward = MainScript.cam.Forward;
                
                // Create appropriate plane normal
                float parallelCheck = Math.Abs(Vector3.Dot(cameraForward, axisDirection));
                
                if (parallelCheck > 0.99f)
                {
                    // Handle case where camera is parallel to axis
                    if (axis == ActiveAxis.Y)
                    {
                        // For Y axis, use X or Z as fallback
                        normal = Vector3.Transform(Vector3.UnitX, orientation);
                    }
                    else if (axis == ActiveAxis.X)
                    {
                        normal = Vector3.UnitY;
                    }
                    else
                    {
                        normal = Vector3.Transform(Vector3.UnitX, orientation);
                    }
                }
                else
                {
                    // Create a plane that contains the axis and is as perpendicular to the view as possible
                    normal = Vector3.Normalize(Vector3.Cross(axisDirection, 
                        Vector3.Cross(cameraForward, axisDirection)));
                }
                break;
        }
        
        // Create the plane at the gizmo position
        return new Plane(normal, -Vector3.Dot(normal, virtualPosition));
    }
    
    // Ray-Plane intersection calculation
    private static float? RayPlaneIntersection(Ray ray, Plane plane)
    {
        // Get the dot product of the ray direction and plane normal
        float dirDotNormal = Vector3.Dot(ray.Direction, plane.Normal);
        
        // Check if ray is parallel to the plane (or very close to parallel)
        if (Math.Abs(dirDotNormal) < 0.0001f)
            return null; // No intersection
        
        // Calculate the intersection distance
        float t = -(Vector3.Dot(ray.Origin, plane.Normal) + plane.D) / dirDotNormal;
        
        // Safety check for NaN or infinity
        if (float.IsNaN(t) || float.IsInfinity(t))
            return null;
            
        // Check if intersection is behind the ray
        if (t < 0)
            return null; // No intersection in front of the ray
            
        return t;
    }

    // Helper method to get axis direction vector
    private static Vector3 GetAxisDirection(ActiveAxis axis)
    {
        Matrix4x4 orientation = GetGizmoOrientation();
    
        switch (axis)
        {
            case ActiveAxis.X: return Vector3.Transform(Vector3.UnitX, orientation);
            case ActiveAxis.Y: return Vector3.UnitY; // Y still points up
            case ActiveAxis.Z: return Vector3.Transform(Vector3.UnitZ, orientation);
            case ActiveAxis.XY: return Vector3.Transform(Vector3.UnitZ, orientation); // Normal to XY plane
            case ActiveAxis.XZ: return Vector3.UnitY; // Normal to XZ plane
            case ActiveAxis.YZ: return Vector3.Transform(Vector3.UnitX, orientation); // Normal to YZ plane
            default: return Vector3.Zero;
        }
    }

    // Input

    public static void MouseDown(IMouse mouse, MouseButton button)
    {
        if (button == MouseButton.Left)
        {
            ActiveAxis axis = GetClickedAxis(mouse.Position);
            DraggingAxis = axis;
            
            if (axis != ActiveAxis.None)
            {
                Freecam.Locked = true;

                // Start dragging
                isDragging = true;
                dragStartPosition = virtualPosition;
                dragStartMousePosition = mouse.Position;

                if(CurrentMode == GizmoMode.Rotate)
                {
                    // Rotation handling stays the same
                    rotationStartValue = virtualRotation;
                    Vector2 centerPos = MainScript.cam.WorldToScreen(virtualPosition);
                    Vector2 mouseOffset = mouse.Position - centerPos;
                    rotationStartAngle = MathF.Atan2(mouseOffset.Y, mouseOffset.X);
                }
                else // Translation mode
                {
                    // Store current scale
                    float startScale = GetGizmoScale();
                    
                    // Create appropriate drag plane
                    currentDragPlane = CreateDragPlane(axis);

                    Ray ray = RaycastPhysics.ScreenPointToRay(mouse.Position);
                    float? intersection = RayPlaneIntersection(ray, currentDragPlane);
                    
                    if (intersection.HasValue)
                    {
                        // Get the world-space position of the intersection
                        Vector3 hitPoint = ray.Origin + ray.Direction * intersection.Value;
                        
                        // Calculate a scale-independent offset
                        // Store the ratio between the hit offset and the current scale
                        // This way, we can adjust the offset as the scale changes
                        dragClickOffset = (hitPoint - virtualPosition) / startScale;
                        
                        MainScript.debugText["NormalizedOffset"] = dragClickOffset.ToString("F3");
                    }
                    else
                    {
                        isDragging = false;
                        DraggingAxis = ActiveAxis.None;
                    }
                }
            }
        }
    }

    // Mouse Move handler - update position while dragging
    public static void MouseMove(IMouse mouse, Vector2 position)
    {
        if (isDragging && DraggingAxis != ActiveAxis.None)
        {
            if (CurrentMode == GizmoMode.Translate)
            {
                // Calculate new position based on mouse movement
                virtualPosition = CalculateDragPosition(position);
            }
            else // Rotation mode
            {
                // Calculate new rotation based on mouse movement
                virtualRotation = CalculateRotation(position);
            }
        }
        else
        {
            ActiveAxis newHoveredAxis = GetClickedAxis(mouse.Position);
            
            // Only update if the hovered axis changes
            if (newHoveredAxis != hoveredAxis)
            {
                hoveredAxis = newHoveredAxis;
                // Force update of visual state
                UpdateGizmoLines();
                UpdateGizmoCones();
            }
        }
    }
    
    // Mouse Up handler - stop dragging
    public static void MouseUp(IMouse mouse, MouseButton button)
    {
        if (button == MouseButton.Left && isDragging)
        {
            // Finalize the dragging operation
            isDragging = false;
            DraggingAxis = ActiveAxis.None;

            Freecam.Locked = false;

            hoveredAxis = GetClickedAxis(mouse.Position);
        }
    }


    // Show all axes
    public static void ShowAllAxes()
    {
        VisibleAxes = ActiveAxis.X | ActiveAxis.Y | ActiveAxis.Z | 
                    ActiveAxis.XY | ActiveAxis.XZ | ActiveAxis.YZ;
    }

    // Hide all axes
    public static void HideAllAxes()
    {
        VisibleAxes = 0;
    }

    // Set specific axes to be visible/invisible
    public static void SetAxesVisibility(ActiveAxis axes, bool visible)
    {
        if (visible)
            VisibleAxes |= axes;
        else
            VisibleAxes &= ~axes;
    }

    // Toggle specific axes
    public static void ToggleAxes(ActiveAxis axes)
    {
        VisibleAxes ^= axes;
    }

    // Check if a specific axis is visible
    public static bool IsAxisVisible(ActiveAxis axis)
    {
        return (VisibleAxes & axis) != 0;
    }


    public static ActiveAxis GetClickedAxis(Vector2 mousePosition)
    {
        Ray ray = RaycastPhysics.ScreenPointToRay(mousePosition);
        
        if (CurrentMode == GizmoMode.Rotate)
        {
            // Check for rotation circle hits
            ActiveAxis axis = CheckRotationCircleCollision(ray);
            
            // Only return if the axis is visible
            if (axis != ActiveAxis.None && (VisibleAxes & axis) != 0)
            {
                MainScript.debugText["RotationHit"] = $"Rotation hit: {axis}";
                return axis;
            }
            
            return ActiveAxis.None;
        }
        else // Translation mode
        {
            // Check for cone hit first
            var axis = CheckConeBoxCollision(ray);
            if (axis != ActiveAxis.None && (VisibleAxes & axis) != 0)
            {
                MainScript.debugText["ConeHit"] = $"Cone hit: {axis}";
                return axis;
            }
            
            // Check for plane hits
            axis = CheckPlaneCollision(ray);
            if (axis != ActiveAxis.None && (VisibleAxes & axis) != 0)
            {
                MainScript.debugText["PlaneHit"] = $"Plane hit: {axis}";
                return axis;
            }
        }
        
        // Check for line hits (used in both modes)
        var collisionLines = new List<(Vector3 start, Vector3 end, Vector3 color, float thickness)>();
        
        for (int i = 0; i < GizmoLines.Count; i++)
        {
            var line = GizmoLines[i];
            
            // Skip invisible lines (thickness 0 or null)
            if (line.thickness <= 0)
                continue;
                
            // Map index to axis
            ActiveAxis lineAxis = (ActiveAxis)(1 << i); // X=1, Y=2, Z=4
            
            // Only include visible lines
            if ((VisibleAxes & lineAxis) != 0)
            {
                collisionLines.Add((line.start, line.end, line.color, line.thickness * 3.0f));
            }
        }
        
        var (lineIndex, distance) = ThickLineRenderer.Raycast(ray, collisionLines);
        
        if (lineIndex >= 0 && distance < 100f)
        {
            // Convert index to axis enum
            ActiveAxis hitAxis = (ActiveAxis)(1 << lineIndex);
            
            // Double-check it's visible (shouldn't be needed but good for safety)
            if ((VisibleAxes & hitAxis) != 0)
                return hitAxis;
        }
        
        return ActiveAxis.None;
    }

    // Simple box collision check for cones
    private static ActiveAxis CheckConeBoxCollision(Ray ray)
    {
        float scale = GetGizmoScale();
        
        // Check each cone with a simple box collision
        for (int i = 0; i < 3; i++)
        {
            var cone = GizmoCones[i];
            
            // Determine axis based on index
            ActiveAxis axis = (ActiveAxis)(1 << i); // X=0, Y=1, Z=2

            if ((VisibleAxes & axis) == 0)
                continue;
            
            // Create a box at the cone's position
            // The box extends half cone length back from the tip
            Vector3 boxCenter = cone.position + (cone.direction * (cone.height * 0.5f));
            
            // Make the box slightly larger than the cone for better interaction
            float boxSize = cone.radius * 2.0f * scale; // Wider than the cone radius for easier clicking
            
            // Check for ray-box intersection
            Vector3 min = boxCenter - new Vector3(boxSize);
            Vector3 max = boxCenter + new Vector3(boxSize);
            
            // Special case for each axis: adjust min/max to make the box longer along axis direction
            switch (axis)
            {
                case ActiveAxis.X:
                    min.X = cone.position.X;
                    max.X = cone.position.X + cone.height * 1.2f; // Extend slightly past the tip
                    break;
                case ActiveAxis.Y:
                    min.Y = cone.position.Y;
                    max.Y = cone.position.Y + cone.height * 1.2f;
                    break;
                case ActiveAxis.Z:
                    min.Z = cone.position.Z;
                    max.Z = cone.position.Z + cone.height * 1.2f;
                    break;
            }
            
            float? intersection = RayBoxIntersection(ray, min, max);
            if (intersection.HasValue)
            {
                return axis;
            }
        }
        
        return ActiveAxis.None;
    }

    private static ActiveAxis CheckPlaneCollision(Ray ray)
    {
        float closestDist = float.MaxValue;
        ActiveAxis closestAxis = ActiveAxis.None;
        
        for (int i = 0; i < GizmoPlanes.Count; i++)
        {
            var plane = GizmoPlanes[i];
            
            // Create a mathematical plane from the first three vertices
            Vector3 v1 = plane.vertices[0];
            Vector3 v2 = plane.vertices[1];
            Vector3 v3 = plane.vertices[2];
            
            // Calculate plane normal
            Vector3 edge1 = v2 - v1;
            Vector3 edge2 = v3 - v1;
            Vector3 normal = Vector3.Normalize(Vector3.Cross(edge1, edge2));
            
            // Create plane
            Plane quadPlane = new Plane(normal, -Vector3.Dot(normal, v1));
            
            // Ray-plane intersection
            float? t = RayPlaneIntersection(ray, quadPlane);
            
            if (t.HasValue && t.Value < closestDist)
            {
                // Get intersection point
                Vector3 hitPoint = ray.Origin + ray.Direction * t.Value;
                
                // Check if point is inside the quad using barycentric coordinates
                if (IsPointInQuad(hitPoint, plane.vertices))
                {
                    closestDist = t.Value;
                    closestAxis = plane.axis;
                }
            }
        }
        
        return closestAxis;
    }

    private static ActiveAxis CheckRotationCircleCollision(Ray ray)
    {
        // Only check in rotation mode
        if (CurrentMode != GizmoMode.Rotate)
            return ActiveAxis.None;

        float scale = GetGizmoScale();
        float scaledRadius = rotationCircleRadius * scale;
        float scaledThickness = rotationCircleThickness * scale;
        
        float closestDistance = float.MaxValue;
        ActiveAxis closestAxis = ActiveAxis.None;
        
        // Precalculate camera-related values
        Vector3 cameraPosition = ray.Origin;
        Vector3 viewDirection = ray.Direction;
        Vector3 toGizmo = virtualPosition - cameraPosition;
        float distanceToGizmo = toGizmo.Length();
        
        // For each rotation axis
        for (int i = 0; i < 3; i++)
        {
            ActiveAxis axis = (ActiveAxis)(1 << i);

            if ((VisibleAxes & axis) == 0)
                continue;
            
            // Get the circle for this axis
            Vector3[] circlePoints = rotationCircles[i].points;
            if (circlePoints.Length == 0)
                continue;
            
            // Pick which test method to use based on view angle
            Matrix4x4 orientation = GetGizmoOrientation();
            Vector3 circleNormal;
            switch (axis)
            {
                case ActiveAxis.X: circleNormal = Vector3.Transform(Vector3.UnitX, orientation); break;
                case ActiveAxis.Y: circleNormal = Vector3.UnitY; break; // Y still points up
                case ActiveAxis.Z: circleNormal = Vector3.Transform(Vector3.UnitZ, orientation); break;
                default: continue;
            }
            
            // Check if the circle is viewed edge-on
            float dotProduct = Math.Abs(Vector3.Dot(Vector3.Normalize(viewDirection), circleNormal));
            
            // If viewed nearly edge-on, use a different approach
            if (dotProduct < 0.1f) // circle viewed from edge (within ~6 degrees)
            {
                // For edge-on circles, test if ray passes close to any segment of the circle
                for (int j = 0; j < circlePoints.Length; j++)
                {
                    int nextIdx = (j + 1) % circlePoints.Length;
                    
                    // Get the closest point on this line segment to the ray
                    Vector3 lineStart = circlePoints[j];
                    Vector3 lineEnd = circlePoints[nextIdx];
                    
                    float t = ClosestPointToLineParameter(ray.Origin, ray.Direction, lineStart, lineEnd);
                    t = Math.Clamp(t, 0, 1); // Constrain to line segment
                    
                    Vector3 closestPoint = lineStart + t * (lineEnd - lineStart);
                    float distance = DistanceFromPointToRay(closestPoint, ray.Origin, ray.Direction);
                    
                    // Use adjusted tolerance for edge-on views
                    float baseHitArea = scaledThickness * rotationHitTolerance;
                    float tolerance = baseHitArea * edgeOnToleranceMultiplier;
                    
                    if (distance < tolerance && distance < closestDistance)
                    {
                        float rayT = ClosestPointOnRayToPoint(ray.Origin, ray.Direction, closestPoint);
                        if (rayT > 0) // In front of ray origin
                        {
                            closestDistance = distance;
                            closestAxis = axis;
                        }
                    }
                }
            }
            else // Circle is viewed more face-on
            {
                // Create a plane containing the circle
                Plane circlePlane = new Plane(circleNormal, -Vector3.Dot(circleNormal, virtualPosition));
                
                // Check ray intersection with plane
                float? t = RayPlaneIntersection(ray, circlePlane);
                if (!t.HasValue) continue;
                
                // Calculate intersection point
                Vector3 hitPoint = ray.Origin + ray.Direction * t.Value;
                
                // Get distance from hit point to circle center
                Vector3 hitOffset = hitPoint - virtualPosition;
                
                // Project the hit offset onto the plane
                Vector3 projectedHitOffset = hitOffset - Vector3.Dot(hitOffset, circleNormal) * circleNormal;
                
                // Get distance from center
                float distance = projectedHitOffset.Length();
                
                // Check if the distance is close to the circle radius
                float baseHitArea = scaledThickness * rotationHitTolerance;
                float tolerance = baseHitArea * (1.0f + (1.0f - dotProduct) * 2.0f);
                
                // Calculate distance from hit point to circle
                float distToCircle = Math.Abs(distance - scaledRadius);
                
                if (distToCircle <= tolerance && t.Value < closestDistance)
                {
                    closestDistance = t.Value;
                    closestAxis = axis;
                }
            }
        }
        
        return closestAxis;
    }

    private static bool IsPointInQuad(Vector3 point, Vector3[] quad)
    {
        // For simplicity, project to 2D based on dominant normal axis
        Vector3 normal = Vector3.Cross(
            quad[1] - quad[0], 
            quad[3] - quad[0]
        );
        
        // Find the dominant axis
        float absX = Math.Abs(normal.X);
        float absY = Math.Abs(normal.Y);
        float absZ = Math.Abs(normal.Z);
        
        Vector2[] quad2D = new Vector2[4];
        Vector2 point2D;
        
        // Project onto the plane with largest projection
        if (absX >= absY && absX >= absZ)
        {
            // Project onto YZ plane
            for (int i = 0; i < 4; i++)
                quad2D[i] = new Vector2(quad[i].Y, quad[i].Z);
            
            point2D = new Vector2(point.Y, point.Z);
        }
        else if (absY >= absX && absY >= absZ)
        {
            // Project onto XZ plane
            for (int i = 0; i < 4; i++)
                quad2D[i] = new Vector2(quad[i].X, quad[i].Z);
                
            point2D = new Vector2(point.X, point.Z);
        }
        else
        {
            // Project onto XY plane
            for (int i = 0; i < 4; i++)
                quad2D[i] = new Vector2(quad[i].X, quad[i].Y);
                
            point2D = new Vector2(point.X, point.Y);
        }
        
        // Check if point is inside the quad using crossing number algorithm
        int crossings = 0;
        
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            
            if (((quad2D[i].Y <= point2D.Y) && (quad2D[j].Y > point2D.Y)) ||
                ((quad2D[i].Y > point2D.Y) && (quad2D[j].Y <= point2D.Y)))
            {
                float slope = (quad2D[j].X - quad2D[i].X) / (quad2D[j].Y - quad2D[i].Y);
                float intersectX = quad2D[i].X + (point2D.Y - quad2D[i].Y) * slope;
                
                if (intersectX < point2D.X)
                    crossings++;
            }
        }
        
        // Odd number of crossings means point is inside
        return (crossings % 2) == 1;
    }

    private static float ClosestPointToLineParameter(Vector3 rayOrigin, Vector3 rayDirection, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 lineDirection = lineEnd - lineStart;
        float lineLength = lineDirection.Length();
        
        if (lineLength < 0.0001f)
            return 0; // Degenerate line
            
        Vector3 lineDir = lineDirection / lineLength; // Normalize
        
        // Calculate parameter of closest point
        Vector3 w0 = rayOrigin - lineStart;
        float a = Vector3.Dot(rayDirection, rayDirection);
        float b = Vector3.Dot(rayDirection, lineDir);
        float c = Vector3.Dot(lineDir, lineDir);
        float d = Vector3.Dot(rayDirection, w0);
        float e = Vector3.Dot(lineDir, w0);
        
        float denom = a*c - b*b;
        
        // Lines are nearly parallel
        if (Math.Abs(denom) < 0.0001f)
            return 0;
            
        float t = (b*e - c*d) / denom;
        float s = (a*e - b*d) / denom;
        
        return s / lineLength; // Normalize to [0,1]
    }

    // Helper method to find distance from a point to a ray
    private static float DistanceFromPointToRay(Vector3 point, Vector3 rayOrigin, Vector3 rayDirection)
    {
        Vector3 w = point - rayOrigin;
        float c1 = Vector3.Dot(w, rayDirection);
        
        if (c1 <= 0) // Point is behind ray origin
            return (point - rayOrigin).Length();
            
        float c2 = Vector3.Dot(rayDirection, rayDirection);
        float b = c1 / c2;
        Vector3 pb = rayOrigin + b * rayDirection;
        return (point - pb).Length();
    }

    // Helper method to find parameter on ray closest to a point
    private static float ClosestPointOnRayToPoint(Vector3 rayOrigin, Vector3 rayDirection, Vector3 point)
    {
        return Vector3.Dot(point - rayOrigin, rayDirection);
    }


    private static float? RayBoxIntersection(Ray ray, Vector3 min, Vector3 max)
    {
        // Algorithm from "An Efficient and Robust Ray–Box Intersection Algorithm"
        float tMin = float.MinValue;
        float tMax = float.MaxValue;
        
        // For each axis
        for (int i = 0; i < 3; i++)
        {
            float axisMin = min[i];
            float axisMax = max[i];
            float origin = ray.Origin[i];
            float dir = ray.Direction[i];
            
            if (Math.Abs(dir) < 1e-6f) // Ray is parallel to slab
            {
                // Ray origin must be inside slab
                if (origin < axisMin || origin > axisMax)
                    return null;
            }
            else
            {
                // Compute intersection with slab
                float t1 = (axisMin - origin) / dir;
                float t2 = (axisMax - origin) / dir;
                
                // Make t1 the intersection with the near plane
                if (t1 > t2)
                {
                    float temp = t1;
                    t1 = t2;
                    t2 = temp;
                }
                
                // Update tMin and tMax
                tMin = Math.Max(tMin, t1);
                tMax = Math.Min(tMax, t2);
                
                if (tMin > tMax)
                    return null;
            }
        }
        
        // Only return intersections in front of the ray
        if (tMin < 0 && tMax < 0)
            return null;
            
        // Return closest intersection
        return tMin > 0 ? tMin : tMax;
    }


    // Update cones at the end of gizmo axes
    private static void UpdateGizmoCones()
    {
        Matrix4x4 orientation = GetGizmoOrientation();
        float scale = GetGizmoScale();
        
        Vector3 xAxis = Vector3.Transform(Vector3.UnitX, orientation);
        Vector3 yAxis = Vector3.UnitY;
        Vector3 zAxis = Vector3.Transform(Vector3.UnitZ, orientation);
        
        float scaledLength = lineLength * scale;
        float scaledHeight = coneHeight * scale;
        float scaledRadius = coneRadius * scale;
        
        // X-axis cone
        if ((VisibleAxes & ActiveAxis.X) != 0)
        {
            Vector3 xColor = GetAxisColor(ActiveAxis.X);
            Vector3 xConePos = virtualPosition + xAxis * scaledLength;
            GizmoCones[0] = (xConePos, xAxis, scaledHeight, scaledRadius, xColor);
        }
        else
        {
            // Set invisible cone (zero radius or move far away)
            GizmoCones[0] = (Vector3.Zero, Vector3.UnitX, 0, 0, Vector3.Zero);
        }
        
        // Y-axis cone
        if ((VisibleAxes & ActiveAxis.Y) != 0)
        {
            Vector3 yColor = GetAxisColor(ActiveAxis.Y);
            Vector3 yConePos = virtualPosition + yAxis * scaledLength;
            GizmoCones[1] = (yConePos, yAxis, scaledHeight, scaledRadius, yColor);
        }
        else
        {
            GizmoCones[1] = (Vector3.Zero, Vector3.UnitY, 0, 0, Vector3.Zero);
        }
        
        // Z-axis cone
        if ((VisibleAxes & ActiveAxis.Z) != 0)
        {
            Vector3 zColor = GetAxisColor(ActiveAxis.Z);
            Vector3 zConePos = virtualPosition + zAxis * scaledLength;
            GizmoCones[2] = (zConePos, zAxis, scaledHeight, scaledRadius, zColor);
        }
        else
        {
            GizmoCones[2] = (Vector3.Zero, Vector3.UnitZ, 0, 0, Vector3.Zero);
        }
    }
    
    // Update lines with hover visual feedback - no thickness change
    private static void UpdateGizmoLines()
    {
        Matrix4x4 orientation = GetGizmoOrientation();
        float scale = GetGizmoScale();
        
        Vector3 xAxis = Vector3.Transform(Vector3.UnitX, orientation);
        Vector3 yAxis = Vector3.UnitY;
        Vector3 zAxis = Vector3.Transform(Vector3.UnitZ, orientation);
        
        float scaledLength = lineLength * scale;
        
        // X-axis line - only render if X axis is visible
        if ((VisibleAxes & ActiveAxis.X) != 0)
        {
            Vector3 xColor = GetAxisColor(ActiveAxis.X);
            GizmoLines[0] = (virtualPosition, virtualPosition + (xAxis * scaledLength), xColor, lineThickness * scale);
        }
        else
        {
            // Set invisible line (zero length or transparent)
            GizmoLines[0] = (Vector3.Zero, Vector3.Zero, Vector3.Zero, 0);
        }
        
        // Y-axis line
        if ((VisibleAxes & ActiveAxis.Y) != 0)
        {
            Vector3 yColor = GetAxisColor(ActiveAxis.Y);
            GizmoLines[1] = (virtualPosition, virtualPosition + (yAxis * scaledLength), yColor, lineThickness * scale);
        }
        else
        {
            GizmoLines[1] = (Vector3.Zero, Vector3.Zero, Vector3.Zero, 0);
        }
        
        // Z-axis line
        if ((VisibleAxes & ActiveAxis.Z) != 0)
        {
            Vector3 zColor = GetAxisColor(ActiveAxis.Z);
            GizmoLines[2] = (virtualPosition, virtualPosition + (zAxis * scaledLength), zColor, lineThickness * scale);
        }
        else
        {
            GizmoLines[2] = (Vector3.Zero, Vector3.Zero, Vector3.Zero, 0);
        }
    }

    private static void UpdateGizmoPlanes()
    {
        Matrix4x4 orientation = GetGizmoOrientation();
        float scale = GetGizmoScale();
        
        float size = planeSize * lineLength * scale;
        
        Vector3 xAxis = Vector3.Transform(Vector3.UnitX, orientation);
        Vector3 yAxis = Vector3.UnitY;
        Vector3 zAxis = Vector3.Transform(Vector3.UnitZ, orientation);
        
        // XY-plane (only if XY is visible AND both X and Y are visible)
        if ((VisibleAxes & ActiveAxis.XY) != 0 && 
            (VisibleAxes & ActiveAxis.X) != 0 && 
            (VisibleAxes & ActiveAxis.Y) != 0)
        {
            Vector3 xyColor = GetPlaneColor(ActiveAxis.XY);
            Vector3[] xyVerts = new Vector3[4] {
                virtualPosition,
                virtualPosition + xAxis * size,
                virtualPosition + xAxis * size + yAxis * size,
                virtualPosition + yAxis * size
            };
            GizmoPlanes[0] = (xyVerts, new Vector4(xyColor.X, xyColor.Y, xyColor.Z, 1), ActiveAxis.XY, false);
        }
        else
        {
            // Set invisible plane
            GizmoPlanes[0] = (new Vector3[4] {
                Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero
            }, new Vector4(0, 0, 0, 1), ActiveAxis.XY, false);
        }
        
        // XZ-plane
        if ((VisibleAxes & ActiveAxis.XZ) != 0 && 
            (VisibleAxes & ActiveAxis.X) != 0 && 
            (VisibleAxes & ActiveAxis.Z) != 0)
        {
            Vector3 xzColor = GetPlaneColor(ActiveAxis.XZ);
            Vector3[] xzVerts = new Vector3[4] {
                virtualPosition,
                virtualPosition + xAxis * size,
                virtualPosition + xAxis * size + zAxis * size,
                virtualPosition + zAxis * size
            };
            GizmoPlanes[1] = (xzVerts, new Vector4(xzColor.X, xzColor.Y, xzColor.Z, 1), ActiveAxis.XZ, false);
        }
        else
        {
            GizmoPlanes[1] = (new Vector3[4] {
                Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero
            }, new Vector4(0, 0, 0, 1), ActiveAxis.XZ, false);
        }
        
        // YZ-plane
        if ((VisibleAxes & ActiveAxis.YZ) != 0 && 
            (VisibleAxes & ActiveAxis.Y) != 0 && 
            (VisibleAxes & ActiveAxis.Z) != 0)
        {
            Vector3 yzColor = GetPlaneColor(ActiveAxis.YZ);
            Vector3[] yzVerts = new Vector3[4] {
                virtualPosition,
                virtualPosition + yAxis * size,
                virtualPosition + yAxis * size + zAxis * size,
                virtualPosition + zAxis * size
            };
            GizmoPlanes[2] = (yzVerts, new Vector4(yzColor.X, yzColor.Y, yzColor.Z, 1), ActiveAxis.YZ, false);
        }
        else
        {
            GizmoPlanes[2] = (new Vector3[4] {
                Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero
            }, new Vector4(0, 0, 0, 1), ActiveAxis.YZ, false);
        }
    }

    private static void UpdateRotationCircles()
    {
        if (CurrentMode != GizmoMode.Rotate)
            return;
        
        float scale = GetGizmoScale();
        float radius = rotationCircleRadius * scale;
        
        // X-axis rotation circle (YZ plane)
        if ((VisibleAxes & ActiveAxis.X) != 0)
        {
            Vector3[] xCirclePoints = new Vector3[rotationCircleSegments];
            Vector3 xColor = GetAxisColor(ActiveAxis.X);
            for (int i = 0; i < rotationCircleSegments; i++)
            {
                float angle = (float)i / rotationCircleSegments * MathF.PI * 2;
                float y = MathF.Sin(angle) * radius;
                float z = MathF.Cos(angle) * radius;
                xCirclePoints[i] = virtualPosition + new Vector3(0, y, z);
            }
            rotationCircles[0] = (xCirclePoints, xColor, ActiveAxis.X);
        }
        else
        {
            // Empty points array for invisible circle
            rotationCircles[0] = (new Vector3[0], Vector3.Zero, ActiveAxis.X);
        }
        
        // Y-axis rotation circle (XZ plane)
        if ((VisibleAxes & ActiveAxis.Y) != 0)
        {
            Vector3[] yCirclePoints = new Vector3[rotationCircleSegments];
            Vector3 yColor = GetAxisColor(ActiveAxis.Y);
            for (int i = 0; i < rotationCircleSegments; i++)
            {
                float angle = (float)i / rotationCircleSegments * MathF.PI * 2;
                float x = MathF.Sin(angle) * radius;
                float z = MathF.Cos(angle) * radius;
                yCirclePoints[i] = virtualPosition + new Vector3(x, 0, z);
            }
            rotationCircles[1] = (yCirclePoints, yColor, ActiveAxis.Y);
        }
        else
        {
            rotationCircles[1] = (new Vector3[0], Vector3.Zero, ActiveAxis.Y);
        }
        
        // Z-axis rotation circle (XY plane)
        if ((VisibleAxes & ActiveAxis.Z) != 0)
        {
            Vector3[] zCirclePoints = new Vector3[rotationCircleSegments];
            Vector3 zColor = GetAxisColor(ActiveAxis.Z);
            for (int i = 0; i < rotationCircleSegments; i++)
            {
                float angle = (float)i / rotationCircleSegments * MathF.PI * 2;
                float x = MathF.Sin(angle) * radius;
                float y = MathF.Cos(angle) * radius;
                zCirclePoints[i] = virtualPosition + new Vector3(x, y, 0);
            }
            rotationCircles[2] = (zCirclePoints, zColor, ActiveAxis.Z);
        }
        else
        {
            rotationCircles[2] = (new Vector3[0], Vector3.Zero, ActiveAxis.Z);
        }
    }

    private static List<(Vector3 start, Vector3 end, Vector3 color, float thickness)> GetPlaneOutlines()
    {
        var outlines = new List<(Vector3 start, Vector3 end, Vector3 color, float thickness)>();
        float scale = GetGizmoScale();
        float outlineThickness = lineThickness * 0.5f * scale; // Thinner than axis lines
        
        // Only show outlines for hovered or active planes
        for (int i = 0; i < GizmoPlanes.Count; i++)
        {
            var plane = GizmoPlanes[i];
            
            // Skip if not hovered or active
            if (hoveredAxis != plane.axis && DraggingAxis != plane.axis)
                continue;

            Vector3 outlineColor = (DraggingAxis == plane.axis) ? activeColor : new Vector3(plane.color.X * 1.5f, plane.color.Y * 1.5f, plane.color.Z * 1.5f);
            Vector3[] verts = plane.vertices;
            
            // Add outline for each edge of the quad
            for (int j = 0; j < 4; j++)
            {
                int next = (j + 1) % 4;
                outlines.Add((verts[j], verts[next], outlineColor, outlineThickness));
            }
        }
        
        return outlines;
    }
    
    // Modified helper to determine color based on axis state - now includes hover darkening
    private static Vector3 GetAxisColor(ActiveAxis axis)
    {
        // Default colors for each axis
        Vector3 baseColor;
        switch (axis)
        {
            case ActiveAxis.X: baseColor = Vector3.UnitX; break;     // Red
            case ActiveAxis.Y: baseColor = Vector3.UnitY; break;     // Green
            case ActiveAxis.Z: baseColor = Vector3.UnitZ; break;     // Blue
            default: return Vector3.One;
        }
        
        // If this axis is being dragged, or if a plane that includes this axis is being dragged
        if (DraggingAxis == axis || 
            (DraggingAxis == ActiveAxis.XY && (axis == ActiveAxis.X || axis == ActiveAxis.Y)) ||
            (DraggingAxis == ActiveAxis.XZ && (axis == ActiveAxis.X || axis == ActiveAxis.Z)) ||
            (DraggingAxis == ActiveAxis.YZ && (axis == ActiveAxis.Y || axis == ActiveAxis.Z)))
        {
            return activeColor; // Use the yellow active color
        }
        
        // If another axis is being dragged, make this one semi-transparent
        if (DraggingAxis != ActiveAxis.None)
        {
            return baseColor * inactiveAlpha;
        }
        
        // If axis is hovered, or if a plane that includes this axis is hovered
        if (hoveredAxis == axis || 
            (hoveredAxis == ActiveAxis.XY && (axis == ActiveAxis.X || axis == ActiveAxis.Y)) ||
            (hoveredAxis == ActiveAxis.XZ && (axis == ActiveAxis.X || axis == ActiveAxis.Z)) ||
            (hoveredAxis == ActiveAxis.YZ && (axis == ActiveAxis.Y || axis == ActiveAxis.Z)))
        {
            return baseColor * hoverDarkenFactor; // Darken the color
        }
        
        // Otherwise use normal color
        return baseColor;
    }

    private static Vector3 GetPlaneColor(ActiveAxis planeAxis)
    {
        // Base colors for each plane - color scheme matching the axes
        Vector3 baseColor;
        switch (planeAxis)
        {
            case ActiveAxis.XY: 
                // For XY plane, blue for Z
                baseColor = Vector3.UnitZ;
                break;
            case ActiveAxis.XZ: 
                // For XZ plane, green for Y
                baseColor = Vector3.UnitY;
                break;
            case ActiveAxis.YZ: 
                // For YZ plane, red for X
                baseColor = Vector3.UnitX;
                break;
            default:
                return Vector3.One;
        }
        
        // If this is the dragging plane, use yellow with higher opacity
        if (DraggingAxis == planeAxis)
        {
            return activeColor * planeHoverOpacity;
        }
        
        // If we're dragging any axis, make other planes more transparent
        if (DraggingAxis != ActiveAxis.None)
        {
            return baseColor * (planeOpacity * 0.5f);
        }
        
        // If plane is hovered, make it darker and more opaque
        if (hoveredAxis == planeAxis)
        {
            return baseColor * hoverDarkenFactor * planeHoverOpacity;
        }
        
        // Default transparency
        return baseColor * planeOpacity;
    }
}

public struct Plane
{
    public Vector3 Normal;
    public float D; // Distance from origin
    
    public Plane(Vector3 normal, float d)
    {
        Normal = Vector3.Normalize(normal);
        D = d;
    }
}