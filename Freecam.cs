using System.Numerics;
using Silk.NET.Input;

public class Freecam
{
    public static bool Enabled;
    public static bool Locked;

    private static Vector2 LastMousePosition;
    private static Vector2 beforeEnabledPos;

    public static float Speed = 8f;
    public static float SprintSpeedModifer = 2;

    

    public static void Load()
    {
        beforeEnabledPos = new Vector2(MainScript.window.Size[0] / 2, MainScript.window.Size[1] / 2);

        MainScript.window.Update += Update;
        MainScript.mouse.MouseMove += MouseMove;
        MainScript.mouse.MouseDown += delegate(IMouse mouse, MouseButton button)
        {
            if(button == MouseButton.Right && !UIManager.isMouseOverUI)
            {
                Enabled = true;
                beforeEnabledPos = MainScript.mouse.Position;
                MainScript.mouse.Position = new Vector2(MainScript.window.Size[0] / 2, MainScript.window.Size[1] / 2);
                LastMousePosition = MainScript.mouse.Position; // Update LastMousePosition to match new position
                MainScript.mouse.Cursor.CursorMode = CursorMode.Raw;
            }
        };

        MainScript.mouse.MouseUp += delegate(IMouse mouse, MouseButton button)
        {
            if(button == MouseButton.Right && (!UIManager.isMouseOverUI && Enabled))
            {
                Enabled = false;
                MainScript.mouse.Cursor.CursorMode = CursorMode.Normal;
                MainScript.mouse.Position = beforeEnabledPos;
                LastMousePosition = default;
            }
        };

        MainScript.mouse.Scroll += MouseScroll;
    }

    public static void Update(double deltaTime)
    {
        if(!Enabled) { return; }
        if(Locked) { return; }

        var moveSpeed = Speed * (MainScript.keyboard.IsKeyPressed(Key.ShiftLeft) ? SprintSpeedModifer : 1) * (float) deltaTime;

        if (MainScript.keyboard.IsKeyPressed(Key.W))
        {
            //Move forwards
            MainScript.cam.Pos += moveSpeed * MainScript.cam.Forward;
        }
        if (MainScript.keyboard.IsKeyPressed(Key.S))
        {
            //Move backwards
            MainScript.cam.Pos -= moveSpeed * MainScript.cam.Forward;
        }
        if (MainScript.keyboard.IsKeyPressed(Key.A))
        {
            //Move left
            MainScript.cam.Pos -= Vector3.Normalize(Vector3.Cross(MainScript.cam.Forward, MainScript.cam.Up)) * moveSpeed;
        }
        if (MainScript.keyboard.IsKeyPressed(Key.D))
        {
            //Move right
            MainScript.cam.Pos += Vector3.Normalize(Vector3.Cross(MainScript.cam.Forward, MainScript.cam.Up)) * moveSpeed;
        }
    }

    public static void MouseMove(IMouse mouse, Vector2 position)
    {
        if(!Enabled) { return; }
        if(Locked) { return; }

        var lookSensitivity = 0.1f;
        if (LastMousePosition == default) { LastMousePosition = position; }
        else
        {
            var xOffset = (position.X - LastMousePosition.X) * lookSensitivity;
            var yOffset = (position.Y - LastMousePosition.Y) * lookSensitivity;
            LastMousePosition = position;

            MainScript.cam.Angle = new Vector2(MainScript.cam.Angle.X + xOffset, Math.Clamp(MainScript.cam.Angle.Y - yOffset, -89.0f, 89.0f));
            MainScript.cam.UpdateDirection();
        }
    }

    public static void MouseScroll(IMouse mouse, ScrollWheel scrollWheel)
    {
        if(!Enabled) { return; }
        if(Locked) { return; }

        Speed += scrollWheel.Y * 0.5f;
        UIManager.CenterPopupTracked.Show("Speed " + Speed, 1.5f);
    }
}