

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

public class AssetBrowser
{
    public static HierarchyElement[] textureRootElements;
    public static Dictionary<string, ItemElement> textureItemElementsAll = new Dictionary<string, ItemElement>(); // Dont remove or add to after init

    public static HierarchyElement[] modelRootElements;
    public static Dictionary<string, ItemElement> modelItemElementsAll = new Dictionary<string, ItemElement>();

    public static HierarchyElement[] entityRootElements;
    public static Dictionary<string, ItemElement> entityItemElementsAll = new Dictionary<string, ItemElement>();


    public static HierarchyElement elementSelected = null;

    private static Dictionary<string, Texture> previewTextures = new Dictionary<string, Texture>();
    private static readonly object _textureLock = new object();
    private static readonly Queue<(byte[] data, uint width, uint height, string filePath, string fileName)> _pendingTextureLoads = new Queue<(byte[], uint, uint, string, string)>();
    public static Texture emptyTexturePreview;

    public static string searchText = "";
    public static Dictionary<string, ItemElement> searchResults = new Dictionary<string, ItemElement>();

    public static int selectedTab = 0;
    public static string[] tabs = new string[] { "Textures", "Models", "Entities" };

    public static bool isDraggingItem = false;
    public static ItemElement draggingItem = null;


    public static void Load()
    {
        emptyTexturePreview = ResourceManager.GetTexture("editor/PreviewEmpty");

        HierarchyElement textureRootElement = new HierarchyElement("root");
        BuildTextureHierarchyRecursively(ResourceManager.textureDir, textureRootElement);
        textureRootElements = textureRootElement.Children.ToArray();

        HierarchyElement modelRootElement = new HierarchyElement("root");
        BuildTextureHierarchyRecursively(ResourceManager.modelDir, modelRootElement);
        modelRootElements = modelRootElement.Children.ToArray();

        HierarchyElement entityRootElement = new HierarchyElement("root");
        BuildTextureHierarchyRecursively(ResourceManager.entityDir, entityRootElement);
        entityRootElements = entityRootElement.Children.ToArray();

        MainScript.window.Update += Update;
    }

    public static void Update(double deltaTime)
    {
        AssetBrowser.ProcessPendingTextures(3);
        AssetBrowser.ProcessPendingTextures_SEARCH(3);
    }

    private static void BuildTextureHierarchyRecursively(string directoryPath, HierarchyElement parentElement)
    {
        // Get all subdirectories in the current directory
        string[] subdirectories = Directory.GetDirectories(directoryPath);

        // Iterate over each subdirectory
        foreach (string subdirectory in subdirectories)
        {
            // Create a new hierarchy element for the subdirectory
            HierarchyElement subdirectoryElement = new HierarchyElement(Path.GetFileName(subdirectory));
            subdirectoryElement.Parent = parentElement;

            // Store the directory path for lazy loading
            subdirectoryElement.DirectoryPath = subdirectory;

            // Don't load textures yet - just store path information for later
            //subdirectoryElement.texInDir = new List<(string name, IntPtr texPtr)>();

            string[] files = Directory.GetFiles(subdirectory, "*.*", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);

                subdirectoryElement.itemsInDir[fileName] = new ItemElement()
                {
                    name = fileName,
                    pathToItem = file,
                    tex = emptyTexturePreview.handle,
                };

                switch (tabs[selectedTab])
                {
                    case "Textures":
                        if (!textureItemElementsAll.ContainsKey(fileName))
                        {
                            textureItemElementsAll[fileName] = subdirectoryElement.itemsInDir[fileName];
                        }
                        break;
                    case "Models":
                        if (!modelItemElementsAll.ContainsKey(fileName))
                        {
                            modelItemElementsAll[fileName] = subdirectoryElement.itemsInDir[fileName];
                        }
                        break;
                    case "Entities":
                        if (!entityItemElementsAll.ContainsKey(fileName))
                        {
                            entityItemElementsAll[fileName] = subdirectoryElement.itemsInDir[fileName];
                        }
                        break;
                    
                }

                
            }

            // Add the subdirectory element to the parent's children
            parentElement.Children.Add(subdirectoryElement);

            // Recursively build the hierarchy for the subdirectory
            BuildTextureHierarchyRecursively(subdirectory, subdirectoryElement);
        }
    }

    public static void UpdateFilteredAssets(HierarchyElement selected)
    {
        if (selected == null) return;

        lock (_textureLock)
        {
            _pendingTextureLoads.Clear();
        }

        searchText = "";
        searchResults.Clear();

        elementSelected = selected;
        Console.WriteLine("Selected: " + selected.Name);



        TaskRunLoadTexture();

        
        
    }

    public async static void UpdateSearch()
    {
        //Console.WriteLine("New Search : " + searchText);

        searchResults.Clear();

        if (string.IsNullOrWhiteSpace(searchText))
        {
            if (elementSelected != null)
            {
                Console.WriteLine("Search cleared, reverting to selected element: " + elementSelected.Name);
            }
            return;
        }

        List<string> dictKeys = new List<string>();
        switch (tabs[selectedTab])
        {
            case "Textures":
                dictKeys = textureItemElementsAll.Select(item => item.Key).ToList();
                break;
            case "Models":
                dictKeys = modelItemElementsAll.Select(item => item.Key).ToList();
                break;
            case "Entities":
                dictKeys = entityItemElementsAll.Select(item => item.Key).ToList();
                break;
            default:
                return;
        }


        List<int> indexes = await SearchIndexesAsync(dictKeys, searchText);
        foreach (int index in indexes)
        {
            ItemElement originalItem = null;
            switch (tabs[selectedTab])
            {
                case "Textures":
                    originalItem = textureItemElementsAll[dictKeys[index]];
                    break;
                case "Models":
                    originalItem = modelItemElementsAll[dictKeys[index]];
                    break;
                case "Entities":
                    originalItem = entityItemElementsAll[dictKeys[index]];
                    break;
                default:
                    continue;
            }

            searchResults.Add(originalItem.name, new ItemElement()
            {
                name = originalItem.name,
                pathToItem = originalItem.pathToItem,
                tex = originalItem.tex
            });
        }

        TaskRunLoadTexture_SEARCH();
    }

    private static async Task<List<int>> SearchIndexesAsync(List<string> list, string searchTerm)
    {
        return await Task.Run(() =>
        {
            object lockObj = new();
            List<int> results = new();

            Parallel.For(0, list.Count, i =>
            {
                if (list[i]?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true)
                {
                    lock (lockObj)
                    {
                        results.Add(i);
                    }
                }
            });

            return results;
        });
    }


    private static async void TaskRunLoadTexture_SEARCH()
    {
        await Task.Run(() =>
        {
            ThreadLoadTexture_SEARCH();
        });
    }

    private static async void TaskRunLoadTexture()
    {
        await Task.Run(() =>
        {
            ThreadLoadTexture();
        });
    }


    private static async void ThreadLoadTexture_SEARCH()
    {
        if(searchResults.Count == 0) { return; }

        string[] imageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tga" };
        string[] allFiles = searchResults.Select(item => item.Value.pathToItem)
            .Where(file => imageExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
            .ToArray();

        int totalFiles = allFiles.Length;
        int processedCount = 0;
        int skippedCount = 0;
        int batchSize = 10;

        for (int i = 0; i < totalFiles; i += batchSize)
        {
            if (searchResults.Count == 0) { return; }

            // Take a batch of files
            var fileBatch = allFiles.Skip(i).Take(batchSize).ToArray();

            foreach (string file in fileBatch)
            {
                if (searchResults.Count == 0) { Console.WriteLine("Break, It has changed"); return; }

                string fileName = Path.GetFileNameWithoutExtension(file);

                bool isAlreadyLoaded = previewTextures.ContainsKey(file) &&
                    searchResults.ContainsKey(fileName) &&
                    searchResults[fileName].tex != emptyTexturePreview.handle;

                if (isAlreadyLoaded)
                {
                    // Skip files that have already been fully processed
                    skippedCount++;

                    if (skippedCount % 10 == 0)
                    {
                        Console.WriteLine($"Skipped {skippedCount} already loaded textures");
                    }
                    continue;
                }

                if (previewTextures.ContainsKey(file))
                {
                    if (searchResults.ContainsKey(fileName))
                    {
                        searchResults[fileName].tex = previewTextures[file].handle;
                        Console.WriteLine($"Linked existing texture: {fileName}");
                    }
                    else
                    {
                        /*
                        searchResults[fileName] = new ItemElement()
                        {
                            name = fileName,
                            pathToItem = file,
                            tex = previewTextures[file].handle,
                        };
                        Console.WriteLine($"Created link for existing texture: {fileName}");
                        */
                    }

                    continue;
                }


                try
                {
                    // Load and process the image in background thread
                    using (var img = Image.Load<Rgba32>(file))
                    {
                        int maxSize = 256;

                        int newWidth = img.Width;
                        int newHeight = img.Height;

                        if (img.Width > maxSize || img.Height > maxSize)
                        {
                            float ratio = (float)img.Width / img.Height;

                            if (ratio > 1) // Width > Height
                            {
                                newWidth = maxSize;
                                newHeight = (int)(maxSize / ratio);
                            }
                            else // Height > Width or equal
                            {
                                newHeight = maxSize;
                                newWidth = (int)(maxSize * ratio);
                            }

                            img.Mutate(x => x.Resize(newWidth, newHeight));
                        }

                        // Create a byte array from the resized image
                        byte[] imageData = new byte[newWidth * newHeight * 4];
                        img.CopyPixelDataTo(imageData);

                        string fileNameID = Path.GetFileNameWithoutExtension(file);

                        if (searchResults.Count != 0)
                        {
                            lock (_textureLock)
                            {
                                _pendingTextureLoads.Enqueue((imageData, (uint)newWidth, (uint)newHeight, file, fileNameID));
                            }
                        }

                        // Force cleanup
                        imageData = null;

                    }

                    processedCount++;
                    if (processedCount % 3 == 0)
                    {
                        Console.WriteLine($"Processed {processedCount}/{totalFiles} images");
                        await Task.Delay(1);
                        GC.Collect(); // More aggressive collection
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading texture {file}: {ex.Message}");
                }


                if (processedCount % 3 == 0)
                {
                    await Task.Delay(1);

                    if (processedCount > 50 && processedCount % 20 == 0)
                        GC.Collect();
                }
            }

            // After each batch, wait a bit and collect garbage
            await Task.Delay(5);
            if (i % (batchSize * 3) == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
    }

    public static void ProcessPendingTextures_SEARCH(int maxPerFrame = 3)
    {
        if(searchResults.Count == 0) { return; }
        int processed = 0;

        lock (_textureLock)
        {
            while (_pendingTextureLoads.Count > 0 && processed < maxPerFrame)
            {
                var (imageData, width, height, filePath, fileName) = _pendingTextureLoads.Dequeue();

                try
                {
                    // Create texture on the main thread with the preloaded data
                    Texture newTex = new Texture(MainScript.Gl, imageData, width, height);
                    previewTextures[filePath] = newTex;

                    Console.WriteLine($"Created Texture: {filePath}, Handle: {newTex.handle}");

                    // Make sure we're still on the right element
                    if (searchResults.Count != 0)
                    {
                        if (!searchResults.ContainsKey(fileName))
                        {
                            /*
                            searchResults[fileName] = new ItemElement()
                            {
                                name = fileName,
                                pathToItem = filePath,
                                tex = newTex.handle,
                            };
                            */
                        }
                        else
                        {
                            searchResults[fileName].tex = newTex.handle;
                        }
                    }

                    processed++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating texture {filePath}: {ex.Message}");
                }
            }
        }
    }

    

    // Normal Loading Textures ▼▼▼▼▼▼

    private static async void ThreadLoadTexture()
    {
        HierarchyElement currentLoopingElement = elementSelected;

        string[] imageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tga" };
        string[] allFiles = Directory.GetFiles(currentLoopingElement.DirectoryPath, "*.*", SearchOption.AllDirectories)
            .Where(file => imageExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
            .ToArray();

        int totalFiles = allFiles.Length;
        int processedCount = 0;
        int skippedCount = 0;
        int batchSize = 10;

        for (int i = 0; i < totalFiles; i += batchSize)
        {
            if (elementSelected != currentLoopingElement) { return; }

            // Take a batch of files
            var fileBatch = allFiles.Skip(i).Take(batchSize).ToArray();

            foreach (string file in fileBatch)
            {
                if (elementSelected != currentLoopingElement) { Console.WriteLine("Break, It has changed"); return; }

                string fileName = Path.GetFileNameWithoutExtension(file);

                bool isAlreadyLoaded = previewTextures.ContainsKey(file) &&
                    elementSelected.itemsInDir.ContainsKey(fileName) &&
                    elementSelected.itemsInDir[fileName].tex != emptyTexturePreview.handle;

                if (isAlreadyLoaded)
                {
                    // Skip files that have already been fully processed
                    skippedCount++;

                    if (skippedCount % 10 == 0)
                    {
                        Console.WriteLine($"Skipped {skippedCount} already loaded textures");
                    }
                    continue;
                }

                if (previewTextures.ContainsKey(file))
                {
                    if (elementSelected.itemsInDir.ContainsKey(fileName))
                    {
                        elementSelected.itemsInDir[fileName].tex = previewTextures[file].handle;
                        Console.WriteLine($"Linked existing texture: {fileName}");
                    }
                    else
                    {
                        /*
                        elementSelected.itemsInDir[fileName] = new ItemElement()
                        {
                            name = fileName,
                            pathToItem = file,
                            tex = previewTextures[file].handle,
                        };
                        Console.WriteLine($"Created link for existing texture: {fileName}");
                        */
                    }

                    continue;
                }


                try
                {
                    // Load and process the image in background thread
                    using (var img = Image.Load<Rgba32>(file))
                    {
                        int maxSize = 256;

                        int newWidth = img.Width;
                        int newHeight = img.Height;

                        if (img.Width > maxSize || img.Height > maxSize)
                        {
                            float ratio = (float)img.Width / img.Height;

                            if (ratio > 1) // Width > Height
                            {
                                newWidth = maxSize;
                                newHeight = (int)(maxSize / ratio);
                            }
                            else // Height > Width or equal
                            {
                                newHeight = maxSize;
                                newWidth = (int)(maxSize * ratio);
                            }

                            img.Mutate(x => x.Resize(newWidth, newHeight));
                        }

                        // Create a byte array from the resized image
                        byte[] imageData = new byte[newWidth * newHeight * 4];
                        img.CopyPixelDataTo(imageData);

                        string fileNameID = Path.GetFileNameWithoutExtension(file);

                        if (elementSelected == currentLoopingElement)
                        {
                            lock (_textureLock)
                            {
                                _pendingTextureLoads.Enqueue((imageData, (uint)newWidth, (uint)newHeight, file, fileNameID));
                            }
                        }

                        // Force cleanup
                        imageData = null;

                    }

                    processedCount++;
                    if (processedCount % 3 == 0)
                    {
                        Console.WriteLine($"Processed {processedCount}/{totalFiles} images");
                        await Task.Delay(1);
                        GC.Collect(); // More aggressive collection
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading texture {file}: {ex.Message}");
                }


                if (processedCount % 3 == 0)
                {
                    await Task.Delay(1);

                    if (processedCount > 50 && processedCount % 20 == 0)
                        GC.Collect();
                }
            }

            // After each batch, wait a bit and collect garbage
            await Task.Delay(5);
            if (i % (batchSize * 3) == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
    }

    public static void ProcessPendingTextures(int maxPerFrame = 3)
    {
        int processed = 0;
        HierarchyElement currentElementSnapshot = elementSelected;

        lock (_textureLock)
        {
            while (_pendingTextureLoads.Count > 0 && processed < maxPerFrame)
            {
                var (imageData, width, height, filePath, fileName) = _pendingTextureLoads.Dequeue();

                try
                {
                    // Create texture on the main thread with the preloaded data
                    Texture newTex = new Texture(MainScript.Gl, imageData, width, height);
                    previewTextures[filePath] = newTex;

                    Console.WriteLine($"Created Texture: {filePath}, Handle: {newTex.handle}");

                    // Make sure we're still on the right element
                    if (elementSelected != null && elementSelected == currentElementSnapshot)
                    {
                        if (!elementSelected.itemsInDir.ContainsKey(fileName))
                        {
                            /*
                            elementSelected.itemsInDir[fileName] = new ItemElement()
                            {
                                name = fileName,
                                pathToItem = filePath,
                                tex = newTex.handle,
                            };
                            */
                        }
                        else
                        {
                            elementSelected.itemsInDir[fileName].tex = newTex.handle;
                        }
                    }

                    processed++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating texture {filePath}: {ex.Message}");
                }
            }
        }
    }

    public static void DeselectAllExcept(List<HierarchyElement> roots, HierarchyElement exception)
    {
        if (roots == null) return;

        foreach (var root in roots)
        {
            DeselectElementAndChildren(root, exception);
        }
    }
    
    public static void DeselectElementAndChildren(HierarchyElement element, HierarchyElement exception)
    {
        if (element == null) return;
        
        if (element != exception)
            element.IsExpanded = false;
            
        foreach (var child in element.Children)
        {
            DeselectElementAndChildren(child, exception);
        }
    }

    public class ItemElement
    {
        public string name;
        public string pathToItem;

        public IntPtr tex;
    }

    public class HierarchyElement
    {
        public string Name;
        public string DirectoryPath; // Store the directory path
        public Dictionary<string, ItemElement> itemsInDir;
        

        public bool IsExpanded;
        public List<HierarchyElement> Children;
        public HierarchyElement Parent;

        public HierarchyElement(string name)
        {
            Name = name;
            IsExpanded = false;
            Children = new List<HierarchyElement>();
            itemsInDir = new Dictionary<string, ItemElement>();
        }
    }
}