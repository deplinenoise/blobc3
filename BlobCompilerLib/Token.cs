using System;
using System.Text;

namespace BlobCompiler
{
    public struct Location
    {
        public string Filename;
        public int LineNumber;

        public override string ToString()
        {
            return $"{Filename}({LineNumber})";
        }
    }

    public struct Token
    {
        public TokenType Type;
        public string StringValue;
        public long IntValue;
        public Location Location;

        public Token(TokenType type, Location loc)
        {
            Type = type;
            Location = loc;
            StringValue = null;
            IntValue = Int64.MinValue;
        }

        public Token(TokenType type, Location loc, string value)
        {
            Type = type;
            Location = loc;
            StringValue = value;
            IntValue = Int64.MinValue;
        }

        public Token(TokenType type, Location loc, long value)
        {
            Type = type;
            Location = loc;
            StringValue = null;
            IntValue = value;
        }

        public override string ToString()
        {
            return $"Location=({Location}) Type={Type} Str=\"{(StringValue != null ? StringValue : "")}\" Int={IntValue}";
        }

        public string SummaryWithoutLocation()
        {
            var buf = new StringBuilder(128);
            buf.Append(Type).Append(' ');
            if (StringValue != null)
                buf.Append("(\"").Append(StringValue).Append("\")");
            else if (IntValue != Int64.MinValue)
                buf.Append('(').Append(IntValue).Append(')');
            return buf.ToString();
        }
    }
}