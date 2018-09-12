using BlobCompiler;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlobCompilerTests
{
    [TestFixture]
    public class CompilerTests : BaseParserFixture
    {
        [Test]
        public void Padding1()
        {
            AddFile("a", "struct Foo { u8 a; u32 b; }");
            var result = Parse("a");
            Compiler.Resolve(result);

            var s0 = result.Structs[0];
            Assert.AreEqual(6, s0.SizeBytes);

            var f0 = result.Structs[0].Fields[0];
            var f1 = result.Structs[0].Fields[1];

            Assert.AreEqual(6, s0.SizeBytes);
            Assert.AreEqual(0, f0.OffsetBytes);
            Assert.AreEqual(1, f0.Type.SizeBytes);
            Assert.AreEqual(2, f1.OffsetBytes);
            Assert.AreEqual(4, f1.Type.SizeBytes);
        }

        [Test]
        public void Padding2()
        {
            AddFile("a", "struct Foo { u8 a; u16 b; u32 c; u8 d; }");
            var result = Parse("a");
            Compiler.Resolve(result);

            var s0 = result.Structs[0];
            var f0 = result.Structs[0].Fields[0];
            var f1 = result.Structs[0].Fields[1];
            var f2 = result.Structs[0].Fields[2];
            var f3 = result.Structs[0].Fields[3];

            Assert.AreEqual(0, f0.OffsetBytes);
            Assert.AreEqual(1, f0.Type.SizeBytes);
            Assert.AreEqual(2, f1.OffsetBytes);
            Assert.AreEqual(2, f1.Type.SizeBytes);
            Assert.AreEqual(4, f2.OffsetBytes);
            Assert.AreEqual(4, f2.Type.SizeBytes);
            Assert.AreEqual(8, f3.OffsetBytes);
            Assert.AreEqual(1, f3.Type.SizeBytes);

            Assert.AreEqual(10, s0.SizeBytes);
        }

        [Test]
        public void RecursiveTypesThrow()
        {
            AddFile("a", "struct Foo { Bar b; }\nstruct Bar { Foo a; }\n");
            var result = Parse("a");
            var ex = Assert.Throws<TypeCheckException>(() => Compiler.Resolve(result));
            Assert.IsTrue(ex.Message.Contains("recursive relationship"));
        }

        [Test]
        public void RecursiveTypesThrow3()
        {
            AddFile("a", "struct Foo { Bar b; }\nstruct Bar { Baz a; }\nstruct Baz { Foo f; }\n");
            var result = Parse("a");
            var ex = Assert.Throws<TypeCheckException>(() => Compiler.Resolve(result));
            Assert.IsTrue(ex.Message.Contains("recursive relationship"));
        }

        [Test]
        public void RepeatedFieldNamesThrow()
        {
            AddFile("a", "struct Foo { u32 a; u32 b; u32 a; }");
            var result = Parse("a");
            var ex = Assert.Throws<TypeCheckException>(() => Compiler.Resolve(result));
            Assert.IsTrue(ex.Message.Contains("duplicate field name"));
        }

        [Test]
        public void TestDependencySort()
        {
            AddFile("a", "struct Foo { Z a; B b; }\nstruct Z { u32 a; }\nstruct B { Z[2] f; }\n");
            var result = Parse("a");
            Compiler.Resolve(result);
            Assert.AreEqual(3, result.Structs.Count);
            Assert.AreEqual("Z", result.Structs[0].Name);
            Assert.AreEqual("B", result.Structs[1].Name);
            Assert.AreEqual("Foo", result.Structs[2].Name);
        }

        [Test]
        public void TestDependencySortWithIncludes()
        {
            AddFile("a", "struct Foo { Z a; B b; }\nstruct Z { u32 a; }\nstruct B { Z[2] f; }\n");
            AddFile("b", "include \"a\"\nstruct Moo { u32 a; }\n");
            var result = Parse("b");
            Compiler.Resolve(result);
            Assert.AreEqual(4, result.Structs.Count);
            // We don't care about the order of the included types, just that our non-included one sorted last.
            Assert.AreEqual("Moo", result.Structs[3].Name);
        }

        [Test]
        public void TestPointerToVoidWorks()
        {
            AddFile("a", "struct Foo { void* a; }\n");
            var result = Parse("a");
            Compiler.Resolve(result);
            Assert.AreEqual(1, result.Structs.Count);
            Assert.AreEqual(1, result.Structs[0].Fields.Count);
            var field = result.Structs[0].Fields[0];
            var fieldType = field.Type as PointerType;
            var pointeeType = fieldType.PointeeType;
            Assert.AreSame(VoidType.Instance, pointeeType);
        }

        [Test]
        public void TestArrayOfVoidThrows()
        {
            AddFile("a", "struct Foo { void[2] a; }\n");
            var result = Parse("a");
            var ex = Assert.Throws<TypeCheckException>(() => Compiler.Resolve(result));
            Assert.IsTrue(ex.Message.Contains("void"));
        }

        [Test]
        public void TestVoidFieldThrows()
        {
            AddFile("a", "struct Foo { void a; }\n");
            var result = Parse("a");
            var ex = Assert.Throws<TypeCheckException>(() => Compiler.Resolve(result));
            Assert.IsTrue(ex.Message.Contains("void"));
        }

        [Test]
        public void FunctionTypesCannotBeUsedAsValues()
        {
            AddFile("a", "struct Foo { void() a; }\n");
            var result = Parse("a");
            var ex = Assert.Throws<TypeCheckException>(() => Compiler.Resolve(result));
            Assert.IsTrue(ex.Message.Contains("function"));
        }

        [Test]
        public void FunctionTypesCannotBeUsedAsArrayElements()
        {
            AddFile("a", "struct Foo { void()[12] a; }\n");
            var result = Parse("a");
            var ex = Assert.Throws<TypeCheckException>(() => Compiler.Resolve(result));
            Assert.IsTrue(ex.Message.Contains("function"));
        }

        [Test]
        public void FunctionsCannotReturnArraysByValue()
        {
            AddFile("a", "struct Foo { u32[8](u32 n) a; }\n");
            var result = Parse("a");
            var ex = Assert.Throws<TypeCheckException>(() => Compiler.Resolve(result));
            Assert.IsTrue(ex.Message.Contains("function"));
        }

        [Test]
        public void FunctionPointerTypesCanBeUsedAsArrayElements()
        {
            AddFile("a", "struct Foo { void(u32 a)*[12] a; }\n");
            var result = Parse("a");
            Compiler.Resolve(result);
            Assert.AreEqual(1, result.Structs.Count);
            Assert.AreEqual(1, result.Structs[0].Fields.Count);
            var field = result.Structs[0].Fields[0];
            var fieldType = field.Type as ArrayType;
            Assert.AreEqual(12, fieldType.Length);
            var elementType = fieldType.ElementType as PointerType;
            var funcType = elementType.PointeeType as FunctionType;
            Assert.AreSame(VoidType.Instance, funcType.ReturnType);
            Assert.AreEqual(1, funcType.Arguments.Count);
        }

        [Test]
        public void UndefinedStructDoesNotTypecheck()
        {
            AddFile("a", "struct Foo { Bar a; }\n");
            var result = Parse("a");
            var ex = Assert.Throws<TypeException>(() => Compiler.Resolve(result));
            Assert.IsTrue(ex.Message.Contains("Bar"));
        }

        [Test]
        public void SimpleConstants()
        {
            AddFile("a", "const a = 1; const b = 2;\n");
            var result = Parse("a");
            Compiler.Resolve(result);

            Assert.AreEqual(2, result.ResolvedConstants.Count);
            Assert.AreEqual("a", result.ResolvedConstants[0].Definition.Name);
            Assert.AreEqual(1, result.ResolvedConstants[0].Value);
            Assert.AreEqual("b", result.ResolvedConstants[1].Definition.Name);
            Assert.AreEqual(2, result.ResolvedConstants[1].Value);
        }

        [Test]
        public void SimpleReferences()
        {
            AddFile("a", "const a = 134; const b = a;\n");
            var result = Parse("a");
            Compiler.Resolve(result);

            Assert.AreEqual(2, result.ResolvedConstants.Count);
            Assert.AreEqual("a", result.ResolvedConstants[0].Definition.Name);
            Assert.AreEqual(134, result.ResolvedConstants[0].Value);
            Assert.AreEqual("b", result.ResolvedConstants[1].Definition.Name);
            Assert.AreEqual(134, result.ResolvedConstants[1].Value);
        }

        [Test]
        [Sequential]
        public void BinaryOperators(
                [Values("+", "-", "*", "/", "<<", ">>")]
                string op,
                [Values(123, 123, 123, 123, 123, 123)]
                long a,
                [Values(123, 121, 2, 2, 1, 1)]
                long b,
                [Values(246, 2, 246, 61, 246, 61)]
                long expected)
        {
            AddFile("a", $"const a = {a} {op} {b};");
            var result = Parse("a");
            Compiler.Resolve(result);

            Assert.AreEqual(1, result.ResolvedConstants.Count);
            Assert.AreEqual("a", result.ResolvedConstants[0].Definition.Name);
            Assert.AreEqual(expected, result.ResolvedConstants[0].Value);
        }

        [Test]
        public void DivisionByZeroThrows()
        {
            AddFile("a", "const a = 134 / 0;");
            var result = Parse("a");
            var ex = Assert.Throws<TypeCheckException>(() => Compiler.Resolve(result));
            Assert.IsTrue(ex.Message.Contains("division by zero"));
        }

        [Test]
        public void IllegalShifts(
                [Values("<<", ">>")] string op,
                [Values(-1, 64, 123, -100)] long val)
        {
            AddFile("a", $"const a = 134 {op} {val};");
            var result = Parse("a");
            var ex = Assert.Throws<TypeCheckException>(() => Compiler.Resolve(result));
            Assert.IsTrue(ex.Message.Contains("shift"));
        }

        [Test]
        public void IllegalShifts2([Values("<<", ">>")] string op)
        {
            AddFile("a", $"const a = -1 {op} 2;");
            var result = Parse("a");
            var ex = Assert.Throws<TypeCheckException>(() => Compiler.Resolve(result));
            Assert.IsTrue(ex.Message.Contains("shift"));
        }

        [Test]
        [Sequential]
        public void CompositeExpressions(
                [Values("12 * (2 + 3)", "9 << 2", "12 / 2 + 9 * 7 << 1", "-7 * -9", "~12", "-(12)")]
                string expr,
                [Values( 12 * (2 + 3),   9 << 2,   12 / 2 + 9 * 7 << 1,   -7 * -9,   ~12,   -(12))]
                long expected)
        {
            AddFile("a", $"const a = {expr};");
            var result = Parse("a");
            Compiler.Resolve(result);
            Assert.AreEqual(expected, result.ResolvedConstants[0].Value);
        }

        [Test]
        public void RecursiveConstantsThrow()
        {
            AddFile("a", $"const a = b; const b = a;");
            var result = Parse("a");
            var ex = Assert.Throws<TypeCheckException>(() => Compiler.Resolve(result));
            Assert.IsTrue(ex.Message.Contains("recursive"));
        }

        [Test]
        public void DuplicateConstantsThrow()
        {
            AddFile("a", $"const a = 1; const a = 1;");
            var result = Parse("a");
            var ex = Assert.Throws<TypeCheckException>(() => Compiler.Resolve(result));
            Assert.IsTrue(ex.Message.Contains("already defined"));
        }
    }
}
