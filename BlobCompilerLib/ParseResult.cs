using System.Collections.Generic;

namespace BlobCompiler
{
    public class ParseResult
    {
        public string InputFilename { get; private set; }
        public List<string> Includes { get; private set; }
        public List<StructDef> Structs { get; private set; }
        public List<FunctionType> FunctionTypes { get; private set; }

        public ParseResult(string inputFilename)
        {
            InputFilename = inputFilename;
            Includes = new List<string>();
            Structs = new List<StructDef>();
            FunctionTypes = new List<FunctionType>();
        }
    }

    public class StructDef
    {
        public Location Location;
        public string Name;
        public List<FieldDef> Fields;
        public int SizeBytes;
        public int AlignmentBytes;
        public int SortWeight;
        public bool WasIncluded;

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj);
        }
    }

    public class FieldDef
    {
        public Location Location;
        public string Name;
        public TypeDef Type;
        public int OffsetBytes;
    }

    public abstract class TypeDef
    {
        public abstract int SizeBytes { get; }
        public abstract int AlignmentBytes { get; }

        internal abstract void Resolve(Dictionary<string, StructDef> allStructs);
    }

    public sealed class VoidType : TypeDef
    {
        public override int SizeBytes => throw new System.NotImplementedException();
        public override int AlignmentBytes => throw new System.NotImplementedException();

        internal override void Resolve(Dictionary<string, StructDef> allStructs)
        {
        }

        private VoidType() { }

        public static readonly VoidType Instance = new VoidType();
    }

    public sealed class PrimitiveType : TypeDef
    {
        private readonly int m_SizeBytes;
        private readonly int m_AlignmentBytes;

        public override int SizeBytes => m_SizeBytes;
        public override int AlignmentBytes => m_AlignmentBytes;

        public bool IsSigned { get; }
        public bool IsIntegral { get; }

        private PrimitiveType(int sizeBytes, int alignmentBytes, bool signed, bool integral)
        {
            m_SizeBytes = sizeBytes;
            m_AlignmentBytes = alignmentBytes;
            IsSigned = signed;
            IsIntegral = integral;
        }

        public static readonly PrimitiveType U8  = new PrimitiveType(1, 1, false, true);
        public static readonly PrimitiveType U16 = new PrimitiveType(2, 2, false, true);
        public static readonly PrimitiveType U32 = new PrimitiveType(4, 2, false, true);
        public static readonly PrimitiveType I8  = new PrimitiveType(1, 1, true, true);
        public static readonly PrimitiveType I16 = new PrimitiveType(2, 2, true, true);
        public static readonly PrimitiveType I32 = new PrimitiveType(4, 2, true, true);
        public static readonly PrimitiveType F32 = new PrimitiveType(4, 2, true, false);
        public static readonly PrimitiveType F64 = new PrimitiveType(8, 2, true, false);

        internal override void Resolve(Dictionary<string, StructDef> allStructs)
        {
        }
    }

    public sealed class StructType : TypeDef
    {
        public override int SizeBytes
        {
            get
            {
                CheckResolved();
                return Definition.SizeBytes;
            }
        }

        public override int AlignmentBytes
        {
            get
            {
                CheckResolved();
                return Definition.AlignmentBytes;
            }
        }

        public Location Location { get; private set; }
        public string Name { get; private set; }
        public StructDef Definition { get; private set; }

        public bool IsResolved => Definition != null;

        public StructType(Location loc, string name)
        {
            Location = loc;
            Name = name;
            Definition = null;
        }

        internal override void Resolve(Dictionary<string, StructDef> allStructs)
        {
            if (IsResolved)
                return;

            StructDef def;
            if (!allStructs.TryGetValue(Name, out def))
            {
                throw new TypeException(this, $"struct {Name} unknown");
            }
            else
            {
                Definition = def;
            }

            foreach (FieldDef fieldDef in def.Fields)
            {
                fieldDef.Type.Resolve(allStructs);
            }
        }

        private void CheckResolved()
        {
            if (!IsResolved)
                throw new TypeException(this, $"struct type {Name} is not yet resolved");
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj);
        }
    }

    public sealed class ArrayType : TypeDef
    {
        public override int SizeBytes => (int)Length * ElementType.SizeBytes;
        public override int AlignmentBytes => ElementType.AlignmentBytes;

        public TypeDef ElementType { get; private set; }
        public Location Location { get; private set; }
        public long Length { get; private set; }

        public ArrayType(Location loc, long length, TypeDef elementType)
        {
            Location = loc;
            Length = length;
            ElementType = elementType;
        }

        internal override void Resolve(Dictionary<string, StructDef> allStructs)
        {
            ElementType.Resolve(allStructs);
        }
    }

    public sealed class PointerType : TypeDef
    {
        public override int SizeBytes => 4;
        public override int AlignmentBytes => 2;

        public TypeDef PointeeType { get; private set; }
        public Location Location { get; private set; }

        public PointerType(Location loc, TypeDef pointeeType)
        {
            Location = loc;
            PointeeType = pointeeType;
        }

        internal override void Resolve(Dictionary<string, StructDef> allStructs)
        {
            PointeeType.Resolve(allStructs);
        }
    }

    public struct FunctionArgument
    {
        public string Name;
        public TypeDef Type;
    }

    public sealed class FunctionType : TypeDef
    {
        public override int SizeBytes => throw new TypeCheckException(Location, "function types do not have storage");
        public override int AlignmentBytes => throw new TypeCheckException(Location, "function types do not have storage");

        public Location Location { get; private set; }

        public TypeDef ReturnType { get; private set; }
        public IList<FunctionArgument> Arguments { get; private set; }

        public FunctionType(Location location, TypeDef returnType, List<FunctionArgument> arguments)
        {
            Location = location;
            Arguments = arguments;
            ReturnType = returnType;
        }

        internal override void Resolve(Dictionary<string, StructDef> allStructs)
        {
            ReturnType.Resolve(allStructs);
            foreach (var t in Arguments)
            {
                t.Type.Resolve(allStructs);
            }
        }
    }
}
