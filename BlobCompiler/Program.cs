
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BlobCompiler;

namespace BlobCompilerMain
{
    internal class FileSystemAccess : IParserFileAccess
    {
        private List<string> m_IncludePaths;

        public FileSystemAccess(ICollection<string> includePaths)
        {
            m_IncludePaths = new List<string>(includePaths);
        }

        public Lexer OpenFileForLexing(string sourceFile, string path)
        {
            // First search locally
            {
                string targetDir = sourceFile != null ? Path.GetDirectoryName(sourceFile) : "";
                string targetFile = Path.Combine(targetDir, path);
                if (File.Exists(targetFile))
                    return CreateLexer(targetFile);
            }

            // Try include paths
            foreach (var includePath in m_IncludePaths)
            {
                string candidate = Path.Combine(includePath, path);

                if (File.Exists(candidate))
                    return CreateLexer(candidate);
            }

            throw new IOException($"cannot find '{path}' in any include paths");
        }

        private Lexer CreateLexer(string targetFile)
        {
            string text = File.ReadAllText(targetFile, Encoding.UTF8);
            return new Lexer(new StringReader(text), targetFile);
        }
    }

    internal class ProgramArgs
    {
        public List<string> IncludePaths = new List<string>();
        public string InputFile;
        public string OutputFile;
        public string CodeGenerator;

        public static readonly string[] kValidGenerators =
            {"asm68k", "cheader"};

        public static string ValidGeneratorString()
        {
            var b = new StringBuilder(128);
            foreach (var g in kValidGenerators)
            {
                if (b.Length > 0)
                    b.Append(", ");
                b.Append(g);
            }
            return b.ToString();
        }

        public ProgramArgs(string[] args)
        {
            for (int i = 0; i < args.Length; ++i)
            {
                string arg = args[i];
                string nextArg = (i + 1) < args.Length ? args[i + 1] : String.Empty;
                if (arg.StartsWith("-"))
                {
                    switch (arg[1])
                    {
                        case 'I':
                            IncludePaths.Add(nextArg);
                            ++i;
                            break;
                        case 'o':
                            OutputFile = nextArg;
                            ++i;
                            break;
                        case 'g':
                            CodeGenerator = nextArg;
                            ++i;
                            break;
                        default:
                            throw new IOException($"unsupported option {arg}");
                    }
                }
                else
                {
                    if (!String.IsNullOrEmpty(InputFile))
                        throw new IOException("only one input file allowed");
                    InputFile = arg;
                }
            }

            if (String.IsNullOrEmpty(InputFile) ||
                String.IsNullOrEmpty(OutputFile) ||
                String.IsNullOrEmpty(CodeGenerator))
                throw new IOException("insufficient arguments provided");

            if (Array.IndexOf(kValidGenerators, CodeGenerator) == -1)
            {
                throw new IOException($"unknown code generator {CodeGenerator} - use one of {ValidGeneratorString()}");
            }
        }
    }

    internal class Program
    {
        public static int Main(string[] args)
        {
            ProgramArgs options;
            try
            {
                options = new ProgramArgs(args);
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                Usage();
                return 1;
            }

            try
            {

                var access = new FileSystemAccess(options.IncludePaths);

                var parser = new Parser(access);

                var parseResult = parser.Parse(options.InputFile);

                Compiler.Resolve(parseResult);

                using (var writer = new StringWriter())
                {
                    switch (options.CodeGenerator)
                    {
                        case "asm68k":
                            new AsmCodeGenerator().GenerateCode(parseResult, writer);
                            break;
                        case "cheader":
                            new CHeaderGenerator(parseResult).GenerateCode(writer);
                            break;
                    }

                    writer.Flush();

                    byte[] result = Encoding.UTF8.GetBytes(writer.ToString());
                    File.WriteAllBytes(options.OutputFile, result);
                }
            }
            catch (LexerException ex)
            {
                Console.Error.WriteLine($"{ex.Filename}({ex.LineNumber}): {ex.Message}");
                return 1;
            }
            catch (ParseException ex)
            {
                Console.Error.WriteLine($"{ex.Token.Location.Filename}({ex.Token.Location.LineNumber}): {ex.Message}");
                return 1;
            }
            catch (TypeCheckException ex)
            {
                Console.Error.WriteLine($"{ex.Location.Filename}({ex.Location.LineNumber}): {ex.Message}");
                return 1;
            }

            return 0;
        }

        private static void Usage()
        {
            Console.Error.WriteLine($"BlobCompilerMain -o outputfile -g {{ {ProgramArgs.ValidGeneratorString()} }} [-I include-path ...] inputfile");
        }
    }
}