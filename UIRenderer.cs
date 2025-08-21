

using System.Drawing;
using System.Numerics;
using ImGuiNET;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

public class UIRenderer
{
    private static ImGuiController imGUI { get { return MainScript.imGUI; } }
    private static GL Gl { get { return MainScript.Gl; } }
    private static IWindow window { get { return MainScript.window; } }


    private static readonly ImGuiWindowFlags windowFlags =
        ImGuiNET.ImGuiWindowFlags.NoCollapse |
        ImGuiNET.ImGuiWindowFlags.NoMove |
        ImGuiNET.ImGuiWindowFlags.NoBringToFrontOnFocus |
        ImGuiNET.ImGuiWindowFlags.NoTitleBar |
        ImGuiNET.ImGuiWindowFlags.NoDocking |
        ImGuiNET.ImGuiWindowFlags.NoScrollbar |
        ImGuiNET.ImGuiWindowFlags.NoScrollWithMouse |
        ImGuiNET.ImGuiWindowFlags.NoResize;


    // UI Design vars

    private static float panelWidthRight = 300f; // Width of the side panels
    private static float panelWidthLeft = 300f;
    private static float topBarHeight = 55f; // Height of the top bar
    private static float buttonSize = 20.0f;
    private static float bottomPanelHeight = 300f; // Size of bottom panel (model browser)
    private static float buttonSizeCenter = 24.0f; // Size of buttons in center toolbar
    private static int numButtons = 4; // Number of buttons in center toolbar
    private static float buttonSpacing = 10.0f; // The Center toolbar button spacing
    private static Vector4 editModeSelectColor = new Vector4(0.4f, 0.6f, 1.0f, 0.7f);
    private static Vector4 editModeHoverColor = new Vector4(0.4f, 0.6f, 1.0f, 0.4f);
    private static Vector4 editModePressColor = new Vector4(0.4f, 0.6f, 1.0f, 0.55f);

    // Predefined calculated vars

    private static ImGuiViewportPtr viewport;
    private static Vector2 displayPos;
    private static Vector2 displaySize;

    private static float centerX;
    private static float centerWidth;
    private static float centerHeight;
    private static float totalButtonsWidth;
    private static float startPosX; // center toolbar
    private static string debugStringConstruct = "";
    private static Vector2 debugStringPos = new Vector2(260, 100);


    // Testing


    public static List<Vector2> debugPoints = new List<Vector2>();

    public static void Load()
    {
        viewport = ImGui.GetMainViewport();
        displaySize = viewport.Size;
        displayPos = viewport.Pos;

        centerX = displayPos.X + panelWidthLeft;
        centerWidth = displaySize.X - (panelWidthLeft + panelWidthRight);
        centerHeight = displaySize.Y - topBarHeight - bottomPanelHeight;

        totalButtonsWidth = (buttonSizeCenter * numButtons) + (buttonSpacing * (numButtons - 1));
        startPosX = (centerWidth - totalButtonsWidth) / 2.0f;
    }

    public static void Render(double delta)
    {
        imGUI.Update((float)delta);

        debugStringConstruct = string.Join("\n", MainScript.debugText.Values);

        ImDrawListPtr imDrawList = ImGui.GetForegroundDrawList();
        imDrawList.AddText(debugStringPos, 0xFFFFFFFF, debugStringConstruct);

        /*
        foreach(Vector2 point in debugPoints)
        {
            imDrawList.AddCircleFilled(point, 3, 0xFF0000FF);
        }
        */

        ImGuiNET.ImGui.PushStyleColor(ImGuiNET.ImGuiCol.Button, new System.Numerics.Vector4(0, 0, 0, 0)); // Transparent
        ImGuiNET.ImGui.PushStyleColor(ImGuiNET.ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.3f, 0.3f, 0.3f, 0.5f)); // Semi-transparent when hovered
        ImGuiNET.ImGui.PushStyleColor(ImGuiNET.ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 0.5f)); // Semi-transparent when clicked

        ImGuiNET.ImGui.SetNextWindowPos(new System.Numerics.Vector2(displayPos.X, displayPos.Y));
        ImGuiNET.ImGui.SetNextWindowSize(new System.Numerics.Vector2(displaySize.X, topBarHeight));
        if (ImGuiNET.ImGui.Begin("Top Bar", windowFlags | ImGuiNET.ImGuiWindowFlags.MenuBar))
        {
            if (ImGuiNET.ImGui.BeginMenuBar())
            {
                if (ImGuiNET.ImGui.BeginMenu("File"))
                {
                    if (ImGuiNET.ImGui.MenuItem("New", "Ctrl+N")) { }
                    if (ImGuiNET.ImGui.MenuItem("Open...", "Ctrl+O")) { }
                    if (ImGuiNET.ImGui.MenuItem("Save", "Ctrl+S")) { }
                    if (ImGuiNET.ImGui.MenuItem("Save As...", "Ctrl+Shift+S")) { }
                    ImGuiNET.ImGui.Separator();
                    if (ImGuiNET.ImGui.MenuItem("Exit", "Alt+F4")) { window.Close(); }
                    ImGuiNET.ImGui.EndMenu();
                }

                if (ImGuiNET.ImGui.BeginMenu("Edit"))
                {
                    if (ImGuiNET.ImGui.MenuItem("Undo", "Ctrl+Z")) { }
                    if (ImGuiNET.ImGui.MenuItem("Redo", "Ctrl+Y")) { }
                    ImGuiNET.ImGui.Separator();
                    if (ImGuiNET.ImGui.MenuItem("Cut", "Ctrl+X")) { }
                    if (ImGuiNET.ImGui.MenuItem("Copy", "Ctrl+C")) { }
                    if (ImGuiNET.ImGui.MenuItem("Paste", "Ctrl+V")) { }
                    if (ImGuiNET.ImGui.MenuItem("Delete", "Del")) { }
                    ImGuiNET.ImGui.EndMenu();
                }

                if (ImGuiNET.ImGui.BeginMenu("View"))
                {
                    if (ImGuiNET.ImGui.MenuItem("Wireframe", "", false)) { }
                    if (ImGuiNET.ImGui.MenuItem("Solid", "", true)) { }
                    ImGuiNET.ImGui.EndMenu();
                }

                ImGuiNET.ImGui.EndMenuBar();
            }

            ImGuiNET.ImGui.SetCursorPosY(ImGuiNET.ImGui.GetCursorPosY()); // Padding

            if (ImGuiNET.ImGui.ImageButton("NewBtn", ResourceManager.GetTexture("editor/icons/file-earmark").handle, new System.Numerics.Vector2(buttonSize, buttonSize))) { }
            ImGuiNET.ImGui.SameLine();
            if (ImGuiNET.ImGui.ImageButton("OpenBtn", ResourceManager.GetTexture("editor/icons/openfolder").handle, new System.Numerics.Vector2(buttonSize, buttonSize))) { }
            ImGuiNET.ImGui.SameLine();
            if (ImGuiNET.ImGui.ImageButton("SaveBtn", ResourceManager.GetTexture("editor/icons/floppy").handle, new System.Numerics.Vector2(buttonSize, buttonSize))) { }
            ImGuiNET.ImGui.SameLine(0, 20);
            if (ImGuiNET.ImGui.ImageButton("CutBtn", ResourceManager.GetTexture("editor/icons/scissors").handle, new System.Numerics.Vector2(buttonSize, buttonSize))) { }
            ImGuiNET.ImGui.SameLine();
            if (ImGuiNET.ImGui.ImageButton("CopyBtn", ResourceManager.GetTexture("editor/icons/files").handle, new System.Numerics.Vector2(buttonSize, buttonSize))) { }
            ImGuiNET.ImGui.SameLine();
            if (ImGuiNET.ImGui.ImageButton("PasteBtn", ResourceManager.GetTexture("editor/icons/clipboard").handle, new System.Numerics.Vector2(buttonSize, buttonSize))) { }
            ImGuiNET.ImGui.SameLine(0, 20);
            if (ImGuiNET.ImGui.ImageButton("UndoBtn", ResourceManager.GetTexture("editor/icons/arrow-counterclockwise").handle, new System.Numerics.Vector2(buttonSize, buttonSize))) { }
            ImGuiNET.ImGui.SameLine();
            if (ImGuiNET.ImGui.ImageButton("RedoBtn", ResourceManager.GetTexture("editor/icons/arrow-clockwise").handle, new System.Numerics.Vector2(buttonSize, buttonSize))) { }
            ImGuiNET.ImGui.SameLine(0, 20);
            if (ImGuiNET.ImGui.ImageButton("wireframeBtn", ResourceManager.GetTexture("editor/icons/box-wire").handle, new System.Numerics.Vector2(buttonSize, buttonSize))) { }
            ImGuiNET.ImGui.SameLine();
            if (ImGuiNET.ImGui.ImageButton("solidframeBtn", ResourceManager.GetTexture("editor/icons/box-solid").handle, new System.Numerics.Vector2(buttonSize, buttonSize))) { }
            ImGuiNET.ImGui.SameLine(0, 20);

            if (Gizmo.CurrentMode == Gizmo.GizmoMode.Translate) { ImGui.PushStyleColor(ImGuiCol.Button, editModeSelectColor); ImGui.PushStyleColor(ImGuiCol.ButtonHovered, editModeHoverColor); ImGui.PushStyleColor(ImGuiCol.ButtonActive, editModePressColor); }
            if (ImGuiNET.ImGui.ImageButton("TranslateBtn", ResourceManager.GetTexture("editor/icons/arrows-move").handle, new System.Numerics.Vector2(buttonSize, buttonSize))) { Gizmo.SetMode(Gizmo.GizmoMode.Translate); }
            if (Gizmo.CurrentMode == Gizmo.GizmoMode.Translate) { ImGui.PopStyleColor(3); }

            ImGuiNET.ImGui.SameLine();

            if (Gizmo.CurrentMode == Gizmo.GizmoMode.Rotate) { ImGui.PushStyleColor(ImGuiCol.Button, editModeSelectColor); ImGui.PushStyleColor(ImGuiCol.ButtonHovered, editModeHoverColor); ImGui.PushStyleColor(ImGuiCol.ButtonActive, editModePressColor); }
            if (ImGuiNET.ImGui.ImageButton("RotateBtn", ResourceManager.GetTexture("editor/icons/arrow-repeat").handle, new System.Numerics.Vector2(buttonSize, buttonSize))) { Gizmo.SetMode(Gizmo.GizmoMode.Rotate); }
            if (Gizmo.CurrentMode == Gizmo.GizmoMode.Rotate) { ImGui.PopStyleColor(3); }

            ImGuiNET.ImGui.End();
        }

        // Left panel - adjust height to not overlap with bottom panel
        ImGuiNET.ImGui.SetNextWindowPos(new System.Numerics.Vector2(displayPos.X, displayPos.Y + topBarHeight));
        ImGuiNET.ImGui.SetNextWindowSize(new System.Numerics.Vector2(panelWidthLeft, displaySize.Y - topBarHeight - bottomPanelHeight));
        if (ImGuiNET.ImGui.Begin("Left Panel", windowFlags))
        {
            ImGuiNET.ImGui.Text("Left Panel Content");

            ImGuiNET.ImGui.End();
        }

        // Right panel - full height
        ImGuiNET.ImGui.SetNextWindowPos(new System.Numerics.Vector2(displayPos.X + displaySize.X - panelWidthRight, displayPos.Y + topBarHeight));
        ImGuiNET.ImGui.SetNextWindowSize(new System.Numerics.Vector2(panelWidthRight, displaySize.Y - topBarHeight));
        if (ImGuiNET.ImGui.Begin("Right Panel", windowFlags))
        {
            RenderInspector();

            ImGuiNET.ImGui.End();
        }

        // Center toolbar - update X position and width
        ImGuiNET.ImGui.SetNextWindowPos(new System.Numerics.Vector2(centerX, displayPos.Y + topBarHeight));
        ImGuiNET.ImGui.SetNextWindowSize(new System.Numerics.Vector2(centerWidth, 35));
        if (ImGuiNET.ImGui.Begin("Center Toolbar", windowFlags))
        {
            ImGuiNET.ImGui.SetCursorPosX(startPosX);

            //ImGui.PushStyleColor(ImGuiCol.Butt)

            if (EditorMain.Mode == EditMode.Place) { ImGui.PushStyleColor(ImGuiCol.Button, editModeSelectColor); ImGui.PushStyleColor(ImGuiCol.ButtonHovered, editModeHoverColor); ImGui.PushStyleColor(ImGuiCol.ButtonActive, editModePressColor); }
            if (ImGuiNET.ImGui.ImageButton("PlaceEdit", ResourceManager.GetTexture("editor/icons/box-solid").handle, new System.Numerics.Vector2(buttonSizeCenter, buttonSizeCenter))) { EditorMain.ChangeEditMode(EditMode.Place); }
            if (EditorMain.Mode == EditMode.Place) { ImGui.PopStyleColor(3); }

            ImGuiNET.ImGui.SameLine(0, buttonSpacing);

            if (EditorMain.Mode == EditMode.Generate) { ImGui.PushStyleColor(ImGuiCol.Button, editModeSelectColor); ImGui.PushStyleColor(ImGuiCol.ButtonHovered, editModeHoverColor); ImGui.PushStyleColor(ImGuiCol.ButtonActive, editModePressColor); }
            if (ImGuiNET.ImGui.ImageButton("GenerateEdit", ResourceManager.GetTexture("editor/icons/face-edit").handle, new System.Numerics.Vector2(buttonSizeCenter, buttonSizeCenter))) { EditorMain.ChangeEditMode(EditMode.Generate); }
            if (EditorMain.Mode == EditMode.Generate) { ImGui.PopStyleColor(3); }

            ImGuiNET.ImGui.SameLine(0, buttonSpacing);

            if (EditorMain.Mode == EditMode.Edit) { ImGui.PushStyleColor(ImGuiCol.Button, editModeSelectColor); ImGui.PushStyleColor(ImGuiCol.ButtonHovered, editModeHoverColor); ImGui.PushStyleColor(ImGuiCol.ButtonActive, editModePressColor); }
            if (ImGuiNET.ImGui.ImageButton("VerticEdit", ResourceManager.GetTexture("editor/icons/vertic-edit").handle, new System.Numerics.Vector2(buttonSizeCenter, buttonSizeCenter))) { EditorMain.ChangeEditMode(EditMode.Edit); }
            if (EditorMain.Mode == EditMode.Edit) { ImGui.PopStyleColor(3); }

            ImGuiNET.ImGui.SameLine(0, buttonSpacing);

            if (EditorMain.Mode == EditMode.Surface) { ImGui.PushStyleColor(ImGuiCol.Button, editModeSelectColor); ImGui.PushStyleColor(ImGuiCol.ButtonHovered, editModeHoverColor); ImGui.PushStyleColor(ImGuiCol.ButtonActive, editModePressColor); }
            if (ImGuiNET.ImGui.ImageButton("SurfaceEdit", ResourceManager.GetTexture("editor/icons/entity").handle, new System.Numerics.Vector2(buttonSizeCenter, buttonSizeCenter))) { EditorMain.ChangeEditMode(EditMode.Surface); }
            if (EditorMain.Mode == EditMode.Surface) { ImGui.PopStyleColor(3); }


            ImGuiNET.ImGui.End();
        }

        ImGui.PopStyleColor(3);

        // Bottom panel - positioned below everything else
        float bottomPanelWidth = centerWidth + panelWidthLeft;

        ImGuiNET.ImGui.SetNextWindowPos(new System.Numerics.Vector2(displayPos.X, displayPos.Y + displaySize.Y - bottomPanelHeight));
        ImGuiNET.ImGui.SetNextWindowSize(new System.Numerics.Vector2(bottomPanelWidth, bottomPanelHeight));
        if (ImGuiNET.ImGui.Begin("Bottom Panel", windowFlags))
        {
            //ImGuiNET.ImGui.Text("Bottom Panel Content");
            RenderAssetBrowser();

            ImGuiNET.ImGui.End();
        }


        if (AssetBrowser.isDraggingItem && AssetBrowser.draggingItem != null)
        {
            // Draw a small preview of the dragged texture at the mouse position
            Vector2 mousePos = ImGui.GetMousePos();
            float previewSize = 64;
            
            ImDrawListPtr drawList = ImGui.GetForegroundDrawList();
            drawList.AddImage(
                AssetBrowser.draggingItem.tex,
                new Vector2(mousePos.X - previewSize/2, mousePos.Y - previewSize/2),
                new Vector2(mousePos.X + previewSize/2, mousePos.Y + previewSize/2)
            );
            
            // End drag when mouse is released
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                UIManager.EndAssetDrag();
            }
        }


        UIManager.CenterPopupTracked?.Render(displaySize);

        imGUI.Render();
    }
    
    static float separatorPosition = 200f;
    static float separatorWidth = 8f;
    static int buttonsPerRow = 6;
    static float widthButtons = 80f;

    private static void RenderAssetBrowser()
    {


        // Calculate search bar height for consistent sizing
        float searchBarHeight = ImGui.GetTextLineHeight() + ImGui.GetStyle().FramePadding.Y * 2;

        // Set the settings button to match search bar height
        ImGui.SetCursorPosX(12);
        ImGui.Text("Asset Browser");

        // Add tabs for different asset types next to the title
        ImGui.SameLine(ImGui.GetCursorPosX() + 10);

        // Style for the tabs
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 4));
        ImGui.PushStyleColor(ImGuiCol.Tab, new Vector4(0.2f, 0.2f, 0.2f, 0.8f));
        ImGui.PushStyleColor(ImGuiCol.TabActive, new Vector4(0.3f, 0.3f, 0.3f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.TabHovered, new Vector4(0.4f, 0.4f, 0.4f, 0.8f));

        // Reserve space for tabs that fits between title and search box
        float availableTabWidth = ImGui.GetContentRegionAvail().X - 200 - 10; // 200 for search box, 10 for spacing
        float tabBarWidth = Math.Min(300, availableTabWidth); // Limit tab bar width

        ImGui.SetCursorPosX(238);

        if (ImGui.BeginTabBar("AssetTypeTabs", ImGuiTabBarFlags.None))
        {
            for (int i = 0; i < AssetBrowser.tabs.Length; i++)
            {
                if (ImGui.BeginTabItem(AssetBrowser.tabs[i]))
                {
                    if (AssetBrowser.selectedTab != i)
                    {
                        AssetBrowser.selectedTab = i;
                        AssetBrowser.elementSelected = null;
                        AssetBrowser.searchResults.Clear();
                        AssetBrowser.searchText = "";
                        

                        //Console.WriteLine("Selected Tab Changed, elementSelected: " + AssetBrowser.elementSelected);   
                    }

                    //UIManager.elementSelected = null;
                    ImGui.EndTabItem();
                }
            }
            ImGui.EndTabBar();
        }

        ImGui.PopStyleColor(3);
        ImGui.PopStyleVar();

        // Search bar on the right
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - 158);
        ImGui.SetNextItemWidth(170);

        
        if (ImGui.InputText("##Search", ref AssetBrowser.searchText, 100))
        {
            AssetBrowser.UpdateSearch();
        }

        /*
        ImGui.SameLine();
        if (ImGui.Button("⌄", new Vector2(24, searchBarHeight)))
        {
            ImGui.OpenPopup("SearchOptionsPopup");
        }

        if (ImGui.BeginPopup("SearchOptionsPopup"))
        {
            ImGui.Text("Search Options");
            ImGui.Separator();
            ImGui.Checkbox("Search names", ref searchNames);
            ImGui.Checkbox("Search tags", ref searchTags);
            ImGui.EndPopup();
        }
        */

        //ImGui.Separator();

        // Calculate content area dimensions
        float totalWidth = ImGui.GetContentRegionAvail().X;
        float leftPanelWidth = separatorPosition;
        float rightPanelWidth = totalWidth - leftPanelWidth - separatorWidth;

        // Left panel (folder tree)
        ImGui.BeginChild("LeftPanel", new Vector2(leftPanelWidth, ImGui.GetContentRegionAvail().Y), ImGuiChildFlags.Border);

        // Show category specific title based on selected tab
        ImGui.Text($"{AssetBrowser.tabs[AssetBrowser.selectedTab]} Collections");

        // Render the full texture hierarchy
        // Updated to work with a list of hierarchy elements
        /*
        if (AssetBrowser.textureRootElements != null && AssetBrowser.textureRootElements.Length > 0)
        {
            RenderTextureHierarchy(AssetBrowser.textureRootElements.ToList());
        }
        else
        {
            ImGui.TextColored(new Vector4(1, 0.5f, 0.5f, 1), "No texture hierarchy available");
        }
        */
        switch (AssetBrowser.tabs[AssetBrowser.selectedTab])
        {
            case "Textures":
                if (AssetBrowser.textureRootElements != null && AssetBrowser.textureRootElements.Length > 0)
                {
                    RenderTextureHierarchy(AssetBrowser.textureRootElements.ToList());
                }
                else
                {
                    ImGui.TextColored(new Vector4(1, 0.5f, 0.5f, 1), "No texture hierarchy available");
                }
                break;
            case "Models":
                if (AssetBrowser.modelRootElements != null && AssetBrowser.modelRootElements.Length > 0)
                {
                    RenderTextureHierarchy(AssetBrowser.modelRootElements.ToList());
                }
                else
                {
                    ImGui.TextColored(new Vector4(1, 0.5f, 0.5f, 1), "No model hierarchy available");
                }
                break;
            case "Entities":
                if (AssetBrowser.entityRootElements != null && AssetBrowser.entityRootElements.Length > 0)
                {
                    RenderTextureHierarchy(AssetBrowser.entityRootElements.ToList());
                }
                else
                {
                    ImGui.TextColored(new Vector4(1, 0.5f, 0.5f, 1), "No entity hierarchy available");
                }
                break;
        }

        ImGui.EndChild();

        // Separator (draggable)
        ImGui.SameLine();
        ImGui.InvisibleButton("Separator", new Vector2(separatorWidth, ImGui.GetContentRegionAvail().Y));
        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
        if (ImGui.IsItemActive())
        {
            separatorPosition += ImGui.GetIO().MouseDelta.X;
            separatorPosition = Math.Max(50, Math.Min(totalWidth - 100, separatorPosition));
        }

        // Right panel (asset grid)
        ImGui.SameLine();
        ImGui.BeginChild("RightPanel", new Vector2(rightPanelWidth, ImGui.GetContentRegionAvail().Y), ImGuiChildFlags.Border);

        // Header section with title and slider
        ImGui.BeginChild("HeaderSection", new Vector2(ImGui.GetContentRegionAvail().X, 30), ImGuiChildFlags.None);
        ImGui.Text(AssetBrowser.tabs[AssetBrowser.selectedTab]);

        // Calculate slider width to fit nicely
        float sliderWidth = 120;
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - sliderWidth);
        ImGui.SetNextItemWidth(sliderWidth);
        int tempButtonsPerRow = buttonsPerRow;
        if (ImGui.SliderInt("##ItemsPerRow", ref tempButtonsPerRow, 2, 15))
        { 
            buttonsPerRow = tempButtonsPerRow;
        }

        // Small helper text for the slider
        float textWidth = ImGui.CalcTextSize("Items Per Row").X;
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - sliderWidth - textWidth - 5);
        ImGui.Text("Items Per Row");
        ImGui.EndChild();

        ImGui.Separator();

        ImGui.BeginChild("AssetsGrid", ImGui.GetContentRegionAvail(), ImGuiChildFlags.None);

        // Calculate grid layout parameters
        float availWidth = ImGui.GetContentRegionAvail().X;
        float availHeight = ImGui.GetContentRegionAvail().Y;
        float itemSpacing = 5f; // Space between grid items
        widthButtons = (availWidth - (itemSpacing * (buttonsPerRow - 1))) / buttonsPerRow;

        if (AssetBrowser.searchResults.Count != 0)
        {
            int loopAmount = AssetBrowser.searchResults.Count;

            MainScript.debugText["loop"] = "loopingAmount: " + loopAmount + " texCount: " + AssetBrowser.searchResults.Count;

            if (loopAmount > 0)
            {   
                // Draw asset grid - filtered by selected tab type
                for (int i = 0; i < loopAmount; i++)
                {
                    // Start new row when needed
                    if (i % buttonsPerRow != 0 && i > 0)
                        ImGui.SameLine(0, itemSpacing);

                    // Skip if index is out of range (safety check)
                    if (i >= AssetBrowser.searchResults.Count)
                        continue;
                        
                    AssetBrowser.ItemElement itemElement = AssetBrowser.searchResults[AssetBrowser.searchResults.Keys.ToArray()[i]]; // i
                    
                    // Skip invalid textures
                    if (itemElement.tex == IntPtr.Zero) { Console.WriteLine("Continuing cus ZERO"); continue; }
                        
                        
                    ImGui.PushID(i);
                    
                    // Begin a group for the asset (button + text)
                    ImGui.BeginGroup();
                    
                    // Display asset as image button with no padding
                    ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
                    bool clicked = ImGui.ImageButton($"##AssetBtn{i}", itemElement.tex, 
                        new Vector2(widthButtons, widthButtons));

                    // Handle drag start when mouse is clicked and held
                    if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left) && !AssetBrowser.isDraggingItem)
                    {
                        AssetBrowser.isDraggingItem = true;
                        AssetBrowser.draggingItem = itemElement;
                        Console.WriteLine($"Started dragging texture: {AssetBrowser.draggingItem.name}");
                    }

                    ImGui.PopStyleVar();
                    
                    // Get item dimensions for overlay text
                    float startY = ImGui.GetItemRectMin().Y;
                    float endY = ImGui.GetItemRectMax().Y;
                    float startX = ImGui.GetItemRectMin().X;
                    float endX = ImGui.GetItemRectMax().X;
                    
                    // Calculate text size and handle long names
                    Vector2 textSize = ImGui.CalcTextSize(itemElement.name);
                    string displayName = itemElement.name;
                    
                    if (textSize.X > widthButtons - 6)
                    {
                        int maxChars = (int)((widthButtons - 10) / (textSize.X / itemElement.name.Length));
                        displayName = itemElement.name.Substring(0, Math.Max(3, maxChars - 3)) + "...";
                        textSize = ImGui.CalcTextSize(displayName);
                    }
                    
                    // Use less height for the text background - just enough to fit text
                    float textHeight = ImGui.GetTextLineHeight() + 2;
                    
                    // Draw semi-transparent background for text at bottom of image
                    ImGui.GetWindowDrawList().AddRectFilled(
                        new Vector2(startX, endY - textHeight - 2),
                        new Vector2(endX, endY),
                        ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.1f, 0.1f, 0.7f))
                    );
                    
                    // Draw centered text with custom positioning
                    ImGui.GetWindowDrawList().AddText(
                        new Vector2(startX + (widthButtons - textSize.X) * 0.5f, endY - textHeight - 1),
                        ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1)),
                        displayName
                    );
                    
                    ImGui.EndGroup();
                    ImGui.PopID();
                }
            }
            else
            {
                ImGui.SetCursorPos(new Vector2(availWidth/2 - 100, availHeight/2));
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Select a folder to view textures");
            }
        }
        else
        {
            int loopAmount = AssetBrowser.elementSelected == null ? 0 : AssetBrowser.elementSelected.itemsInDir.Count;

            MainScript.debugText["loop"] = "loopingAmount: " + loopAmount + " texCount: " + (AssetBrowser.elementSelected == null ? "NULL" : "" + AssetBrowser.elementSelected.itemsInDir.Count);

            if (loopAmount > 0)
            {   
                // Draw asset grid - filtered by selected tab type
                for (int i = 0; i < loopAmount; i++)
                {
                    // Start new row when needed
                    if (i % buttonsPerRow != 0 && i > 0)
                        ImGui.SameLine(0, itemSpacing);

                    // Skip if index is out of range (safety check)
                    if (i >= AssetBrowser.elementSelected.itemsInDir.Count)
                        continue;
                        
                    AssetBrowser.ItemElement itemElement = AssetBrowser.elementSelected.itemsInDir[AssetBrowser.elementSelected.itemsInDir.Keys.ToArray()[i]];
                    
                    // Skip invalid textures
                    if (itemElement.tex == IntPtr.Zero) { Console.WriteLine("Continuing cus ZERO"); continue; }
                        
                        
                    ImGui.PushID(i);
                    
                    // Begin a group for the asset (button + text)
                    ImGui.BeginGroup();
                    
                    // Display asset as image button with no padding
                    ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
                    bool clicked = ImGui.ImageButton($"##AssetBtn{i}", itemElement.tex, 
                        new Vector2(widthButtons, widthButtons));

                    // Handle drag start when mouse is clicked and held
                    if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left) && !AssetBrowser.isDraggingItem)
                    {
                        AssetBrowser.isDraggingItem = true;
                        AssetBrowser.draggingItem = itemElement;
                        Console.WriteLine($"Started dragging texture: {AssetBrowser.draggingItem.name}");
                    }

                    ImGui.PopStyleVar();
                    
                    // Get item dimensions for overlay text
                    float startY = ImGui.GetItemRectMin().Y;
                    float endY = ImGui.GetItemRectMax().Y;
                    float startX = ImGui.GetItemRectMin().X;
                    float endX = ImGui.GetItemRectMax().X;
                    
                    // Calculate text size and handle long names
                    Vector2 textSize = ImGui.CalcTextSize(itemElement.name);
                    string displayName = itemElement.name;
                    
                    if (textSize.X > widthButtons - 6)
                    {
                        int maxChars = (int)((widthButtons - 10) / (textSize.X / itemElement.name.Length));
                        displayName = itemElement.name.Substring(0, Math.Max(3, maxChars - 3)) + "...";
                        textSize = ImGui.CalcTextSize(displayName);
                    }
                    
                    // Use less height for the text background - just enough to fit text
                    float textHeight = ImGui.GetTextLineHeight() + 2;
                    
                    // Draw semi-transparent background for text at bottom of image
                    ImGui.GetWindowDrawList().AddRectFilled(
                        new Vector2(startX, endY - textHeight - 2),
                        new Vector2(endX, endY),
                        ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.1f, 0.1f, 0.7f))
                    );
                    
                    // Draw centered text with custom positioning
                    ImGui.GetWindowDrawList().AddText(
                        new Vector2(startX + (widthButtons - textSize.X) * 0.5f, endY - textHeight - 1),
                        ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1)),
                        displayName
                    );
                    
                    ImGui.EndGroup();
                    ImGui.PopID();
                }
            }
            else
            {
                ImGui.SetCursorPos(new Vector2(availWidth/2 - 100, availHeight/2));
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Select a folder to view textures");
            }
        }

        

        ImGui.EndChild();
    }

    private static void RenderTextureHierarchy(List<AssetBrowser.HierarchyElement> elements, List<string> expandedPaths = null)
    {
        if (elements == null || elements.Count == 0)
        {
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "No elements to display");
            return;
        }
        
        // Render each root element
        foreach (var rootElement in elements)
        {
            // Render individual element and its children
            RenderHierarchyElement(rootElement, expandedPaths);
        }
    }

    // Helper method to render a single hierarchy element and its children
    private static void RenderHierarchyElement(AssetBrowser.HierarchyElement element, List<string> expandedPaths = null, string currentPath = "")
    {
        if (element == null)
            return;

        // Determine flags for this tree node
        ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow;

        // If this is a leaf node (no children), use leaf flag
        if (element.Children.Count == 0)
            flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

        // If this element is selected, add selected flag
        if (element.IsExpanded)
            flags |= ImGuiTreeNodeFlags.Selected;

        // Build the full path for this element
        string fullPath = string.IsNullOrEmpty(currentPath) ? element.Name : $"{currentPath}/{element.Name}";

        // Check if this node should be pre-expanded
        bool defaultOpen = expandedPaths != null && expandedPaths.Contains(fullPath);
        if (defaultOpen)
            ImGui.SetNextItemOpen(true);

        // Render the tree node
        bool isOpen = ImGui.TreeNodeEx(element.Name, flags);

        // Handle clicking on the node
        if (ImGui.IsItemClicked())
        {
            // Handle selection
            element.IsExpanded = !element.IsExpanded;

            // Deselect all other elements
            if (element.IsExpanded)
            {
                switch (AssetBrowser.tabs[AssetBrowser.selectedTab])
                {
                    case "Textures":
                        AssetBrowser.DeselectAllExcept(AssetBrowser.textureRootElements.ToList(), element);
                        break;
                    case "Models":
                        AssetBrowser.DeselectAllExcept(AssetBrowser.modelRootElements.ToList(), element);
                        break;
                    case "Entities":
                        AssetBrowser.DeselectAllExcept(AssetBrowser.entityRootElements.ToList(), element);
                        break;
                }

                

                // Update filtered assets based on the selected category
                AssetBrowser.UpdateFilteredAssets(element);
            }
        }

        // If the node is open and has children, render child nodes
        if (isOpen && element.Children.Count > 0)
        {
            foreach (var child in element.Children)
            {
                RenderHierarchyElement(child, expandedPaths, fullPath);
            }

            ImGui.TreePop();
        }
    }

    // Modified to support multiple root hierarchies
    

    /*
    private static void RenderAssetBrowser()
    {
        GUILayout.Box("", GUILayout.Width(position.width));
        Rect boxBox = GUILayoutUtility.GetLastRect();



        if (GUI.Button(new Rect(5, 3, boxBox.height, boxBox.height), "")) //texSettings_con))
        {
            if (AssetBrowserSettings.DebugToggle) { Debug.Log("Settings"); }
            AssetBrowserSettings.ShowWindow();
        }

        GUI.DrawTexture(new Rect(5, 3, boxBox.height, boxBox.height), texSettings);

        EditorGUI.LabelField(new Rect(31, 5, 170, boxBox.height - 4), "Asset Browser", EditorStyles.boldLabel);

        //GUIStyle searchBoxStyle = new GUIStyle(GUI.skin.textField) { alignment = TextAnchor.MiddleLeft };

        if (GUI.Button(new Rect(boxBox.width - (170) - (boxBox.height - 4) - 10, 5, boxBox.height - 4, boxBox.height - 4), "", EditorStyles.miniButtonLeft))
        {
            //Debug.Log("Dropdown ");

            if (true) //dropdownSearch == false)
            {
                searchWindow = ScriptableObject.CreateInstance<PopdownSearchWindow>();

                Vector2 pos = GUIUtility.GUIToScreenPoint(new Vector2(boxBox.width - (170) - (boxBox.height - 4) - 10, 5 + (boxBox.height - 4)));

                searchWindow.position = new Rect(pos.x, pos.y, 170, 40);
                searchWindow.ShowPopup();
            }

            dropdownSearch = !dropdownSearch;
        }

        EditorGUI.BeginChangeCheck();

        GUI.SetNextControlName("SearchField");
        textSearch = EditorGUI.TextField(new Rect(boxBox.width - (170), 5, 170, boxBox.height - 4), "", EditorStyles.toolbarSearchField);

        if (EditorGUI.EndChangeCheck())
        {
            textSearchChanged = textSearch;
        }

        if (GUI.GetNameOfFocusedControl() == "SearchField")
        {
            Event e = Event.current;
            if (e.isKey && e.keyCode == KeyCode.Return)
            {
                //e.Use();  // Prevent the event from propagating further
                //Debug.Log("Searched " + textSearchChanged);
                textSearch = textSearchChanged;
                Searched(textSearch);
            }
        }






        buttonsPerRow = EditorGUI.IntSlider(new Rect(boxBox.width - (110), 5 + boxBox.height, 110, boxBox.height - 4), buttonsPerRow, 2, 15); //EditorGUI.IntField(new Rect(boxBox.width - (100), 5 + boxBox.height, 100, boxBox.height - 4), buttonPerRowUI);


        EditorGUILayout.BeginHorizontal();

        // First column
        GUILayout.BeginVertical(GUILayout.Width(separatorPosition));
        GUILayout.Label("Asset Collections", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        selectedDropdown = EditorGUILayout.Popup(selectedDropdown, itemsDropdown.ToArray());

        if (EditorGUI.EndChangeCheck())
        {
            //Debug.Log("Dropdown " + itemsDropdown[selectedDropdown]);
            AssetBrowserLogic.Start();
        }

        scrollPositionHira = EditorGUILayout.BeginScrollView(scrollPositionHira);

        foreach (var element in elements)
        {
            DrawElement(element);
        }

        EditorGUILayout.EndScrollView();

        GUILayout.EndVertical();

        // Separator
        EditorGUIUtility.AddCursorRect(new Rect(separatorPosition, 0, separatorWidth, position.height), MouseCursor.ResizeHorizontal);
        GUILayout.Box("", GUILayout.Width(separatorWidth), GUILayout.ExpandHeight(true));

        // Handle mouse dragging for separator
        HandleSeparatorDragging();

        // Second column
        GUILayout.BeginVertical(GUILayout.Width(position.width - separatorPosition - separatorWidth));
        GUILayout.Label("Assets" + searchTermText, EditorStyles.boldLabel);


        widthButtons = (position.width - separatorPosition - separatorWidth - (buttonsPerRow * 5)) / buttonsPerRow;

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Width(position.width - separatorPosition - 14));

        int rowCount = Mathf.CeilToInt((float)numberOfButtons / buttonsPerRow);
        Rect scrollViewRect = GUILayoutUtility.GetRect(0, rowCount * widthButtons);
        float scrollViewTop = scrollPosition.y;
        float scrollViewBottom = scrollViewTop + position.height;



        for (int i = 0; i < numberOfButtons; i++)
        {
            int row = i / buttonsPerRow;
            int column = i % buttonsPerRow;

            float itemTop = row * widthButtons;
            float itemBottom = itemTop + widthButtons;

            if (itemBottom >= scrollViewTop && itemTop <= scrollViewBottom)
            {
                if ((buttonsAssets.Count - 1) < i) { continue; }

                Rect itemRect = new Rect(column * widthButtons, itemTop, widthButtons, widthButtons);

                GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);

                if (buttonsAssets[i].previewPNG != null)
                { buttonStyle.normal.background = buttonsAssets[i].previewPNG; }

                buttonStyle.alignment = TextAnchor.LowerLeft;
                buttonStyle.fontSize = Mathf.Max(10, (int)(widthButtons / 11));

                Vector2 textSize = buttonStyle.CalcSize(new GUIContent(buttonsAssets[i].assetName));

                if (textSize.x > widthButtons) { buttonStyle.alignment = TextAnchor.LowerLeft; }
                else { buttonStyle.alignment = TextAnchor.LowerCenter; }

                //if (GUI.Button(itemRect, buttonsAssets[i].fileName, buttonStyle))
                //EditorGUI.DropdownButton

                //if(buttonsAssets[i].assetName.Contains("00")) { continue; }

                if (EditorGUI.DropdownButton(itemRect, new GUIContent(buttonsAssets[i].assetName, buttonsAssets[i].assetName), FocusType.Passive, buttonStyle))
                {
                    string rebuiltFilepath = "";

                    string[] filePathSplit = buttonsAssets[i].filePath.Replace(Path.DirectorySeparatorChar, '/').Split("/");

                    int i2 = 0;
                    foreach (string splitdir in filePathSplit)
                    {
                        if (i2 == filePathSplit.Length - 1) { rebuiltFilepath += buttonsAssets[i].assetName; continue; }

                        rebuiltFilepath += splitdir + "/";
                        i2++;
                    }

                    currentlyDraggingAsset = rebuiltFilepath;
                    currentlyDraggingAssetTex = buttonsAssets[i].previewPNG;

                    //currentlyDraggingType = filePathSplit[filePathSplit.Length - 1].Split('.')[1];

                    if (AssetBrowserSettings.DebugToggle) { Debug.Log("currentlyDragging " + currentlyDraggingAsset); }

                    if (filePathSplit[filePathSplit.Length - 1].Split('.')[1] == "fbx") { SceneManagerAssetBrowser.draggingStarted = true; }
                    else
                    {
                        if (!Directory.Exists(UnityAssetPathToOSPath("Assets/ImportedModels/Materials/")))
                        { AssetDatabase.CreateFolder("Assets/ImportedModels", "Materials"); }

                        string[] splitdraggingpath = currentlyDraggingAsset.Split('/');
                        string rebuiltdraggingpath = ""; int i3 = 0; foreach (string filefrag in splitdraggingpath) { if (i3 != splitdraggingpath.Length - 1) { rebuiltdraggingpath += filefrag + "/"; } i3++; }

                        if (!Directory.Exists(UnityAssetPathToOSPath("Assets/ImportedModels/Materials/" + splitdraggingpath[splitdraggingpath.Length - 2])))
                        { AssetDatabase.CreateFolder("Assets/ImportedModels/Materials", splitdraggingpath[splitdraggingpath.Length - 2]); }

                        string materialPathUnityProj = UnityAssetPathToOSPath("Assets/ImportedModels/Materials/" + splitdraggingpath[splitdraggingpath.Length - 2] + "/" + filePathSplit[filePathSplit.Length - 1]);

                        if (AssetBrowserSettings.DebugToggle) { Debug.Log("rebuot Source Thingy " + materialPathUnityProj); }

                        //Debug.Log("splitdraggingpath [" + string.Join(", ", splitdraggingpath) + "]" + "\nfilepathsplit [" + string.Join(", ", filePathSplit) + "]");


                        if (File.Exists(materialPathUnityProj))
                        {
                            materialPathMade = "Assets/ImportedModels/Materials/" + splitdraggingpath[splitdraggingpath.Length - 2] + "/" + filePathSplit[filePathSplit.Length - 1];
                            if (AssetBrowserSettings.DebugToggle) { Debug.Log("materialPath SKIPPING " + materialPathMade); }

                            SceneManagerAssetBrowser.draggingStarted = true;

                            return;
                        }

                        //AssetDatabase.Ex

                        foreach (string line in File.ReadAllLines(currentlyDraggingAsset + ".mat"))
                        {
                            if (line.Contains(":::t["))
                            {
                                string tex = line.Split(":::")[1];
                                tex = tex.Replace("t[", "");
                                tex = tex.Replace("]", "");

                                //Debug.Log("")

                                string texFilePath = rebuiltdraggingpath + tex + ".png";

                                if (AssetBrowserSettings.DebugToggle) { Debug.Log("textFilePath " + texFilePath); }





                                //string[] destFilePathSplit = ("Assets/ImportedAssets/Materials/" + splitdraggingpath[splitdraggingpath.Length - 2]).Split("/");
                                //string rebuiltdestFile = "";

                                //int i2 = 0; foreach(string frag in destFilePathSplit) { if(destFilePathSplit.Length - 1 != i2) { rebuiltdestFile += frag + "/"; } i2++; }

                                string unityPath = "Assets/ImportedModels/Materials/" + splitdraggingpath[splitdraggingpath.Length - 2] + "/" + tex + ".png";

                                //Debug.Log("unityPAthPhysical " + UnityAssetPathToOSPath(unityPath));

                                if (!File.Exists(FileUtil.GetPhysicalPath("Assets/ImportedModels/Materials/" + splitdraggingpath[splitdraggingpath.Length - 2] + "/" + tex + ".png")))
                                {
                                    FileUtil.CopyFileOrDirectory(FileUtil.GetPhysicalPath(texFilePath), UnityAssetPathToOSPath(unityPath));

                                    AssetDatabase.ImportAsset(unityPath);
                                }
                            }
                        }

                        // LOAD HERE
                        materialPathMade = AssetBrowserLogic.LoadMat("Assets/ImportedModels/Materials", currentlyDraggingAsset + ".mat");
                        if (AssetBrowserSettings.DebugToggle) { Debug.Log("materialPath " + materialPathMade); }

                        SceneManagerAssetBrowser.draggingStarted = true;

                    }

                    //Debug.Log($"Clicked " + rebuiltFilepath);
                }
            }
            else
            {
                //Debug.Log("Skipped " + i);
            }
        }

        EditorGUILayout.EndScrollView();




        GUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

    }
    */


    private static void RenderInspector()
    {
        // Check if we have a selected object
        if (EditorMain.selectedBrush == null)
        {
            //ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "No object selected");
            return;
        }

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.2f, 0.2f, 1.0f)); // Normal button color
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.3f, 0.3f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.25f, 0.25f, 0.25f, 1.0f));

        // Title bar
        ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[0]);
        ImGui.Text("Inspector");
        ImGui.PopFont();
        ImGui.Separator();

        ImGui.Dummy(new Vector2(0, 4));

        // Common properties section - No collapsible header
        float checkboxWidth = 20f;
        float spacing = 8f;

        // Active status checkbox and name on the same line
        ImGui.Checkbox("##Active", ref EditorMain.selectedBrush.Active);

        ImGui.SameLine();
        float remainingWidth = ImGui.GetContentRegionAvail().X;
        ImGui.SetNextItemWidth(remainingWidth);
        ImGui.InputText("##Name", ref EditorMain.selectedBrush.Name, 100);

        ImGui.Dummy(new Vector2(0, 4));
        ImGui.Separator();
        ImGui.Dummy(new Vector2(0, 10));

        if (EditorMain.Mode == EditMode.Surface && RealtimeCSG.selectedSurfacesIndices.Count > 0)
        {
            // For multiple face selection handling
            bool scaleXSame = true;
            bool scaleYSame = true;
            bool offsetXSame = true;
            bool offsetYSame = true;
            bool rotationSame = true;
            bool texPtrSame = true;
            bool normalStrengthSame = true;
            bool metallicSame = true;
            bool smoothnessSame = true;



            // Store reference values from the first selected face
            float referenceScaleX = EditorMain.selectedBrush.Faces[EditorMain.selectedBrush.IndicesToFaceIndex[RealtimeCSG.selectedSurfacesIndices[0]]].Scale.X;
            float referenceScaleY = EditorMain.selectedBrush.Faces[EditorMain.selectedBrush.IndicesToFaceIndex[RealtimeCSG.selectedSurfacesIndices[0]]].Scale.Y;
            float referenceOffsetX = EditorMain.selectedBrush.Faces[EditorMain.selectedBrush.IndicesToFaceIndex[RealtimeCSG.selectedSurfacesIndices[0]]].Offset.X;
            float referenceOffsetY = EditorMain.selectedBrush.Faces[EditorMain.selectedBrush.IndicesToFaceIndex[RealtimeCSG.selectedSurfacesIndices[0]]].Offset.Y;
            float referenceRotation = EditorMain.selectedBrush.Faces[EditorMain.selectedBrush.IndicesToFaceIndex[RealtimeCSG.selectedSurfacesIndices[0]]].Rotation;
            Texture refrenceTexture = EditorMain.selectedBrush.Faces[EditorMain.selectedBrush.IndicesToFaceIndex[RealtimeCSG.selectedSurfacesIndices[0]]].Tex;
            float referenceNormalStrength = EditorMain.selectedBrush.Faces[EditorMain.selectedBrush.IndicesToFaceIndex[RealtimeCSG.selectedSurfacesIndices[0]]].NormalStrength;
            float referenceMetallic = EditorMain.selectedBrush.Faces[EditorMain.selectedBrush.IndicesToFaceIndex[RealtimeCSG.selectedSurfacesIndices[0]]].Metallic;
            float referenceSmoothness = EditorMain.selectedBrush.Faces[EditorMain.selectedBrush.IndicesToFaceIndex[RealtimeCSG.selectedSurfacesIndices[0]]].Smoothness;



            // Compare other selected faces to check for differences
            foreach ((int, int, int, int) faceIndices in RealtimeCSG.selectedSurfacesIndices)
            {
                int faceIndex = EditorMain.selectedBrush.IndicesToFaceIndex[faceIndices];

                if (faceIndex == EditorMain.selectedBrush.IndicesToFaceIndex[RealtimeCSG.selectedSurfacesIndices[0]]) continue;

                if (Math.Abs(EditorMain.selectedBrush.Faces[faceIndex].Scale.X - referenceScaleX) > 0.0001f)
                    scaleXSame = false;

                if (Math.Abs(EditorMain.selectedBrush.Faces[faceIndex].Scale.Y - referenceScaleY) > 0.0001f)
                    scaleYSame = false;

                if (Math.Abs(EditorMain.selectedBrush.Faces[faceIndex].Offset.X - referenceOffsetX) > 0.0001f)
                    offsetXSame = false;

                if (Math.Abs(EditorMain.selectedBrush.Faces[faceIndex].Offset.Y - referenceOffsetY) > 0.0001f)
                    offsetYSame = false;

                if (Math.Abs(EditorMain.selectedBrush.Faces[faceIndex].Rotation - referenceRotation) > 0.0001f)
                    rotationSame = false;

                if (refrenceTexture.handle != EditorMain.selectedBrush.Faces[faceIndex].Tex.handle)
                    texPtrSame = false;

                if (Math.Abs(EditorMain.selectedBrush.Faces[faceIndex].NormalStrength - referenceNormalStrength) > 0.0001f)
                    normalStrengthSame = false;

                if (Math.Abs(EditorMain.selectedBrush.Faces[faceIndex].Metallic - referenceMetallic) > 0.0001f)
                    metallicSame = false;

                if (Math.Abs(EditorMain.selectedBrush.Faces[faceIndex].Smoothness - referenceSmoothness) > 0.0001f)
                    smoothnessSame = false;

            }

            // Temporary values for the dash state inputs
            float tempScaleX = referenceScaleX;
            float tempScaleY = referenceScaleY;
            float tempOffsetX = referenceOffsetX;
            float tempOffsetY = referenceOffsetY;
            float tempRotation = referenceRotation;
            Texture tempTexture = refrenceTexture;
            float tempNormalStrength = referenceNormalStrength;
            float tempMetallic = referenceMetallic;
            float tempSmoothness = referenceSmoothness;

            // UV Scale
            ImGui.Text("UV Scale");
            ImGui.SameLine(ImGui.GetWindowWidth() * 0.25f);
            ImGui.PushItemWidth((ImGui.GetContentRegionAvail().X) / 2 - 8);

            //EditorMain.selectedBrush.IndicesToFaceIndex[RealtimeCSG.selectedSurfacesIndices[0]]

            // X Scale with dash for mixed values
            if (!scaleXSame)
            {
                // Display dash for mixed values
                string dashValue = "-";
                if (ImGui.InputText("##ScaleX", ref dashValue, 100, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    // Parse the new value if it's not a dash
                    if (dashValue != "-" && float.TryParse(dashValue, out float newValue))
                    {
                        // Apply to all selected faces
                        foreach ((int, int, int, int) faceIndices in RealtimeCSG.selectedSurfacesIndices)
                        {
                            int faceIndex = EditorMain.selectedBrush.IndicesToFaceIndex[faceIndices];
                            EditorMain.selectedBrush.Faces[faceIndex].Scale.X = newValue;
                        }
                    }
                }
            }
            else
            {
                // Show actual value when all the same
                if (ImGui.InputFloat("##ScaleX", ref tempScaleX))
                {
                    // Apply to all selected faces
                    foreach ((int, int, int, int) faceIndices in RealtimeCSG.selectedSurfacesIndices)
                    {
                        int faceIndex = EditorMain.selectedBrush.IndicesToFaceIndex[faceIndices];
                        EditorMain.selectedBrush.Faces[faceIndex].Scale.X = tempScaleX;
                    }
                }
            }

            ImGui.SameLine();

            // Y Scale with dash for mixed values
            if (!scaleYSame)
            {
                string dashValue = "-";
                if (ImGui.InputText("##ScaleY", ref dashValue, 100, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (dashValue != "-" && float.TryParse(dashValue, out float newValue))
                    {
                        foreach ((int, int, int, int) faceIndices in RealtimeCSG.selectedSurfacesIndices)
                        {
                            int faceIndex = EditorMain.selectedBrush.IndicesToFaceIndex[faceIndices];
                            EditorMain.selectedBrush.Faces[faceIndex].Scale.Y = newValue;
                        }
                    }
                }
            }
            else
            {
                if (ImGui.InputFloat("##ScaleY", ref tempScaleY))
                {
                    foreach ((int, int, int, int) faceIndices in RealtimeCSG.selectedSurfacesIndices)
                    {
                        int faceIndex = EditorMain.selectedBrush.IndicesToFaceIndex[faceIndices];
                        EditorMain.selectedBrush.Faces[faceIndex].Scale.Y = tempScaleY;
                    }
                }
            }

            ImGui.Spacing();

            // Similar pattern for UV Offset and Rotation
            // UV Offset
            ImGui.Text("UV Offset");
            ImGui.SameLine(ImGui.GetWindowWidth() * 0.25f);
            ImGui.PushItemWidth((ImGui.GetContentRegionAvail().X) / 2 - 8);

            // X Offset with dash for mixed values
            if (!offsetXSame)
            {
                string dashValue = "-";
                if (ImGui.InputText("##OffsetX", ref dashValue, 100, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (dashValue != "-" && float.TryParse(dashValue, out float newValue))
                    {
                        foreach ((int, int, int, int) faceIndices in RealtimeCSG.selectedSurfacesIndices)
                        {
                            int faceIndex = EditorMain.selectedBrush.IndicesToFaceIndex[faceIndices];
                            EditorMain.selectedBrush.Faces[faceIndex].Offset.X = newValue;
                        }
                    }
                }
            }
            else
            {
                if (ImGui.InputFloat("##OffsetX", ref tempOffsetX))
                {
                    foreach ((int, int, int, int) faceIndices in RealtimeCSG.selectedSurfacesIndices)
                    {
                        int faceIndex = EditorMain.selectedBrush.IndicesToFaceIndex[faceIndices];
                        EditorMain.selectedBrush.Faces[faceIndex].Offset.X = tempOffsetX;
                    }
                }
            }

            ImGui.SameLine();

            // Y Offset with dash for mixed values
            if (!offsetYSame)
            {
                string dashValue = "-";
                if (ImGui.InputText("##OffsetY", ref dashValue, 100, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (dashValue != "-" && float.TryParse(dashValue, out float newValue))
                    {
                        foreach ((int, int, int, int) faceIndices in RealtimeCSG.selectedSurfacesIndices)
                        {
                            int faceIndex = EditorMain.selectedBrush.IndicesToFaceIndex[faceIndices];
                            EditorMain.selectedBrush.Faces[faceIndex].Offset.Y = newValue;
                        }
                    }
                }
            }
            else
            {
                if (ImGui.InputFloat("##OffsetY", ref tempOffsetY))
                {
                    foreach ((int, int, int, int) faceIndices in RealtimeCSG.selectedSurfacesIndices)
                    {
                        int faceIndex = EditorMain.selectedBrush.IndicesToFaceIndex[faceIndices];
                        EditorMain.selectedBrush.Faces[faceIndex].Offset.Y = tempOffsetY;
                    }
                }
            }

            ImGui.Spacing();

            // Rotation with dash for mixed values
            ImGui.Text("Rotation");
            ImGui.SameLine(ImGui.GetWindowWidth() * 0.25f);
            ImGui.PushItemWidth((ImGui.GetContentRegionAvail().X) / 2 - 8);

            if (!rotationSame)
            {
                string dashValue = "-";
                if (ImGui.InputText("##Rotation", ref dashValue, 100, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (dashValue != "-" && float.TryParse(dashValue, out float newValue))
                    {
                        foreach ((int, int, int, int) faceIndices in RealtimeCSG.selectedSurfacesIndices)
                        {
                            int faceIndex = EditorMain.selectedBrush.IndicesToFaceIndex[faceIndices];
                            EditorMain.selectedBrush.Faces[faceIndex].Rotation = newValue;
                        }
                    }
                }
            }
            else
            {
                if (ImGui.InputFloat("##Rotation", ref tempRotation))
                {
                    foreach ((int, int, int, int) faceIndices in RealtimeCSG.selectedSurfacesIndices)
                    {
                        int faceIndex = EditorMain.selectedBrush.IndicesToFaceIndex[faceIndices];
                        EditorMain.selectedBrush.Faces[faceIndex].Rotation = tempRotation;
                    }
                }
            }

            // Reset buttons section
            ImGui.Dummy(new Vector2(0, 10));
            ImGui.Text("Reset");
            ImGui.SameLine(ImGui.GetWindowWidth() * 0.25f);
            float buttonWidth = (ImGui.GetContentRegionAvail().X) / 3 - 4;

            if (ImGui.Button("UV", new Vector2(buttonWidth, 0)))
            {
                // Reset UV for all selected faces
                foreach ((int, int, int, int) faceIndices in RealtimeCSG.selectedSurfacesIndices)
                {
                    int faceIndex = EditorMain.selectedBrush.IndicesToFaceIndex[faceIndices];
                    EditorMain.selectedBrush.Faces[faceIndex].Scale = Vector2.One;
                    EditorMain.selectedBrush.Faces[faceIndex].Offset = Vector2.Zero;
                    EditorMain.selectedBrush.Faces[faceIndex].Rotation = 0;
                }
            }

            ImGui.SameLine();
            if (ImGui.Button("U", new Vector2(buttonWidth, 0)))
            {
                // Reset UV for all selected faces
                foreach ((int, int, int, int) faceIndices in RealtimeCSG.selectedSurfacesIndices)
                {
                    int faceIndex = EditorMain.selectedBrush.IndicesToFaceIndex[faceIndices];
                    EditorMain.selectedBrush.Faces[faceIndex].Scale = Vector2.One;
                    EditorMain.selectedBrush.Faces[faceIndex].Offset = Vector2.Zero;
                    EditorMain.selectedBrush.Faces[faceIndex].Rotation = 0;
                }
            }

            ImGui.SameLine();
            if (ImGui.Button("V", new Vector2(buttonWidth, 0)))
            {
                // Reset UV for all selected faces
                foreach ((int, int, int, int) faceIndices in RealtimeCSG.selectedSurfacesIndices)
                {
                    int faceIndex = EditorMain.selectedBrush.IndicesToFaceIndex[faceIndices];
                    EditorMain.selectedBrush.Faces[faceIndex].Scale = Vector2.One;
                    EditorMain.selectedBrush.Faces[faceIndex].Offset = Vector2.Zero;
                    EditorMain.selectedBrush.Faces[faceIndex].Rotation = 0;
                }
            }

            ImGui.Dummy(new Vector2(0, 10));
            ImGui.Text("Select");
            ImGui.SameLine(ImGui.GetWindowWidth() * 0.25f);
            buttonWidth = (ImGui.GetContentRegionAvail().X) / 3 - 4;

            if (ImGui.Button("All", new Vector2(buttonWidth, 0)))
            {
                
            }

            ImGui.Dummy(new Vector2(0, 10));
            ImGui.Text("Texture");



            if (!texPtrSame)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Mixed textures");
                // Still show a texture preview area that can receive drops
                ImGui.Image(AssetBrowser.emptyTexturePreview.handle, Vector2.One * 100);
            }
            else
            {
                ImGui.Image(tempTexture.handle, Vector2.One * 100);
            }

            // Add a texture drop target in the inspector
            if (ImGui.BeginDragDropTarget() && AssetBrowser.isDraggingItem && 
                AssetBrowser.draggingItem != null && AssetBrowser.tabs[AssetBrowser.selectedTab] == "Textures")
            {
                // Handle the drop when mouse is released
                if (EditorMain.Mode == EditMode.Surface && 
                    EditorMain.selectedBrush != null && 
                    RealtimeCSG.selectedSurfacesIndices.Count > 0)
                {
                    // Get the texture from resource manager using the path in draggingItem
                    string texturePath = Path.GetFileNameWithoutExtension(AssetBrowser.draggingItem.pathToItem);
                    // Convert file system path to proper resource path
                    texturePath = texturePath.Replace('\\', '/');
                    
                    // Apply the texture to all selected faces
                    foreach ((int, int, int, int) faceIndices in RealtimeCSG.selectedSurfacesIndices)
                    {
                        int faceIndex = EditorMain.selectedBrush.IndicesToFaceIndex[faceIndices];
                        EditorMain.selectedBrush.Faces[faceIndex].Tex = ResourceManager.GetTexture(texturePath);
                    }
                    
                    // Clear the drag state
                    UIManager.EndAssetDrag();
                }
                ImGui.EndDragDropTarget();
            }

            ImGui.Dummy(new Vector2(0, 10));

            ImGui.Text("Normal Strength");
            ImGui.SameLine(ImGui.GetWindowWidth() * 0.25f);
            ImGui.PushItemWidth((ImGui.GetContentRegionAvail().X) / 2 - 8);

            ImGui.Dummy(new Vector2((ImGui.GetContentRegionAvail().X) / 2 - 8, 0));
            ImGui.SameLine();

            if (!normalStrengthSame)
            {
                string dashValue = "-";
                if (ImGui.InputText("##NormalStrength", ref dashValue, 100, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (dashValue != "-" && float.TryParse(dashValue, out float newValue))
                    {
                        foreach ((int, int, int, int) faceIndices in RealtimeCSG.selectedSurfacesIndices)
                        {
                            int faceIndex = EditorMain.selectedBrush.IndicesToFaceIndex[faceIndices];
                            EditorMain.selectedBrush.Faces[faceIndex].NormalStrength = newValue;
                        }
                    }
                }
            }
            else
            {
                if (ImGui.InputFloat("##NormalStrength", ref tempNormalStrength))
                {
                    foreach ((int, int, int, int) faceIndices in RealtimeCSG.selectedSurfacesIndices)
                    {
                        int faceIndex = EditorMain.selectedBrush.IndicesToFaceIndex[faceIndices];
                        EditorMain.selectedBrush.Faces[faceIndex].NormalStrength = tempNormalStrength;
                    }
                }
            }

            ImGui.Dummy(new Vector2(0, 10));

            ImGui.Text("Metallic");
            ImGui.SameLine(ImGui.GetWindowWidth() * 0.25f);
            ImGui.PushItemWidth((ImGui.GetContentRegionAvail().X) / 2 - 8);

            ImGui.Dummy(new Vector2((ImGui.GetContentRegionAvail().X) / 2 - 8, 0));
            ImGui.SameLine();

            if (!metallicSame)
            {
                string dashValue = "-";
                if (ImGui.InputText("##Metallic", ref dashValue, 100, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (dashValue != "-" && float.TryParse(dashValue, out float newValue))
                    {
                        foreach ((int, int, int, int) faceIndices in RealtimeCSG.selectedSurfacesIndices)
                        {
                            int faceIndex = EditorMain.selectedBrush.IndicesToFaceIndex[faceIndices];
                            EditorMain.selectedBrush.Faces[faceIndex].Metallic = newValue;
                        }
                    }
                }
            }
            else
            {
                if (ImGui.InputFloat("##Metallic", ref tempMetallic))
                {
                    foreach ((int, int, int, int) faceIndices in RealtimeCSG.selectedSurfacesIndices)
                    {
                        int faceIndex = EditorMain.selectedBrush.IndicesToFaceIndex[faceIndices];
                        EditorMain.selectedBrush.Faces[faceIndex].Metallic = tempMetallic;
                    }
                }
            }
            
            ImGui.Dummy(new Vector2(0, 10));

            ImGui.Text("Smoothness");
            ImGui.SameLine(ImGui.GetWindowWidth() * 0.25f);
            ImGui.PushItemWidth((ImGui.GetContentRegionAvail().X) / 2 - 8);

            ImGui.Dummy(new Vector2((ImGui.GetContentRegionAvail().X) / 2 - 8, 0));
            ImGui.SameLine();

            if (!smoothnessSame)
            {
                string dashValue = "-";
                if (ImGui.InputText("##Smoothness", ref dashValue, 100, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (dashValue != "-" && float.TryParse(dashValue, out float newValue))
                    {
                        foreach ((int, int, int, int) faceIndices in RealtimeCSG.selectedSurfacesIndices)
                        {
                            int faceIndex = EditorMain.selectedBrush.IndicesToFaceIndex[faceIndices];
                            EditorMain.selectedBrush.Faces[faceIndex].Smoothness = newValue;
                        }
                    }
                }
            }
            else
            {
                if (ImGui.InputFloat("##Smoothness", ref tempSmoothness))
                {
                    foreach ((int, int, int, int) faceIndices in RealtimeCSG.selectedSurfacesIndices)
                    {
                        int faceIndex = EditorMain.selectedBrush.IndicesToFaceIndex[faceIndices];
                        EditorMain.selectedBrush.Faces[faceIndex].Smoothness = tempSmoothness;
                    }
                }
            }
            
        }



        ImGui.PopStyleColor(3);
    }
}