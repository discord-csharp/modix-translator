using System;
using System.Runtime.Serialization;

namespace TranslatorBot9000
{
    [Serializable]
    internal class LanguageNotSupportedException : Exception
    {
        public LanguageNotSupportedException()
        {
        }

        public LanguageNotSupportedException(string? message) : base(message)
        {
        }

        public LanguageNotSupportedException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected LanguageNotSupportedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}