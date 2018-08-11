using System;
using System.Runtime.Serialization;

namespace BlobCompiler
{
    public class ParseException : Exception
    {
        public Token Token { get; private set; }

        public ParseException(Token tok, string error)
            : base(error)
        {
            Token = tok;
        }
    }
}