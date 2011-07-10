#if DEBUG
#define VERBOSE
#endif

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Razor;
using System.Xml;
using System.Collections;

namespace Explain
{
    class Program
    {
        
        static TypeMap _typeMap = new TypeMap();

        static int Main(string[] args)
        {
            // Explain only expects one argument: the target file. That can be either a .cs file or msbuild .csproj file.
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

            FileInfo fi = new FileInfo(args[0]);

            IEnumerable<string> sources;
            
            switch (Path.GetExtension(args[0]).ToLower())
            {
                case ".cs":
                    sources = new string[] { fi.FullName };
                    break;
                default:
                    // Assume MSBuild
                    sources = ProbeMSBuildForSources(fi.FullName).ToArray();
                    break;
            }

            var explain = new Explain(sources.ToList(), fi.DirectoryName);
            explain.Generate("docs");

            return 0;
        }

        // Open specified MSBuild file and return relative .cs file paths for each `<Compile>` element
        static List<string> ProbeMSBuildForSources(string projfile)
        {
            var doc = new XmlDocument();
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

            var nodes = doc.SelectNodes(String.Format("//{0}Compile[@Include]", ns), nsmgr);
            var result = new List<string>(nodes.Count);
            foreach (XmlNode node in nodes)
            {
                if (!node.Attributes["Include"].Value.EndsWith(".cs"))
                    continue;

                result.Add(Path.Combine(Path.GetDirectoryName(projfile), node.Attributes["Include"].Value));
            }

            return result;
        }

        static void Verbose(string str, params object[] p)
        {
#if VERBOSE
            Console.WriteLine("explain: {0}", String.Format(str, p));
#endif
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: explain <file>");
            Console.WriteLine("<file> can be a .cs or .csproj file");
        }
    }
}
