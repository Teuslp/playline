using System.Text;

namespace Playline.Discovery.Steam;

internal static class ValveKeyValuesParser
{
    public static ValveKeyValuesNode Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var reader = new TokenReader(Tokenize(content));
        return ParseNode(reader, expectClosingBrace: false);
    }

    private static ValveKeyValuesNode ParseNode(TokenReader reader, bool expectClosingBrace)
    {
        var node = new ValveKeyValuesNode();

        while (reader.TryRead(out var keyToken))
        {
            if (keyToken.Kind == TokenKind.CloseBrace)
            {
                if (!expectClosingBrace)
                {
                    throw new InvalidDataException("Unexpected closing brace in Valve KeyValues data.");
                }

                return node;
            }

            if (keyToken.Kind != TokenKind.Text || !reader.TryRead(out var valueToken))
            {
                throw new InvalidDataException("Invalid Valve KeyValues entry.");
            }

            if (valueToken.Kind == TokenKind.OpenBrace)
            {
                node.Children[keyToken.Value] = ParseNode(reader, expectClosingBrace: true);
            }
            else if (valueToken.Kind == TokenKind.Text)
            {
                node.Values[keyToken.Value] = valueToken.Value;
            }
            else
            {
                throw new InvalidDataException("Invalid Valve KeyValues value.");
            }
        }

        if (expectClosingBrace)
        {
            throw new InvalidDataException("Unclosed Valve KeyValues object.");
        }

        return node;
    }

    private static IEnumerable<Token> Tokenize(string content)
    {
        var index = 0;

        while (index < content.Length)
        {
            SkipWhitespaceAndComments(content, ref index);
            if (index >= content.Length)
            {
                yield break;
            }

            if (content[index] == '{')
            {
                index++;
                yield return new Token(TokenKind.OpenBrace, "{");
                continue;
            }

            if (content[index] == '}')
            {
                index++;
                yield return new Token(TokenKind.CloseBrace, "}");
                continue;
            }

            yield return new Token(TokenKind.Text, content[index] == '"'
                ? ReadQuotedString(content, ref index)
                : ReadUnquotedString(content, ref index));
        }
    }

    private static void SkipWhitespaceAndComments(string content, ref int index)
    {
        while (index < content.Length)
        {
            if (char.IsWhiteSpace(content[index]))
            {
                index++;
                continue;
            }

            if (index + 1 < content.Length && content[index] == '/' && content[index + 1] == '/')
            {
                index += 2;
                while (index < content.Length && content[index] is not '\r' and not '\n')
                {
                    index++;
                }

                continue;
            }

            break;
        }
    }

    private static string ReadQuotedString(string content, ref int index)
    {
        index++;
        var value = new StringBuilder();

        while (index < content.Length)
        {
            var character = content[index++];
            if (character == '"')
            {
                return value.ToString();
            }

            if (character == '\\' && index < content.Length)
            {
                var escaped = content[index++];
                value.Append(escaped switch
                {
                    '\\' => '\\',
                    '"' => '"',
                    'n' => '\n',
                    't' => '\t',
                    _ => $"\\{escaped}"
                });
                continue;
            }

            value.Append(character);
        }

        throw new InvalidDataException("Unclosed quoted string in Valve KeyValues data.");
    }

    private static string ReadUnquotedString(string content, ref int index)
    {
        var start = index;
        while (index < content.Length
               && !char.IsWhiteSpace(content[index])
               && content[index] is not '{' and not '}')
        {
            index++;
        }

        return content[start..index];
    }

    private enum TokenKind
    {
        Text,
        OpenBrace,
        CloseBrace
    }

    private readonly record struct Token(TokenKind Kind, string Value);

    private sealed class TokenReader(IEnumerable<Token> tokens)
    {
        private readonly IEnumerator<Token> _enumerator = tokens.GetEnumerator();

        public bool TryRead(out Token token)
        {
            if (_enumerator.MoveNext())
            {
                token = _enumerator.Current;
                return true;
            }

            token = default;
            return false;
        }
    }
}

internal sealed class ValveKeyValuesNode
{
    public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, ValveKeyValuesNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? GetValue(string key) => Values.GetValueOrDefault(key);

    public ValveKeyValuesNode? GetChild(string key) => Children.GetValueOrDefault(key);
}

