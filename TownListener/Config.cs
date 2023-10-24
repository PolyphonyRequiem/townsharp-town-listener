using System.Text.Json;

public class Config
{
    public static Config Current
    {
        get
        {
            if (current == null)
            {
                Load();
            }

            return current!;
        }

        private set
        {
            current = value;
        }
    }

    static Config? current;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string GrammarFilePath { get; set; } = "grammar.xml";

    public float? OverrideConfidence { get; set; }

    public bool ConsoleMode { get; set; }

    public string Language { get; set; } = "en-US";

    public string AliasFilePath { get; set; } = String.Empty;

    const string ConfigFilePath = "config.json";

    public static void Load()
    {
        if (!File.Exists(ConfigFilePath))
        {
            Current = new Config();
            Current.Save();
            return;
        }

        Current = JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigFilePath)) ?? throw new InvalidOperationException("The configuration file was in the wrong format.");
    }

    public void Save()
    {
        File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}