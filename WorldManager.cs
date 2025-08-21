using System.Numerics;
using Silk.NET.OpenGL;

public class WorldManager
{
    private static GL Gl { get { return MainScript.Gl; } }
    private static Shader defaultShader { get { return MainScript.defaultShader; } }

    public static List<Brush> brushes = new List<Brush>();

    private static (List<Vector3> verts, List<Vector2> uvs, List<Vector3> normals, uint[] indices) _meshCube = default;
    public static (List<Vector3> verts, List<Vector2> uvs, List<Vector3> normals, uint[] indices) MeshCube
    {
        get
        {
            if (_meshCube == default) { _meshCube = GenMeshCube(); }

            return _meshCube;

        }
    }

    private static List<(Vector3 start, Vector3 end, Vector3 color, float thickness)> WorldAlignedAxisLines =
        new List<(Vector3 start, Vector3 end, Vector3 color, float thickness)>() {
        (Vector3.Zero, Vector3.UnitX, Vector3.UnitX, 1),
        (Vector3.Zero, Vector3.UnitY, Vector3.UnitY, 1),
        (Vector3.Zero, Vector3.UnitZ, Vector3.UnitZ, 1),
    };


    public static void OnInit()
    {
        brushes.Add(new Brush(new Vector3(0, 0, 2), Vector3.One, MeshCube.verts.ToArray(), MeshCube.uvs.ToArray(), MeshCube.normals.ToArray(), MeshCube.indices, new Face[] {
            new Face(ResourceManager.GetTexture("dev/dev_light_checker"), Vector2.One, Vector2.Zero, 0),
            new Face(ResourceManager.GetTexture("dev/dev_light_checker"), Vector2.One, Vector2.Zero, 0),
            new Face(ResourceManager.GetTexture("dev/dev_light_checker"), Vector2.One, Vector2.Zero, 0),
            new Face(ResourceManager.GetTexture("dev/dev_light_checker"), Vector2.One, Vector2.Zero, 0),
            new Face(ResourceManager.GetTexture("dev/dev_light_checker"), Vector2.One, Vector2.Zero, 0),
            new Face(ResourceManager.GetTexture("dev/dev_light_checker"), Vector2.One, Vector2.Zero, 0),
        }));
    }

    public static unsafe void OnRender()
    {
        for (int i = 0; i < brushes.Count; i++)
        {
            var mesh = brushes[i];
            mesh.Vao.Bind();
            // Convert bool to int for the shader
            defaultShader.SetUniform("uUseMultiTexture", 1);

            // Set texture transform uniforms for each face
            for (int face = 0; face < mesh.Faces.Length && face < 6; face++)
            {
                Face faceClass = mesh.Faces[face];
                defaultShader.SetUniform($"uTexScale[{face}]", faceClass.Scale);
                defaultShader.SetUniform($"uTexOffset[{face}]", faceClass.Offset);
                defaultShader.SetUniform($"uTexRotation[{face}]", faceClass.Rotation);

                faceClass.Tex.Bind((TextureUnit)((int)TextureUnit.Texture0 + face));
            }

            defaultShader.SetUniform("uModel", mesh.ViewMatrix);

            // Test Comment

            Gl.DrawElements(PrimitiveType.Triangles, (uint)mesh.Indices.Length, DrawElementsType.UnsignedInt, null);


        }

        ThickLineRenderer.Render(MainScript.cam.projection, MainScript.cam.view, WorldAlignedAxisLines);
    }
    private static (List<Vector3> verts, List<Vector2> uvs, List<Vector3> normals, uint[] indices) GenMeshCube()
    {
        return (new List<Vector3>() {
            // Front face
            new(-0.5f, -0.5f,  0.5f), new( 0.5f, -0.5f,  0.5f), new( 0.5f,  0.5f,  0.5f), new(-0.5f,  0.5f,  0.5f),
            // Back face
            new(-0.5f, -0.5f, -0.5f), new( 0.5f, -0.5f, -0.5f), new( 0.5f,  0.5f, -0.5f), new(-0.5f,  0.5f, -0.5f),
            // Left face
            new(-0.5f, -0.5f, -0.5f), new(-0.5f, -0.5f,  0.5f), new(-0.5f,  0.5f,  0.5f), new(-0.5f,  0.5f, -0.5f),
            // Right face
            new( 0.5f, -0.5f, -0.5f), new( 0.5f, -0.5f,  0.5f), new( 0.5f,  0.5f,  0.5f), new( 0.5f,  0.5f, -0.5f),
            // Top face
            new(-0.5f,  0.5f, -0.5f), new( 0.5f,  0.5f, -0.5f), new( 0.5f,  0.5f,  0.5f), new(-0.5f,  0.5f,  0.5f),
            // Bottom face
            new(-0.5f, -0.5f, -0.5f), new( 0.5f, -0.5f, -0.5f), new( 0.5f, -0.5f,  0.5f), new(-0.5f, -0.5f,  0.5f),
        }, new List<Vector2>() {
            // Front face
            new(0.0f, 0.0f), new(1.0f, 0.0f), new(1.0f, 1.0f), new(0.0f, 1.0f),
            // Back face
            new(1.0f, 0.0f), new(0.0f, 0.0f), new(0.0f, 1.0f), new(1.0f, 1.0f),
            // Left face
            new(0.0f, 0.0f), new(1.0f, 0.0f), new(1.0f, 1.0f), new(0.0f, 1.0f),
            // Right face
            new(1.0f, 0.0f), new(0.0f, 0.0f), new(0.0f, 1.0f), new(1.0f, 1.0f),
            // Top face
            new(0.0f, 0.0f), new(1.0f, 0.0f), new(1.0f, 1.0f), new(0.0f, 1.0f),
            // Bottom face
            new(1.0f, 1.0f), new(0.0f, 1.0f), new(0.0f, 0.0f), new(1.0f, 0.0f),
        }, new List<Vector3>() {
            // Front face
            new(0.0f, 0.0f, 1.0f), new(0.0f, 0.0f, 1.0f), new(0.0f, 0.0f, 1.0f), new(0.0f, 0.0f, 1.0f),
            // Back face
            new(0.0f, 0.0f, -1.0f), new(0.0f, 0.0f, -1.0f), new(0.0f, 0.0f, -1.0f), new(0.0f, 0.0f, -1.0f),
            // Left face
            new(-1.0f, 0.0f, 0.0f), new(-1.0f, 0.0f, 0.0f), new(-1.0f, 0.0f, 0.0f), new(-1.0f, 0.0f, 0.0f),
            // Right face
            new(1.0f, 0.0f, 0.0f), new(1.0f, 0.0f, 0.0f), new(1.0f, 0.0f, 0.0f), new(1.0f, 0.0f, 0.0f),
            // Top face
            new(0.0f, 1.0f, 0.0f), new(0.0f, 1.0f, 0.0f), new(0.0f, 1.0f, 0.0f), new(0.0f, 1.0f, 0.0f),
            // Bottom face
            new(0.0f, -1.0f, 0.0f), new(0.0f, -1.0f, 0.0f), new(0.0f, -1.0f, 0.0f), new(0.0f, -1.0f, 0.0f),
        }, new uint[] {
            0, 1, 2,  2, 3, 0,  // Front face
            4, 5, 6,  6, 7, 4,  // Back face
            8, 9, 10, 10, 11, 8, // Left face
            12, 13, 14, 14, 15, 12, // Right face
            16, 17, 18, 18, 19, 16, // Bottom face
            20, 21, 22, 22, 23, 20  // Top face
        });
    }
    
}