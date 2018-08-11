using System;

namespace BlobCompiler
{
    internal class TypeException : Exception
    {
        public TypeDef TypeDef { get; private set; }

        public TypeException(TypeDef type, string message) : base(message)
        {
            TypeDef = type;
        }
    }
}