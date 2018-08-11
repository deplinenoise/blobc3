using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BlobCompiler
{
    public class CHeaderGenerator
    {
        private Dictionary<PrimitiveType, string> m_PrimitiveMappings;
        private ParseResult m_Result;
        private Dictionary<FunctionType, string> m_FunctionAliases;

        public CHeaderGenerator(ParseResult result)
        {
            m_Result = result;
            m_PrimitiveMappings = new Dictionary<PrimitiveType, string>(24);

            m_PrimitiveMappings.Add(PrimitiveType.U8, "unsigned char");
            m_PrimitiveMappings.Add(PrimitiveType.U16, "unsigned short");
            m_PrimitiveMappings.Add(PrimitiveType.U32, "unsigned int");
            m_PrimitiveMappings.Add(PrimitiveType.I8, "signed char");
            m_PrimitiveMappings.Add(PrimitiveType.I16, "signed short");
            m_PrimitiveMappings.Add(PrimitiveType.I32, "signed int");
            m_PrimitiveMappings.Add(PrimitiveType.F32, "float");
            m_PrimitiveMappings.Add(PrimitiveType.F64, "double");

            m_FunctionAliases = new Dictionary<FunctionType, string>(m_Result.FunctionTypes.Count);

            // Generate typedefs for function types
            ComputeFunctionAliases(result);
        }

        private void ComputeFunctionAliases(ParseResult result)
        {
            string uniqueId = ComputeUniqueId(result);
            int index = 0;

            foreach (var functionType in result.FunctionTypes)
            {
                var id = $"_blobc_fn_{uniqueId}_{index}";
                ++index;
                m_FunctionAliases.Add(functionType, id);
            }
        }

        public void GenerateCode(TextWriter writer)
        {
            writer.WriteLine("#pragma once");
            writer.WriteLine("/* This file was automatically generated. Do not edit it. */");

            foreach (var include in m_Result.Includes)
            {
                var incstr = Path.GetFileNameWithoutExtension(include) + ".h";
                writer.WriteLine("#include\t\"{0}\"", incstr);
            }

            writer.WriteLine();

            // Generate struct predeclarations and typedefs
            foreach (var structDef in m_Result.Structs)
            {
                if (structDef.WasIncluded)
                    continue;

                writer.WriteLine("typedef struct {0} {0};", structDef.Name);
            }

            foreach (var functionType in m_Result.FunctionTypes)
            {
                var alias = m_FunctionAliases[functionType];

                writer.Write("typedef ");
                EmitPreType(functionType.ReturnType, writer);
                writer.Write(" {0}(", alias);
                int index = 0;
                foreach (var arg in functionType.Arguments)
                {
                    if (index > 0)
                        writer.Write(", ");
                    EmitPreType(arg.Type, writer);
                    writer.Write(" {0}", arg.Name);
                    EmitPostType(arg.Type, writer);
                    ++index;
                }
                writer.WriteLine(");");
            }

            // Generate structs
            foreach (var structDef in m_Result.Structs)
            {
                if (structDef.WasIncluded)
                    continue;

                writer.WriteLine("struct {0} {{", structDef.Name);

                foreach (var fieldDef in structDef.Fields)
                {
                    EmitField(fieldDef, writer);
                }

                writer.WriteLine("};");
            }

            // Generate static checks
            foreach (var structDef in m_Result.Structs)
            {
                if (structDef.WasIncluded)
                    continue;

                EmitStructStaticChecks(structDef, writer);

                foreach (var fieldDef in structDef.Fields)
                {
                    EmitFieldStaticChecks(structDef, fieldDef, writer);
                }
            }
        }

        private static string ComputeUniqueId(ParseResult result)
        {
            using (var md5 = MD5.Create())
            {
                var uniqueFileId = result.InputFilename.Replace('\\', '/');
                var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(uniqueFileId));
                var buf = new StringBuilder(hashBytes.Length * 2);
                for (int i = 0; i < hashBytes.Length; ++i)
                {
                    buf.AppendFormat("{0:X02}", hashBytes[i]);
                }
                return buf.ToString();
            }
        }

        private void EmitField(FieldDef fieldDef, TextWriter writer)
        {
            writer.Write("    ");
            EmitPreType(fieldDef.Type, writer);
            writer.Write(" {0}", fieldDef.Name);
            EmitPostType(fieldDef.Type, writer);
            writer.WriteLine(";");
        }

        private void EmitPreType(TypeDef fieldDefType, TextWriter writer)
        {
            if (fieldDefType is PrimitiveType)
            {
                EmitPrimitiveType((PrimitiveType)fieldDefType, writer);
            }
            else if (fieldDefType is PointerType)
            {
                EmitPreType(((PointerType)fieldDefType).PointeeType, writer);
                writer.Write('*');
            }
            else if (fieldDefType is ArrayType)
            {
                EmitPreType(((ArrayType)fieldDefType).ElementType, writer);
            }
            else if (fieldDefType is VoidType)
            {
                writer.Write("void");
            }
            else if (fieldDefType is FunctionType)
            {
                writer.Write(m_FunctionAliases[(FunctionType)fieldDefType]);
            }
            else
            {
                writer.Write("{0}", ((StructType)fieldDefType).Name);
            }
        }

        private void EmitPostType(TypeDef fieldDefType, TextWriter writer)
        {
            while (fieldDefType is ArrayType)
            {
                var arrayType = (ArrayType)fieldDefType;
                writer.Write("[{0}]", arrayType.Length);
                fieldDefType = arrayType.ElementType;
            }
        }


        private void EmitPrimitiveType(PrimitiveType fieldDefType, TextWriter writer)
        {
            string label;
            if (!m_PrimitiveMappings.TryGetValue(fieldDefType, out label))
                throw new ApplicationException("unregistered primitive type - program bug");
            writer.Write(label);
        }

        private void EmitStructStaticChecks(StructDef structDef, TextWriter writer)
        {
            writer.WriteLine("_Static_assert(sizeof({0}) == {1}, \"size of struct {0} does not match assembly output\");",
                structDef.Name, structDef.SizeBytes);
        }

        private void EmitFieldStaticChecks(StructDef structDef, FieldDef fieldDef, TextWriter writer)
        {
            writer.WriteLine("_Static_assert(__builtin_offsetof({0}, {1}) == {2}, \"offset of field {0}::{1} does not match assembly output\");",
                structDef.Name, fieldDef.Name, fieldDef.OffsetBytes);
        }
    }
}