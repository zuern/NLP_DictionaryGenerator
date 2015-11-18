using System;
using System.IO;
using System.Xml;
using NLP_DictionaryGenerator.Properties;
using static LogType;

namespace NLP_DictionaryGenerator
{
    /// <summary>
    ///     Hooks into the Merriam-Webster API to look up the categories of words from a word list and append them into a
    ///     specified CSV dictionary file.
    ///     This class also exposes a method to look up a single word's category.
    /// </summary>
    public static class DictionaryGenerator
    {
        /// <summary>
        ///     Traverses the word list and looks up lexical categories for each of the words. Appends an entry into the dictionary
        ///     for each word.
        /// </summary>
        /// <param name="wordListPath">
        ///     The file path (relative to the program's working directory) of the list of words (1 word per
        ///     line).
        /// </param>
        /// <param name="dictionaryPath">
        ///     The file path (relative to the program's working directory) of the dictionary to
        ///     write/append to.
        /// </param>
        /// <param name="logPath">
        ///     The file path (relative to the program's working directory) that the log file will be generated
        ///     at.
        /// </param>
        public static void CreateDictionary(string wordListPath, string dictionaryPath, string logPath)
        {
            _wordListPath = wordListPath;
            _dictionaryPath = dictionaryPath;
            _logPath = logPath;

            Main(new string[] {});
        }

        /// <summary>
        ///     Returns a string in the format "{word}, {category}". Uses the Merriam-Webster API to look up the category.
        /// </summary>
        /// <param name="word">The word to look up the category of.</param>
        /// <returns>A string in the format <c>"{word}, {category}"</c></returns>
        public static string GetDictionaryEntry(string word)
        {
            return $"{word}, {LookupCategory(word, ApiKey, false)}";
        }

        /// <summary>
        ///     Checks the number of API calls that have been made since the last access of the program.
        /// </summary>
        /// <returns>True if program hasn't reached API Call limit yet. Otherwise, returns False.</returns>
        private static bool CanCallApi()
        {
            try
            {
                var lastAccess = Settings.Default.LastAccess;
                var numCalls = Settings.Default.NumAPICallsMadeSinceLastAccess;
                var callLimit = Settings.Default.APICallLimit;


                var testDate = new DateTime(); // Used to see if lastAccess has been initialized or not.

                if (lastAccess.CompareTo(testDate) == 0) // If we haven't accessed the API before
                {
                    return true;
                }
                if (DateTime.Now.DayOfYear > lastAccess.Date.DayOfYear || numCalls < callLimit)
                    return true;

                return false;
            }
            catch (NullReferenceException) // Thrown if LastAccess has not been set in the settings yet.
            {
                Settings.Default.LastAccess = DateTime.Now;
                Settings.Default.Save();
                return true;
            }
        }

        /// <summary>
        ///     Traverses the word list and looks up lexical categories for each of the words. Appends an entry into the dictionary
        ///     for each word.
        /// </summary>
        private static void CreateDictionary()
        {
            // Counts how many words we've looked up so far in this session.
            var numEntriesAdded = 1;

            // If we have enough remaining API calls for the day without going over limit. 
            if (CanCallApi())
            {
                // Load the word list.
                Log(Info, "Loading the word list from <" + _wordListPath + ">.");
                _wordListReader = new StreamReader(_wordListPath);

                // Load the dictionary
                Log(Info, "Loading the dictionary from <" + _dictionaryPath + ">.");
                // Will append to the file instead of overwriting it.
                _dictionaryWriter = new StreamWriter(_dictionaryPath, true) {AutoFlush = true};
                // Write to file after every call to WriteLine.

                while (!_wordListReader.EndOfStream && CanCallApi())
                {
                    var word = _wordListReader.ReadLine();
                    // This line is where the program accesses the Merriam-Webster API
                    var category = LookupCategory(word, ApiKey);
                    // The string that will go in the dictionary.
                    string definition = $"{word}, {category}";

                    // Write the entry into the dictionary.
                    _dictionaryWriter.WriteLine(definition);

                    // Log what we just did.
                    Log(Info, numEntriesAdded + "th entry added: " + definition);

                    // Increment the number of entries added.
                    numEntriesAdded++;
                }

                // If we reached our limit for API Calls...
                if (!CanCallApi())
                {
                    Log(Error, "API Call Limit reached. Cannot call API again until tomorrow. Sorry!");
                    Log(Normal, "API Call Limit is: " + Settings.Default.APICallLimit);

                    // Dumps the remaining words from the word list to a new text file just so we can resume later using only new words
                    // and avoid duplicates in the dictionary.
                    var dumpPath = "remainingWordList.txt";
                    Log(Normal, "Dumping remaining words in word list to: <" + dumpPath + ">");
                    var dumpWriter = new StreamWriter(dumpPath);

                    while (!_wordListReader.EndOfStream)
                    {
                        dumpWriter.WriteLine(_wordListReader.ReadLine());
                    }

                    dumpWriter.Flush();
                    dumpWriter.Close();
                }

                _wordListReader.Close();

                _dictionaryWriter.Flush();
                _dictionaryWriter.Close();
            } // end if (CanCallAPI())
            else
            {
                Log(Error, "API Call Limit reached. Cannot call API again until tomorrow. Sorry!");
                Log(Normal, "API Call Limit is: " + Settings.Default.APICallLimit);
            }
        }

        /// <summary>
        ///     Writes the <c>Message </c> to the Console and to <see cref="_logWriter" />. Note that it prepends the
        ///     <see cref="LogType" /> to the Message.
        /// </summary>
        /// <param name="logType"></param>
        /// <param name="message"></param>
        private static void Log(LogType logType, string message)
        {
            var logPrefix = $"[{logType}]".PadLeft(10);

            string logMessage = $"{logPrefix}: {message}";

            Console.WriteLine(logMessage);
            _logWriter.WriteLine(logMessage);

            if (logType == Error)
                _errorCount++;
        }

        /// <summary>
        ///     Pulls a definition from the Merriam-Webster API and searches for the lexical category of the supplied word.
        /// </summary>
        /// <param name="word">The word to look up the lexical category for.</param>
        /// <param name="apiKey">The secret API key to use.</param>
        /// <param name="recordLog">
        ///     If true, the <see cref="Log(LogType, string)">Log</see> method will be called. If an external
        ///     class is calling, <c>recordLog</c> should be set to false.
        /// </param>
        /// <returns></returns>
        private static string LookupCategory(string word, string apiKey, bool recordLog = true)
        {
            if (CanCallApi()) // If we can call the API without going over our API Call Limit
            {
                string lookupUrl = $"http://www.dictionaryapi.com/api/v1/references/collegiate/xml/{word}?key={apiKey}";
                var d = new XmlDocument();
                d.Load(lookupUrl); // This downloads the actual XML data from Merriam-Webster

                Settings.Default.NumAPICallsMadeSinceLastAccess++; // Increment our API Call counter.
                Settings.Default.LastAccess = DateTime.Now; // Update our Last Access Time
                Settings.Default.Save();

                // Find the node that specifies the category of the word. For multiple definitions, get only the first definition's category.
                // Note that "fl" stands for "Functional Label".
                try
                {
                    var category = d.GetElementsByTagName("fl").Item(0).InnerText;

                    // Sometimes the functional labels are like this:
                    // => "noun plural but singular in construction"
                    // This line will return only "noun"
                    category = category.Split(' ')[0];

                    return category;
                }
                catch (NullReferenceException e) // Word is not in the Merriam-Webster dictionary
                {
                    _errorCount++;
                    if (!recordLog)
                        throw new DictionaryLookupException("Could not find category for \"" + word + "\".");
                    Log(Error, $"Could not find category for \"{word}\".\n Error Message: {e.Message}");

                    throw new DictionaryLookupException("Could not find category for \"" + word + "\".");
                }
            } // End if (CanCallAPI())
            throw new DictionaryLookupException(
                "Can't call the API right now because the API Call Limit has been reached.");
        }

        /// <summary>
        ///     Main method to run program standalone via the console.
        /// </summary>
        /// <param name="args">Program arguments</param>
        private static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                Console.WriteLine(
                    "(FYI): This program doesn't accept any arguments.\nPress any key to continue as usual...");
                Console.ReadKey();
            }

            //
            // START PROGRAM
            //

            Console.WriteLine("Dictionary Generator by Kevin Zuern");
            Console.WriteLine("===================================");
            Console.WriteLine("~~ API Calls Remaining ~~ (For Today): " +
                              (Settings.Default.APICallLimit - Settings.Default.NumAPICallsMadeSinceLastAccess));
            Console.WriteLine();
            Console.WriteLine("Word List:  " + _wordListPath);
            Console.WriteLine("Dictionary: " + _dictionaryPath);
            Console.WriteLine("Log File:   " + _logPath);
            Console.WriteLine();

            Console.WriteLine("Press enter to begin generating the dictionary...");
            Console.ReadLine();

            // Initialize the Log Writer.
            _logWriter = new StreamWriter(_logPath, false);
            Log(Normal, "Program starting up now.");

            try
            {
                CreateDictionary();
                Log(Normal, "Finished dictionary.");
                Log(Normal, "Closed all resources. Program terminating now...");
            }
            catch (Exception ex)
            {
                Log(Error, ex.Message);
                Log(Normal, "Saving dictionary to disk and exiting now.");
            }
            finally
            {
                Log(Normal, "Finished program with " + _errorCount + " error(s).");

                _logWriter.Flush();
                _logWriter.Close();

                _wordListReader.Close();

                _dictionaryWriter.Flush();
                _dictionaryWriter.Close();

                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        #region Class variables

        private static int _errorCount;

        private static readonly string ApiKey = Keys.ApiKey;

        /// <summary>
        ///     The path to the word list relative to the working directory.
        /// </summary>
        private static string _wordListPath = "testWordList.txt";

        /// <summary>
        ///     The path where the dictionary file is/will be created relative to the working directory.
        /// </summary>
        private static string _dictionaryPath = "dict.csv";

        /// <summary>
        ///     If true, the Log method will print any events labeled as "Info".
        /// </summary>
        public static bool VerboseLogFile = true;

        /// <summary>
        ///     Appends the date and time to ensure that the log file is created unique every time.
        /// </summary>
        private static string _logPath = $"log{DateTime.Now.ToFileTime()}.txt";

        private static StreamWriter _logWriter;
        private static StreamReader _wordListReader;
        private static StreamWriter _dictionaryWriter;

        #endregion
    }
}

/// <summary>
///     Describes the type of message that is being logged.
/// </summary>
internal enum LogType
{
    Info,
    Warning,
    Error,
    Normal
}