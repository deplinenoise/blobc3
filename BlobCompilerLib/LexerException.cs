using System;
using System.Runtime.Serialization;

namespace BlobCompiler
{
    public class LexerException : Exception
    {
        public string Filename { get; private set; }
        public int LineNumber { get; private set; }

        public LexerException(string filename, int lineNumber, string errorText)
        : base(errorText)
        {
            Filename = filename;
            LineNumber = lineNumber;
        }

        public override string ToString()
        {
            return $"{Filename}({LineNumber}): {Message}";
        }
    }
}