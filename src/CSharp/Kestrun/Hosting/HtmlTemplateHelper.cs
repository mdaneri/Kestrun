using System.Text;

namespace Kestrun.Hosting;
public static class HtmlTemplateHelper
{
    /// <summary>
    /// Scans the template once and replaces each {{Key}} with vars[Key].
    /// </summary>
    public static string RenderInlineTemplate(string template, IReadOnlyDictionary<string, object?> vars)
    {
        var sb = new StringBuilder(template.Length);
        for (int i = 0; i < template.Length; i++)
        {
            // look for opening {{
            if (template[i] == '{' && i + 1 < template.Length && template[i + 1] == '{')
            {
                int start = i + 2;
                // find the matching }}
                int end = template.IndexOf("}}", start, StringComparison.Ordinal);
                if (end > start)
                {
                    var key = template.Substring(start, end - start).Trim();
                    if (vars.TryGetValue(key, out var val) && val is not null)
                        sb.Append(val);
                    // jump past the }}
                    i = end + 1;
                    continue;
                }
            }

            // normal character
            sb.Append(template[i]);
        }

        return sb.ToString();
    }
}
