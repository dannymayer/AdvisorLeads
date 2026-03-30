namespace AdvisorLeads;

internal static class AppSettings
{
    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AdvisorLeads", "settings.txt");

    internal static void Save(string key, string value)
    {
        try
        {
            var lines = File.Exists(SettingsPath)
                ? File.ReadAllLines(SettingsPath).ToList()
                : new List<string>();

            var existing = lines.FindIndex(l => l.StartsWith(key + "="));
            var newLine = $"{key}={value}";
            if (existing >= 0) lines[existing] = newLine;
            else lines.Add(newLine);

            File.WriteAllLines(SettingsPath, lines);
        }
        catch { /* best effort */ }
    }

    internal static string? Load(string key)
    {
        try
        {
            if (!File.Exists(SettingsPath)) return null;
            var line = File.ReadAllLines(SettingsPath)
                .FirstOrDefault(l => l.StartsWith(key + "="));
            return line?.Substring(key.Length + 1);
        }
        catch { return null; }
    }
}
