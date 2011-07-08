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
                    Explain(new string[] { args[0] }, @"C:\Users\brandon.MONDOROBOT\src\explain\src\Explain\bin\Debug\explain.exe");
                    break;
                default:
                    // Assume MSBuild
                    Explain(ProbeMSBuild(args[0]), @"C:\Users\brandon.MONDOROBOT\src\explain\src\Explain\bin\Debug\explain.exe");
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

        delegate void EmitLineEventHandler(string line, int sourceLineNumber);

        class FileParser
        {
            public event EmitLineEventHandler EmitLine;
            public event EmitLineEventHandler EmitCommentLine;

            private readonly Queue<string> tokens;
            private readonly string path;

            private int sourceLineNumber = 0;

            public void Parse()
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string line = reader.ReadLine();
                    sourceLineNumber++;
                }
            }

            public FileParser(string path)
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    this.tokens = Tokenizer.Tokenize(reader);
                }

                this.path = path;
            }
        }

        class Tokenizer
        {
            public const char CHAR_LITERAL = '\'';
            public const char STRING_LITERAL = '\"';
            public const char BLOCK_BEGIN = '{';
            public const char BLOCK_END = '}';
            public const char FORWARDSLASH = '/';
            public const char BACKSLASH = '\\';
            public const char CR = '\r';
            public const char LF = '\n';
            public const char SPLAT = '*';
            public const char AT = '@';
            public const char GREATERTHAN = '>';
            public const char LESSTHAN = '<';
            public const char PAREN_BEGIN = '(';
            public const char PAREN_END = ')';
            public const char BRACKET_BEGIN = '[';
            public const char BRACKET_END = ']';

            public static readonly string EOL = System.Environment.NewLine;

            private static void ChompUntil(char until, StreamReader reader, StringBuilder token)
            {
                bool isescaped = false;
                char c = Char.MinValue;
                do
                {
                    isescaped = c == BACKSLASH && !isescaped;
                    c = (char)reader.Read();
                    token.Append(c);
                } while (reader.Peek() >= 0 && !(c == until && !isescaped));
            }

            private static void ChompUntil(string until, StreamReader reader, StringBuilder token)
            {
                do {
                    char c = (char)reader.Read();
                    token.Append(c);
                } while(token.Length < until.Length || token.ToString().Substring(token.Length - until.Length, until.Length) != until);
            }

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

                while(reader.Peek() >= 0) {
                    char c = (char)reader.Read();
                    
                    if (c == LF)
                    {
                        pushtoken();
                        token.Append(EOL);
                        pushtoken();
                        continue;
                    }
                    
                    if (Char.IsWhiteSpace(c))
                    {
                        pushtoken();
                        continue;
                    }

                    if(c == STRING_LITERAL || c == AT) 
                    {
                        pushtoken();
                        token.Append(c);
                        if(c == AT)
                            token.Append((char)reader.Read());

                        ChompUntil(STRING_LITERAL, reader, token);
                        pushtoken();
                        continue;
                    }
                    
                    if(c == CHAR_LITERAL)
                    {
                        pushtoken();
                        token.Append(c);
                        ChompUntil(CHAR_LITERAL, reader, token);
                        pushtoken();
                        continue;
                    }
                    
                    if (c == FORWARDSLASH && (char)reader.Peek() == SPLAT)
                    {
                        // Multiline comment begin
                        pushtoken();
                        token.Append(c);
                        ChompUntil("*/", reader, token);
                        pushtoken();
                        continue;
                    }
                    
                    if(c == FORWARDSLASH && (char)reader.Peek() == FORWARDSLASH) {
                        token.Append(c);
                        ChompUntil(LF, reader, token);
                        while(token[token.Length - 1] == CR || token[token.Length - 1] == LF)
                        {
                            token.Remove(token.Length - 1, 1);
                        }
                        pushtoken();
                        token.Append(EOL);
                        pushtoken();
                        continue;
                    }

                    if (c == BLOCK_BEGIN ||
                        c == BLOCK_END ||
                        c == GREATERTHAN ||
                        c == LESSTHAN/* || 
                        c == PAREN_BEGIN ||
                        c == PAREN_END ||
                        c == BRACKET_BEGIN ||
                        c == BRACKET_END*/)
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
                if (Path.GetFileNameWithoutExtension(codefile) != "StringRegexExtensions")
                    continue;

                Verbose("Open: {0}", codefile);

                FileParser parser = new FileParser(codefile);
                
                Verbose("Close: {0}", codefile);
            }
        }

        static void WriteLine(string line)
        {
            // NOP
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
