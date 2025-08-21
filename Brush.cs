using System.Numerics;
using Silk.NET.OpenGL;
using System.Drawing.Drawing2D;

public class Brush
{
    public BufferObject<float> Vbo { get; }
    public BufferObject<uint> Ebo { get; }
    public VertexArrayObject<float, uint> Vao { get; }

    public Face[] Faces;
    public Vector3[] FaceCenters { get; private set; }
    public Dictionary<(int, int), Vector3> EdgeMidpoints = new Dictionary<(int, int), Vector3>();
    public Dictionary<int, List<(int, int)>> FaceEdges = new Dictionary<int, List<(int, int)>>();
    public Dictionary<(int, int, int, int), int> IndicesToFaceIndex { get; private set; } = new Dictionary<(int, int, int, int), int>();

    public Vector3[] Vertices;
    public Vector2[] Uvs;

    public Vector3[] Normals;

    public float[] GLRenderVertices;

    public uint[] Indices;

    public Vector3 Pos;

    public Vector3 Rot;

    private Vector3 scaleTrack = Vector3.Zero;
    public Vector3 Scale
    {
        get { return scaleTrack; }
        set { scaleTrack = value; RecalculateMatrix(); }
    }

    public Matrix4x4 ViewMatrix { get; private set; }


    public bool Active = true;
    public string Name = "";

    // Constructor that takes multiple textures and texture transforms
    public Brush(Vector3 pos, Vector3 scale, Vector3[] vertices, Vector2[] uvs, Vector3[] normals, uint[] indices, Face[] faces)
    {
        Name = "New Brush";

        Vertices = vertices;
        Uvs = uvs;
        Indices = indices;
        Normals = normals;

        Pos = pos;

        // Add face indices to the interleaved data
        GLRenderVertices = GetInterleavedDataWithFaceIndices(vertices, uvs, normals);

        Vbo = new BufferObject<float>(MainScript.Gl, GLRenderVertices, BufferTargetARB.ArrayBuffer);
        Ebo = new BufferObject<uint>(MainScript.Gl, indices, BufferTargetARB.ElementArrayBuffer);
        Vao = new VertexArrayObject<float, uint>(MainScript.Gl, Vbo, Ebo);

        Vao.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 9, 0); // Position
        Vao.VertexAttributePointer(1, 2, VertexAttribPointerType.Float, 9, 3); // UV
        Vao.VertexAttributePointer(2, 3, VertexAttribPointerType.Float, 9, 5); // Normal
        Vao.VertexAttributePointer(3, 1, VertexAttribPointerType.Float, 9, 8); // Face index

        Faces = faces;
        FaceCenters = new Vector3[faces.Length];

        for (int i = 0; i < Faces.Length; i++)
        {
            Faces[i].Tex.Bind();
            MainScript.Gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            MainScript.Gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        }

        scaleTrack = scale;

        CalculateFaceAndEdgeData();
        RecalculateMatrix();
    }

    public void RecalculateMatrix()
    {
        ViewMatrix =
            Matrix4x4.CreateRotationX(((float)Math.PI / 180) * Rot.X) *
            Matrix4x4.CreateRotationY(((float)Math.PI / 180) * Rot.Y) *
            Matrix4x4.CreateRotationZ(((float)Math.PI / 180) * Rot.Z) *

            Matrix4x4.CreateScale(Scale) *

            Matrix4x4.CreateTranslation(Pos);

        UpdateFaceAndEdgeData();

        GLRenderVertices = GetInterleavedDataWithFaceIndices(Vertices, Uvs, Normals);
        Vbo.Update(GLRenderVertices);
    }

    public void CenterBrushPosFromMesh()
    {
        /*
        Vector3 vertsSum = Vector3.Zero;
        foreach (Vector3 vert in Vertices) { vertsSum += vert; }

        Vector3 targetPosOffet = (vertsSum / Vertices.Length) - Pos;

        Pos += targetPosOffet / 2;

        for (int i = 0; i < Vertices.Length; i++)
        {
            Vertices[i] -= targetPosOffet / 2;
        }
        */
        Vector3 center = Vector3.Zero;
        foreach (Vector3 vert in Vertices)
        {
            center += vert;
        }
        center /= Vertices.Length;

        // Move all vertices so their center is at the origin (0,0,0)
        for (int i = 0; i < Vertices.Length; i++)
        {
            Vertices[i] -= center;
        }

        // Update the brush position to compensate
        Pos += center;
    }

    public void Dispose()
    {
        Vbo.Dispose();
        Ebo.Dispose();
        Vao.Dispose();
        foreach (Face face in Faces)
        {
            face.Tex.Dispose();
        }
    }

    // New method that includes face indices for multi-texture support
    private static float[] GetInterleavedDataWithFaceIndices(Vector3[] verts, Vector2[] uvs, Vector3[] normals)
    {
        float[] interleaved = new float[verts.Length * 9]; // 3 (position) + 2 (UV) + 3 (normal) + 1 (face index)

        // Assign face indices (4 vertices per face, 6 faces total)
        for (int face = 0; face < 6; face++)
        {
            int vertexOffset = face * 4; // Each face has 4 vertices
            for (int i = 0; i < 4; i++)
            {
                int vertexIndex = vertexOffset + i;
                int dataIndex = vertexIndex * 9;

                interleaved[dataIndex + 0] = verts[vertexIndex].X;
                interleaved[dataIndex + 1] = verts[vertexIndex].Y;
                interleaved[dataIndex + 2] = verts[vertexIndex].Z;
                interleaved[dataIndex + 3] = uvs[vertexIndex].X;
                interleaved[dataIndex + 4] = uvs[vertexIndex].Y;
                interleaved[dataIndex + 5] = normals[vertexIndex].X;
                interleaved[dataIndex + 6] = normals[vertexIndex].Y;
                interleaved[dataIndex + 7] = normals[vertexIndex].Z;
                interleaved[dataIndex + 8] = face; // Face index (0-5)
            }
        }

        return interleaved;
    }

    private void UpdateFaceAndEdgeData()
    {
        // Update face centers with the new transformation
        for (int face = 0; face < 6; face++)
        {
            // Get vertices for this face
            int baseIndex = face * 4;
            Vector3 v0 = Vertices[baseIndex];
            Vector3 v1 = Vertices[baseIndex + 1];
            Vector3 v2 = Vertices[baseIndex + 2];
            Vector3 v3 = Vertices[baseIndex + 3];

            // Calculate face center by averaging the vertices
            FaceCenters[face] = (v0 + v1 + v2 + v3) / 4.0f + Pos;
        }

        // Update edge midpoints
        foreach (var edge in EdgeMidpoints.Keys.ToList())
        {
            // Calculate midpoint in local space
            Vector3 midpoint = (Vertices[edge.Item1] + Vertices[edge.Item2]) / 2.0f;

            // Store the updated midpoint in world space
            EdgeMidpoints[edge] = midpoint + Pos;
        }
    }

    /*
    public void CalculateFaceNormalDict()
    {
        NormalToFace.Clear();
        // Use a custom comparer to ensure consistent normal key lookup
        var normalComparer = new NormalVectorComparer();
        NormalToFace = new Dictionary<Vector3, List<uint>>(normalComparer);

        //

        // Process each triangle
        for (int i = 0; i < Indices.Length; i += 3)
        {
            // Get vertices of the triangle
            Vector3 v0 = Vertices[Indices[i]];
            Vector3 v1 = Vertices[Indices[i + 1]];
            Vector3 v2 = Vertices[Indices[i + 2]];
            
            // Calculate triangle edges
            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;

            // Calculate normal (preserving direction)
            Vector3 normal = Vector3.Normalize(Vector3.Cross(edge1, edge2));
            
            // Round normal components to avoid floating point precision issues
            normal = new Vector3(
                (float)Math.Round(normal.X, 5),
                (float)Math.Round(normal.Y, 5),
                (float)Math.Round(normal.Z, 5)
            );
            
            // Debug output
            Console.WriteLine($"Triangle {i/3}: Normal = {normal}");
            
            // Add to dictionary, creating new list if needed
            if(!NormalToFace.ContainsKey(normal)) 
            { 
                NormalToFace.Add(normal, new List<uint>()); 
            }
            
            // Add vertex indices if not already present
            if(!NormalToFace[normal].Contains(Indices[i])) { NormalToFace[normal].Add(Indices[i]); }
            if(!NormalToFace[normal].Contains(Indices[i + 1])) { NormalToFace[normal].Add(Indices[i + 1]); }
            if(!NormalToFace[normal].Contains(Indices[i + 2])) { NormalToFace[normal].Add(Indices[i + 2]); }
        }
        
        // Debug output
        Console.WriteLine($"Face count: {NormalToFace.Count}");
        foreach (var normal in NormalToFace.Keys)
        {
            Console.WriteLine($"Normal {normal} has {NormalToFace[normal].Count} vertices");
        }

        //
    }
    */

    private void CalculateFaceAndEdgeData()
    {
        EdgeMidpoints.Clear();
        FaceEdges.Clear();
        IndicesToFaceIndex.Clear();

        // For each face in the mesh
        for (int face = 0; face < Faces.Length; face++)
        {
            // Calculate which vertices belong to this face using the face index in interleaved data
            List<int> faceVertexIndices = new List<int>();
            for (int i = 0; i < Vertices.Length; i++)
            {
                int dataIndex = i * 9 + 8;
                if (dataIndex < GLRenderVertices.Length && (int)GLRenderVertices[dataIndex] == face)
                {
                    faceVertexIndices.Add(i);
                }
            }

            IndicesToFaceIndex[(faceVertexIndices[0], faceVertexIndices[1], faceVertexIndices[2], faceVertexIndices[3])] = face;

            // Calculate face center
            Vector3 faceCenter = Vector3.Zero;
            foreach (int vertexIndex in faceVertexIndices)
            {
                faceCenter += Vertices[vertexIndex];
            }
            FaceCenters[face] = faceCenter / faceVertexIndices.Count;

            // Determine face edges
            List<(int, int)> edges = new List<(int, int)>();
            for (int i = 0; i < faceVertexIndices.Count; i++)
            {
                int current = faceVertexIndices[i];
                int next = faceVertexIndices[(i + 1) % faceVertexIndices.Count];
                edges.Add((current, next));
            }

            FaceEdges[face] = edges;

            // Calculate edge midpoints
            foreach (var edge in edges)
            {
                if (!EdgeMidpoints.ContainsKey(edge) && !EdgeMidpoints.ContainsKey((edge.Item2, edge.Item1)))
                {
                    Vector3 midpoint = (Vertices[edge.Item1] + Vertices[edge.Item2]) / 2.0f;
                    EdgeMidpoints[edge] = midpoint;
                }
            }
        }
    }

    public Vector3 GetFaceCenterWorld(int faceIndex)
    {
        if (faceIndex < 0 || faceIndex >= FaceCenters.Length)
            return Pos; // Return brush position if face index is invalid

        return FaceCenters[faceIndex];
    }

    // Helper method to get all edge midpoints for a face in world space
    public List<Vector3> GetFaceEdgeMidpointsWorld(int faceIndex)
    {
        List<Vector3> midpoints = new List<Vector3>();

        if (FaceEdges.ContainsKey(faceIndex))
        {
            foreach (var edge in FaceEdges[faceIndex])
            {
                // Check both possible orderings of the edge
                if (EdgeMidpoints.ContainsKey(edge))
                {
                    midpoints.Add(EdgeMidpoints[edge]);
                }
                else if (EdgeMidpoints.ContainsKey((edge.Item2, edge.Item1)))
                {
                    midpoints.Add(EdgeMidpoints[(edge.Item2, edge.Item1)]);
                }
            }
        }

        return midpoints;
    }


    public static void DisposeBrushList(List<Brush> brushesParam)
    {
        Console.WriteLine("Disposing Brush List  LEN " + brushesParam.Count);

        foreach (Brush brush in brushesParam) { brush.Dispose(); }
    }

    public Vector3 GetMinVert()
    {
        Vector3 minVert = Vertices[0];
        foreach (Vector3 v in Vertices)
        {
            minVert = Vector3.Min(minVert, v);
        }

        return minVert + Pos;
    }
}

public class NormalVectorComparer : IEqualityComparer<Vector3>
{
    // Since we round to 2 decimal places, our tolerance can be smaller
    private const float Tolerance = 0.001f;
    
    public bool Equals(Vector3 x, Vector3 y)
    {
        return Math.Abs(x.X - y.X) < Tolerance &&
               Math.Abs(x.Y - y.Y) < Tolerance &&
               Math.Abs(x.Z - y.Z) < Tolerance;
    }
    
    public int GetHashCode(Vector3 obj)
    {
        // Round to 2 decimal places to match our GetCurrentFaceNormal method
        return HashCode.Combine(
            Math.Round(obj.X, 2),
            Math.Round(obj.Y, 2),
            Math.Round(obj.Z, 2)
        );
    }
}