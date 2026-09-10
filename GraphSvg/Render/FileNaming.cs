using System.Text;

namespace GraphSvg.Render;

/// <summary>Превращает подпись узла в безопасное имя файла.</summary>
public static class FileNaming
{
    private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>Пробелы сохраняются, запрещённые символы заменяются подчёркиванием.</summary>
    public static string Sanitize(string name, string fallback = "node")
    {
        if (string.IsNullOrWhiteSpace(name)) name = fallback;

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);

        foreach (var ch in name.Replace('\n', ' ').Replace('\r', ' '))
            sb.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);

        // Точки и пробелы в конце имени Windows молча отбрасывает — убираем сами.
        var result = sb.ToString().Trim().TrimEnd('.', ' ');

        if (result.Length == 0) result = fallback;
        if (ReservedWindowsNames.Contains(result)) result = "_" + result;

        // Запас под расширение и суффикс уникальности.
        return result.Length > 120 ? result[..120].TrimEnd('.', ' ') : result;
    }

    /// <summary>Добавляет « (2)», « (3)»… если такое имя уже занято в этом запуске.</summary>
    public static string MakeUnique(string baseName, ISet<string> used)
    {
        var candidate = baseName;
        var suffix = 2;

        while (!used.Add(candidate.ToLowerInvariant()))
        {
            candidate = $"{baseName} ({suffix})";
            suffix++;
        }

        return candidate;
    }
}