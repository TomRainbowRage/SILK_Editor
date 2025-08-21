
public class ResourceManager
{
    public static Dictionary<string, Texture> textureCache = new Dictionary<string, Texture>();
    private static Dictionary<string, Shader> shaderCache = new Dictionary<string, Shader>(); 

    public static string textureDir = "";
    public static string modelDir = "";
    public static string entityDir = "";
    private static string shaderDir = "";
    private static string mapDir = "";

    public static void Init()
    {
        Console.WriteLine(AppDomain.CurrentDomain.BaseDirectory);
        textureDir = AppDomain.CurrentDomain.BaseDirectory + @"textures\";
        modelDir = AppDomain.CurrentDomain.BaseDirectory + @"models\";
        entityDir = AppDomain.CurrentDomain.BaseDirectory + @"entities\";

        shaderDir = AppDomain.CurrentDomain.BaseDirectory + @"shaders\";
        mapDir = AppDomain.CurrentDomain.BaseDirectory + @"maps\";
        

        /*
        foreach (string path in Directory.GetFiles(textureDir, "*.*", SearchOption.AllDirectories))
        {
            string correctPath = path.Replace(AppDomain.CurrentDomain.BaseDirectory, "");
            correctPath = correctPath.Replace(Path.DirectorySeparatorChar, '/');

            if (correctPath.Contains(".png")) { correctPath = correctPath.Replace(".png", ""); }
            else if (correctPath.Contains(".jpg")) { correctPath = correctPath.Replace(".jpg", ""); }
            else { Console.WriteLine("Couldnt get correctPath for |" + path + "|"); continue; }

            correctPath = correctPath.Replace("textures/", "");







            Texture newTex = new Texture(MainScript.Gl, path);
            textureCache.Add(path, newTex);
        }
        */
    }

    // dev/dev_light_checker -> C:/Users/tomra/Documents/RaylibProjects/FPS GAMES/FPS BOTH SILK/dev/dev_light_checker.png
    public static Texture GetTexture(string path)
    {
        if (textureCache.TryGetValue(path, out Texture tex)) { return tex; }
        else
        {
            string extImage = "";

            if(Path.Exists(textureDir + path.Replace('/', Path.DirectorySeparatorChar) + ".png")) { extImage = ".png"; }
            else if(Path.Exists(textureDir + path.Replace('/', Path.DirectorySeparatorChar) + ".jpg")) { extImage = ".jpg"; }

            if(extImage == "") { throw new Exception("Image has Wrong extension :: " + textureDir + path.Replace('/', Path.DirectorySeparatorChar)); }

            Texture newTex = new Texture(MainScript.Gl, textureDir + path.Replace('/', Path.DirectorySeparatorChar) + extImage);
            textureCache.Add(path, newTex);
            return newTex;
        }
    }

    public static Shader GetShader(string path)
    {
        if (shaderCache.TryGetValue(path, out Shader shader)) { return shader; }
        else
        {
            string shaderPath = shaderDir + path.Replace('/', Path.DirectorySeparatorChar);
            Shader newShader = new Shader(MainScript.Gl, shaderPath + ".vert", shaderPath + ".frag");

            shaderCache.Add(path, newShader);
            return newShader;
        }
    }

    public static string GetMap(string path)
    {
        return mapDir + path.Replace('/', Path.DirectorySeparatorChar) + ".rmap";
    }

    public static void DisposeTexture(string path)
    {
        if (textureCache.TryGetValue(path, out Texture tex))
        {
            tex.Dispose(); // Dispose of the object
            textureCache.Remove(path); // Remove from dictionary
        }
    }

    public static void DisposeShader(string path)
    {
        if (shaderCache.TryGetValue(path, out Shader shader))
        {
            shader.Dispose(); // Dispose of the object
            shaderCache.Remove(path); // Remove from dictionary
        }
    }
}