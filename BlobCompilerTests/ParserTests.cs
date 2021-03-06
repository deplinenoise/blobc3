﻿using BlobCompiler;
using NUnit.Framework;
using System;
using System.IO;
using System.Collections.Generic;

namespace BlobCompilerTests
{
    internal class MockFileAccess : IParserFileAccess
    {
        public Dictionary<string, string> Files = new Dictionary<string, string>();

        public Lexer OpenFileForLexing(string sourceFile, string path)
        {
            string targetDir = sourceFile != null ? Path.GetDirectoryName(sourceFile) : "";
            string targetFile = Path.Combine(targetDir, path);
            string neutralTarget = targetFile.Replace('\\', '/');

            string data;
            if (!Files.TryGetValue(neutralTarget, out data))
            {
                throw new IOException($"file '{neutralTarget}' could not be found");
            }

            return new Lexer(new StringReader(data), neutralTarget);
        }
    }

    public class BaseParserFixture
    {
        private MockFileAccess m_FileAccess;

        [SetUp]
        protected void SetUpFileAccess()
        {
            m_FileAccess = new MockFileAccess();
        }

        protected void AddFile(string path, string data)
        {
            m_FileAccess.Files[path.Replace('\\', '/')] = data;
        }

        protected ParseResult Parse(string path)
        {
            var parser = new Parser(m_FileAccess);
            return parser.Parse(path);
        }
    }

    [TestFixture]
    public class ParserTests : BaseParserFixture
    {
        [Test]
        public void EmptyParseTree()
        {
            AddFile("foo", "");
            var result = Parse("foo");
            Assert.AreEqual(0, result.Structs.Count);
            Assert.AreEqual("foo", result.InputFilename);
        }

        [Test]
        public void BadFileScopeToken()
        {
            AddFile("foo", " blah ");
            var ex = Assert.Throws<ParseException>(() => Parse("foo"));
            Assert.IsTrue(ex.Message.Contains("at file scope"));
        }

        [Test]
        public void BadBaseType()
        {
            AddFile("foo", "struct Foo { ; A");
            var ex = Assert.Throws<ParseException>(() => Parse("foo"));
            Assert.IsTrue(ex.Message.Contains("expected type"));
        }

        [Test]
        public void MissingBraceThrows()
        {
            AddFile("foo", " struct Foo { u32 a; ");
            var ex = Assert.Throws<ParseException>(() => Parse("foo"));
            Assert.IsTrue(ex.Message.Contains("expected"));
        }

        [Test]
        public void MissingIncludeFile()
        {
            AddFile("foo", "include \"bar\"");
            var ex = Assert.Throws<ParseException>(() => Parse("foo"));
            Assert.IsTrue(ex.Message.Contains("file not found: 'bar'"));
        }

        [Test]
        public void UnexpectedToken()
        {
            AddFile("foo", " struct ; { u32 a; }");
            var ex = Assert.Throws<ParseException>(() => Parse("foo"));
            Assert.IsTrue(ex.Message.Contains("expected Identifier"));
        }

        [Test]
        public void UnexpectedToplevel()
        {
            AddFile("foo", "foo bar");
            var ex = Assert.Throws<ParseException>(() => Parse("foo"));
            Assert.IsTrue(ex.Message.Contains("file scope"));
        }

        private void TestPrimitiveType(string id, PrimitiveType expectedType)
        {
            AddFile("foo", $"struct Foo {{ {id} Bar; }}");
            var result = Parse("foo");
            Assert.AreEqual(1, result.Structs.Count);

            var sfoo = result.Structs[0];
            Assert.AreEqual("Foo", sfoo.Name);
            Assert.AreEqual(1, sfoo.Fields.Count);

            var f1 = sfoo.Fields[0];
            Assert.AreEqual("Bar", f1.Name);
            Assert.IsTrue(f1.Type is PrimitiveType);
            Assert.AreSame(expectedType, f1.Type);
        }

        [Test] public void TestPrimitiveU8()  { TestPrimitiveType("u8",  PrimitiveType.U8); }
        [Test] public void TestPrimitiveU16() { TestPrimitiveType("u16", PrimitiveType.U16); }
        [Test] public void TestPrimitiveU32() { TestPrimitiveType("u32", PrimitiveType.U32); }
        [Test] public void TestPrimitiveI8()  { TestPrimitiveType("i8",  PrimitiveType.I8); }
        [Test] public void TestPrimitiveI16() { TestPrimitiveType("i16", PrimitiveType.I16); }
        [Test] public void TestPrimitiveI32() { TestPrimitiveType("i32", PrimitiveType.I32); }
        [Test] public void TestPrimitiveF32() { TestPrimitiveType("f32", PrimitiveType.F32); }
        [Test] public void TestPrimitiveF64() { TestPrimitiveType("f64", PrimitiveType.F64); }

        [Test]
        public void TestMultipleFields()
        {
            AddFile("foo", "struct Foo { u32 A; f32 B; f64 C; u8 D; }");

            var result = Parse("foo");
            Assert.AreEqual(1, result.Structs.Count);

            var sfoo = result.Structs[0];
            Assert.AreEqual(4, sfoo.Fields.Count);

            Assert.AreEqual("A", sfoo.Fields[0].Name);
            Assert.AreEqual("B", sfoo.Fields[1].Name);
            Assert.AreEqual("C", sfoo.Fields[2].Name);
            Assert.AreEqual("D", sfoo.Fields[3].Name);

            Assert.AreSame(PrimitiveType.U32, sfoo.Fields[0].Type);
            Assert.AreSame(PrimitiveType.F32, sfoo.Fields[1].Type);
            Assert.AreSame(PrimitiveType.F64, sfoo.Fields[2].Type);
            Assert.AreSame(PrimitiveType.U8,  sfoo.Fields[3].Type);
        }

        [Test]
        public void TestStructType()
        {
            AddFile("foo", "struct Foo { Bar A; }");

            var result = Parse("foo");
            Assert.AreEqual(1, result.Structs.Count);

            var sfoo = result.Structs[0];
            Assert.AreEqual(1, sfoo.Fields.Count);

            Assert.AreEqual("A", sfoo.Fields[0].Name);

            Assert.IsTrue(sfoo.Fields[0].Type is StructType);

            var st = sfoo.Fields[0].Type as StructType;
            Assert.AreEqual("Bar", st.Name);
            Assert.IsFalse(st.IsResolved);
        }

        [Test]
        public void TestUnresolvedStructThrows()
        {
            AddFile("foo", "struct Foo { Bar A; }");

            var result = Parse("foo");
            Assert.AreEqual(1, result.Structs.Count);

            var sfoo = result.Structs[0];
            Assert.AreEqual(1, sfoo.Fields.Count);

            Assert.AreEqual("A", sfoo.Fields[0].Name);

            Assert.IsTrue(sfoo.Fields[0].Type is StructType);

            var st = sfoo.Fields[0].Type as StructType;

            Assert.Throws<TypeException>(() => { int dummy = st.SizeBytes; });
            Assert.Throws<TypeException>(() => { int dummy = st.AlignmentBytes; });
        }

        [Test]
        public void TestIncludes()
        {
            AddFile("a", "include \"b\"\nstruct A { u32 f; }");
            AddFile("b", "include \"c\"\nstruct B { u32 f; }");
            AddFile("c", "struct C { u32 f; }");

            var result = Parse("a");
            Assert.AreEqual(3, result.Structs.Count);
            Assert.AreEqual(1, result.Includes.Count);
            Assert.AreEqual("b", result.Includes[0]);


            Assert.AreEqual("C", result.Structs[0].Name);
            Assert.IsTrue(result.Structs[0].WasIncluded);
            Assert.AreEqual("c", result.Structs[0].Location.Filename);
            Assert.AreEqual("B", result.Structs[1].Name);
            Assert.AreEqual("b", result.Structs[1].Location.Filename);
            Assert.IsTrue(result.Structs[1].WasIncluded);
            Assert.AreEqual("A", result.Structs[2].Name);
            Assert.AreEqual("a", result.Structs[2].Location.Filename);
            Assert.IsFalse(result.Structs[2].WasIncluded);
        }

        [Test]
        public void TestPointerType()
        {
            AddFile("foo", "struct Foo { Bar* A; }");

            var result = Parse("foo");
            Assert.AreEqual(1, result.Structs.Count);

            var sfoo = result.Structs[0];
            Assert.AreEqual(1, sfoo.Fields.Count);

            Assert.AreEqual("A", sfoo.Fields[0].Name);

            Assert.IsTrue(sfoo.Fields[0].Type is PointerType);

            var st = sfoo.Fields[0].Type as PointerType;
            var struc = st.PointeeType as StructType;
            Assert.IsTrue(struc != null);
            Assert.AreEqual("Bar", struc.Name);
            Assert.IsFalse(struc.IsResolved);
        }

        [Test]
        public void TestMultiPointerType()
        {
            AddFile("foo", "struct Foo { Bar** A; }");

            var result = Parse("foo");
            Assert.AreEqual(1, result.Structs.Count);

            var sfoo = result.Structs[0];
            Assert.AreEqual(1, sfoo.Fields.Count);

            Assert.AreEqual("A", sfoo.Fields[0].Name);

            var p1 = sfoo.Fields[0].Type as PointerType;
            Assert.IsTrue(p1 != null);
            var p2 = p1.PointeeType as PointerType;
            Assert.IsTrue(p2 != null);
            var st = p2.PointeeType as StructType;
            Assert.IsTrue(st != null);
            Assert.AreEqual("Bar", st.Name);
            Assert.IsFalse(st.IsResolved);
        }

        [Test]
        public void TestNoPointersToArrays()
        {
            AddFile("foo", "struct Foo { Bar[1]* A; }");

            var ex = Assert.Throws<ParseException>(() =>Parse("foo"));
            Assert.IsTrue(ex.Message.Contains("pointer to array type"));
        }

        [Test]
        public void TestFunctionSizeBytesThrows()
        {
            AddFile("foo", "struct Foo { u32(u32 arg) A; }");

            var result = Parse("foo");
            var ft = result.Structs[0].Fields[0].Type as FunctionType;
            Assert.IsNotNull(ft);
            Assert.Throws<TypeCheckException>(() => { int size = ft.SizeBytes; });
        }

        [Test]
        public void TestFunctionEmptySignature()
        {
            AddFile("foo", "struct Foo { u32() A; }");

            var result = Parse("foo");
            var ft = result.Structs[0].Fields[0].Type as FunctionType;
            Assert.IsNotNull(ft);

            Assert.AreSame(PrimitiveType.U32, ft.ReturnType);
            Assert.AreEqual(0, ft.Arguments.Count);
        }

        [Test]
        public void TestFunctionReturnVoid()
        {
            AddFile("foo", "struct Foo { void() A; }");

            var result = Parse("foo");
            var ft = result.Structs[0].Fields[0].Type as FunctionType;
            Assert.IsNotNull(ft);

            Assert.AreSame(VoidType.Instance, ft.ReturnType);
            Assert.AreEqual(0, ft.Arguments.Count);
        }

        [Test]
        public void TestFunctionOneArg()
        {
            AddFile("foo", "struct Foo { void(u32 a) A; }");

            var result = Parse("foo");
            var ft = result.Structs[0].Fields[0].Type as FunctionType;
            Assert.IsNotNull(ft);

            Assert.AreSame(VoidType.Instance, ft.ReturnType);
            Assert.AreEqual(1, ft.Arguments.Count);
            Assert.AreEqual("a", ft.Arguments[0].Name);
            Assert.AreSame(PrimitiveType.U32, ft.Arguments[0].Type);
        }

        [Test]
        public void TestFunctionMultiArg()
        {
            AddFile("foo", "struct Foo { void(u32 a, u32* b, Bar* c) A; }");

            var result = Parse("foo");
            var ft = result.Structs[0].Fields[0].Type as FunctionType;
            Assert.IsNotNull(ft);

            Assert.AreSame(VoidType.Instance, ft.ReturnType);
            Assert.AreEqual(3, ft.Arguments.Count);
            Assert.AreEqual("a", ft.Arguments[0].Name);
            Assert.AreEqual("b", ft.Arguments[1].Name);
            Assert.AreEqual("c", ft.Arguments[2].Name);

            Assert.AreSame(PrimitiveType.U32, ft.Arguments[0].Type);

            Assert.IsTrue(ft.Arguments[1].Type is PointerType);
            Assert.AreSame(PrimitiveType.U32, ((PointerType)ft.Arguments[1].Type).PointeeType);

            Assert.IsTrue(ft.Arguments[2].Type is PointerType);
            Assert.AreEqual("Bar", ((StructType)((PointerType)ft.Arguments[2].Type).PointeeType).Name);
        }

        [Test]
        public void TestLiteralConstant()
        {
            AddFile("foo", "const foo = 123;");

            var result = Parse("foo");
            Assert.AreEqual("foo", result.Constants[0].Name);
            Assert.AreEqual(123, ((LiteralExpression)result.Constants[0].Expression).Value);
        }

        [Test]
        [Sequential]
        public void SingleExpressionTest(
            [Values("+", "-", "*", "/")] string opString,
            [Values(BinaryExpressionType.Add, BinaryExpressionType.Sub, BinaryExpressionType.Mul, BinaryExpressionType.Div)] BinaryExpressionType expectedType)
        {
            AddFile("foo", $"const foo = 123 {opString} 456;");
            var result = Parse("foo");
            var expected = new BinaryExpression { ExpressionType = expectedType, Left = new LiteralExpression { Value = 123 }, Right = new LiteralExpression { Value = 456 } };
            Assert.AreEqual(expected, result.Constants[0].Expression);
        }

        [Test]
        public void BasicPrecedence()
        {
            AddFile("foo", $"const foo = 1 + 2 / 3;");
            Assert.AreEqual(new BinaryExpression
            {
                ExpressionType = BinaryExpressionType.Add,
                Left = new LiteralExpression { Value = 1 },
                Right = new BinaryExpression
                {
                    ExpressionType = BinaryExpressionType.Div,
                    Left = new LiteralExpression { Value = 2 },
                    Right = new LiteralExpression { Value = 3 },
                }
            }, Parse("foo").Constants[0].Expression);
        }

        [Test]
        public void BasicPrecedence2()
        {
            AddFile("foo", $"const foo = 1 / 2 + 3;");
            Assert.AreEqual(new BinaryExpression
            {
                ExpressionType = BinaryExpressionType.Add,
                Left = new BinaryExpression
                {
                    ExpressionType = BinaryExpressionType.Div,
                    Left = new LiteralExpression { Value = 1 },
                    Right = new LiteralExpression { Value = 2 },
                },
                Right = new LiteralExpression { Value = 3 },
            }, Parse("foo").Constants[0].Expression);
        }

        [Test]
        public void IdExpression()
        {
            AddFile("foo", $"const foo = x;");
            Assert.AreEqual(new IdentifierExpression { Name = "x" }, Parse("foo").Constants[0].Expression);
        }

        [Test]
        public void NegativeValue()
        {
            AddFile("foo", $"const foo = -12;");
            Assert.AreEqual(new LiteralExpression { Value = -12 }, Parse("foo").Constants[0].Expression);
        }

        [Test]
        public void BadExpression()
        {
            AddFile("foo", $"const foo = 12 + /;");
            Assert.Throws<ParseException>(() => Parse("foo"));
        }

        [Test]
        public void BadExpression2()
        {
            AddFile("foo", $"const foo = /;");
            Assert.Throws<ParseException>(() => Parse("foo"));
        }
    }
}
