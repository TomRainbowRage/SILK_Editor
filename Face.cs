using System.Numerics;

// Holds texture transformation data for each face
public struct Face
{
    public Vector2 Scale;    // Scales the UV coordinates
    public Vector2 Offset;   // Offsets the UV coordinates
    public float Rotation;   // Rotation in radians
    public float NormalStrength;
    public float Metallic;
    public float Smoothness;

    public Texture Tex;

    // Constructor with all parameters
    public Face(Texture tex, Vector2 scale, Vector2 offset, float rotation)
    {
        Tex = tex;
        Scale = scale;
        Offset = offset;
        Rotation = rotation;
    }
}
