
using System;
using System.Collections.Generic;
using System.Numerics;

public class CustomCamera
{
    public Vector3 Pos;

    public Vector2 Angle;
    public Vector3 Direction;

    public Matrix4x4 view;
    public Matrix4x4 projection;


    public Vector3 Forward {private set; get; }
    public Vector3 Up {private set; get; }
    public Vector3 Right {private set; get; }

    public float FOV;

    public void UpdateDirection()
    {
        Direction.X = MathF.Cos(MainScript.DegreesToRadians(Angle.X)) * MathF.Cos(MainScript.DegreesToRadians(Angle.Y));
        Direction.Y = MathF.Sin(MainScript.DegreesToRadians(Angle.Y));
        Direction.Z = MathF.Sin(MainScript.DegreesToRadians(Angle.X)) * MathF.Cos(MainScript.DegreesToRadians(Angle.Y));

        Forward = Vector3.Normalize(Direction);
        Right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, Forward));
        Up = Vector3.Normalize(Vector3.Cross(Forward, Right));
    }

    public Vector2 WorldToScreen(Vector3 worldPosition)
    {   
        // Transform the world position to clip space
        Vector4 clipSpace = Vector4.Transform(
            new Vector4(worldPosition, 1.0f),
            view * projection);
        
        // Perform perspective division to get normalized device coordinates
        if (Math.Abs(clipSpace.W) > 0.000001f) // Avoid division by near-zero
        {
            float invW = 1.0f / clipSpace.W;
            clipSpace.X *= invW;
            clipSpace.Y *= invW;
            clipSpace.Z *= invW;
        }
        
        // Map to window coordinates
        float screenX = ((clipSpace.X + 1.0f) / 2.0f) * MainScript.window.Size.X;
        float screenY = ((1.0f - clipSpace.Y) / 2.0f) * MainScript.window.Size.Y; // Y is flipped in screen coordinates
        
        return new Vector2(screenX, screenY);
    }

    public bool IsInView(Vector3 worldPosition, float boundsRadius = 0f)
    {    
        // Transform the world position to clip space
        Vector4 clipSpace = Vector4.Transform(
            new Vector4(worldPosition, 1.0f),
            view * projection);
        
        // Check if the point is behind the camera
        if (clipSpace.W <= 0)
            return false;
        
        // Perform perspective division to get normalized device coordinates (NDC)
        float invW = 1.0f / clipSpace.W;
        float ndcX = clipSpace.X * invW;
        float ndcY = clipSpace.Y * invW;
        float ndcZ = clipSpace.Z * invW;
        
        // Apply bounds radius adjustment in NDC space
        // This converts the world-space radius to NDC-space
        float radiusAdjustment = 0;
        if (boundsRadius > 0)
        {
            // Project a point slightly offset from the original position
            Vector3 offsetPos = worldPosition + new Vector3(boundsRadius, 0, 0);
            Vector4 offsetClipSpace = Vector4.Transform(
                new Vector4(offsetPos, 1.0f),
                view * projection);
            
            // Calculate how much the offset affects NDC coords
            if (offsetClipSpace.W > 0)
            {
                float offsetNdcX = offsetClipSpace.X / offsetClipSpace.W;
                radiusAdjustment = Math.Abs(offsetNdcX - ndcX);
            }
        }
        
        // Check if the point is within the normalized device coordinates (-1 to 1)
        // Include the bounds radius adjustment for more forgiving checks
        bool isInNdcX = ndcX >= -1.0f - radiusAdjustment && ndcX <= 1.0f + radiusAdjustment;
        bool isInNdcY = ndcY >= -1.0f - radiusAdjustment && ndcY <= 1.0f + radiusAdjustment;
        bool isInNdcZ = ndcZ >= -1.0f && ndcZ <= 1.0f; // Z is typically 0 to 1 in NDC space
        
        return isInNdcX && isInNdcY && isInNdcZ;
    }
}