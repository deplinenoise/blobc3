using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BlobCompiler
{
    public class Lexer : IDisposable
    {
        private TextReader m_Reader;
        private bool m_CacheValid;
        private Token m_Cache;
        private StringBuilder m_StringBuffer;
        private int m_LineNumber;
        private string m_Filename;
        private bool m_Disposed = false; // To detect redundant calls
        private List<int> m_PeekBuffer;

        public string Filename => m_Filename;

        private Location CurrentLocation()
        {
            return new Location { Filename = Filename, LineNumber = m_LineNumber };
        }

        public Lexer(TextReader reader)
            : this(reader, "<unknown file>")
        {
        }

        public Lexer(TextReader reader, string filename)
        {
            m_LineNumber = 1;
            m_Filename = filename;
            m_Reader = reader;
            m_CacheValid = false;
            m_Cache = default(Token);
            m_StringBuffer = new StringBuilder(128);

            m_PeekBuffer = new List<int>(8);
        }

        public Token Next()
        {
            if (m_CacheValid)
            {
                m_CacheValid = false;
                return m_Cache;
            }

            return ReadToken();
        }

        public Token Peek()
        {
            if (!m_CacheValid)
            {
                m_Cache = ReadToken();
                m_CacheValid = true;
            }

            return m_Cache;
        }

        private int GetChar()
        {
            if (m_PeekBuffer.Count > 0)
            {
                int index = m_PeekBuffer.Count - 1;
                int result = m_PeekBuffer[index];
                m_PeekBuffer.RemoveAt(index);
                return result;
            }

            return m_Reader.Read();
        }

        private void UnGetChar(int ch)
        {
            m_PeekBuffer.Add(ch);
        }

        private int PeekChar()
        {
            int r = GetChar();
            UnGetChar(r);
            return r;
        }

        private Token ReadToken()
        {
            SkipWhitespace();

            int ch = GetChar();
            if (ch == -1)
            {
                return new Token(TokenType.EndOfFile, CurrentLocation());
            }

            char c = (char) ch;

            if (char.IsDigit(c))
            {
                UnGetChar(ch);
                return ReadNumber();
            }

            if (char.IsLetter(c) || c == '_')
            {
                UnGetChar(ch);
                return ReadIdentifier();
            }

            if (c == '-')
            {
                int next = PeekChar();
                if (char.IsDigit((char)next))
                {
                    return ReadNegativeNumber();
                }
            }

            if (c == '"')
            {
                return ReadQuotedString();
            }

            UnGetChar(ch);
            return ReadSimpleToken();
        }

        private Token ReadNegativeNumber()
        {
            Token num = ReadNumber();
            num.IntValue = -num.IntValue;
            return num;
        }

        private LexerException MakeLexerException(string errorText)
        {
            return new LexerException(m_Filename, m_LineNumber, errorText);
        }

        private Token ReadQuotedString()
        {
            m_StringBuffer.Clear();
            bool done = false;
            while (!done)
            {
                int ch = PeekChar();
                if (-1 == ch)
                    throw MakeLexerException("end of file inside quoted string");

                char c = (char)GetChar();

                switch (c)
                {
                    case '\\':
                    {
                        int nch = PeekChar();
                        if (-1 == nch)
                            throw MakeLexerException("end of file inside quoted string");

                        char nc = (char)GetChar();

                        switch (nc)
                        {
                            case '\\':
                                m_StringBuffer.Append('\\');
                                break;
                            case 'n':
                                m_StringBuffer.Append('\n');
                                break;
                            case 'r':
                                m_StringBuffer.Append('\r');
                                break;
                            case 't':
                                m_StringBuffer.Append('\t');
                                break;
                            case '"':
                                m_StringBuffer.Append('"');
                                break;

                            default:
                                throw MakeLexerException($"unsupported escape: '{nc}'");
                        }
                    }
                    break;

                    case '\n':
                        throw MakeLexerException("newline in quoted string");

                    case '"':
                        done = true;
                        break;

                    default:
                        m_StringBuffer.Append(c);
                        break;
                }
            }

            return new Token(TokenType.QuotedString, CurrentLocation(), m_StringBuffer.ToString());
        }

        private Token ReadSimpleToken()
        {
            TokenType tt = TokenType.EndOfFile;

            char c = (char)GetChar();
            switch (c)
            {
                case '{': tt = TokenType.LeftBrace; break;
                case '}': tt = TokenType.RightBrace; break;
                case '(': tt = TokenType.LeftParen; break;
                case ')': tt = TokenType.RightParen; break;
                case ',': tt = TokenType.Comma; break;
                case ':': tt = TokenType.Colon; break;
                case ';': tt = TokenType.SemiColon; break;
                case '*': tt = TokenType.Star; break;
                case '[': tt = TokenType.LeftBracket; break;
                case ']': tt = TokenType.RightBracket; break;
                case '+': tt = TokenType.Plus; break;
                case '-': tt = TokenType.Minus; break;
                case '/': tt = TokenType.Slash; break;
                case '=': tt = TokenType.Equal; break;
                case '~': tt = TokenType.BitwiseNegate; break;
                case '<':
                case '>':
                    if (PeekChar() == c)
                    {
                        GetChar();
                        tt = c == '<' ? TokenType.LeftShift : TokenType.RightShift;
                    }
                    else
                    {
                        throw MakeLexerException($"illegal character: '{c}'");
                    }
                    break;
                default:
                    throw MakeLexerException($"illegal character: '{c}'");
            }

            return new Token(tt, CurrentLocation());
        }

        private Token ReadIdentifier()
        {
            m_StringBuffer.Clear();
            for (;;)
            {
                int ch = GetChar();
                if (-1 == ch)
                    break;

                if (char.IsLetterOrDigit((char)ch) || ch == '_')
                {
                    m_StringBuffer.Append((char)ch);
                }
                else
                {
                    UnGetChar(ch);
                    break;
                }
            }

            string s = m_StringBuffer.ToString();

            TokenType t = TokenType.Identifier;

            switch (s)
            {
                case "struct":      t = TokenType.Struct; break;
                case "include":     t = TokenType.Include; break;
                case "const":       t = TokenType.Constant; break;
                case "u8":          t = TokenType.U8; break;
                case "u16":         t = TokenType.U16; break;
                case "u32":         t = TokenType.U32; break;
                case "i8":          t = TokenType.I8; break;
                case "i16":         t = TokenType.I16; break;
                case "i32":         t = TokenType.I32; break;
                case "f32":         t = TokenType.F32; break;
                case "f64":         t = TokenType.F64; break;
                case "void":        t = TokenType.Void; break;
            }

            return new Token(t, CurrentLocation(), s);
        }

        private Token ReadNumber()
        {
            long num = 0;

            for (;;)
            {
                int ch = GetChar();
                if (-1 == ch)
                    break;

                char c = (char) ch;
                if (c >= '0' && c <= '9')
                {
                    int dig = ch - '0';
                    num *= 10;
                    num += dig;
                }
                else
                {
                    UnGetChar(ch);
                    break;
                }
            }

            return new Token(TokenType.IntegerLiteral, CurrentLocation(), num);
        }

        private void SkipWhitespace()
        {
            for (;;)
            {
                int ch = GetChar();

                if (ch == -1)
                    break;

                if (!Char.IsWhiteSpace((char)ch))
                {
                    UnGetChar(ch);
                    break;
                }

                if (ch == '\n')
                    ++m_LineNumber;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!m_Disposed)
            {
                if (disposing)
                {
                    m_Reader.Dispose();
                }

                m_Disposed = true;
            }
        }

        ~Lexer()
        {
           Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
