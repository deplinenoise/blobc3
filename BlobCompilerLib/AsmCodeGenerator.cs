using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlobCompiler
{
    public class AsmCodeGenerator
    {
        public void GenerateCode(ParseResult result, TextWriter writer, string includePrefix)
        {
            writer.WriteLine("; This file was automatically generated. Do not edit it.");

            foreach (var include in result.Includes)
            {
                var incstr = Path.GetFileNameWithoutExtension(include) + ".i";
                writer.WriteLine("\t\tinclude\t\"{0}{1}\"", includePrefix, incstr);
            }

            writer.WriteLine();

            foreach (var constant in result.ResolvedConstants)
            {
                if (constant.Definition.WasIncluded)
                    continue;

                writer.WriteLine("{0}\t\tEQU {1}", constant.Definition.Name, constant.Value);
            }

            foreach (var structDef in result.Structs)
            {
                if (structDef.WasIncluded)
                    continue;

                foreach (var fieldDef in structDef.Fields)
                {
                    writer.WriteLine("{0}_{1}\t\tEQU {2}", structDef.Name, fieldDef.Name, fieldDef.OffsetBytes);
                }
                writer.WriteLine("{0}_SIZEOF\t\tEQU {1}", structDef.Name, structDef.SizeBytes);
                writer.WriteLine("{0}_ALIGNOF\t\tEQU {1}", structDef.Name, structDef.AlignmentBytes);
            }
        }
    }
}
