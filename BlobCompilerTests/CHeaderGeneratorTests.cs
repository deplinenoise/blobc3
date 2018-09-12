using NUnit.Framework;
using BlobCompiler;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace BlobCompilerTests
{
    public class CHeaderGeneratorTests : BaseParserFixture
    {
        protected static readonly Regex kCompressWhitespace = new Regex("\\s+");
        protected static readonly Regex kLeadingWhitespace = new Regex("^ ");
        protected static readonly Regex kTrailingWhitespace = new Regex(" $");

        protected List<string> ParseAndGenerate(string fn)
        {
            var result = Parse(fn);
            Compiler.Resolve(result);
            var generator = new CHeaderGenerator(result);
            using (var writer = new StringWriter())
            {
                generator.GenerateCode(writer);
                var output = writer.ToString();
                var lineList = new List<string>();
                foreach (var line in output.Split('\n'))
                {
                    var l = kCompressWhitespace.Replace(line, " ");
                    l = kLeadingWhitespace.Replace(l, "");
                    l = kTrailingWhitespace.Replace(l, "");
                    if (l.Length == 0)
                        continue;
                    lineList.Add(l);
                }

                return lineList;
            }
        }

        [Test]
        public void TestBasicCodeGen()
        {
            AddFile("foo", "const Q = 1; struct A { u32 B; } struct B { f32 C; };");
            var lines = ParseAndGenerate("foo");
            Assert.Contains("struct A {", lines);
            Assert.Contains("unsigned int B;", lines);
            Assert.Contains("struct B {", lines);
            Assert.Contains("float C;", lines);
        }

        [Test]
        public void AllTypes()
        {
            AddFile("a", "struct Bar { u32 A; }; struct Foo { u8 A; u16 B; u32 C; i8 D; i16 E; i32 F; f32 G; f64 H; void* I; u8[12] array; i32 (i32 i, i32 j)* fp; Bar nested_struct; }");
            var lines = ParseAndGenerate("a");
            Assert.Contains("struct Foo {", lines);
        }

        [Test]
        public void Include()
        {
            AddFile("a", "include \"b\"");
            AddFile("b", "const b = 1;");
            var lines = ParseAndGenerate("a");
            Assert.Contains("#include \"b.h\"", lines);
        }

        [Test]
        public void IncludedStructsDontOutput()
        {
            AddFile("a", "include \"b\"");
            AddFile("b", "const b = 1; struct bs {}");
            var lines = ParseAndGenerate("a");
            Assert.Contains("#include \"b.h\"", lines);
            Assert.IsFalse(lines.Contains("struct bs {"));
        }
    }
}