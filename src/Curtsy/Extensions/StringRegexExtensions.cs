// Uncomment the next line depending on your environment (Mono, Visual Studio 2008, .NET 3.5)
// #define NET35

// Use .NET 4.0 to enable thread safe cacheing.


// Copyright (c)2011 Brandon Croft and contributors

using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Text.RegularExpressions;
#if !NET35
using System.Collections.Concurrent;
#endif

namespace System
{
    /// <summary>
    /// A simplified collection of regex groups and captures.
    /// </summary>
    /// <example>
    /// var matches = "John Wilkes Booth".MatchesPattern(@"(?&lt;firstname&gt;\w+)\s(\w+)\s(?&lt;lastname&gt;\w+)");
    /// Assert.AreEqual("John Wilkes Booth", matches[0]);
    /// Assert.AreEqual("John", matches["firstname"]);
    /// Assert.AreEqual("Wilkes", matches[1]);
    /// Assert.AreEqual("Booth", matches["lastname"]);
    /// Assert.AreEqual(4, matches.Count);
    /// </example>
    /// <remarks>
    /// See http://stackoverflow.com/questions/2250335/differences-among-net-capture-group-match/2251774#2251774 for a good
    /// explanation of why MatchCollection is so complicated.
    /// </remarks>
    public class MatchData : IEnumerable<string>
    {
        List<Capture> indexcaptures = new List<Capture>();
        Dictionary<string, Capture> namedcaptures = null;

        /// <summary>
        /// Gets a numbered capture value. The first index (0) is always the entire match.
        /// </summary>
        /// <param name="index">The index of the numbered capture</param>
        /// <returns>The value of the specified numbered capture</returns>
        public string this[int index]
        {
            get
            {
                try
                {
                    return indexcaptures[index].Value;
                } catch (ArgumentOutOfRangeException ex)
                {
                    throw new IndexOutOfRangeException(String.Format("The index {0} was out of range", index), ex);
                }
            }
        }

        /// <summary>
        /// Gets a named capture value.
        /// </summary>
        /// <param name="name">The name of the named capture</param>
        /// <returns>The value of the specified named capture</returns>
        public string this[string name]
        {
            get
            {
                Capture result = null;
                if (namedcaptures == null || !namedcaptures.TryGetValue(name, out result))
                    return null;

                return result.Value;
            }
        }

        /// <summary>
        /// Gets the position of the specifed numbered capture
        /// </summary>
        /// <param name="index">The index of the numbered capture</param>
        /// <returns>The position of the capture</returns>
        public int Begin(int index)
        {
            try
            {
                Capture cap = indexcaptures[index];
                return cap.Index;
            } catch (ArgumentOutOfRangeException ex)
            {
                throw new IndexOutOfRangeException(String.Format("The index {0} was out of range", index), ex);
            }
        }

        /// <summary>
        /// Gets the position of the character immediately following the end of the specified numbered capture
        /// </summary>
        /// <param name="index">The index of the numbered capture</param>
        /// <returns>The position of the character immediately following the end of the specified numbered capture</returns>
        public int End(int index)
        {
            try
            {
                Capture cap = indexcaptures[index];
                return cap.Index + cap.Length;
            } catch (ArgumentOutOfRangeException ex)
            {
                throw new IndexOutOfRangeException(String.Format("The index {0} was out of range", index), ex);
            }
        }

        /// <summary>
        /// Gets the position of the named capture
        /// </summary>
        /// <param name="name">The name of the named capture</param>
        /// <returns>The position of the capture</returns>
        public int Begin(string name)
        {
            Capture cap;
            if(namedcaptures == null || !namedcaptures.TryGetValue(name, out cap))
                return -1;
            
            return cap.Index;
        }

        /// <summary>
        /// Gets the position of the character immediately following the end of the specified named capture
        /// </summary>
        /// <param name="index">The index of the named capture</param>
        /// <returns>The position of the character immediately following the end of the specified named capture</returns>
        public int End(string name)
        {
            Capture cap;
            if (namedcaptures == null || !namedcaptures.TryGetValue(name, out cap))
                return -1;

            return cap.Index + cap.Length;
        }

        /// <summary>
        /// Gets the total number of captures
        /// </summary>
        public int Count
        {
            get { return indexcaptures.Count + (namedcaptures == null ? 0 : namedcaptures.Count); }
        }

        /// <summary>
        /// Gets the total number of numbered captures
        /// </summary>
        public int MatchCount
        {
            get { return indexcaptures.Count; }
        }

        /// <summary>
        /// Retrieves an array of capture names from the matches.
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public string[] GetNames()
        {
            if(namedcaptures == null)
                return new string[0];

            return this.namedcaptures.Keys.ToArray();
        }

        void AddMatch(Regex regex, Match match)
        {
            if (!match.Success)
                return;

            for (int index = 0; index < match.Groups.Count; index++)
            {
                Group group = match.Groups[index];
                if (group.Captures.Count == 0)
                {
                    continue;
                }

                string name = regex.GroupNameFromNumber(index);
                int tryint;

                // We only record the LAST capture in this group. This simplifies matching so
                // that multiple captures in the same group are overwritten.
                if (Int32.TryParse(name, out tryint))
                {
                    this.indexcaptures.Add(group.Captures[group.Captures.Count - 1]);
                }
                else
                {
                    if (namedcaptures == null)
                        namedcaptures = new Dictionary<string, Capture>();

                    this.namedcaptures[name] = group.Captures[group.Captures.Count - 1];
                }
            }
        }

        IEnumerable<Capture> GetCaptures()
        {
            // First return numbered capture values...
            foreach (Capture capture in indexcaptures)
            {
                yield return capture;
            }

            // ...then return named captures
            if (namedcaptures != null)
            {
                foreach (KeyValuePair<string, Capture> capture in namedcaptures)
                {
                    yield return capture.Value;
                }
            }
        }

        IEnumerator<string> GetEnumeratorInternal()
        {
            foreach (Capture cap in GetCaptures())
            {
                yield return cap.Value;
            }
        }

        public IEnumerator<string> GetEnumerator()
        {
            return GetEnumeratorInternal();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumeratorInternal();
        }

        public MatchData(Regex regex, MatchCollection matches)
        {
            if (matches == null || matches.Count == 0)
                return;

            foreach (Match match in matches)
            {
                AddMatch(regex, match);
            }
        }

        public MatchData(Regex regex, Match match)
        {
            AddMatch(regex, match);
        }
    }

    public static class StringRegexExtensions
    {
        static readonly Dictionary<char, RegexOptions> _optionChars = new Dictionary<char, RegexOptions> {
            { 'i', RegexOptions.IgnoreCase },
            { 's', RegexOptions.Singleline },
            { 'm', RegexOptions.Multiline },
            { 'x', RegexOptions.IgnorePatternWhitespace },
            { 'c', RegexOptions.Compiled },
            { 'r', RegexOptions.RightToLeft }
        };

#if !NET35
        static readonly ConcurrentDictionary<Tuple<string, RegexOptions>, Regex> _cache = new ConcurrentDictionary<Tuple<string, RegexOptions>, Regex>();

        static Tuple<string, RegexOptions> MakeCacheKey(string pattern, RegexOptions opt)
        {
            return new Tuple<string, RegexOptions>(pattern, opt);
        }

        static Tuple<string, RegexOptions> MakeCacheKey(string pattern)
        {
            return new Tuple<string, RegexOptions>(pattern, RegexOptions.None);
        }

        static Regex ToRegex(this string pattern)
        {
            return _cache.GetOrAdd(MakeCacheKey(pattern), p =>
            {
                return new Regex(pattern);
            });
        }

        static Regex ToRegex(this string pattern, RegexOptions options)
        {
            return _cache.GetOrAdd(MakeCacheKey(pattern, options), p =>
            {
                return new Regex(pattern, options);
            });
        }

        /// <summary>
        /// The number of Regex objects that occupy the cache.
        /// </summary>
        public static int CacheCount
        {
            get
            {
                return _cache.Count;
            }
        }
#else
        static Regex ToRegex(this string pattern)
        {
            return new Regex(pattern);
        }

        static Regex ToRegex(this string pattern, RegexOptions options)
        {
            return new Regex(pattern, options);
        }
#endif

        static RegexOptions GetOptions(string options)
        {
            if (String.IsNullOrEmpty(options))
                return RegexOptions.None;

            return (RegexOptions)options.Select(c => {
                try { return (int)_optionChars[c]; } catch (KeyNotFoundException) { return 0; }
            }).Sum();
        }

        /// <summary>
        /// Tests whether this string matches a string regex pattern
        /// </summary>
        /// <param name="pattern">The regex pattern to test against this string</param>
        /// <returns></returns>
        public static bool HasPattern(this string input, string pattern)
        {
            return HasPattern(input, pattern, null);
        }

        /// <summary>
        /// Tests whether this string matches a string regex pattern with optional regex options.
        /// </summary>
        /// <param name="pattern">The regex pattern to test against this string</param>
        /// <param name="options">Combine any characters -- i: ignore case, s: single line mode (period [.] matches newlines), m: multiline mode (^ and $ match lines), x: ignore whitespace, c: compiled, r: right to left</param>
        public static bool HasPattern(this string input, string pattern, string options)
        {
            return pattern.ToRegex(GetOptions(options)).IsMatch(input);
        }

        /// <summary>
        /// Tests whether this string matches a string regex pattern
        /// </summary>
        /// <param name="pattern">The regex pattern to test against this string</param>
        public static bool HasPattern(this string input, string pattern, int startat)
        {
            return HasPattern(input, pattern, null, startat);
        }

        /// <summary>
        /// Tests whether this string matches a string regex pattern
        /// </summary>
        /// <param name="pattern">The regex pattern to test against this string</param>
        /// <param name="options">Combine any characters -- i: ignore case, s: single line mode (period [.] matches newlines), m: multiline mode (^ and $ match lines), x: ignore whitespace, c: compiled, r: right to left</param>
        public static bool HasPattern(this string input, string pattern, string options, int startat)
        {
            return pattern.ToRegex(GetOptions(options)).IsMatch(input, startat);
        }

        /// <summary>
        /// Returns matches within the string that match a specified regex pattern
        /// </summary>
        /// <param name="pattern">The regex pattern to match against this string</param>
        /// <returns>The <see cref="MatchData"/> associated with the pattern match.</returns>
        public static MatchData MatchesPattern(this string input, string pattern)
        {
            return MatchesPattern(input, pattern, null);
        }

        /// <summary>
        /// Returns matches within the string that match a specified regex pattern with the specified regex options
        /// </summary>
        /// <param name="pattern">The regex pattern to match against this string</param>
        /// <param name="options">Combine any characters -- i: ignore case, s: single line mode (period [.] matches newlines), m: multiline mode (^ and $ match lines), x: ignore whitespace, c: compiled, r: right to left</param>
        /// <returns>The <see cref="MatchData"/> associated with the pattern match.</returns>
        public static MatchData MatchesPattern(this string input, string pattern, string options)
        {
            var re = pattern.ToRegex(GetOptions(options));
            return new MatchData(re, re.Matches(input));
        }

        /// <summary>
        /// Returns matches within the string that match a specified regex pattern beginning at the specified offset
        /// </summary>
        /// <param name="pattern">The regex pattern to match against this string</param>
        /// <param name="startat">The offset at which to begin matching</param>
        /// <returns>The <see cref="MatchData"/> associated with the pattern match.</returns>
        public static MatchData MatchesPattern(this string input, string pattern, int startat)
        {
            return MatchesPattern(input, pattern, null, startat);
        }

        /// <summary>
        /// Returns matches within the string that match a specified regex pattern beginning at the specified offset with the specified regex options
        /// </summary>
        /// <param name="pattern">The regex pattern to match against this string</param>
        /// <param name="startat">The offset at which to begin matching</param>
        /// <param name="options">Combine any characters -- i: ignore case, s: single line mode (period [.] matches newlines), m: multiline mode (^ and $ match lines), x: ignore whitespace, c: compiled, r: right to left</param>
        /// <returns>The <see cref="MatchData"/> associated with the pattern match.</returns>
        public static MatchData MatchesPattern(this string input, string pattern, string options, int startat)
        {
            var re = pattern.ToRegex(GetOptions(options));
            return new MatchData(re, re.Matches(input, startat));
        }

        /// <summary>
        /// Returns a copy of this string with the first occurrence of the specified regex pattern replaced with the specified replacement text
        /// </summary>
        /// <param name="pattern">The regex pattern to find within this string</param>
        /// <param name="replacement">The text replacement to use</param>
        /// <returns>A copy of this string with specified pattern replaced</returns>
        public static string Sub(this string input, string pattern, string replacement)
        {
            return Sub(input, pattern, null, replacement);
        }

        /// <summary>
        /// Returns a copy of this string with the first occurrence of the specified regex pattern replaced with the specified replacement text and options
        /// </summary>
        /// <param name="pattern">The regex pattern to find within this string</param>
        /// <param name="replacement">The text replacement to use</param>
        /// <param name="options">Combine any characters -- i: ignore case, s: single line mode (period [.] matches newlines), m: multiline mode (^ and $ match lines), x: ignore whitespace, c: compiled, r: right to left</param>
        /// <returns>A copy of this string with specified pattern replaced</returns>
        public static string Sub(this string input, string pattern, string options, string replacement)
        {
            return pattern.ToRegex(GetOptions(options)).Replace(input, replacement, 1);
        }

        /// <summary>
        /// Returns a copy of this string with the first occurrence of the specified regex pattern replaced with the specified replacement text
        /// </summary>
        /// <param name="pattern">The regex pattern to find within this string</param>
        /// <param name="replacement">The text replacement to use</param>
        /// <returns>A copy of this string with specified pattern replaced</returns>
        public static string Sub(this string input, string pattern, Func<Match, string> evaluator)
        {
            return Sub(input, pattern, null, evaluator);
        }

        /// <summary>
        /// Returns a copy of this string with the first occurrence of the specified regex pattern replaced with the specified replacement text and options
        /// </summary>
        /// <param name="pattern">The regex pattern to find within this string</param>
        /// <param name="replacement">The text replacement to use</param>
        /// <param name="options">Combine any characters -- i: ignore case, s: single line mode (period [.] matches newlines), m: multiline mode (^ and $ match lines), x: ignore whitespace, c: compiled, r: right to left</param>
        /// <returns>A copy of this string with specified pattern replaced</returns>
        public static string Sub(this string input, string pattern, string options, Func<Match, string> evaluator)
        {
            return pattern.ToRegex(GetOptions(options)).Replace(input, delegate(Match arg) { return evaluator(arg); }, 1);
        }

        /// <summary>
        /// Returns a copy of this string with all occurrences of the specified regex pattern replaced with the specified replacement text
        /// </summary>
        /// <param name="pattern">The regex pattern to find within this string</param>
        /// <param name="replacement">The text replacement to use</param>
        /// <returns>A copy of this string with specified pattern replaced</returns>
        public static string GSub(this string input, string pattern, string replacement)
        {
            return GSub(input, pattern, null, replacement);
        }

        /// <summary>
        /// Returns a copy of this string with all occurrences of the specified regex pattern and options replaced with the specified replacement text
        /// </summary>
        /// <param name="pattern">The regex pattern to find within this string</param>
        /// <param name="replacement">The text replacement to use</param>
        /// <param name="options">Combine any characters -- i: ignore case, s: single line mode (period [.] matches newlines), m: multiline mode (^ and $ match lines), x: ignore whitespace, c: compiled, r: right to left</param>
        /// <returns>A copy of this string with specified pattern replaced</returns>
        public static string GSub(this string input, string pattern, string options, string replacement)
        {
            return pattern.ToRegex(GetOptions(options)).Replace(input, replacement);
        }

        /// <summary>
        /// Returns a copy of this string with all occurrences of the specified regex pattern replaced with the text returned from the given function
        /// </summary>
        /// <param name="pattern">The regex pattern to match</param>
        /// <param name="evaluator">A function that returns either the replacement text or the original string</param>
        /// <returns>A copy of this string with specified pattern replaced</returns>
        public static string GSub(this string input, string pattern, Func<Match, string> evaluator)
        {
            return GSub(input, pattern, null, evaluator);
        }

        /// <summary>
        /// Returns a copy of this string with all occurrences of the specified regex pattern and options replaced with the text returned from the given function
        /// </summary>
        /// <param name="pattern">The regex pattern to match</param>
        /// <param name="evaluator">A function that returns either the replacement text or the original string</param>
        /// <param name="options">Combine any characters -- i: ignore case, s: single line mode (period [.] matches newlines), m: multiline mode (^ and $ match lines), x: ignore whitespace, c: compiled, r: right to left</param>
        /// <returns>A copy of this string with specified pattern replaced</returns>
        public static string GSub(this string input, string pattern, string options, Func<Match, string> evaluator)
        {
            return pattern.ToRegex(GetOptions(options)).Replace(input, delegate(Match arg) { return evaluator(arg); });
        }

        /// <summary>
        /// Returns the first index of the specified regex pattern
        /// </summary>
        /// <param name="pattern">The regex pattern to find within this string</param>
        /// <returns>The offset of the first match</returns>
        public static int IndexOfPattern(this string input, string pattern)
        {
            return IndexOfPattern(input, pattern, null);
        }

        /// <summary>
        /// Returns the first index of the specified regex pattern and options
        /// </summary>
        /// <param name="pattern">The regex pattern to find within this string</param>
        /// <param name="options">Combine any characters -- i: ignore case, s: single line mode (period [.] matches newlines), m: multiline mode (^ and $ match lines), x: ignore whitespace, c: compiled, r: right to left</param>
        /// <returns>The offset of the first match</returns>
        public static int IndexOfPattern(this string input, string pattern, string options)
        {
            return input.MatchPattern(pattern, options).Begin(0);
        }

        /// <summary>
        /// Returns the first index of the specified regex pattern after the specified position
        /// </summary>
        /// <param name="pattern">The regex pattern to find within this string</param>
        /// <param name="startat">The string position from which to begin</param>
        /// <returns>The offset of the first match</returns>
        public static int IndexOfPattern(this string input, string pattern, int startat)
        {
            return IndexOfPattern(input, pattern, null, startat);
        }

        /// <summary>
        /// Returns the first index of the specified regex pattern and options after the specified position
        /// </summary>
        /// <param name="pattern">The regex pattern to find within this string</param>
        /// <param name="options">Combine any characters -- i: ignore case, s: single line mode (period [.] matches newlines), m: multiline mode (^ and $ match lines), x: ignore whitespace, c: compiled, r: right to left</param>
        /// <param name="startat">The string position from which to begin</param>
        /// <returns>The offset of the first match</returns>
        public static int IndexOfPattern(this string input, string pattern, string options, int startat)
        {
            return input.MatchPattern(pattern, options, startat).Begin(0);
        }

        /// <summary>
        /// Returns the last index of the specified regex pattern
        /// </summary>
        /// <param name="pattern">The regex pattern to find within this string</param>
        /// <returns>The offset of the last match</returns>
        public static int LastIndexOfPattern(this string input, string pattern)
        {
            return LastIndexOfPattern(input, pattern, null);
        }

        /// <summary>
        /// Returns the last index of the specified regex pattern
        /// </summary>
        /// <param name="pattern">The regex pattern to find within this string</param>
        /// <param name="options">Combine any characters -- i: ignore case, s: single line mode (period [.] matches newlines), m: multiline mode (^ and $ match lines), x: ignore whitespace, c: compiled, r: right to left</param>
        /// <returns>The offset of the last match</returns>
        public static int LastIndexOfPattern(this string input, string pattern, string options)
        {
            var m = input.MatchesPattern(pattern, options);
            return m.Begin(m.MatchCount - 1);
        }

        /// <summary>
        /// Return the value of the first match of the specified regex pattern
        /// </summary>
        /// <param name="pattern">The regex pattern to find within this string</param>
        /// <returns>The first value matching the specified pattern</returns>
        public static string FindPattern(this string input, string pattern)
        {
            return input.MatchPattern(pattern).First();
        }

        /// <summary>
        /// Return the value of the first match of the specified regex pattern
        /// </summary>
        /// <param name="pattern">The regex pattern to find within this string</param>
        /// <param name="options">Combine any characters -- i: ignore case, s: single line mode (period [.] matches newlines), m: multiline mode (^ and $ match lines), x: ignore whitespace, c: compiled, r: right to left</param>
        /// <returns>The first value matching the specified pattern</returns>
        public static string FindPattern(this string input, string pattern, string options)
        {
            return input.MatchPattern(pattern, options).First();
        }

        /// <summary>
        /// Return the value of the specified numbered capture of the first match of the specified regex pattern
        /// </summary>
        /// <param name="pattern">The regex pattern to find within this string</param>
        /// <param name="capture">The index of the capture to return</param>
        /// <returns>The first value matching the specified pattern or null if the pattern is not found.</returns>
        public static string FindPattern(this string input, string pattern, int capture)
        {
            return FindPattern(input, pattern, null, capture);
        }

        /// <summary>
        /// Return the value of the specified numbered capture of the first match of the specified regex pattern
        /// </summary>
        /// <param name="pattern">The regex pattern to find within this string</param>
        /// <param name="options">Combine any characters -- i: ignore case, s: single line mode (period [.] matches newlines), m: multiline mode (^ and $ match lines), x: ignore whitespace, c: compiled, r: right to left</param>
        /// <param name="capture">The index of the capture to return</param>
        /// <returns>The first value matching the specified pattern or null if the pattern is not found.</returns>
        public static string FindPattern(this string input, string pattern, string options, int capture)
        {
            try { return input.MatchPattern(pattern, options)[capture]; } catch (IndexOutOfRangeException) { return null; }
        }

        /// <summary>
        /// Partition this string into the head, match value, and tail according to the specified regex value
        /// </summary>
        /// <param name="pattern">The regex pattern to use when partitioning this string</param>
        /// <returns>A string array with 3 elements, containing the partitioned string</returns>
        public static string[] Partition(this string input, string pattern)
        {
            return Partition(input, pattern, null);
        }

        /// <summary>
        /// Partition this string into the head, match value, and tail according to the specified regex value
        /// </summary>
        /// <param name="pattern">The regex pattern to use when partitioning this string</param>
        /// <param name="options">Combine any characters -- i: ignore case, s: single line mode (period [.] matches newlines), m: multiline mode (^ and $ match lines), x: ignore whitespace, c: compiled, r: right to left</param>
        /// <returns>A string array with 3 elements, containing the partitioned string</returns>
        public static string[] Partition(this string input, string pattern, string options)
        {
            var m = input.MatchPattern(pattern, options);
            if (m.Count == 0)
                return new string[] { String.Empty, String.Empty, input };

            return new string[] { input.Substring(0, m.Begin(0)), m.First(), input.Substring(m.End(0)) };
        }

        /// <summary>
        /// Scans this string for the specified pattern, and calls the specified function for each match.
        /// </summary>
        /// <param name="pattern">The regex pattern to find within this string</param>
        /// <param name="each">The function to call for each match.</param>
        /// <returns></returns>
        public static void Scan(this string input, string pattern, Action<string> each)
        {
            input.Scan(pattern, null, each);
        }

        /// <summary>
        /// Scans this string for the specified pattern which can contains two capturing groups or a group with one or more subgroups. Each time the pattern is encountered, the specified function is invoked with the first two captures.
        /// </summary>
        /// <param name="pattern">The regex pattern to find within the string</param>
        /// <param name="each">The function to call for each maching group.</param>
        public static void Scan(this string input, string pattern, Action<string, string> each)
        {
            input.Scan(pattern, null, each);
        }

        /// <summary>
        /// Scans this string for the specified pattern which can contain three capturing groups or a group with one or more subgroups. Each time the pattern is encountered, the specified function is invoked with the first three captures specified.
        /// </summary>
        /// <param name="pattern">The regex pattern to find within the string</param>
        /// <param name="each">The function to call for each maching group.</param>
        public static void Scan(this string input, string pattern, Action<string, string, string> each)
        {
            input.Scan(pattern, null, each);
        }

        /// <summary>
        /// Scans this string for the specified pattern which can contain four capturing groups or a group with one or more subgroups. Each time the pattern is encountered, the specified function is invoked with the first four captures specified.
        /// </summary>
        /// <param name="pattern">The regex pattern to find within the string</param>
        /// <param name="each">The function to call for each maching group.</param>
        public static void Scan(this string input, string pattern, Action<string, string, string, string> each)
        {
            input.Scan(pattern, null, each);
        }

        /// <summary>
        /// Scans this string for the specified pattern and options, and calls the specified function for each match.
        /// </summary>
        /// <param name="pattern">The regex pattern to find within this string</param>
        /// <param name="options">Combine any characters -- i: ignore case, s: single line mode (period [.] matches newlines), m: multiline mode (^ and $ match lines), x: ignore whitespace, c: compiled, r: right to left</param>
        /// <param name="each">The function to call for each match.</param>
        /// <returns></returns>
        public static void Scan(this string input, string pattern, string options, Action<string> each)
        {
            var matches = pattern.ToRegex(GetOptions(options)).Matches(input);
            foreach (Match match in matches)
            {
                each(match.Groups.Count > 1 ? match.Groups[1].Value : match.Value);
            }
        }

        /// <summary>
        /// Scans this string for the specified pattern and options which can contains two capturing groups or a group with one or more subgroups. Each time the pattern is encountered, the specified function is invoked with the first two captures.
        /// </summary>
        /// <param name="pattern">The regex pattern to find within the string</param>
        /// <param name="options">Combine any characters -- i: ignore case, s: single line mode (period [.] matches newlines), m: multiline mode (^ and $ match lines), x: ignore whitespace, c: compiled, r: right to left</param>
        /// <param name="each">The function to call for each maching group.</param>
        public static void Scan(this string input, string pattern, string options, Action<string, string> each)
        {
            var matches = pattern.ToRegex(GetOptions(options)).Matches(input);
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 2)
                    each(match.Groups[1].Value, match.Groups[2].Value);
                else
                    each(match.Value, null);
            }
        }

        /// <summary>
        /// Scans this string for the specified pattern and options which can contain three capturing groups or a group with one or more subgroups. Each time the pattern is encountered, the specified function is invoked with the first three captures specified.
        /// </summary>
        /// <param name="pattern">The regex pattern to find within the string</param>
        /// <param name="options">Combine any characters -- i: ignore case, s: single line mode (period [.] matches newlines), m: multiline mode (^ and $ match lines), x: ignore whitespace, c: compiled, r: right to left</param>
        /// <param name="each">The function to call for each maching group.</param>
        public static void Scan(this string input, string pattern, string options, Action<string, string, string> each)
        {
            var matches = pattern.ToRegex(GetOptions(options)).Matches(input);
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 3)
                    each(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
                else if (match.Groups.Count > 2)
                    each(match.Groups[1].Value, match.Groups[2].Value, null);
                else
                    each(match.Value, null, null);
            }
        }

        /// <summary>
        /// Scans this string for the specified pattern and options which can contain four capturing groups or a group with one or more subgroups. Each time the pattern is encountered, the specified function is invoked with the first four captures specified.
        /// </summary>
        /// <param name="pattern">The regex pattern to find within the string</param>
        /// <param name="options">Combine any characters -- i: ignore case, s: single line mode (period [.] matches newlines), m: multiline mode (^ and $ match lines), x: ignore whitespace, c: compiled, r: right to left</param>
        /// <param name="each">The function to call for each maching group.</param>
        public static void Scan(this string input, string pattern, string options, Action<string, string, string, string> each)
        {
            var matches = pattern.ToRegex(GetOptions(options)).Matches(input);
            foreach (Match match in matches)
            {
                if(match.Groups.Count > 4)
                    each(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, match.Groups[4].Value);
                else if (match.Groups.Count > 3)
                    each(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, null);
                else if (match.Groups.Count > 2)
                    each(match.Groups[1].Value, match.Groups[2].Value, null, null);
                else
                    each(match.Value, null, null, null);
            }
        }

        // Single match, used internally
        static MatchData MatchPattern(this string input, string pattern)
        {
            return MatchPattern(input, pattern, null);
        }

        static MatchData MatchPattern(this string input, string pattern, string options)
        {
            var re = pattern.ToRegex(GetOptions(options));
            return new MatchData(re, re.Match(input));
        }

        static MatchData MatchPattern(this string input, string pattern, int startat)
        {
            return MatchPattern(input, pattern, null, startat);
        }

        static MatchData MatchPattern(this string input, string pattern, string options, int startat)
        {
            var re = pattern.ToRegex(GetOptions(options));
            return new MatchData(re, re.Match(input, startat));
        }
    }
}
