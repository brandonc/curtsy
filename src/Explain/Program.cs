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
                    Explain(new string[] { args[0] });
                    break;
                default:
                    // Assume MSBuild
                    Explain(ProbeMSBuild(args[0]));
                    break;
            }
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

            foreach (XmlNode node in doc.SelectNodes("//Compile[@Include]"))
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
            public readonly Stack<int> NamespaceScopeDepths = new Stack<int>(2);

            public bool EOF
            {
                get { return this.Line == null; }
            }
        }

        static void Explain(IEnumerable<string> files, string assembly)
        {
            Assembly ass = assembly == null ? null : Assembly.LoadFile(assembly);

            foreach (string codefile in files)
            {
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
                        var linecomment = fc.Line.MatchesPattern("//(.+)$", "c");
                        var scope_enter = fc.Line.IndexOf('{');
                        var scope_exit = fc.Line.IndexOf('}');

                        if(scope_enter >= 0 && (linecomment.Begin(0) == -1 || linecomment.Begin(0) > scope_enter))
                            fc.ScopeDepth++;
                        else if(scope_exit >= 0 && (linecomment.Begin(0) == -1 || linecomment.Begin(0) > scope_exit))
                            fc.ScopeDepth--;

                        if (fc.ScopeDepth == fc.NamespaceScopeDepths.Peek())
                        {
                            int remaining = fc.CurrentNamespace.LastIndexOf('.');
                            fc.CurrentNamespace = remaining >= 0 ? fc.CurrentNamespace.Substring(0, remaining) : String.Empty;
                            fc.NamespaceScopeDepths.Pop();
                        }

                        var ns = fc.Line.FindPattern(@"^\s*namespace\s+([\w\.]+)", "c", 1);
                        if (ns != null) {
                            fc.CurrentNamespace += (fc.CurrentNamespace.Length > 0 ? "." : "") + ns;
                            fc.NamespaceScopeDepths.Push(fc.ScopeDepth);
                        }

                        if (ass != null)
                        {                            
                            var type = fc.Line.MatchesPattern(@"^(public)?(?:static|internal|protected|\s+)*(class|interface|struct|enum|delegate [\w<>]+)\s+([\w<>]+)", "c", 1);
                            if (type.MatchCount > 0)
                            {
                                bool isPublic = !String.IsNullOrEmpty(type[1]);
                                string typeFlavor = type[2];
                                string name = type[3];
                            }
                        }
                        fc.Line = reader.ReadLine();
                    }
                }
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: explain <file>");
            Console.WriteLine("<file> can be a .cs, .vb, .csproj, or .sln");
        }
    }
}
