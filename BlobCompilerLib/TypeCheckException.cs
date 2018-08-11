using System;
using System.Runtime.Serialization;

namespace BlobCompiler
{
    public class TypeCheckException : Exception
    {
        public Location Location { get; private set; }

        public TypeCheckException()
        {
        }

        public TypeCheckException(string message) : base(message)
        {
        }

        public TypeCheckException(Location location, string message) : base(message)
        {
            Location = location;
        }
    }
}