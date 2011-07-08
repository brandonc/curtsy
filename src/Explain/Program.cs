#if DEBUG
#define VERBOSE
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Reflection;
using System.Security.Policy;

namespace Explain
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                PrintUsage();
                return -1;
            }

            if (!File.Exists(args[0]))
            {
                Console.WriteLine("explain: file not found");
                return -2;
            }

            switch (Path.GetExtension(args[0]).ToLower())
            {
                case ".cs":
                    Explain(new string[] { args[0] }, @"C:\Documents and Settings\BSC\src\explain\src\Explain\bin\Debug\explain.exe");
                    break;
                default:
                    // Assume MSBuild
                    Explain(ProbeMSBuild(args[0]), @"C:\Documents and Settings\BSC\src\explain\src\Explain\bin\Debug\explain.exe");
                    break;
            }

#if DEBUG
            Console.Write("Press enter to quit.");
            Console.ReadLine();
#endif
            return 0;
        }

        static IEnumerable<string> ProbeMSBuild(string projfile)
        {
            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(projfile);
            }
            catch (XmlException)
            {
                Console.WriteLine("explain: {0} is not an msbuild file.", Path.GetFileName(projfile));
                Environment.Exit(-3);
            }

            
            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            string ns = "";

            if (doc.DocumentElement.Attributes["xmlns"] != null)
            {
                ns = "msbuild:";
                string xmlns = doc.DocumentElement.Attributes["xmlns"].Value;
                nsmgr.AddNamespace("msbuild", xmlns);
            }

            foreach (XmlNode node in doc.SelectNodes(String.Format("//{0}Compile[@Include]", ns), nsmgr))
            {
                yield return Path.Combine(Path.GetDirectoryName(projfile), node.Attributes["Include"].Value);
            }
        }

        class FileContext
        {
            public string Line;
            public int ScopeDepth;
            public List<string> Namespace;
            public string MultilineComment;

            public bool EOF
            {
                get { return this.Line == null; }
            }
        }

        class Tokenizer
        {
            public const char CHAR_LITERAL = '\'';
            public const char STRING_LITERAL = '\"';
            public const char BLOCK_BEGIN = '{';
            public const char BLOCK_END = '}';
            public const char FORWARDSLASH = '/';
            public const char CR = '\r';
            public const char LF = '\n';
            public const char SPLAT = '*';

            public static Queue<string> Tokenize(StreamReader reader)
            {
                var token = new StringBuilder();
                var result = new Queue<string>(512);

                Action pushtoken = new Action(() =>
                {
                    if (token.Length > 0)
                    {
                        result.Enqueue(token.ToString());
                        token.Clear();
                    }
                });

                bool inwhitespace = true;
                while(reader.Peek() >= 0) {
                    char c = (char)reader.Read();

                    if (c == CR || c == LF)
                    {
                        pushtoken();
                        token.Append(c);
                        if ((char)reader.Peek() == LF)
                            token.Append((char)reader.Read());

                        inwhitespace = true;
                        continue;
                    }
                    else if (Char.IsWhiteSpace(c))
                    {
                        if (!inwhitespace)
                            pushtoken();

                        token.Append(c);
                        inwhitespace = true;
                        continue;
                    }
                    else
                    {
                        if (inwhitespace)
                            pushtoken();

                        inwhitespace = false;
                    }

                    if (c == FORWARDSLASH &&
                        ((char)reader.Peek() == FORWARDSLASH || (char)reader.Peek() == SPLAT))
                    {
                        // Comment begin
                        pushtoken();
                        token.Append(c);
                        token.Append((char)reader.Read());
                        pushtoken();
                    }

                    if (c == SPLAT && (char)reader.Peek() == FORWARDSLASH)
                    {
                        // Comment end
                        pushtoken();
                        token.Append(c);
                        token.Append((char)reader.Read());
                        pushtoken();
                    }

                    if (c == BLOCK_BEGIN ||
                        c == BLOCK_END ||
                        c == CHAR_LITERAL ||
                        c == STRING_LITERAL)
                    {
                        pushtoken();
                        token.Append(c);
                        pushtoken();
                        continue;
                    }

                    token.Append(c);
                }

                return result;
            }
        }

        static void Explain(IEnumerable<string> files, string assembly)
        {
            Assembly ass = assembly == null ? null : Assembly.LoadFile(assembly);

            Verbose("Assembly loaded? ", (ass != null));

            foreach (string codefile in files)
            {
                if (Path.GetFileNameWithoutExtension(codefile) == "StringRegexExtensions")
                    continue;

                Verbose("Open: {0}", codefile);

                Queue<string> tokens = null;
                using (StreamReader reader = new StreamReader(codefile))
                {
                    tokens = Tokenizer.Tokenize(reader);
                }

                Verbose("Close: {0}", codefile);
            }
        }

        static void Verbose(string str, params object[] p)
        {
#if VERBOSE
            Console.WriteLine("[debug]: {0}", String.Format(str, p));
#endif
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: explain <file>");
            Console.WriteLine("<file> can be a .cs, .vb, .csproj, or .sln");
        }
    }
}
