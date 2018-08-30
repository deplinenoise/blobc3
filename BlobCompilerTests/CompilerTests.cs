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
            Compiler.ResolveTypes(result);

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
            Compiler.ResolveTypes(result);

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
            var ex = Assert.Throws<TypeCheckException>(() => Compiler.ResolveTypes(result));
            Assert.IsTrue(ex.Message.Contains("recursive relationship"));
        }

        [Test]
        public void RecursiveTypesThrow3()
        {
            AddFile("a", "struct Foo { Bar b; }\nstruct Bar { Baz a; }\nstruct Baz { Foo f; }\n");
            var result = Parse("a");
            var ex = Assert.Throws<TypeCheckException>(() => Compiler.ResolveTypes(result));
            Assert.IsTrue(ex.Message.Contains("recursive relationship"));
        }

        [Test]
        public void RepeatedFieldNamesThrow()
        {
            AddFile("a", "struct Foo { u32 a; u32 b; u32 a; }");
            var result = Parse("a");
            var ex = Assert.Throws<TypeCheckException>(() => Compiler.ResolveTypes(result));
            Assert.IsTrue(ex.Message.Contains("duplicate field name"));
        }

        [Test]
        public void TestDependencySort()
        {
            AddFile("a", "struct Foo { Z a; B b; }\nstruct Z { u32 a; }\nstruct B { Z[2] f; }\n");
            var result = Parse("a");
            Compiler.ResolveTypes(result);
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
            Compiler.ResolveTypes(result);
            Assert.AreEqual(4, result.Structs.Count);
            // We don't care about the order of the included types, just that our non-included one sorted last.
            Assert.AreEqual("Moo", result.Structs[3].Name);
        }

        [Test]
        public void TestPointerToVoidWorks()
        {
            AddFile("a", "struct Foo { void* a; }\n");
            var result = Parse("a");
            Compiler.ResolveTypes(result);
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
            var ex = Assert.Throws<TypeCheckException>(() => Compiler.ResolveTypes(result));
            Assert.IsTrue(ex.Message.Contains("void"));
        }

        [Test]
        public void TestVoidFieldThrows()
        {
            AddFile("a", "struct Foo { void a; }\n");
            var result = Parse("a");
            var ex = Assert.Throws<TypeCheckException>(() => Compiler.ResolveTypes(result));
            Assert.IsTrue(ex.Message.Contains("void"));
        }

        [Test]
        public void FunctionTypesCannotBeUsedAsValues()
        {
            AddFile("a", "struct Foo { void() a; }\n");
            var result = Parse("a");
            var ex = Assert.Throws<TypeCheckException>(() => Compiler.ResolveTypes(result));
            Assert.IsTrue(ex.Message.Contains("function"));
        }

        [Test]
        public void FunctionTypesCannotBeUsedAsArrayElements()
        {
            AddFile("a", "struct Foo { void()[12] a; }\n");
            var result = Parse("a");
            var ex = Assert.Throws<TypeCheckException>(() => Compiler.ResolveTypes(result));
            Assert.IsTrue(ex.Message.Contains("function"));
        }

        [Test]
        public void FunctionsCannotReturnArraysByValue()
        {
            AddFile("a", "struct Foo { u32[8](u32 n) a; }\n");
            var result = Parse("a");
            var ex = Assert.Throws<TypeCheckException>(() => Compiler.ResolveTypes(result));
            Assert.IsTrue(ex.Message.Contains("function"));
        }

        [Test]
        public void FunctionPointerTypesCanBeUsedAsArrayElements()
        {
            AddFile("a", "struct Foo { void(u32 a)*[12] a; }\n");
            var result = Parse("a");
            Compiler.ResolveTypes(result);
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
            var ex = Assert.Throws<TypeException>(() => Compiler.ResolveTypes(result));
            Assert.IsTrue(ex.Message.Contains("Bar"));
        }
    }
}
