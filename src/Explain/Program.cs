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
            public StringBuilder Line = new StringBuilder(128);
            public Queue<string> Namespace = new Queue<string>(3);
            public StringBuilder Comment = new StringBuilder(256);
            public int ScopeDepth;
            public Stack<int> NamespaceScopes = new Stack<int>();
            public bool InLiteral = false;
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
                        while((char)reader.Peek() == FORWARDSLASH)
                            token.Append((char)reader.Read());

                        pushtoken();
                        continue;
                    }

                    if (c == SPLAT && (char)reader.Peek() == FORWARDSLASH)
                    {
                        // Comment end
                        pushtoken();
                        token.Append(c);
                        token.Append((char)reader.Read());
                        pushtoken();
                        continue;
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
                if (Path.GetFileNameWithoutExtension(codefile) != "StateMachine")
                    continue;

                Verbose("Open: {0}", codefile);

                Queue<string> tokens = null;
                using (StreamReader reader = new StreamReader(codefile))
                {
                    tokens = Tokenizer.Tokenize(reader);
                }

                FileContext ctx = new FileContext();

                while (tokens.Count > 0)
                {
                    string tok = tokens.Dequeue();
                    switch (tok[0])
                    {
                        case Tokenizer.STRING_LITERAL:
                        case Tokenizer.CHAR_LITERAL:
                            ctx.InLiteral = !ctx.InLiteral && ctx.Comment.Length == 0;
                            break;
                        case Tokenizer.BLOCK_BEGIN:
                            if (!ctx.InLiteral)
                                ctx.ScopeDepth++;
                            break;
                        case Tokenizer.BLOCK_END:
                            if (!ctx.InLiteral)
                                ctx.ScopeDepth--;
                            break;
                        case Tokenizer.CR:
                        case Tokenizer.LF:
                            int afterline = 0;
                            while (afterline != tok.Length && (tok[afterline] == Tokenizer.CR || tok[afterline] == Tokenizer.LF))
                            {
                                afterline++;
                            }
                            
                            tok = afterline == tok.Length ? "" : tok.Substring(afterline);
                            // EOL
                            WriteLine(ctx.Line.ToString());
                            ctx.Line.Clear();

                            if(tok.Length > 0)
                                goto more;

                            continue;
                        default:
                            goto more;
                    }
                    ctx.Line.Append(tok);
                    continue;
                more:
                    
                    ctx.Line.Append(tok);
                }

                Verbose("Close: {0}", codefile);
            }
        }

        static void WriteLine(string line)
        {
            Console.WriteLine(line);
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
