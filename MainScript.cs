using Silk.NET.Core;
using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Input;
using Silk.NET.Maths;
using System.Drawing;
using ImGuiNET;
using System.Numerics;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static Gizmo;


public class MainScript
{
    public static IKeyboard keyboard;
    public static IMouse mouse;
    public static IWindow window;

    public static IInputContext inputContext;
    public static ImGuiController imGUI;

    public static GL Gl;
    public static Shader defaultShader;
    public static CustomCamera cam;

    public static bool mouseLock;
    
    public static Dictionary<string, string> debugText = new Dictionary<string, string>();

    // -----

    private static Stopwatch deltaTimeCalc;
    private static float lastFrameTime = 0f;
    public static float deltaTime;
    private static Vector2 lastMousePosition;
    public static Vector2 mouseDelta;

    public static float camAspectRatio = 1;

    public static void Main(string[] args)
    {
        deltaTimeCalc = new Stopwatch();
        deltaTimeCalc.Start();

        MainMethod();
    }

    public static void MainMethod()
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(1920, 1080);
        options.Title = "Silk.NET Game Editor";
        options.WindowState = WindowState.Normal;

        window = Window.Create(options);

        window.Load += OnLoad;
        window.Render += OnRender;
        window.FramebufferResize += OnFramebufferResize;
        window.Resize += delegate (Vector2D<int> s)
        {
            window.Size = new Silk.NET.Maths.Vector2D<int>(s[0], s[1]);
            Gl.Viewport(s);
        };

        window.Update += delegate (double delta)
        {
            if (mouse == null) { return; }

            mouseDelta = mouse.Position - lastMousePosition;
            lastMousePosition = mouse.Position;
        };

        window.Closing += delegate
        {
            imGUI?.Dispose();
            Gl?.Dispose();
        };

        //try { window.Run(); } catch {  } // errors out when trying to exit using esc.
        window.Run();
        
    }


    public static void OnLoad()
    {
        lastFrameTime = (float)deltaTimeCalc.Elapsed.TotalSeconds;

        IInputContext input = window.CreateInput();

        keyboard = input.Keyboards.FirstOrDefault();
        if(keyboard == null) { return; }

        mouse = input.Mice.FirstOrDefault();
        if(mouse == null) { return; }


        keyboard.KeyDown += KeyDown;
        mouse.Cursor.CursorMode = CursorMode.Normal;

        Gl = GL.GetApi(window);
        EnableGLDebugOutput();

        imGUI = new ImGuiController(
            Gl,
            window,
            input
        );

        window.WindowState = WindowState.Maximized;

        ResourceManager.Init();
        defaultShader = ResourceManager.GetShader("default");

        cam = new CustomCamera();
        cam.Pos = new Vector3(10, 6, 10);
        cam.Angle = new Vector2(-45 - 90, -25); //Vector2.Zero;
        cam.FOV = 60;

        cam.UpdateDirection();

        WorldManager.OnInit();

        GridRenderer.Load(true);
        AxisRenderer.Load();
        QuadRenderer.Load();
        ConeRenderer.Load();
        ThickLineRenderer.Load();
        BillboardRenderer.Load();
        //InputManager.Load();

        EditorMain.Load();
        UIRenderer.Load();
        Freecam.Load();
        UIManager.Load();
        Gizmo.Load();
        RealtimeCSG.Load();

        List<(Vector3 start, Vector3 end, Vector3 color)> customLines = new()
        {
            (new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 0)), // Red line
            (new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 0)), // Green line
            (new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 0, 1)), // Blue line
            (new Vector3(1, 1, 1), new Vector3(-1, -1, -1), new Vector3(1, 1, 0)) // Yellow line
        };

        //customLines.Clear();
        //AxisRenderer.UpdateLines(customLines);
    }

    private static unsafe void OnRender(double time)
    {
        float currentTime = (float)deltaTimeCalc.Elapsed.TotalSeconds;
        deltaTime = currentTime - lastFrameTime;
        lastFrameTime = currentTime;

        // Render stuff

        

        

        Gl.Enable(EnableCap.DepthTest);
        //Gl.ClearColor(Color.White);
        Gl.Clear((uint) (ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

        defaultShader.Use();
        
        // Set uniform for texture samplers
        defaultShader.SetUniform("uTexture0", 0);
        defaultShader.SetUniform("uTexture1", 1);
        defaultShader.SetUniform("uTexture2", 2);
        defaultShader.SetUniform("uTexture3", 3);
        defaultShader.SetUniform("uTexture4", 4);
        defaultShader.SetUniform("uTexture5", 5);

        Vector2D<int> size = window.FramebufferSize;

        cam.view = Matrix4x4.CreateLookAt(cam.Pos, cam.Pos + cam.Forward, cam.Up);
        cam.projection = Matrix4x4.CreatePerspectiveFieldOfView(DegreesToRadians(cam.FOV), (float)size.X / size.Y, 0.1f, 100.0f);

        camAspectRatio = (float)size.X / size.Y;

        //Console.WriteLine("Aspect Main Render Loop " + ((float)size.X / size.Y));

        defaultShader.SetUniform("uView", cam.view);
        defaultShader.SetUniform("uProjection", cam.projection);

        WorldManager.OnRender();

        EditorMain.Render();
        
        // Need to Impliment Wireframe
        // Need to add the background Color

        //WorldManager.WorldManagerDraw();

        RealtimeCSG.Render(time, cam.projection, cam.view);
        
        GridRenderer.Render(cam.projection, cam.view);
        AxisRenderer.Render(cam.projection, cam.view);
        Gizmo.Render(time, cam.projection, cam.view);

        RealtimeCSG.RenderAfterGizmo(time, cam.projection, cam.view);

        

        UIRenderer.Render(time);
    }

    private static void OnFramebufferResize(Vector2D<int> newSize)
    {
        Gl.Viewport(newSize);
    }

    // Input callbacks

    private static void KeyDown(IKeyboard keyboard, Key key, int arg3)
    {
        if (key == Key.Escape)
        {
            window.Close();
        }
    }

    public static float DegreesToRadians(float degrees)
    {
        return MathF.PI / 180f * degrees;
    }

    public static int GetInputAxis(Key first, Key second)
    {
        return (Convert.ToInt32(MainScript.keyboard.IsKeyPressed(first))) + -(Convert.ToInt32(MainScript.keyboard.IsKeyPressed(second)));
    }

    private static void EnableGLDebugOutput()
    {
        // Enable debug output
        Gl.Enable(GLEnum.DebugOutput);
        Gl.Enable(GLEnum.DebugOutputSynchronous);

        // Set the debug message callback
        Gl.DebugMessageCallback((source, type, id, severity, length, message, userParam) =>
        {
            if (severity == GLEnum.DebugSeverityNotification) { return; }

            string msg = Marshal.PtrToStringAnsi(message, length);
            Console.WriteLine($"[OpenGL Debug] Source: {source}, Type: {type}, ID: {id}, Severity: {severity}, Message: {msg}");
        }, IntPtr.Zero);

        Console.WriteLine("OpenGL debug output enabled.");
    }

    public static List<int> GetAllIndexes(Vector3[] array, Vector3 searchValue)
    {
        List<int> indexes = new List<int>();

        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] == searchValue)
            {
                indexes.Add(i);
            }
        }

        return indexes;
    }
}

public static class AxisRenderer
{
    private static GL gl { get { return MainScript.Gl; } }
    private static uint vao, vbo;
    private static Shader axisShader;

    private static List<float> lineVertices = new();
    private static List<(Vector3 start, Vector3 end, Vector3 color, float thickness)> currentLines = new();

    public static unsafe void Load()
    {
        axisShader = ResourceManager.GetShader("line");

        vao = gl.GenVertexArray();
        vbo = gl.GenBuffer();

        gl.BindVertexArray(vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

        // Initialize buffer with empty data
        gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(0), null, BufferUsageARB.DynamicDraw);

        // Position Attribute (location = 0)
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)0);
        gl.EnableVertexAttribArray(0);

        // Color Attribute (location = 1)
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)(3 * sizeof(float)));
        gl.EnableVertexAttribArray(1);

        gl.BindVertexArray(0);
    }

    public static unsafe void UpdateLines(List<(Vector3 start, Vector3 end, Vector3 color, float thickness)> lines)
    {
        lineVertices.Clear();
        currentLines = new List<(Vector3, Vector3, Vector3, float)>(lines);

        foreach (var line in lines)
        {
            // Add start point (position + color)
            lineVertices.Add(line.start.X);
            lineVertices.Add(line.start.Y);
            lineVertices.Add(line.start.Z);
            lineVertices.Add(line.color.X);
            lineVertices.Add(line.color.Y);
            lineVertices.Add(line.color.Z);

            // Add end point (position + color)
            lineVertices.Add(line.end.X);
            lineVertices.Add(line.end.Y);
            lineVertices.Add(line.end.Z);
            lineVertices.Add(line.color.X);
            lineVertices.Add(line.color.Y);
            lineVertices.Add(line.color.Z);
        }

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        fixed (float* vertexPtr = lineVertices.ToArray())
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(lineVertices.Count * sizeof(float)), vertexPtr, BufferUsageARB.DynamicDraw);
        }
    }

    public static void Render(Matrix4x4 projection, Matrix4x4 view)
    {
        if (lineVertices.Count == 0) return;

        axisShader.Use();
        axisShader.SetUniform("projection", projection);
        axisShader.SetUniform("view", view);
        axisShader.SetUniform("model", Matrix4x4.Identity);

        gl.BindVertexArray(vao);

        //gl.DrawArrays(GLEnum.Lines, 0, (uint)(lineVertices.Count / 6));

        // Draw each line with its own thickness
        for (int i = 0; i < currentLines.Count; i++)
        {
            gl.LineWidth(currentLines[i].thickness);
            gl.DrawArrays(GLEnum.Lines, (int)(uint)(i * 2), 2);
        }

        // Reset line width to default
        gl.LineWidth(1.0f);

        gl.BindVertexArray(0);
    }

    public static void Dispose()
    {
        gl.DeleteBuffer(vbo);
        gl.DeleteVertexArray(vao);
        axisShader.Dispose();
    }
}

public static class ThickLineRenderer
{
    private static GL gl { get { return MainScript.Gl; } }
    private static uint vao, vbo, ebo;
    private static Shader lineShader;

    public static unsafe void Load()
    {
        lineShader = ResourceManager.GetShader("line");

        vao = gl.GenVertexArray();
        vbo = gl.GenBuffer();
        ebo = gl.GenBuffer();

        gl.BindVertexArray(vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);

        // Initialize buffers with empty data
        gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)0, null, BufferUsageARB.DynamicDraw);
        gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)0, null, BufferUsageARB.DynamicDraw);

        // Position Attribute (location = 0)
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)0);
        gl.EnableVertexAttribArray(0);

        // Color Attribute (location = 1)
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)(3 * sizeof(float)));
        gl.EnableVertexAttribArray(1);

        gl.BindVertexArray(0);
        
        Console.WriteLine("ThickLineRenderer loaded");
    }

    public static unsafe void Render(Matrix4x4 projection, Matrix4x4 view, List<(Vector3 start, Vector3 end, Vector3 color, float thickness)> lines)
    {
        if (lines == null || lines.Count == 0) return;
        
        // Process the lines into vertices and indices
        List<float> lineVertices = new();
        List<uint> lineIndices = new();
        uint vertexCount = 0;

        foreach (var line in lines)
        {
            AddCuboidLine(
                line.start, 
                line.end, 
                line.color, 
                line.thickness * 0.01f, // Scale down thickness to reasonable size
                ref vertexCount,
                lineVertices,
                lineIndices
            );
        }
        
        if (lineVertices.Count == 0) return;

        gl.BindVertexArray(vao);
        
        // Update vertex buffer
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        fixed (float* vertexPtr = lineVertices.ToArray())
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(lineVertices.Count * sizeof(float)), vertexPtr, BufferUsageARB.DynamicDraw);
        }

        // Update index buffer
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        fixed (uint* indexPtr = lineIndices.ToArray())
        {
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(lineIndices.Count * sizeof(uint)), indexPtr, BufferUsageARB.DynamicDraw);
        }

        // Enable depth test
        gl.Enable(EnableCap.DepthTest);
        
        lineShader.Use();
        lineShader.SetUniform("projection", projection);
        lineShader.SetUniform("view", view);
        lineShader.SetUniform("model", Matrix4x4.Identity);

        gl.DrawElements(PrimitiveType.Triangles, (uint)lineIndices.Count, DrawElementsType.UnsignedInt, (void*)0);
        gl.BindVertexArray(0);
    }

    private static void AddCuboidLine(
        Vector3 start, 
        Vector3 end, 
        Vector3 color, 
        float halfThickness, 
        ref uint vertexCount,
        List<float> lineVertices,
        List<uint> lineIndices)
    {
        // Calculate direction vector
        Vector3 direction = Vector3.Normalize(end - start);
        
        // Calculate perpendicular vectors
        Vector3 up = Vector3.UnitY;
        if (Math.Abs(Vector3.Dot(direction, up)) > 0.99f)
            up = Vector3.UnitZ;
            
        Vector3 right = Vector3.Normalize(Vector3.Cross(direction, up));
        up = Vector3.Normalize(Vector3.Cross(right, direction));
        
        uint baseIndex = vertexCount;
        
        // Generate 8 vertices of the cuboid
        AddVertex(start + right * halfThickness + up * halfThickness, color, lineVertices); // 0: front top right
        AddVertex(start - right * halfThickness + up * halfThickness, color, lineVertices); // 1: front top left
        AddVertex(start - right * halfThickness - up * halfThickness, color, lineVertices); // 2: front bottom left
        AddVertex(start + right * halfThickness - up * halfThickness, color, lineVertices); // 3: front bottom right
        
        AddVertex(end + right * halfThickness + up * halfThickness, color, lineVertices);   // 4: back top right
        AddVertex(end - right * halfThickness + up * halfThickness, color, lineVertices);   // 5: back top left
        AddVertex(end - right * halfThickness - up * halfThickness, color, lineVertices);   // 6: back bottom left
        AddVertex(end + right * halfThickness - up * halfThickness, color, lineVertices);   // 7: back bottom right

        // Add indices for the 6 faces (12 triangles)
        
        // Front face
        AddQuad(baseIndex + 0, baseIndex + 1, baseIndex + 2, baseIndex + 3, lineIndices);
        
        // Back face
        AddQuad(baseIndex + 5, baseIndex + 4, baseIndex + 7, baseIndex + 6, lineIndices);
        
        // Right face
        AddQuad(baseIndex + 4, baseIndex + 0, baseIndex + 3, baseIndex + 7, lineIndices);
        
        // Left face
        AddQuad(baseIndex + 1, baseIndex + 5, baseIndex + 6, baseIndex + 2, lineIndices);
        
        // Top face
        AddQuad(baseIndex + 4, baseIndex + 5, baseIndex + 1, baseIndex + 0, lineIndices);
        
        // Bottom face
        AddQuad(baseIndex + 3, baseIndex + 2, baseIndex + 6, baseIndex + 7, lineIndices);

        vertexCount += 8;
    }

    private static void AddVertex(Vector3 position, Vector3 color, List<float> lineVertices)
    {
        // Add position
        lineVertices.Add(position.X);
        lineVertices.Add(position.Y);
        lineVertices.Add(position.Z);
        
        // Add color
        lineVertices.Add(color.X);
        lineVertices.Add(color.Y);
        lineVertices.Add(color.Z);
    }

    private static void AddQuad(uint a, uint b, uint c, uint d, List<uint> lineIndices)
    {
        // First triangle
        lineIndices.Add(a);
        lineIndices.Add(b);
        lineIndices.Add(c);
        
        // Second triangle
        lineIndices.Add(a);
        lineIndices.Add(c);
        lineIndices.Add(d);
    }

    public static (int lineIndex, float distance) Raycast(Ray ray, List<(Vector3 start, Vector3 end, Vector3 color, float thickness)> lines)
    {
        int closestLineIndex = -1;
        float closestDistance = float.MaxValue;

        // For each line, check if the ray intersects it
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            
            // Calculate the distance from ray to line segment (treating line as a cylinder)
            float? intersection = RayIntersectThickLine(ray, line.start, line.end, line.thickness * 0.01f); // Match the scaling used in Render
            
            if (intersection.HasValue && intersection.Value < closestDistance)
            {
                closestDistance = intersection.Value;
                closestLineIndex = i;
            }
        }

        return (closestLineIndex, closestDistance);
    }

    private static float? RayIntersectThickLine(Ray ray, Vector3 lineStart, Vector3 lineEnd, float thickness)
    {
        // Method adapted from "Real-Time Collision Detection" by Christer Ericson
        Vector3 u = ray.Direction;
        Vector3 v = lineEnd - lineStart;
        Vector3 w = ray.Origin - lineStart;
        
        float a = Vector3.Dot(u, u);         // Always >= 0
        float b = Vector3.Dot(u, v);
        float c = Vector3.Dot(v, v);         // Always >= 0
        float d = Vector3.Dot(u, w);
        float e = Vector3.Dot(v, w);
        float D = a * c - b * b;             // Always >= 0
        float sc, tc;

        // Compute closest point parameters of the infinite lines
        if (D < 1e-6f) // The lines are almost parallel
        {
            sc = 0.0f;
            tc = (b > c ? d / b : e / c);    // Use the largest denominator
        }
        else
        {
            sc = (b * e - c * d) / D;
            tc = (a * e - b * d) / D;
        }

        // Clamp line segment parameters to [0,1]
        tc = Math.Clamp(tc, 0.0f, 1.0f);

        // Closest points on the lines
        Vector3 pointOnRay = ray.Origin + u * sc;
        Vector3 pointOnLine = lineStart + v * tc;

        // Check if distance between closest points is within thickness
        float distSq = Vector3.DistanceSquared(pointOnRay, pointOnLine);
        float radiusSq = thickness * thickness;
        
        // If distance is smaller than thickness, we have an intersection
        if (distSq <= radiusSq && sc >= 0f) // Only forward ray hits
        {
            // Calculate actual hit point (on the surface of the thick line)
            Vector3 dir = Vector3.Normalize(pointOnLine - pointOnRay);
            Vector3 hitPoint = pointOnLine - dir * thickness;
            
            // Return distance from ray origin to hit point
            return sc - thickness; // Approximate distance adjustment
        }

        return null; // No intersection
    }

    public static void Dispose()
    {
        gl.DeleteBuffer(vbo);
        gl.DeleteBuffer(ebo);
        gl.DeleteVertexArray(vao);
    }
}

public class GridRenderer
{
    private static GL gl { get { return MainScript.Gl; } }

    private static Shader gridShader;
    private static uint vao, vbo, ebo;
    private static readonly float[] vertices = new float[] {
        -1.0f, -1.0f,
        -1.0f, 1.0f,
        1.0f, 1.0f,
        1.0f, -1.0f
    };
    private static readonly uint[] cwIndices = new uint[] {
        0, 1, 2,
        0, 2, 3
    };
    private static readonly uint[] ccwIndices = new uint[] {
        0, 2, 1,
        0, 3, 2
    };

    public static unsafe void Load(bool is_cw)
    {
        gridShader = ResourceManager.GetShader("grid");

        // Create VAO
        vao = gl.GenVertexArray();
        gl.BindVertexArray(vao);

        // Create VBO
        vbo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        
        fixed (float* vertPtr = vertices)
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), 
                         vertPtr, BufferUsageARB.StaticDraw);
        }

        // Create EBO
        ebo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        
        uint[] selectedIndices = is_cw ? cwIndices : ccwIndices;
        fixed (uint* indPtr = selectedIndices)
        {
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(selectedIndices.Length * sizeof(uint)),
                         indPtr, BufferUsageARB.StaticDraw);
        }

        // Set vertex attribute pointers
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), (void*)0);
        gl.EnableVertexAttribArray(0);

        gl.BindVertexArray(0);
    }

    /*
    public static unsafe void Render(Matrix4x4 projection, Matrix4x4 view)
    {
        gl.Enable(EnableCap.DepthTest);
        gl.Enable(EnableCap.Blend);
        
        gridShader.Use();
        gridShader.SetUniform("view", view);
        gridShader.SetUniform("projection", projection);
        
        // Set near/far plane values
        float near = 0.1f;
        float far = 100.0f;
        int location = gl.GetUniformLocation(gridShader._handle, "u_nearfar");
        gl.Uniform1(location, 2, new float[] { near, far });

        gl.BindVertexArray(vao);
        gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);
        gl.BindVertexArray(0);
    }
    */

    public static unsafe void Render(Matrix4x4 projection, Matrix4x4 view)
    {
        gl.Enable(EnableCap.DepthTest);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        
        gridShader.Use();
        gridShader.SetUniform("view", view);
        gridShader.SetUniform("projection", projection);
        
        // Add camera position uniform for distance calculations
        Vector3 camPos = MainScript.cam.Pos;
        int camPosLocation = gl.GetUniformLocation(gridShader._handle, "cameraPosition");
        if (camPosLocation != -1)
        {
            gl.Uniform3(camPosLocation, camPos.X, camPos.Y, camPos.Z);
        }
        
        // Set grid parameters
        int gridParamsLocation = gl.GetUniformLocation(gridShader._handle, "gridSettings");
        if (gridParamsLocation != -1)
        {
            // x: major line every N units
            // y: line thickness
            // z: fade start distance
            // w: fade end distance
            gl.Uniform4(gridParamsLocation, 10.0f, 0.5f, 10.0f, 100.0f);
        }
        
        // Set near/far plane values
        float near = 0.1f;
        float far = 1000.0f;
        int location = gl.GetUniformLocation(gridShader._handle, "u_nearfar");
        if (location != -1)
        {
            gl.Uniform1(location, 2, new float[] { near, far });
        }

        gl.BindVertexArray(vao);
        gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);
        gl.BindVertexArray(0);
    }

    public static void Dispose()
    {
        gl.DeleteBuffer(vbo);
        gl.DeleteBuffer(ebo);
        gl.DeleteVertexArray(vao);
    }
}

public static class ConeRenderer
{
    private static GL gl { get { return MainScript.Gl; } }
    private static uint vao, vbo, ebo;
    private static Shader coneShader;
    
    private static List<float> vertices = new();
    private static List<uint> indices = new();
    private static int segments = 12;

    public static unsafe void Load(int coneSegments = 12)
    {
        segments = coneSegments;
        coneShader = ResourceManager.GetShader("cone"); // You'll need to create this shader

        vao = gl.GenVertexArray();
        vbo = gl.GenBuffer();
        ebo = gl.GenBuffer();

        gl.BindVertexArray(vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);

        // Position Attribute (location = 0)
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)0);
        gl.EnableVertexAttribArray(0);

        // Color Attribute (location = 1)
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)(3 * sizeof(float)));
        gl.EnableVertexAttribArray(1);

        gl.BindVertexArray(0);
        
        Console.WriteLine("ConeRenderer loaded");
    }

    public static unsafe void RenderCones(Matrix4x4 projection, Matrix4x4 view, 
        List<(Vector3 position, Vector3 direction, float height, float radius, Vector3 color)> cones)
    {
        if (cones.Count == 0) return;
        
        // Generate cone geometry data for all cones
        GenerateConeData(cones);
        
        gl.BindVertexArray(vao);
        
        // Update vertex buffer
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        fixed (float* vertexPtr = vertices.ToArray())
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Count * sizeof(float)), vertexPtr, BufferUsageARB.DynamicDraw);
        }

        // Update index buffer
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        fixed (uint* indexPtr = indices.ToArray())
        {
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Count * sizeof(uint)), indexPtr, BufferUsageARB.DynamicDraw);
        }
        
        // Enable depth test
        gl.Enable(EnableCap.DepthTest);
        
        coneShader.Use();
        coneShader.SetUniform("projection", projection);
        coneShader.SetUniform("view", view);
        coneShader.SetUniform("model", Matrix4x4.Identity);

        gl.DrawElements(PrimitiveType.Triangles, (uint)indices.Count, DrawElementsType.UnsignedInt, (void*)0);
        gl.BindVertexArray(0);
    }

    private static void GenerateConeData(
        List<(Vector3 position, Vector3 direction, float height, float radius, Vector3 color)> cones)
    {
        vertices.Clear();
        indices.Clear();
        
        uint baseIndex = 0;
        
        foreach (var (position, direction, height, radius, color) in cones)
        {
            // Generate a coordinate system for the cone
            Vector3 forward = Vector3.Normalize(direction);
            
            // Create arbitrary up and right vectors perpendicular to forward
            Vector3 right;
            if (Math.Abs(forward.Y) < 0.99f)
                right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward));
            else
                right = Vector3.Normalize(Vector3.Cross(Vector3.UnitZ, forward));
                
            Vector3 up = Vector3.Normalize(Vector3.Cross(forward, right));
            
            // Add the tip vertex
            Vector3 tipPosition = position + forward * height;
            AddVertex(tipPosition, color);
            
            // Add the base vertices
            for (int i = 0; i < segments; i++)
            {
                float angle = i * (2 * MathF.PI / segments);
                float x = MathF.Cos(angle) * radius;
                float y = MathF.Sin(angle) * radius;
                
                Vector3 baseOffset = right * x + up * y;
                Vector3 basePosition = position + baseOffset;
                
                AddVertex(basePosition, color);
            }
            
            // Add the center of the base
            AddVertex(position, color);
            
            // Add triangles for the cone sides
            for (int i = 0; i < segments; i++)
            {
                uint nextBaseIndex = (uint)(baseIndex + 1 + ((i + 1) % segments));
                
                // Triangle connecting the tip to adjacent base vertices
                indices.Add(baseIndex); // Tip
                indices.Add((uint)(baseIndex + 1 + i)); // Current base point
                indices.Add(nextBaseIndex); // Next base point
            }
            
            // Add triangles for the base (as a fan from center)
            uint centerIndex = baseIndex + 1 + (uint)segments;
            for (int i = 0; i < segments; i++)
            {
                uint basePointIndex = (uint)(baseIndex + 1 + i);
                uint nextBasePointIndex = (uint)(baseIndex + 1 + ((i + 1) % segments));
                
                indices.Add(centerIndex); // Center
                indices.Add(nextBasePointIndex); // Next point
                indices.Add(basePointIndex); // Current point (reversed winding)
            }
            
            // Update base index for the next cone
            baseIndex = (uint)vertices.Count / 6;
        }
    }

    private static void AddVertex(Vector3 position, Vector3 color)
    {
        // Add position
        vertices.Add(position.X);
        vertices.Add(position.Y);
        vertices.Add(position.Z);
        
        // Add color
        vertices.Add(color.X);
        vertices.Add(color.Y);
        vertices.Add(color.Z);
    }

    public static (int coneIndex, float distance) Raycast(Ray ray, 
    List<(Vector3 position, Vector3 direction, float height, float radius, Vector3 color)> cones)
    {
        int closestConeIndex = -1;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < cones.Count; i++)
        {
            var cone = cones[i];
            float? distance = ImprovedRayIntersectCone(ray, cone.position, cone.direction, cone.height, cone.radius);
            
            if (distance.HasValue && distance.Value < closestDistance)
            {
                closestDistance = distance.Value;
                closestConeIndex = i;
            }
        }

        return (closestConeIndex, closestDistance);
    }

    private static float? ImprovedRayIntersectCone(Ray ray, Vector3 coneBase, Vector3 direction, float height, float radius)
    {
        // Use a simplified approach that's more reliable
        // First, create bounding sphere for the cone
        Vector3 coneAxis = Vector3.Normalize(direction);
        Vector3 coneTip = coneBase + coneAxis * height;
        Vector3 coneCenter = coneBase + coneAxis * (height / 2);
        
        // Use a bounding sphere that encompasses the entire cone
        float boundingSphereRadius = MathF.Sqrt((height/2) * (height/2) + radius * radius);
        
        // Test ray against bounding sphere first (quick rejection)
        Vector3 oc = ray.Origin - coneCenter;
        float a = Vector3.Dot(ray.Direction, ray.Direction); // Should be 1 if normalized
        float b = 2.0f * Vector3.Dot(oc, ray.Direction);
        float c = Vector3.Dot(oc, oc) - boundingSphereRadius * boundingSphereRadius;
        float discriminant = b * b - 4 * a * c;
        
        if (discriminant < 0)
            return null; // No intersection with bounding sphere
            
        // Now test against cone segments - approximation but more reliable
        // 1. Check intersection with cone base (circle) - optional but helps
        Plane basePlane = new Plane(-coneAxis, Vector3.Dot(-coneAxis, coneBase));
        float? baseT = RayPlaneIntersection(ray, basePlane);
        
        if (baseT.HasValue && baseT.Value > 0)
        {
            Vector3 baseHitPoint = ray.Origin + ray.Direction * baseT.Value;
            float distFromBaseCenter = Vector3.Distance(baseHitPoint, coneBase);
            if (distFromBaseCenter <= radius)
                return baseT.Value;
        }
        
        // 2. Check intersection with cone tip sphere
        float tipSphereRadius = radius * 0.25f; // Small sphere at tip
        Vector3 ot = ray.Origin - coneTip;
        float at = Vector3.Dot(ray.Direction, ray.Direction);
        float bt = 2.0f * Vector3.Dot(ot, ray.Direction);
        float ct = Vector3.Dot(ot, ot) - tipSphereRadius * tipSphereRadius;
        float tipDiscriminant = bt * bt - 4 * at * ct;
        
        if (tipDiscriminant >= 0)
        {
            float tipT = (-bt - MathF.Sqrt(tipDiscriminant)) / (2 * at);
            if (tipT >= 0)
                return tipT;
        }
        
        // 3. Check slices along cone height
        // Use multiple slices along cone height for better accuracy
        const int sliceCount = 4;
        for (int i = 1; i < sliceCount; i++)
        {
            float sliceT = (float)i / sliceCount;
            float sliceRadius = radius * (1 - sliceT);
            Vector3 sliceCenter = coneBase + coneAxis * (height * sliceT);
            
            // Test ray against slice (circle)
            Plane slicePlane = new Plane(coneAxis, -Vector3.Dot(coneAxis, sliceCenter));
            float? sliceHitT = RayPlaneIntersection(ray, slicePlane);
            
            if (sliceHitT.HasValue && sliceHitT.Value > 0)
            {
                Vector3 sliceHitPoint = ray.Origin + ray.Direction * sliceHitT.Value;
                float distFromSliceCenter = Vector3.Distance(sliceHitPoint, sliceCenter);
                if (distFromSliceCenter <= sliceRadius)
                    return sliceHitT.Value;
            }
        }
        
        // 4. As last resort, use the approximate hit from bounding sphere
        float sphereT = (-b - MathF.Sqrt(discriminant)) / (2 * a);
        if (sphereT >= 0)
        {
            Vector3 hitPoint = ray.Origin + ray.Direction * sphereT;
            
            // Verify hit is within cone bounds along axis
            float projOnAxis = Vector3.Dot(hitPoint - coneBase, coneAxis);
            if (projOnAxis >= 0 && projOnAxis <= height)
            {
                // Calculate distance from axis
                Vector3 axisPoint = coneBase + coneAxis * projOnAxis;
                float distFromAxis = Vector3.Distance(hitPoint, axisPoint);
                
                // Calculate max allowed radius at this height
                float allowedRadius = radius * (1 - projOnAxis / height);
                
                if (distFromAxis <= allowedRadius * 1.1f) // 10% tolerance
                    return sphereT;
            }
        }
        
        return null; // No valid intersection
    }

    // Helper function for ray-plane intersection
    private static float? RayPlaneIntersection(Ray ray, Plane plane)
    {
        float dirDotNormal = Vector3.Dot(ray.Direction, plane.Normal);
        
        if (Math.Abs(dirDotNormal) < 0.0001f)
            return null; // Ray is parallel to plane
            
        float t = -(Vector3.Dot(ray.Origin, plane.Normal) + plane.D) / dirDotNormal;
        
        if (t < 0)
            return null; // Intersection is behind ray origin
            
        return t;
    }

    public static void Dispose()
    {
        gl.DeleteBuffer(vbo);
        gl.DeleteBuffer(ebo);
        gl.DeleteVertexArray(vao);
    }
}

public static class QuadRenderer
{
    private static GL gl { get { return MainScript.Gl; } }
    private static uint vao, vbo, ebo;
    private static Shader quadShader;

    public static unsafe void Load()
    {
        quadShader = ResourceManager.GetShader("quad");

        vao = gl.GenVertexArray();
        vbo = gl.GenBuffer();
        ebo = gl.GenBuffer();

        gl.BindVertexArray(vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);

        // Position attribute
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)0);
        gl.EnableVertexAttribArray(0);

        // Color attribute (now with RGBA - 4 components)
        gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)(3 * sizeof(float)));
        gl.EnableVertexAttribArray(1);

        gl.BindVertexArray(0);
    }

    public static unsafe void RenderQuads(Matrix4x4 projection, Matrix4x4 view, 
                                 List<(Vector3[] vertices, Vector4 color, ActiveAxis axis, bool useDepthTest)> quads)
    {
        // Notice: Changed Vector3 color to Vector4 color to include alpha
        
        if (quads.Count == 0) return;

        // Group quads by depth test setting
        var depthTestedQuads = quads.Where(q => q.useDepthTest).ToList();
        var noDepthTestQuads = quads.Where(q => !q.useDepthTest).ToList();
        
        // Render depth-tested quads first
        if (depthTestedQuads.Count > 0)
        {
            gl.Enable(EnableCap.DepthTest);
            gl.DepthFunc(DepthFunction.Less);
            RenderQuadBatch(projection, view, depthTestedQuads);
        }
        
        // Then render quads without depth testing
        if (noDepthTestQuads.Count > 0)
        {
            gl.Disable(EnableCap.DepthTest);
            RenderQuadBatch(projection, view, noDepthTestQuads);
        }
        
        // Reset state
        gl.Enable(EnableCap.DepthTest);
    }

    private static unsafe void RenderQuadBatch(Matrix4x4 projection, Matrix4x4 view,
        List<(Vector3[] vertices, Vector4 color, ActiveAxis axis, bool useDepthTest)> quads)
    {
        // Prepare data
        List<float> vertexData = new List<float>();
        List<uint> indices = new List<uint>();
        uint indexOffset = 0;

        foreach (var quad in quads)
        {
            if (quad.vertices.Length != 4)
                continue;
                
            foreach (var vertex in quad.vertices)
            {
                // Position
                vertexData.Add(vertex.X);
                vertexData.Add(vertex.Y);
                vertexData.Add(vertex.Z);
                
                // Color with alpha component
                vertexData.Add(quad.color.X);
                vertexData.Add(quad.color.Y);
                vertexData.Add(quad.color.Z);
                vertexData.Add(quad.color.W); // Add alpha component
            }
            
            // Two triangles per quad
            indices.Add(indexOffset);
            indices.Add(indexOffset + 1);
            indices.Add(indexOffset + 2);
            
            indices.Add(indexOffset);
            indices.Add(indexOffset + 2);
            indices.Add(indexOffset + 3);
            
            indexOffset += 4;
        }

        // Skip if no valid quads
        if (vertexData.Count == 0) return;

        gl.BindVertexArray(vao);
        
        // Update buffers
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        fixed (float* vertexPtr = vertexData.ToArray())
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertexData.Count * sizeof(float)), vertexPtr, BufferUsageARB.DynamicDraw);
        }

        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        fixed (uint* indexPtr = indices.ToArray())
        {
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Count * sizeof(uint)), indexPtr, BufferUsageARB.DynamicDraw);
        }
        
        // Render
        quadShader.Use();
        quadShader.SetUniform("projection", projection);
        quadShader.SetUniform("view", view);
        quadShader.SetUniform("model", Matrix4x4.Identity);
        
        // Enable alpha blending
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        
        gl.DrawElements(PrimitiveType.Triangles, (uint)indices.Count, DrawElementsType.UnsignedInt, (void*)0);
        
        gl.BindVertexArray(0);
    }

    public static void Dispose()
    {
        gl.DeleteBuffer(vbo);
        gl.DeleteBuffer(ebo);
        gl.DeleteVertexArray(vao);
    }
}

public static class BillboardRenderer
{
    private static GL gl { get { return MainScript.Gl; } }
    private static uint vao, vbo, ebo;
    private static Shader billboardShader;
    
    private static readonly uint[] indices = new uint[] { 0, 1, 2, 0, 2, 3 };
    
    public class Billboard
    {
        public Vector3 Position;  // World position
        public Vector2 Size;      // Width, Height in pixels
        public Vector4 Color;     // RGBA
        public float Rotation;    // Rotation in degrees
        public bool DepthTest;    // Whether to use depth testing
        public float ZOffset;     // Z offset for manual layer control (higher = in front)
        
        public Billboard(Vector3 position, Vector2 size, Vector4 color, float rotation = 0f, bool depthTest = true, float zOffset = 0f)
        {
            Position = position;
            Size = size;
            Color = color;
            Rotation = rotation;
            DepthTest = depthTest;
            ZOffset = zOffset;
        }

        public Billboard(Vector3 position, Billboard preset)
        {
            Position = position;
            Size = preset.Size;
            Color = preset.Color;
            Rotation = preset.Rotation;
            DepthTest = preset.DepthTest;
            ZOffset = preset.ZOffset;
        }
    }
    
    public static unsafe void Load()
    {
        // Create or load the shader
        billboardShader = ResourceManager.GetShader("billboard");
        if (billboardShader == null)
        {
            Console.WriteLine("Warning: Billboard shader not found. Create a billboard.vert and billboard.frag shader.");
            return;
        }
        
        // Generate buffers
        vao = gl.GenVertexArray();
        vbo = gl.GenBuffer();
        ebo = gl.GenBuffer();
        
        gl.BindVertexArray(vao);
        
        // Set up vertex buffer (will be populated during render)
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(16 * sizeof(float)), null, BufferUsageARB.DynamicDraw);
        
        // Set up index buffer
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        fixed (uint* indPtr = indices)
        {
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), 
                         indPtr, BufferUsageARB.StaticDraw);
        }
        
        // Position attribute (x, y)
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
        gl.EnableVertexAttribArray(0);
        
        // Texture coordinate attribute (u, v)
        gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
        gl.EnableVertexAttribArray(1);
        
        gl.BindVertexArray(0);
        
        Console.WriteLine("BillboardRenderer loaded");
    }
    
    public static unsafe void Render(Matrix4x4 projection, Matrix4x4 view, List<Billboard> billboards)
    {
        if (billboards == null || billboards.Count == 0 || billboardShader == null) 
            return;
        
        billboardShader.Use();
        billboardShader.SetUniform("projection", projection);
        billboardShader.SetUniform("view", view);
        
        // Get viewport dimensions for proper scaling
        int* viewport = stackalloc int[4];
        gl.GetInteger(GetPName.Viewport, viewport);
        int viewportWidth = viewport[2];
        int viewportHeight = viewport[3];
        
        billboardShader.SetUniform("viewportSize", new Vector2(viewportWidth, viewportHeight));
        
        // Set up the quad vertices
        float[] vertices = new float[]
        {
            -0.5f, -0.5f, 0.0f, 0.0f,  // bottom-left: pos(x,y) + texCoord(u,v)
            0.5f, -0.5f, 1.0f, 0.0f,   // bottom-right
            0.5f, 0.5f, 1.0f, 1.0f,    // top-right
            -0.5f, 0.5f, 0.0f, 1.0f    // top-left
        };
        
        gl.BindVertexArray(vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        
        fixed (float* vertPtr = vertices)
        {
            gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(vertices.Length * sizeof(float)), vertPtr);
        }
        
        // Enable alpha blending
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        
        // Sort billboards based on layering needs
        // For depth test enabled billboards: sort front-to-back for efficiency
        // For depth test disabled billboards: sort by zOffset for manual layering
        var sortedBillboards = billboards
            .Select((billboard, index) => (billboard, index))
            .OrderBy(b => b.billboard.DepthTest ? 0 : 1) // Depth tested billboards first
            .ThenBy(b => b.billboard.DepthTest ? Vector3.Distance(MainScript.cam.Pos, b.billboard.Position) : -b.billboard.ZOffset) // Distance for depth tested, zOffset for others
            .ThenBy(b => b.index) // Preserve original order for equal distances/z-offsets
            .Select(b => b.billboard)
            .ToList();
        
        // Keep track of whether we're in depth test mode
        bool depthTestEnabled = true;
        gl.Enable(EnableCap.DepthTest);
        
        // Render each billboard
        foreach (var billboard in sortedBillboards)
        {
            // Toggle depth testing based on billboard property
            if (billboard.DepthTest != depthTestEnabled)
            {
                if (billboard.DepthTest)
                {
                    gl.Enable(EnableCap.DepthTest);
                }
                else
                {
                    gl.Disable(EnableCap.DepthTest);
                }
                depthTestEnabled = billboard.DepthTest;
            }
            
            Matrix4x4 model = Matrix4x4.CreateTranslation(billboard.Position);
            billboardShader.SetUniform("model", model);
            billboardShader.SetUniform("billboardSize", billboard.Size);
            billboardShader.SetUniform("billboardColor", billboard.Color);
            billboardShader.SetUniform("rotation", billboard.Rotation);
            billboardShader.SetUniform("zOffset", billboard.ZOffset); // Pass Z offset to shader
            
            gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);
        }
        
        // Reset state
        gl.Enable(EnableCap.DepthTest);
        gl.BindVertexArray(0);
    }
    
    public static void Dispose()
    {
        gl.DeleteBuffer(vbo);
        gl.DeleteBuffer(ebo);
        gl.DeleteVertexArray(vao);
    }
}