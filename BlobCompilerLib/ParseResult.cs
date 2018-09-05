using System.Collections.Generic;

namespace BlobCompiler
{
    public struct ResolvedConstant
    {
        public ConstDef Definition;
        public long Value;
    }

    public class ParseResult
    {
        public string InputFilename { get; private set; }
        public List<string> Includes { get; private set; }
        public List<StructDef> Structs { get; private set; }
        public List<ConstDef> Constants { get; private set; }
        public List<FunctionType> FunctionTypes { get; private set; }
        public List<ResolvedConstant> ResolvedConstants { get; private set; }

        public ParseResult(string inputFilename)
        {
            InputFilename = inputFilename;
            Includes = new List<string>();
            Structs = new List<StructDef>();
            FunctionTypes = new List<FunctionType>();
            Constants = new List<ConstDef>();
            ResolvedConstants = new List<ResolvedConstant>();
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

    public class ConstDef
    {
        public Location Location;
        public string Name;
        public Expression Expression;
        public bool WasIncluded;
    }

    public abstract class Expression
    {
        public Location Location;

        public override abstract bool Equals(object other);
        public override abstract int GetHashCode();

        internal long Eval(Dictionary<string, Expression> lookup, HashSet<Expression> stack)
        {
            if (stack.Contains(this))
                throw new TypeCheckException(Location, $"recursive constant expression");

            stack.Add(this);

            long result = InternalEval(lookup, stack);

            stack.Remove(this);

            return result;
        }

        protected abstract long InternalEval(Dictionary<string, Expression> lookup, HashSet<Expression> stack);
    }

    public enum UnaryExpressionType
    {
        Negate,
        BitwiseNegate
    }

    public enum BinaryExpressionType
    {
        Add,
        Sub,
        Mul,
        Div,
        LeftShift,
        RightShift,
    }

    public class BinaryExpression : Expression
    {
        public BinaryExpressionType ExpressionType;
        public Expression Left;
        public Expression Right;

        public override int GetHashCode()
        {
            return ExpressionType.GetHashCode() + Left.GetHashCode() * 4711 + Right.GetHashCode() * 1913;
        }

        public override bool Equals(object obj)
        {
            BinaryExpression other = obj as BinaryExpression;
            if (other == null)
                return false;
            return ExpressionType == other.ExpressionType && Left.Equals(other.Left) && Right.Equals(other.Right);
        }

        public override string ToString()
        {
            return $"({ExpressionType} {Left} {Right})";
        }

        protected override long InternalEval(Dictionary<string, Expression> lookup, HashSet<Expression> stack)
        {
            long lv = Left.Eval(lookup, stack);
            long rv = Right.Eval(lookup, stack);

            switch (ExpressionType)
            {
                case BinaryExpressionType.Add: return lv + rv;
                case BinaryExpressionType.Sub: return lv - rv;
                case BinaryExpressionType.Mul: return lv * rv;
                case BinaryExpressionType.Div:
                                           if (rv == 0)
                                               throw new TypeCheckException(Location, $"division by zero");
                                           return lv / rv;
                case BinaryExpressionType.LeftShift:
                                           CheckShiftOperand(lv);
                                           CheckShiftQuantity(rv);
                                           return lv << (int) rv;
                case BinaryExpressionType.RightShift:
                                           CheckShiftOperand(lv);
                                           CheckShiftQuantity(rv);
                                           return lv >> (int) rv;
                default:
                    throw new TypeCheckException(Location, $"unknown operator - internal compiler error");
            }
        }

        private void CheckShiftOperand(long q)
        {
            if (q < 0)
                throw new TypeCheckException(Location, $"shifted value cannot be negative");
        }

        private void CheckShiftQuantity(long q)
        {
            if (q < 0)
                throw new TypeCheckException(Location, $"shift amount cannot be negative");
            if (q > 63)
                throw new TypeCheckException(Location, $"shift amount cannot be greater than 63");
        }
    }

    public class UnaryExpression : Expression
    {
        public UnaryExpressionType ExpressionType;
        public Expression Expression;

        public override int GetHashCode()
        {
            return ExpressionType.GetHashCode() + Expression.GetHashCode() * 4711;
        }

        public override bool Equals(object obj)
        {
            UnaryExpression other = obj as UnaryExpression;
            if (other == null)
                return false;
            return ExpressionType == other.ExpressionType && Expression.Equals(other.Expression);
        }

        public override string ToString()
        {
            return $"({ExpressionType} {Expression})";
        }

        protected override long InternalEval(Dictionary<string, Expression> lookup, HashSet<Expression> stack)
        {
            long v = Expression.Eval(lookup, stack);

            switch (ExpressionType)
            {
                case UnaryExpressionType.Negate: return -v;
                case UnaryExpressionType.BitwiseNegate: return ~v;
                default:
                    throw new TypeCheckException(Location, $"unknown operator - internal compiler error");
            }
        }
    }

    public class LiteralExpression : Expression
    {
        public long Value;

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            LiteralExpression other = obj as LiteralExpression;
            if (other == null)
                return false;
            return Value == other.Value;
        }

        public override string ToString()
        {
            return $"{Value}";
        }

        protected override long InternalEval(Dictionary<string, Expression> lookup, HashSet<Expression> stack)
        {
            return Value;
        }
    }

    public class IdentifierExpression : Expression
    {
        public string Name;

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            IdentifierExpression other = obj as IdentifierExpression;
            if (other == null)
                return false;
            return Name == other.Name;
        }

        public override string ToString()
        {
            return $"{Name}";
        }

        protected override long InternalEval(Dictionary<string, Expression> lookup, HashSet<Expression> stack)
        {
            Expression e;
            if (!lookup.TryGetValue(Name, out e))
            {
                throw new TypeCheckException(Location, $"undefined constant '{Name}'");
            }
            return e.Eval(lookup, stack);
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
        public override int SizeBytes
        {
          get { throw new System.NotImplementedException(); }
        }

        public override int AlignmentBytes
        {
          get { throw new System.NotImplementedException(); }
        }

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
        public override int SizeBytes
        {
          get { throw new TypeCheckException(Location, "function types do not have storage"); }
        }

        public override int AlignmentBytes
        {
          get { throw new TypeCheckException(Location, "function types do not have storage"); }
        }

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
