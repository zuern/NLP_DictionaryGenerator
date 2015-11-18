using System;

namespace NLP_DictionaryGenerator
{
    /// <summary>
    /// This exception is thrown when a word could not be looked up in the dictionary.
    /// </summary>
    public class DictionaryLookupException : Exception
    {
        public DictionaryLookupException(string message) : base(message) { }
    }
}
