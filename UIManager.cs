using System.Diagnostics;
using System.Numerics;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using static Gizmo;

public class UIManager
{
    private static GL Gl { get { return MainScript.Gl; } }

    public static CenterPopup CenterPopupTracked;
    

    public static bool isMouseOverUI
    {
        get
        {
            return ImGui.IsAnyItemHovered() ||
                ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow) ||
                ImGui.GetIO().WantCaptureMouse;
        }
    }

    public static void Load()
    {
        CenterPopupTracked = new CenterPopup();

        AssetBrowser.Load();
    }


    public static void EndAssetDrag()
    {
        if (AssetBrowser.isDraggingItem)
        {
            Console.WriteLine($"Ended dragging texture: {AssetBrowser.draggingItem?.name ?? "null"}");
            AssetBrowser.isDraggingItem = false;
            AssetBrowser.draggingItem = null;
        }
    }


    public static void OpenFile()
    {
        // ...
    }
}

public class CenterPopup
{
    private Stopwatch _stopwatch = new Stopwatch();
    private float _duration = 3f;
    private string _text = "";
    private bool _visible = false;

    public void Show(string text, float durationSeconds = 3f)
    {
        _text = text;
        _duration = durationSeconds;
        _visible = true;
        _stopwatch.Restart();
    }

    public void Render(Vector2 windowSize)
    {
        if (!_visible) return;

        float elapsed = (float)_stopwatch.Elapsed.TotalSeconds;
        if (elapsed >= _duration)
        {
            _visible = false;
            _stopwatch.Stop();
            return;
        }

        float alpha = 1f - (elapsed / _duration);

        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, alpha);

        Vector2 textSize = ImGui.CalcTextSize(_text);
        Vector2 popupPos = new Vector2(
            (windowSize.X - textSize.X) / 2f,
            (windowSize.Y - textSize.Y) / 2f
        );

        ImGui.SetNextWindowPos(popupPos, ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(alpha);

        ImGui.Begin("CenterPopup", ImGuiWindowFlags.NoDecoration |
                                    ImGuiWindowFlags.NoInputs |
                                    ImGuiWindowFlags.AlwaysAutoResize |
                                    ImGuiWindowFlags.NoNav |
                                    ImGuiWindowFlags.NoFocusOnAppearing |
                                    ImGuiWindowFlags.NoBringToFrontOnFocus);

        ImGui.Text(_text);

        ImGui.End();
        ImGui.PopStyleVar();
    }
}

/*
public class HierarchyElement
{
    public string Name;

    public List<(string name, IntPtr texPtr)> texInDir;


    public bool IsExpanded;
    public List<HierarchyElement> Children;
    public HierarchyElement Parent;

    public HierarchyElement(string name)
    {
        Name = name;
        IsExpanded = false;
        Children = new List<HierarchyElement>();
    }
}
*/
