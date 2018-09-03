using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlobCompiler
{
    public class Compiler
    {
        private struct WeightPair : IComparable<WeightPair>
        {
            public StructDef Struct;
            public int Weight;

            public int CompareTo(WeightPair other)
            {
                // Sort by weight first, then name.
                int diff = this.Weight - other.Weight;
                if (diff != 0)
                    return diff;
                return this.Struct.Name.CompareTo(other.Struct.Name);
            }
        }

        public static void Resolve(ParseResult result)
        {
            ResolveStructs(result);
            ResolveConstants(result);
        }

        private static void ResolveConstants(ParseResult result)
        {
            var lookup = new Dictionary<string, Expression>();

            foreach (var constant in result.Constants)
            {
                if (lookup.ContainsKey(constant.Name))
                {
                  throw new TypeCheckException(constant.Location, $"constant '{constant.Name}' already defined");
                }
                lookup.Add(constant.Name, constant.Expression);
            }

            var stack = new HashSet<Expression>();
            foreach (var constant in result.Constants)
            {
                result.ResolvedConstants.Add(new ResolvedConstant
                {
                    Definition = constant,
                    Value = constant.Expression.Eval(lookup, stack),
                });
            }
        }

        private static void ResolveStructs(ParseResult result)
        {
            Dictionary<string, StructDef> allStructs = new Dictionary<string, StructDef>();

            foreach (StructDef structDef in result.Structs)
            {
                allStructs.Add(structDef.Name, structDef);
            }

            // Resolve all struct types used in fields to point to the structure definitions
            var fieldNames = new HashSet<string>();
            foreach (StructDef structDef in result.Structs)
            {
                fieldNames.Clear();
                foreach (FieldDef fieldDef in structDef.Fields)
                {
                    if (fieldNames.Contains(fieldDef.Name))
                    {
                        throw new TypeCheckException(fieldDef.Location, $"duplicate field name '{fieldDef.Name}'");
                    }

                    fieldNames.Add(fieldDef.Name);
                    fieldDef.Type.Resolve(allStructs);
                }
            }

            var structToWeightIndex = new Dictionary<StructDef, int>();
            WeightPair[] sortWeights = new WeightPair[result.Structs.Count];

            for (int i = 0; i < sortWeights.Length; ++i)
            {
                sortWeights[i] = new WeightPair { Struct = result.Structs[i], Weight = -1 };
                structToWeightIndex[result.Structs[i]] = i;
            }

            for (int i = 0; i < sortWeights.Length; ++i)
            {
                sortWeights[i].Weight = ComputeStructWeight(sortWeights, i, structToWeightIndex);
            }

            Array.Sort(sortWeights);

            // Replace array of structs with sorted result
            for (int i = 0; i < sortWeights.Length; ++i)
            {
                result.Structs[i] = sortWeights[i].Struct;
            }

            // Compute attributes for each struct
            foreach (StructDef structDef in result.Structs)
            {
                ComputeLayout(structDef);
            }
        }

        private static int ComputeStructWeight(WeightPair[] sortWeights, int i, Dictionary<StructDef, int> structToWeightIndex)
        {
            if (sortWeights[i].Weight >= 0)
                return sortWeights[i].Weight;

            var structDef = sortWeights[i].Struct;

            if (sortWeights[i].Weight == -2)
                throw new TypeCheckException(structDef.Location, $"type {structDef.Name} has a recursive relationship with itself");

            // Mark as on stack
            sortWeights[i].Weight = -2;
            
            int weight = 0;
            foreach (var fieldDef in structDef.Fields)
            {
                weight += ComputeTypeWeight(sortWeights, fieldDef.Type, structToWeightIndex);
            }

            // Ensure included structs are sorted first, because we're not going to codegen them.
            if (!structDef.WasIncluded)
                weight += 1;
            else
                weight = 0;

            sortWeights[i].Weight = weight;
            return weight;
        }

        private static int ComputeTypeWeight(WeightPair[] sortWeights, TypeDef type, Dictionary<StructDef, int> structToWeightIndex)
        {
            if (type is PrimitiveType || type is PointerType || type is FunctionType)
                return 0;

            if (type is ArrayType)
                return ComputeTypeWeight(sortWeights, ((ArrayType)type).ElementType, structToWeightIndex);

            if (type is VoidType)
                throw new TypeCheckException($"void cannot be used as a field or array element type");

            var structType = (StructType)type;

            return ComputeStructWeight(sortWeights, structToWeightIndex[structType.Definition], structToWeightIndex);
        }

        private static int Align(int value, int alignment)
        {
            return (value + alignment - 1) & ~(alignment - 1);
        }

        private static void ComputeLayout(StructDef structDef)
        {
            int alignOf = 1;
            int runningOffset = 0;

            foreach (FieldDef fieldDef in structDef.Fields)
            {
                var fieldType = fieldDef.Type;
                int fieldAlignment = fieldType.AlignmentBytes;
                fieldDef.OffsetBytes = Align(runningOffset, fieldAlignment);
                alignOf = Math.Max(fieldAlignment, alignOf);
                runningOffset = fieldDef.OffsetBytes + fieldType.SizeBytes;
            }

            structDef.SizeBytes = Align(runningOffset, alignOf);
            structDef.AlignmentBytes = alignOf;
        }
    }
}
