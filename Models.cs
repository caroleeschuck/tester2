using System.Text.Json;

namespace SoapTester;

public class EnvironmentConfig
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string Company { get; set; } = "";

    public string BaseUrl => Url.TrimEnd('/');
    public override string ToString() => Name;
}

public class ServiceSettings
{
    public string Transport { get; set; } = "Rest";
    public string RestPath { get; set; } = "/api/services/DWService/DWService/process";
    public string ServicePath { get; set; } = "/soap/services/DWService";
    public string Namespace { get; set; } = "http://schemas.microsoft.com/dynamics/2011/01/services";
    public string ServiceName { get; set; } = "DWWebservice";
    public string Operation { get; set; } = "process";
    public string ParameterName { get; set; } = "_request";
}

public static class Store
{
    static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SoapTester");
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static List<EnvironmentConfig> LoadEnvironments() => Load<List<EnvironmentConfig>>("environments.json") ?? new();
    public static void SaveEnvironments(List<EnvironmentConfig> envs) => Save("environments.json", envs);
    public static ServiceSettings LoadSettings()
    {
        var s = Load<ServiceSettings>("settings.json");
        if (s == null) { s = new ServiceSettings(); Save("settings.json", s); }
        return s;
    }

    static T? Load<T>(string file)
    {
        try
        {
            var p = Path.Combine(Dir, file);
            return File.Exists(p) ? JsonSerializer.Deserialize<T>(File.ReadAllText(p)) : default;
        }
        catch { return default; }
    }

    static void Save<T>(string file, T value)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Path.Combine(Dir, file), JsonSerializer.Serialize(value, Json));
    }
}

public class History<T>
{
    readonly List<T> items = new();
    public int Index { get; private set; } = -1;
    public int Count => items.Count;
    public T? Current => Index >= 0 ? items[Index] : default;
    public bool CanBack => Index > 0;
    public bool CanForward => Index < items.Count - 1;

    public void Add(T item) { items.Add(item); Index = items.Count - 1; }
    public T? Back() { if (CanBack) Index--; return Current; }
    public T? Forward() { if (CanForward) Index++; return Current; }
    public void MoveTo(int i) { if (i >= 0 && i < items.Count) Index = i; }
}
