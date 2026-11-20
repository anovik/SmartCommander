using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SmartCommander.Plugins;

// What a detect string is evaluated against: one file about to be viewed.
// Header holds the first bytes of the file (the registry reads up to HeaderSize).
internal sealed record DetectContext(string Extension, long Size, byte[] Header, bool Force = false)
{
    public const int HeaderSize = 8192;

    // EXT is the uppercase extension without the dot, as Total Commander passes it.
    public static DetectContext ForFile(string path, long size, byte[] header)
    {
        string ext = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        return new DetectContext(ext, size, header);
    }
}

// A WLX plugin's detect string (ListGetDetectString), parsed once and evaluated per file.
// Grammar, loosest-binding first:
//   or   := and ('|' and)*
//   and  := cmp ('&' cmp)*
//   cmp  := term (('=' | '!=' | '<' | '>') term)?
//   term := '!' '(' or ')' | '(' or ')' | primary
//   primary := EXT | SIZE | FORCE | MULTIMEDIA | [n] | number | "string"
//            | FIND("string") | FINDI("string")
// '!' only directly before a bracket, as the SDK says: accepting `!EXT="TXT"` would bind '!' to EXT
// alone and silently mean something else. Keywords are matched case-insensitively.
// Known limit: the detect string is decoded as Latin-1, so EXT="..." with characters outside
// Latin-1 (e.g. Cyrillic extensions) never matches.
internal sealed class DetectString
{
    private readonly Node _root;

    private DetectString(Node root)
    {
        _root = root;
    }

    // Throws FormatException for a malformed string.
    public static DetectString Parse(string text)
    {
        var parser = new Parser(Tokenize(text));
        Node root = parser.ParseOr();
        parser.ExpectEnd();
        return new DetectString(root);
    }

    public bool Matches(DetectContext context)
    {
        return _root.Evaluate(context).IsTrue;
    }

    // ---- values ----

    private enum ValueKind { Number, String, Missing }

    // Missing is a header byte beyond the bytes read: any comparison with it fails.
    private readonly record struct Value(ValueKind Kind, long Number, string Text)
    {
        public static Value Num(long n) => new(ValueKind.Number, n, "");
        public static Value Str(string s) => new(ValueKind.String, 0, s);
        public static Value Bool(bool b) => Num(b ? 1 : 0);
        public static readonly Value Missing = new(ValueKind.Missing, 0, "");

        public bool IsTrue => Kind switch
        {
            ValueKind.Number => Number != 0,
            ValueKind.String => Text.Length > 0,
            _ => false,
        };
    }

    // ---- AST ----

    private abstract class Node
    {
        public abstract Value Evaluate(DetectContext c);
    }

    private sealed class Constant(Value value) : Node
    {
        public override Value Evaluate(DetectContext c) => value;
    }

    private sealed class Variable(string name) : Node
    {
        public override Value Evaluate(DetectContext c) => name switch
        {
            "EXT" => Value.Str(c.Extension),
            "SIZE" => Value.Num(c.Size),
            "FORCE" => Value.Bool(c.Force),
            "MULTIMEDIA" => Value.Num(1),
            _ => throw new InvalidOperationException(name),
        };
    }

    private sealed class HeaderByte(int offset) : Node
    {
        public override Value Evaluate(DetectContext c) =>
            offset < c.Header.Length ? Value.Num(c.Header[offset]) : Value.Missing;
    }

    private sealed class Find(byte[] needle, bool ignoreCase) : Node
    {
        public override Value Evaluate(DetectContext c) => Value.Bool(Contains(c.Header, needle, ignoreCase));

        private static bool Contains(byte[] haystack, byte[] needle, bool ignoreCase)
        {
            if (!ignoreCase)
            {
                return haystack.AsSpan().IndexOf(needle) >= 0;
            }
            for (int i = 0; i + needle.Length <= haystack.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && FoldAscii(haystack[i + j]) == FoldAscii(needle[j]))
                {
                    j++;
                }
                if (j == needle.Length)
                {
                    return true;
                }
            }
            return false;
        }

        private static byte FoldAscii(byte b) => b is >= (byte)'a' and <= (byte)'z' ? (byte)(b - 32) : b;
    }

    private sealed class Not(Node operand) : Node
    {
        public override Value Evaluate(DetectContext c) => Value.Bool(!operand.Evaluate(c).IsTrue);
    }

    private sealed class And(Node left, Node right) : Node
    {
        public override Value Evaluate(DetectContext c) =>
            Value.Bool(left.Evaluate(c).IsTrue && right.Evaluate(c).IsTrue);
    }

    private sealed class Or(Node left, Node right) : Node
    {
        public override Value Evaluate(DetectContext c) =>
            Value.Bool(left.Evaluate(c).IsTrue || right.Evaluate(c).IsTrue);
    }

    private sealed class Compare(TokenKind op, Node left, Node right) : Node
    {
        public override Value Evaluate(DetectContext c) => Value.Bool(Apply(op, left.Evaluate(c), right.Evaluate(c)));

        private static bool Apply(TokenKind op, Value a, Value b)
        {
            if (a.Kind == ValueKind.Missing || b.Kind == ValueKind.Missing)
            {
                return false;
            }
            // A one-character string next to a number compares as its character code: [0]="P".
            a = AsCharCode(a, b);
            b = AsCharCode(b, a);

            if (a.Kind == ValueKind.Number && b.Kind == ValueKind.Number)
            {
                return op switch
                {
                    TokenKind.Eq => a.Number == b.Number,
                    TokenKind.Ne => a.Number != b.Number,
                    TokenKind.Lt => a.Number < b.Number,
                    _ => a.Number > b.Number,
                };
            }
            if (a.Kind == ValueKind.String && b.Kind == ValueKind.String)
            {
                bool equal = string.Equals(a.Text, b.Text, StringComparison.OrdinalIgnoreCase);
                return op switch
                {
                    TokenKind.Eq => equal,
                    TokenKind.Ne => !equal,
                    _ => false,
                };
            }
            // Number vs. (longer) string: never equal, never ordered.
            return op == TokenKind.Ne;
        }

        private static Value AsCharCode(Value v, Value other)
        {
            if (v.Kind == ValueKind.String && other.Kind == ValueKind.Number && v.Text.Length == 1 && v.Text[0] <= 0xFF)
            {
                return Value.Num(v.Text[0]);
            }
            return v;
        }
    }

    // ---- tokenizer ----

    private enum TokenKind { LParen, RParen, And, Or, Not, Eq, Ne, Lt, Gt, Number, String, Ident, HeaderByte, End }

    private readonly record struct Token(TokenKind Kind, string Text, int Position);

    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < text.Length)
        {
            char ch = text[i];
            int start = i;
            if (char.IsWhiteSpace(ch))
            {
                i++;
                continue;
            }
            switch (ch)
            {
                case '(':
                    tokens.Add(new Token(TokenKind.LParen, "(", start));
                    i++;
                    break;
                case ')':
                    tokens.Add(new Token(TokenKind.RParen, ")", start));
                    i++;
                    break;
                case '&':
                    tokens.Add(new Token(TokenKind.And, "&", start));
                    i++;
                    break;
                case '|':
                    tokens.Add(new Token(TokenKind.Or, "|", start));
                    i++;
                    break;
                case '=':
                    tokens.Add(new Token(TokenKind.Eq, "=", start));
                    i++;
                    break;
                case '<':
                    tokens.Add(new Token(TokenKind.Lt, "<", start));
                    i++;
                    break;
                case '>':
                    tokens.Add(new Token(TokenKind.Gt, ">", start));
                    i++;
                    break;
                case '!':
                    if (i + 1 < text.Length && text[i + 1] == '=')
                    {
                        tokens.Add(new Token(TokenKind.Ne, "!=", start));
                        i += 2;
                    }
                    else
                    {
                        tokens.Add(new Token(TokenKind.Not, "!", start));
                        i++;
                    }
                    break;
                case '"':
                {
                    int close = text.IndexOf('"', i + 1);
                    if (close < 0)
                    {
                        throw Error("unterminated string", start);
                    }
                    tokens.Add(new Token(TokenKind.String, text.Substring(i + 1, close - i - 1), start));
                    i = close + 1;
                    break;
                }
                case '[':
                {
                    int close = text.IndexOf(']', i + 1);
                    string inner = close < 0 ? "" : text.Substring(i + 1, close - i - 1).Trim();
                    if (close < 0 || inner.Length == 0 || !IsAllDigits(inner))
                    {
                        throw Error("expected [offset]", start);
                    }
                    tokens.Add(new Token(TokenKind.HeaderByte, inner, start));
                    i = close + 1;
                    break;
                }
                default:
                    if (char.IsAsciiDigit(ch))
                    {
                        while (i < text.Length && char.IsAsciiDigit(text[i]))
                        {
                            i++;
                        }
                        tokens.Add(new Token(TokenKind.Number, text[start..i], start));
                    }
                    else if (char.IsAsciiLetter(ch))
                    {
                        while (i < text.Length && char.IsAsciiLetterOrDigit(text[i]))
                        {
                            i++;
                        }
                        tokens.Add(new Token(TokenKind.Ident, text[start..i].ToUpperInvariant(), start));
                    }
                    else
                    {
                        throw Error($"unexpected character '{ch}'", start);
                    }
                    break;
            }
        }
        tokens.Add(new Token(TokenKind.End, "", text.Length));
        return tokens;
    }

    private static bool IsAllDigits(string s)
    {
        foreach (char ch in s)
        {
            if (!char.IsAsciiDigit(ch))
            {
                return false;
            }
        }
        return true;
    }

    private static FormatException Error(string message, int position) =>
        new($"Detect string: {message} at position {position}.");

    // ---- parser ----

    private sealed class Parser(List<Token> tokens)
    {
        private int _pos;

        private Token Current => tokens[_pos];

        private Token Take()
        {
            return tokens[_pos++];
        }

        private void Expect(TokenKind kind, string what)
        {
            if (Current.Kind != kind)
            {
                throw Error($"expected {what}", Current.Position);
            }
            _pos++;
        }

        public void ExpectEnd()
        {
            Expect(TokenKind.End, "end of string");
        }

        public Node ParseOr()
        {
            Node left = ParseAnd();
            while (Current.Kind == TokenKind.Or)
            {
                _pos++;
                left = new Or(left, ParseAnd());
            }
            return left;
        }

        private Node ParseAnd()
        {
            Node left = ParseCompare();
            while (Current.Kind == TokenKind.And)
            {
                _pos++;
                left = new And(left, ParseCompare());
            }
            return left;
        }

        private Node ParseCompare()
        {
            Node left = ParseTerm();
            if (Current.Kind is TokenKind.Eq or TokenKind.Ne or TokenKind.Lt or TokenKind.Gt)
            {
                TokenKind op = Take().Kind;
                return new Compare(op, left, ParseTerm());
            }
            return left;
        }

        private Node ParseTerm()
        {
            Token t = Take();
            switch (t.Kind)
            {
                case TokenKind.Not:
                    if (Current.Kind != TokenKind.LParen)
                    {
                        throw Error("expected '(' after '!'", Current.Position);
                    }
                    return new Not(ParseTerm());
                case TokenKind.LParen:
                {
                    Node inner = ParseOr();
                    Expect(TokenKind.RParen, "')'");
                    return inner;
                }
                case TokenKind.Number:
                    if (!long.TryParse(t.Text, out long n))
                    {
                        throw Error("number too large", t.Position);
                    }
                    return new Constant(Value.Num(n));
                case TokenKind.String:
                    return new Constant(Value.Str(t.Text));
                case TokenKind.HeaderByte:
                    if (!int.TryParse(t.Text, out int offset))
                    {
                        throw Error("offset too large", t.Position);
                    }
                    return new HeaderByte(offset);
                case TokenKind.Ident:
                    return ParseIdent(t);
                default:
                    throw Error("expected a value", t.Position);
            }
        }

        private Node ParseIdent(Token t)
        {
            switch (t.Text)
            {
                case "EXT":
                case "SIZE":
                case "FORCE":
                case "MULTIMEDIA":
                    return new Variable(t.Text);
                case "FIND":
                case "FINDI":
                {
                    Expect(TokenKind.LParen, "'(' after " + t.Text);
                    Token arg = Take();
                    if (arg.Kind != TokenKind.String)
                    {
                        throw Error("expected a string argument", arg.Position);
                    }
                    Expect(TokenKind.RParen, "')'");
                    return new Find(Encoding.Latin1.GetBytes(arg.Text), ignoreCase: t.Text == "FINDI");
                }
                default:
                    throw Error($"unknown keyword '{t.Text}'", t.Position);
            }
        }
    }
}
