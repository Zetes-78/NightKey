using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NightKey.Core;

namespace NightKey.Services;

public sealed class AppSettings
{
    public bool RequireHello { get; set; } = true;

    [JsonIgnore] public bool IsNew { get; private set; }

    private static string FilePath => Path.Combine(Vault.Folder, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonDefaults.Options) ?? new AppSettings();
        }
        catch (JsonException) { }
        catch (IOException) { }
        return new AppSettings { IsNew = true };
    }

    public void Save()
    {
        Directory.CreateDirectory(Vault.Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonDefaults.Indented));
        IsNew = false;
    }
}
