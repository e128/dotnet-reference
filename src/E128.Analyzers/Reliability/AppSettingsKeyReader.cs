using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Reads the key paths of an appsettings JSON document without a JSON library. An object member
///     adds <c lang="csharp">Parent:Name</c>, and an array element adds
///     <c lang="csharp">Parent:Index</c>. Values are skipped. The reader accepts comments and a
///     trailing comma, as the .NET configuration loader does, and rejects nesting past 64 levels.
/// </summary>
internal sealed class AppSettingsKeyReader
{
    private const int MaxDepth = 64;

    private readonly string json;
    private readonly ImmutableHashSet<string>.Builder keys;
    private int position;

    private AppSettingsKeyReader(string json, ImmutableHashSet<string>.Builder keys)
    {
        this.json = json;
        this.keys = keys;
    }

    /// <summary>
    ///     Adds every key path in <paramref name="json" /> to <paramref name="keys" />. Returns false
    ///     when the text is not a well-formed document.
    /// </summary>
    internal static bool TryRead(string json, ImmutableHashSet<string>.Builder keys)
    {
        var reader = new AppSettingsKeyReader(json, keys);

        if (reader.json.Length > 0 && reader.json[0] == '\uFEFF')
        {
            reader.position = 1;
        }

        if (!reader.ReadValue(string.Empty, 1))
        {
            return false;
        }

        reader.SkipTrivia();

        return reader.position == reader.json.Length;
    }

    private bool ReadValue(string prefix, int depth)
    {
        if (depth > MaxDepth)
        {
            return false;
        }

        SkipTrivia();

        return Peek() switch
        {
            '{' => ReadObject(prefix, depth),
            '[' => ReadArray(prefix, depth),
            '"' => ReadString(out _),
            _ => SkipLiteral()
        };
    }

    private bool ReadObject(string prefix, int depth)
    {
        position++;

        while (true)
        {
            SkipTrivia();

            if (Peek() == '}')
            {
                position++;
                return true;
            }

            if (!ReadMember(prefix, depth))
            {
                return false;
            }

            SkipTrivia();

            if (Peek() == ',')
            {
                position++;
            }
            else if (Peek() != '}')
            {
                return false;
            }
        }
    }

    private bool ReadMember(string prefix, int depth)
    {
        if (Peek() != '"' || !ReadString(out var name))
        {
            return false;
        }

        SkipTrivia();

        if (Peek() != ':')
        {
            return false;
        }

        position++;

        var path = prefix.Length == 0 ? name : prefix + ":" + name;
        keys.Add(path);

        return ReadValue(path, depth + 1);
    }

    private bool ReadArray(string prefix, int depth)
    {
        position++;
        var index = 0;

        while (true)
        {
            SkipTrivia();

            if (Peek() == ']')
            {
                position++;
                return true;
            }

            var path = prefix + ":" + index.ToString(CultureInfo.InvariantCulture);
            keys.Add(path);
            index++;

            if (!ReadValue(path, depth + 1))
            {
                return false;
            }

            SkipTrivia();

            if (Peek() == ',')
            {
                position++;
            }
            else if (Peek() != ']')
            {
                return false;
            }
        }
    }

    private bool ReadString(out string value)
    {
        var builder = new StringBuilder();
        value = string.Empty;
        position++;

        while (position < json.Length)
        {
            var current = json[position++];

            if (current == '"')
            {
                value = builder.ToString();
                return true;
            }

            if (current < ' ')
            {
                return false;
            }

            if (current != '\\')
            {
                builder.Append(current);
            }
            else if (!TryReadEscape(builder))
            {
                return false;
            }
        }

        return false;
    }

    private bool TryReadEscape(StringBuilder builder)
    {
        if (position >= json.Length)
        {
            return false;
        }

        var escape = json[position++];

        switch (escape)
        {
            case '"' or '\\' or '/':
                builder.Append(escape);
                return true;
            case 'b':
                builder.Append('\b');
                return true;
            case 'f':
                builder.Append('\f');
                return true;
            case 'n':
                builder.Append('\n');
                return true;
            case 'r':
                builder.Append('\r');
                return true;
            case 't':
                builder.Append('\t');
                return true;
            case 'u':
                return TryReadUnicodeEscape(builder);
            default:
                return false;
        }
    }

    private bool TryReadUnicodeEscape(StringBuilder builder)
    {
        if (position + 4 > json.Length
            || !int.TryParse(
                json.Substring(position, 4),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out var code))
        {
            return false;
        }

        position += 4;
        builder.Append((char)code);

        return true;
    }

    // A number, true, false, or null. The reader never needs the value, only the end of the token.
    private bool SkipLiteral()
    {
        var start = position;

        while (position < json.Length && !IsLiteralEnd(json[position]))
        {
            position++;
        }

        if (position == start)
        {
            return false;
        }

        var token = json.Substring(start, position - start);

        return token is "true" or "false" or "null"
               || double.TryParse(
                   token,
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out _);
    }

    private static bool IsLiteralEnd(char character)
    {
        return character is ',' or '}' or ']' or '/' || char.IsWhiteSpace(character);
    }

    // Skips whitespace, line comments, and block comments. An unterminated block comment runs to
    // the end of the text, so the caller then fails on the missing value.
    private void SkipTrivia()
    {
        while (position < json.Length)
        {
            if (char.IsWhiteSpace(json[position]))
            {
                position++;
            }
            else if (StartsWithAt("//"))
            {
                SkipLineComment();
            }
            else if (StartsWithAt("/*"))
            {
                SkipBlockComment();
            }
            else
            {
                return;
            }
        }
    }

    private void SkipLineComment()
    {
        while (position < json.Length && json[position] != '\n')
        {
            position++;
        }
    }

    private void SkipBlockComment()
    {
        var end = json.IndexOf("*/", position + 2, StringComparison.Ordinal);
        position = end < 0 ? json.Length : end + 2;
    }

    private bool StartsWithAt(string text)
    {
        return string.CompareOrdinal(json, position, text, 0, text.Length) == 0;
    }

    private char Peek()
    {
        return position < json.Length ? json[position] : '\0';
    }
}
