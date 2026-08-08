using System.Text;
using System.Text.RegularExpressions;

namespace ERP.AI.Knowledge.Services;

public interface IDocumentTextNormalizer
{
    string Normalize(string text);
}

public class DocumentTextNormalizer : IDocumentTextNormalizer
{
    private static readonly Regex ExcessiveNewlinesRegex = new Regex(@"\n{3,}", RegexOptions.Compiled);
    private static readonly Regex MultipleSpacesRegex = new Regex(@"[ \t]{2,}", RegexOptions.Compiled);

    public string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            // Filter null characters and control characters except \r, \n, \t
            if (c == '\0' || (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t'))
            {
                continue;
            }
            sb.Append(c);
        }

        var cleaned = sb.ToString();

        // Normalize line endings to \n
        cleaned = cleaned.Replace("\r\n", "\n").Replace("\r", "\n");

        // Trim trailing spaces per line
        var lines = cleaned.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = MultipleSpacesRegex.Replace(lines[i].TrimEnd(), " ");
        }

        var result = string.Join("\n", lines);

        // Normalize 3+ newlines to double newline
        result = ExcessiveNewlinesRegex.Replace(result, "\n\n");

        return result.Trim();
    }
}
