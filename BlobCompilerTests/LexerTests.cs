using System;
using System.IO;
using BlobCompiler;
using NUnit.Framework;

namespace BlobCompilerTests
{
    [TestFixture]
    public partial class LexerTests
    {
        [Test]
        public void TestEmptyInput()
        {
            var lexer = new Lexer(new StringReader(""));
            var token = lexer.Next();
            Assert.AreEqual(TokenType.EndOfFile, token.Type);
        }

        [Test]
        public void TestIllegalInput()
        {
            var lexer = new Lexer(new StringReader("?"), "somefile");
            var ex = Assert.Throws<LexerException>(() => lexer.Next());
            Assert.IsTrue(ex.Message.Contains("illegal character"));
            Assert.AreEqual(1, ex.LineNumber);
            Assert.AreEqual("somefile", ex.Filename);
        }

        [Test]
        public void TestLineNumbers()
        {
            var lexer = new Lexer(new StringReader("foo\nbar\n"), "filename");
            var token = lexer.Next();
            Assert.AreEqual(1, token.Location.LineNumber);
            Assert.AreEqual("filename", token.Location.Filename);
            token = lexer.Next();
            Assert.AreEqual(2, token.Location.LineNumber);
            Assert.AreEqual("filename", token.Location.Filename);
            token = lexer.Next();
            Assert.AreEqual(TokenType.EndOfFile, token.Type);
        }

        [Test]
        public void TestLineNumbersInExceptions()
        {
            var lexer = new Lexer(new StringReader("foo\n\0\n"));
            var token = lexer.Next();
            Assert.AreEqual(1, token.Location.LineNumber);
            var ex = Assert.Throws<LexerException>(() => { lexer.Next(); });
            Assert.AreEqual(2, ex.LineNumber);
        }

        [Test]
        public void TestWhitespaceOnly()
        {
            var lexer = new Lexer(new StringReader("  \n \t\n"));
            var token = lexer.Next();
            Assert.AreEqual(TokenType.EndOfFile, token.Type);
        }

        [Test]
        public void TestPositiveIntegers()
        {
            var lexer = new Lexer(new StringReader("0 1 2 3 4 5 6 7 8 9 10 981 123981029381"));
            var expected = new long[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 981, 123981029381 };

            for (int i = 0; i < expected.Length; ++i)
            {
                var token = lexer.Next();
                Assert.AreEqual(TokenType.IntegerLiteral, token.Type);
                Assert.AreEqual(expected[i], token.IntValue);
            }

            var endToken = lexer.Next();
            Assert.AreEqual(TokenType.EndOfFile, endToken.Type);
        }

        [Test]
        public void TestNegativeIntegers()
        {
            var lexer = new Lexer(new StringReader("-0 -1 -2 -3 -4 -5 -6 -7 -8 -9 -10 -981 -123981029381"));
            var expected = new long[] { 0, -1, -2, -3, -4, -5, -6, -7, -8, -9, -10, -981, -123981029381 };

            for (int i = 0; i < expected.Length; ++i)
            {
                var token = lexer.Next();
                Assert.AreEqual(TokenType.IntegerLiteral, token.Type);
                Assert.AreEqual(expected[i], token.IntValue);
            }

            var endToken = lexer.Next();
            Assert.AreEqual(TokenType.EndOfFile, endToken.Type);
        }

        [Test]
        public void TestAllSimpleTokens()
        {
            var lexer = new Lexer(new StringReader("(){},:;*[]<<>>+-/="));
            var expected = new TokenType[] {
                TokenType.LeftParen,
                TokenType.RightParen,
                TokenType.LeftBrace,
                TokenType.RightBrace,
                TokenType.Comma,
                TokenType.Colon,
                TokenType.SemiColon,
                TokenType.Star,
                TokenType.LeftBracket,
                TokenType.RightBracket,
                TokenType.LeftShift,
                TokenType.RightShift,
                TokenType.Plus,
                TokenType.Minus,
                TokenType.Slash,
                TokenType.Equal,
                TokenType.EndOfFile
            };
            for (int i = 0; i < expected.Length; ++i)
            {
                var token = lexer.Next();
                Assert.AreEqual(expected[i], token.Type);
                Assert.AreEqual(1, token.Location.LineNumber);
            }
        }

        [Test]
        public void TestNoLt()
        {
            var lexer = new Lexer(new StringReader("<foo"));
            Assert.Throws<LexerException>(() => lexer.Next());
        }

        [Test]
        public void TestNoGt()
        {
            var lexer = new Lexer(new StringReader(">foo"));
            Assert.Throws<LexerException>(() => lexer.Next());
        }

        [Test]
        public void TestQuotedStrings()
        {
            var lexer = new Lexer(new StringReader(" \"foo\" \"foo bar\" \"foo\\\"with\\\"escape\" \"a\\nb\" \"a\\\\b\" \"a\\rb\" \"a\\tb\" "));
            var expected = new string[] {
                "foo",
                "foo bar",
                "foo\"with\"escape",
                "a\nb",
                "a\\b",
                "a\rb",
                "a\tb",
            };
            for (int i = 0; i < expected.Length; ++i)
            {
                var token = lexer.Next();
                Assert.AreEqual(TokenType.QuotedString, token.Type);
                Assert.AreEqual(expected[i], token.StringValue);
                Assert.AreEqual(1, token.Location.LineNumber);
            }
        }

        [Test]
        public void TestIdentifiers()
        {
            var lexer = new Lexer(new StringReader("f foo foo_bar f0123_4567"));
            var expected = new string[] {
                "f",
                "foo",
                "foo_bar",
                "f0123_4567",
            };
            for (int i = 0; i < expected.Length; ++i)
            {
                var token = lexer.Next();
                Assert.AreEqual(TokenType.Identifier, token.Type);
                Assert.AreEqual(expected[i], token.StringValue);
            }
        }

        [Test]
        public void TestKeywords()
        {
            var lexer = new Lexer(new StringReader("include struct const u8 u16 u32 i8 i16 i32 f32 f64 void"));
            var expected = new TokenType[] {
                TokenType.Include,
                TokenType.Struct,
                TokenType.Constant,
                TokenType.U8,
                TokenType.U16,
                TokenType.U32,
                TokenType.I8,
                TokenType.I16,
                TokenType.I32,
                TokenType.F32,
                TokenType.F64,
                TokenType.Void,
                TokenType.EndOfFile,
            };
            for (int i = 0; i < expected.Length; ++i)
            {
                var token = lexer.Next();
                Assert.AreEqual(expected[i], token.Type);
            }
        }

        [Test]
        public void TestEndOfFileInString()
        {
            var lexer = new Lexer(new StringReader("\"foo"));
            var ex = Assert.Throws<LexerException>(() => lexer.Next());
            Assert.IsTrue(ex.Message.Contains("end of file inside quoted string"));
        }

        [Test]
        public void TestEndOfFileInStringWithTrailingEscape()
        {
            var lexer = new Lexer(new StringReader("\"foo\\"));
            var ex = Assert.Throws<LexerException>(() => lexer.Next());
            Assert.IsTrue(ex.Message.Contains("end of file inside quoted string"));
        }

        [Test]
        public void TestUnsupportedEscapeThrows()
        {
            var lexer = new Lexer(new StringReader("\"f\\oo\""));
            var ex = Assert.Throws<LexerException>(() => lexer.Next());
            Assert.IsTrue(ex.Message.Contains("unsupported escape"));
        }

        [Test]
        public void TestNewlineInStringThrows()
        {
            var lexer = new Lexer(new StringReader("\"f\no\""));
            var ex = Assert.Throws<LexerException>(() => lexer.Next());
            Assert.IsTrue(ex.Message.Contains("newline"));
        }

        [Test]
        public void TestComments()
        {
            var lexer = new Lexer(new StringReader("//foo\nbar"));
            var t = lexer.Next();
            Assert.AreEqual(TokenType.Identifier, t.Type);
            Assert.AreEqual("bar", t.StringValue);
            t = lexer.Next();
            Assert.AreEqual(TokenType.EndOfFile, t.Type);
        }

        [Test]
        public void TokenStringify()
        {
            var st = new Token(TokenType.Identifier, new Location { Filename = "bar", LineNumber = 123 }, "String");

            Assert.AreEqual($"Location=(bar(123)) Type=Identifier Str=\"String\" Int={st.IntValue}", st.ToString());
        }

        [Test]
        public void TokenStringify2()
        {
            var st = new Token(TokenType.Identifier, new Location { Filename = "bar", LineNumber = 123 }, 12345);

            Assert.AreEqual($"Location=(bar(123)) Type=Identifier Str=\"\" Int={st.IntValue}", st.ToString());
        }

        [Test]
        public void TokenStringify3()
        {
            var st = new Token(TokenType.Identifier, new Location { Filename = "bar", LineNumber = 123 }, "String");

            Assert.AreEqual($"Identifier (\"String\")", st.SummaryWithoutLocation());
        }

        [Test]
        public void TokenStringify4()
        {
            var st = new Token(TokenType.Identifier, new Location { Filename = "bar", LineNumber = 123 }, 123);

            Assert.AreEqual($"Identifier (123)", st.SummaryWithoutLocation());
        }

    }
}
