
using BlobCompiler;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace BlobCompilerTests
{
    [TestFixture]
    public class AsmCodeGeneratorTests : BaseParserFixture
    {
        protected static readonly Regex kCompressWhitespace = new Regex("\\s+");
        protected static readonly Regex kLeadingWhitespace = new Regex("^ ");
        protected static readonly Regex kTrailingWhitespace = new Regex(" $");
        protected static readonly Regex kComment = new Regex(";.*$");

        protected List<string> ParseAndGenerate(string fn, string includePrefix = "")
        {
            var result = Parse(fn);
            Compiler.Resolve(result);
            var generator = new AsmCodeGenerator();
            using (var writer = new StringWriter())
            {
                generator.GenerateCode(result, writer, includePrefix);
                var output = writer.ToString();
                var lineList = new List<string>();
                foreach (var line in output.Split('\n'))
                {
                    var l = kCompressWhitespace.Replace(line, " ");
                    l = kLeadingWhitespace.Replace(l, "");
                    l = kTrailingWhitespace.Replace(l, "");
                    l = kComment.Replace(l, "");
                    if (l.Length == 0)
                        continue;
                    lineList.Add(l);
                }

                return lineList;
            }
        }

        [Test]
        public void SimplestPossible()
        {
            AddFile("a", "struct Foo { u32 Bar; }");
            var lines = ParseAndGenerate("a");
            Assert.Contains("Foo_SIZEOF EQU 4", lines);
            Assert.Contains("Foo_ALIGNOF EQU 2", lines);
            Assert.Contains("Foo_Bar EQU 0", lines);
        }

        [Test]
        public void MultipleFields()
        {
            AddFile("a", "struct Foo { u32 Bar; u32 Baz; }");
            var lines = ParseAndGenerate("a");
            Assert.Contains("Foo_SIZEOF EQU 8", lines);
            Assert.Contains("Foo_ALIGNOF EQU 2", lines);
            Assert.Contains("Foo_Bar EQU 0", lines);
            Assert.Contains("Foo_Baz EQU 4", lines);
        }

        [Test]
        public void MultipleStructs()
        {
            AddFile("a", "struct Foo { Bar F; u32 Baz; } struct Bar { u8 Byte; }");
            var lines = ParseAndGenerate("a");
            Assert.Contains("Foo_SIZEOF EQU 6", lines);
            Assert.Contains("Foo_ALIGNOF EQU 2", lines);
            Assert.Contains("Foo_F EQU 0", lines);
            Assert.Contains("Foo_Baz EQU 2", lines);
            Assert.Contains("Bar_SIZEOF EQU 1", lines);
            Assert.Contains("Bar_ALIGNOF EQU 1", lines);
            Assert.Contains("Bar_Byte EQU 0", lines);
        }

        [Test]
        public void AllTypes()
        {
            AddFile("a", "struct Foo { u8 A; u16 B; u32 C; i8 D; i16 E; i32 F; f32 G; f64 H; void* I; u8[12] array;}");
            var lines = ParseAndGenerate("a");
            Assert.Contains("Foo_SIZEOF EQU 44", lines);
            Assert.Contains("Foo_ALIGNOF EQU 2", lines);
        }

        [Test]
        public void Includes()
        {
            AddFile("a", "include \"b\"\nstruct Foo { Bar F; u32 Baz; }");
            AddFile("b", "struct Bar { u16 Baz; }");
            var lines = ParseAndGenerate("a");
            Assert.Contains("include \"b.i\"", lines);
            Assert.Contains("Foo_SIZEOF EQU 6", lines);
            Assert.Contains("Foo_ALIGNOF EQU 2", lines);
            Assert.Contains("Foo_F EQU 0", lines);
            Assert.Contains("Foo_Baz EQU 2", lines);
            Assert.AreEqual(0, lines.Count((e) => e.StartsWith("Bar_")));
        }

        [Test]
        public void IncludePrefix()
        {
            AddFile("a", "include \"b\"\nstruct Foo { Bar F; u32 Baz; }");
            AddFile("b", "struct Bar { u16 Baz; }");
            var lines = ParseAndGenerate("a", "MY_PREFIX/");
            Assert.Contains("include \"MY_PREFIX/b.i\"", lines);
        }

        [Test]
        public void Constants()
        {
            AddFile("a", "include \"b\"\nconst c1 = 9; const c2 = 9 + c3;");
            AddFile("b", "const c3 = 12;");
            var lines = ParseAndGenerate("a");
            Assert.Contains("c1 EQU 9", lines);
            Assert.Contains("c2 EQU 21", lines);
            Assert.AreEqual(0, lines.Count((e) => e.StartsWith("c3")));
        }
    }
}
