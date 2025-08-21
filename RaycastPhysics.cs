using System.Numerics;
using Silk.NET.Maths;

public class RaycastPhysics
{
    // Convert screen point to ray
    public static Ray ScreenPointToRay(Vector2 mousePosition)
    {
        var window = MainScript.window;
        var camera = MainScript.cam;
        
        // Convert mouse position to normalized device coordinates (-1 to +1)
        float ndcX = (2.0f * mousePosition.X) / window.Size.X - 1.0f;
        float ndcY = 1.0f - (2.0f * mousePosition.Y) / window.Size.Y; // Flip Y for OpenGL
        
        // Create clip space position
        Vector4 clipCoords = new Vector4(ndcX, ndcY, -1.0f, 1.0f);
        
        Matrix4x4.Invert(MainScript.cam.projection, out Matrix4x4 invProjection);
        Matrix4x4.Invert(MainScript.cam.view, out Matrix4x4 invView);
        
        // To eye space
        Vector4 eyeCoords = Vector4.Transform(clipCoords, invProjection);
        eyeCoords = new Vector4(eyeCoords.X, eyeCoords.Y, -1.0f, 0.0f);
        
        // To world space
        Vector4 worldCoords = Vector4.Transform(eyeCoords, invView);
        Vector3 rayDirection = Vector3.Normalize(new Vector3(worldCoords.X, worldCoords.Y, worldCoords.Z));
        
        return new Ray(camera.Pos, rayDirection);
    }

    public static RaycastHit RaycastPlane(Ray ray, int axis, float coordinate)
    {
        // Get the plane normal based on the axis
        Vector3 planeNormal = Vector3.Zero;
        planeNormal[axis] = 1.0f;
        
        // Create a point on the plane
        Vector3 planePoint = Vector3.Zero;
        planePoint[axis] = coordinate;
        
        // Calculate denominator for intersection test
        float denom = Vector3.Dot(planeNormal, ray.Direction);
        
        // If denominator is close to 0, ray is parallel to the plane
        if (Math.Abs(denom) < 1e-6)
        {
            return RaycastHit.Miss;
        }
        
        // Calculate the distance from ray origin to intersection
        float t = Vector3.Dot(planePoint - ray.Origin, planeNormal) / denom;
        
        // If t is negative, the intersection is behind the ray origin
        if (t < 0)
        {
            return RaycastHit.Miss;
        }
        
        // Calculate the intersection point
        Vector3 intersectionPoint = ray.Origin + ray.Direction * t;
        return RaycastHit.Create(intersectionPoint, planeNormal, t);
    }

    // Transform ray from world space to object's local space
    private static Ray TransformRayToLocalSpace(Ray ray, Matrix4x4 worldMatrix)
    {
        //Matrix4x4 invWorldMatrix = Matrix4x4.Invert(worldMatrix);
        Matrix4x4.Invert(worldMatrix, out Matrix4x4 invWorldMatrix);
        
        // Transform origin and direction to local space
        Vector3 localOrigin = Vector3.Transform(ray.Origin, invWorldMatrix);
        
        // For direction vectors, we should use TransformNormal which doesn't apply translation
        Vector3 localDir = Vector3.TransformNormal(ray.Direction, invWorldMatrix);
        localDir = Vector3.Normalize(localDir);
        
        return new Ray(localOrigin, localDir);
    }

    public static RaycastHit RaycastBrushes(Ray ray)
    {
        RaycastHit closestHit = RaycastHit.Miss;
        float closestDistance = float.MaxValue;
        
        foreach (Brush brush in WorldManager.brushes)
        {
            // First do quick AABB test as an optimization
            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);
            
            foreach (var vertex in brush.Vertices)
            {
                Vector3 worldVertex = vertex + brush.Pos;
                min = Vector3.Min(min, worldVertex);
                max = Vector3.Max(max, worldVertex);
            }
            
            if (!RayIntersectsBox(ray, min, max, out _, out _))
                continue; // Skip detailed test if we don't hit the bounding box
                
            // Do precise triangle intersection
            for (int i = 0; i < brush.Indices.Length; i += 3)
            {
                // Get triangle vertices in world space
                Vector3 v0 = brush.Vertices[brush.Indices[i]] + brush.Pos;
                Vector3 v1 = brush.Vertices[brush.Indices[i+1]] + brush.Pos;
                Vector3 v2 = brush.Vertices[brush.Indices[i+2]] + brush.Pos;
                
                if (RayIntersectsTriangle(ray, v0, v1, v2, out float distance, out Vector3 normal))
                {
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        Vector3 hitPoint = ray.Origin + ray.Direction * distance;
                        closestHit = RaycastHit.Create(hitPoint, normal, distance, brush);
                    }
                }
            }
        }
        
        return closestHit;
    }

    private static bool RayIntersectsTriangle(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2, out float distance, out Vector3 normal)
    {
        // Implement Möller–Trumbore algorithm for ray-triangle intersection
        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;
        normal = Vector3.Normalize(Vector3.Cross(edge1, edge2));
        
        Vector3 h = Vector3.Cross(ray.Direction, edge2);
        float a = Vector3.Dot(edge1, h);
        
        // Check if ray is parallel to triangle
        if (Math.Abs(a) < 1e-6)
        {
            distance = 0;
            return false;
        }
        
        float f = 1.0f / a;
        Vector3 s = ray.Origin - v0;
        float u = f * Vector3.Dot(s, h);
        
        if (u < 0 || u > 1)
        {
            distance = 0;
            return false;
        }
        
        Vector3 q = Vector3.Cross(s, edge1);
        float v = f * Vector3.Dot(ray.Direction, q);
        
        if (v < 0 || u + v > 1)
        {
            distance = 0;
            return false;
        }
        
        distance = f * Vector3.Dot(edge2, q);
        return distance > 0;
    }

    /*
    private static bool RayIntersectsBox(Ray ray, Vector3 boxMin, Vector3 boxMax, out float distance, out Vector3 normal)
    {
        // Standard ray-AABB intersection algorithm
        Vector3 tMin = (boxMin - ray.Origin) / ray.Direction;
        Vector3 tMax = (boxMax - ray.Origin) / ray.Direction;

        Vector3 t1 = Vector3.Min(tMin, tMax);
        Vector3 t2 = Vector3.Max(tMin, tMax);

        float tNear = Math.Max(Math.Max(t1.X, t1.Y), t1.Z);
        float tFar = Math.Min(Math.Min(t2.X, t2.Y), t2.Z);

        distance = tNear;

        // Determine the normal based on which face was hit
        normal = Vector3.UnitX; // Default

        if (tNear > tFar || tFar < 0)
        {
            distance = 0;
            normal = Vector3.Zero;
            return false;
        }

        // Calculate which face the ray hit to determine normal
        if (tNear == t1.X) normal = new Vector3(-Math.Sign(ray.Direction.X), 0, 0);
        else if (tNear == t1.Y) normal = new Vector3(0, -Math.Sign(ray.Direction.Y), 0);
        else if (tNear == t1.Z) normal = new Vector3(0, 0, -Math.Sign(ray.Direction.Z));

        return true;
    }
    */
    
    private static bool RayIntersectsBox(Ray ray, Vector3 boxMin, Vector3 boxMax, out float distance, out Vector3 normal)
    {
        // Handle division by zero in the direction
        Vector3 invDir = new Vector3(
            Math.Abs(ray.Direction.X) < 1e-6 ? 1e6f * Math.Sign(ray.Direction.X) : 1.0f / ray.Direction.X,
            Math.Abs(ray.Direction.Y) < 1e-6 ? 1e6f * Math.Sign(ray.Direction.Y) : 1.0f / ray.Direction.Y,
            Math.Abs(ray.Direction.Z) < 1e-6 ? 1e6f * Math.Sign(ray.Direction.Z) : 1.0f / ray.Direction.Z
        );
        
        // Calculate intersection distances
        Vector3 tMin = (boxMin - ray.Origin) * invDir;
        Vector3 tMax = (boxMax - ray.Origin) * invDir;
        
        // Ensure tMin contains the minimum values and tMax the maximum
        Vector3 t1 = Vector3.Min(tMin, tMax);
        Vector3 t2 = Vector3.Max(tMin, tMax);
        
        float tNear = Math.Max(Math.Max(t1.X, t1.Y), t1.Z);
        float tFar = Math.Min(Math.Min(t2.X, t2.Y), t2.Z);
        
        distance = tNear;
        
        // No intersection if tNear > tFar or tFar < 0
        if (tNear > tFar || tFar < 0)
        {
            distance = 0;
            normal = Vector3.Zero;
            return false;
        }
        
        // Determine which face was hit to correctly set the normal
        if (Math.Abs(tNear - t1.X) < 1e-6) normal = new Vector3(-Math.Sign(ray.Direction.X), 0, 0);
        else if (Math.Abs(tNear - t1.Y) < 1e-6) normal = new Vector3(0, -Math.Sign(ray.Direction.Y), 0);
        else if (Math.Abs(tNear - t1.Z) < 1e-6) normal = new Vector3(0, 0, -Math.Sign(ray.Direction.Z));
        else normal = -Vector3.Normalize(ray.Direction); // Fallback
        
        return true;
    }

    // Update the IntersectAABB method to return RaycastHit
    private static RaycastHit IntersectAABB(Ray ray, Vector3 min, Vector3 max)
    {
        float tMin = float.MinValue;
        float tMax = float.MaxValue;

        // Track which face was hit (for normal calculation)
        int minFaceIndex = -1;

        // For each axis
        for (int i = 0; i < 3; i++)
        {
            // Ray is parallel to the planes
            if (Math.Abs(ray.Direction[i]) < 1e-6)
            {
                // Ray origin is outside the slab, no intersection
                if (ray.Origin[i] < min[i] || ray.Origin[i] > max[i])
                    return RaycastHit.Miss;
            }
            else
            {
                // Calculate intersections with the slabs
                float ood = 1.0f / ray.Direction[i];
                float t1 = (min[i] - ray.Origin[i]) * ood;
                float t2 = (max[i] - ray.Origin[i]) * ood;

                // Make t1 the near intersection and t2 the far intersection
                bool flipped = false;
                if (t1 > t2)
                {
                    float temp = t1;
                    t1 = t2;
                    t2 = temp;
                    flipped = true;
                }

                // Update tMin and record which face was hit
                if (t1 > tMin)
                {
                    tMin = t1;
                    // Determine which face based on the axis and whether we flipped
                    minFaceIndex = i * 2 + (flipped ? 1 : 0);
                }

                tMax = Math.Min(tMax, t2);

                // No intersection if tMin > tMax
                if (tMin > tMax)
                    return RaycastHit.Miss;
            }
        }

        // If tMin is negative, ray origin is inside the AABB
        float hitDistance = tMin >= 0 ? tMin : tMax;

        // Calculate hit point
        Vector3 hitPoint = ray.Origin + ray.Direction * hitDistance;

        // Calculate hit normal based on the face index
        Vector3 normal = Vector3.Zero;
        int axis = minFaceIndex / 2;
        bool isPositiveFace = minFaceIndex % 2 == 1;
        normal[axis] = isPositiveFace ? 1.0f : -1.0f;

        return RaycastHit.Create(hitPoint, normal, hitDistance);
    }
}

public struct Ray
{
    public Vector3 Origin;
    public Vector3 Direction;
    
    public Ray(Vector3 origin, Vector3 direction)
    {
        Origin = origin;
        Direction = Vector3.Normalize(direction);
    }
    
    public Vector3 GetPoint(float distance)
    {
        return Origin + Direction * distance;
    }
}

public struct RaycastHit
{
    // Basic hit information
    public bool HasHit;
    public Vector3 Point;
    public Vector3 Normal;
    public float Distance;
    
    // Object and metadata
    public object HitObject;
    
    // Static factory method for misses (no hit)
    public static RaycastHit Miss => new RaycastHit { HasHit = false };
    
    // Static factory method for creating hits
    public static RaycastHit Create(Vector3 point, Vector3 normal, float distance, object hitObject = null)
    {
        return new RaycastHit
        {
            HasHit = true,
            Point = point,
            Normal = normal,
            Distance = distance,
            HitObject = hitObject
        };
    }
}