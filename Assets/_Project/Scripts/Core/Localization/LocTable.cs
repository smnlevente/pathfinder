using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Pathfinder.Core.Localization
{
    /// <summary>
    /// A parsed .loc file (GDD §12.5): one <c>key = value</c> per line. Lines starting with # are comments, and
    /// <c>@name = value</c> lines are metadata, such as the language's display name. Values are trimmed; wrap one in
    /// double quotes to keep leading or trailing spaces. In a value, <c>\n</c> is a line break and <c>\\</c> a
    /// backslash. A later duplicate key replaces the earlier one, with a warning.
    /// </summary>
    public sealed class LocTable
    {
        readonly Dictionary<string, string> _entries = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly Dictionary<string, string> _meta = new Dictionary<string, string>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, string> Entries => _entries;
        public IReadOnlyDictionary<string, string> Meta => _meta;
        public int Count => _entries.Count;

        public bool TryGet(string key, out string value) => _entries.TryGetValue(key, out value);

        public static LocTable Parse(string text, List<string> warnings = null)
        {
            var table = new LocTable();
            if (string.IsNullOrEmpty(text))
                return table;

            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;

                int equals = line.IndexOf('=');
                if (equals <= 0)
                {
                    warnings?.Add($"Line {i + 1}: expected 'key = value', got '{line}'.");
                    continue;
                }

                string key = line.Substring(0, equals).Trim();
                string raw = line.Substring(equals + 1).Trim();
                if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
                    raw = raw.Substring(1, raw.Length - 2);
                string value = Unescape(raw);
                var target = table._entries;
                if (key[0] == '@')
                {
                    key = key.Substring(1);
                    target = table._meta;
                }
                if (target.ContainsKey(key))
                    warnings?.Add($"Line {i + 1}: '{key}' is defined twice; the later value wins.");
                target[key] = value;
            }
            return table;
        }

        static string Unescape(string value)
        {
            if (value.IndexOf('\\') < 0)
                return value;
            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' && i + 1 < value.Length)
                {
                    char next = value[++i];
                    builder.Append(next switch { 'n' => '\n', 't' => '\t', _ => next });
                }
                else
                {
                    builder.Append(c);
                }
            }
            return builder.ToString();
        }
    }

    /// <summary>
    /// Fills named placeholders in localized text: <c>{amount}</c>, or <c>{seconds:0.0}</c> with a number format.
    /// Numbers use the invariant culture. <c>{{</c> and <c>}}</c> are literal braces. Unknown placeholders stay as they
    /// are, so a typo shows up on screen instead of crashing.
    /// </summary>
    public static class LocFormat
    {
        public static string Format(string template, IReadOnlyList<(string name, object value)> args)
        {
            if (string.IsNullOrEmpty(template) || template.IndexOf('{') < 0 && template.IndexOf('}') < 0)
                return template;

            var builder = new StringBuilder(template.Length + 16);
            for (int i = 0; i < template.Length; i++)
            {
                char c = template[i];
                if (c == '{' && i + 1 < template.Length && template[i + 1] == '{')
                {
                    builder.Append('{');
                    i++;
                    continue;
                }
                if (c == '}' && i + 1 < template.Length && template[i + 1] == '}')
                {
                    builder.Append('}');
                    i++;
                    continue;
                }
                if (c != '{')
                {
                    builder.Append(c);
                    continue;
                }

                int close = template.IndexOf('}', i + 1);
                if (close < 0)
                {
                    builder.Append(template, i, template.Length - i);
                    break;
                }

                string token = template.Substring(i + 1, close - i - 1);
                int colon = token.IndexOf(':');
                string name = colon >= 0 ? token.Substring(0, colon) : token;
                string format = colon >= 0 ? token.Substring(colon + 1) : null;
                if (TryFind(args, name, out object value))
                    builder.Append(value is IFormattable formattable ? formattable.ToString(format, CultureInfo.InvariantCulture) : value?.ToString());
                else
                    builder.Append('{').Append(token).Append('}');
                i = close;
            }
            return builder.ToString();
        }

        static bool TryFind(IReadOnlyList<(string name, object value)> args, string name, out object value)
        {
            if (args != null)
            {
                for (int i = 0; i < args.Count; i++)
                {
                    if (args[i].name == name)
                    {
                        value = args[i].value;
                        return true;
                    }
                }
            }
            value = null;
            return false;
        }
    }
}
