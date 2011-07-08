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
                    Explain(new string[] { args[0] }, @"C:\Users\Brandon.MONDOROBOT\src\explain\src\Explain\bin\Debug\explain.exe");
                    break;
                default:
                    // Assume MSBuild
                    Explain(ProbeMSBuild(args[0]), @"C:\Users\Brandon.MONDOROBOT\src\explain\src\Explain\bin\Debug\explain.exe");
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
            public string CurrentNamespace;
            public string MultilineComment;
            public readonly Stack<int> ClassScopeDepths = new Stack<int>(2);
            public readonly Stack<int> NamespaceScopeDepths = new Stack<int>(2);

            public bool EOF
            {
                get { return this.Line == null; }
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

                using (StreamReader reader = new StreamReader(codefile))
                {
                    FileContext fc = new FileContext()
                    {
                        Line = reader.ReadLine(),
                        CurrentNamespace = String.Empty,
                        ScopeDepth = 0,
                        MultilineComment = String.Empty
                    };
                    
                    while(!fc.EOF) {
                        var linecomment = fc.Line.MatchesPattern(@"[^\'\"]//(.+)$", "c");
                        var scope_enter = fc.Line.IndexOf('{');
                        var scope_exit = fc.Line.IndexOf('}');

                        if (scope_enter >= 0 && (linecomment.MatchCount == 0 || linecomment.Begin(0) > scope_enter))
                        {
                            fc.ScopeDepth++;
                            Verbose("Scope enter, depth {0}: {1}", fc.ScopeDepth, fc.Line);
                        }
                        
                        if (scope_exit > scope_enter && (linecomment.MatchCount == 0 || linecomment.Begin(0) > scope_exit))
                        {
                            fc.ScopeDepth--;
                            Verbose("Scope exit, depth {0}: {1}", fc.ScopeDepth, fc.Line);
                        }

                        if (fc.NamespaceScopeDepths.Count > 0 && fc.ScopeDepth == fc.NamespaceScopeDepths.Peek())
                        {
                            Verbose("Namespace [{0}], depth [1]", fc.CurrentNamespace, fc.ScopeDepth);

                            int remaining = fc.CurrentNamespace.LastIndexOf(".");
                            fc.CurrentNamespace = remaining >= 0 ? fc.CurrentNamespace.Substring(0, remaining) : String.Empty;
                            fc.NamespaceScopeDepths.Pop();
                        } else if (fc.ClassScopeDepths.Count > 0 && fc.ScopeDepth == fc.ClassScopeDepths.Peek())
                        {
                            Verbose("Namespace [{0}], depth [1]", fc.CurrentNamespace, fc.ScopeDepth);

                            int remaining = fc.CurrentNamespace.LastIndexOf(".");
                            fc.CurrentNamespace = remaining >= 0 ? fc.CurrentNamespace.Substring(0, remaining) : String.Empty;
                            fc.ClassScopeDepths.Pop();
                        }

                        var ns = fc.Line.FindPattern(@"^\s*namespace\s+([\w\.]+)", "c", 1);
                        if (ns != null) {
                            fc.CurrentNamespace += (fc.CurrentNamespace.Length > 0 ? "." : "") + ns;
                            fc.NamespaceScopeDepths.Push(fc.ScopeDepth);

                            Verbose("Namespace [{0}], depth [1]", fc.CurrentNamespace, fc.ScopeDepth);
                        }

                        var type = fc.Line.MatchesPattern(@"^(?:public)?(?:static|private|internal|protected|\s+)*(class|interface|struct|enum|delegate [\w<>]+)\s+([\w<>]+)", "c");

                        if (type.MatchCount > 0)
                        {
                            Verbose("Type encountered ({0}): {1}", type[1], type[2]);

                            string typeFlavor = type[1];
                            string name = type[2];

                            if (ass != null)
                            {
                                Type t = ass.GetType(fc.CurrentNamespace + (fc.ClassScopeDepths.Count > 0 && fc.ScopeDepth - 1 == fc.ClassScopeDepths.Peek() ? "+" : ".") + name);

                                Verbose("...Found in assembly {0}", t.ToString());
                            }

                            if (typeFlavor == "class")
                            {
                                fc.ClassScopeDepths.Push(fc.ScopeDepth);
                                fc.CurrentNamespace += (fc.CurrentNamespace.Length > 0 ? "." : "") + name;
                                fc.NamespaceScopeDepths.Push(fc.ScopeDepth);

                                Verbose("Namespace [{0}], depth [1]", fc.CurrentNamespace, fc.ScopeDepth);
                            }
                        }

                        fc.Line = reader.ReadLine();
                    }
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
