/*
## Explain ##

> A hyperlinked, readable, c#-to-annotated-html documentation generator.

Explain is a fork of [nocco][]* that does some lexical analysis in order to provide type hyperlinking for c#.

Nocco is simpler and yet can be run against more programming languages, comparatively speaking. It just doesn't hyperlink the source.

This page is the result of running Explain against its own source file. The source for Explain is [available on GitHub][project], and released under the MIT license.

You can run explain either against a `.csproj` file or against individual source files.

Running this: `explain WindowsApplication1.csproj`

will generate linked HTML documentation for the named source files, saving it into a new folder called "docs".

<small>* nocco is a port of [docco][]!</small>

[prettify]: http://code.google.com/p/google-code-prettify/
[project]: https://github.com/brandonc/explain
[nocco]: http://dontangg.github.com/nocco/
[docco]: http://jashkenas.github.com/docco/
*/

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
    public class Section
    {
        public string CodeHtml;
        public string DocsHtml;
        public int StartLine;
        public int EndLine;
    }

    class Program
    {
        static PathHelper _pathHelper;
        static string[] _resources = { "prettify.js", "explain.css" };
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

            // Create "docs" folder and populate with requisite css and js file
            PrepareOutput();

            FileInfo fi = new FileInfo(args[0]);

            _pathHelper = new PathHelper(fi.DirectoryName + Path.DirectorySeparatorChar);

            IEnumerable<string> sources;

            switch (Path.GetExtension(args[0]).ToLower())
            {
                case ".cs":
                    sources = new string[] { args[0] };
                    break;
                default:
                    // Assume MSBuild
                    sources = ProbeMSBuild(args[0]).ToArray();
                    break;
            }

            Explain(sources);

            return 0;
        }

        class OutputUnit
        {
            public string CodeFile;
            public List<Section> Sections;
        }

        static void Explain(IEnumerable<string> sources)
        {
            Type templateType = SetupRazorTemplate();

            List<OutputUnit> output = new List<OutputUnit>();

            foreach (string codefile in sources)
            {
                List<Section> sections = new List<Section>();
                var hasCode = false;
                var docsText = new StringBuilder();
                var codeText = new StringBuilder();

                Action<string, string> save = (string docs, string code) => sections.Add(new Section() { DocsHtml = docs, CodeHtml = code });

                FileParser parser = new FileParser(codefile);
                parser.EmitCommentLine += delegate(string line, int sourceLineNumber)
                {
                    // Throw away intellisense documentation. It doesn't markdown well at all.
                    if (line.TrimStart(' ', '\t').StartsWith("///"))
                        return;

                    if (hasCode)
                    {
                        save(docsText.ToString(), codeText.ToString());
                        docsText.Clear();
                        codeText.Clear();
                        hasCode = false;
                    }

                    docsText.AppendLine(line.TrimStart(' ', '\t', '/', '*'));
                };

                parser.EmitLine += delegate(string line, int sourceLineNumber)
                {
                    codeText.AppendLine(line);
                    hasCode = true;
                };

                parser.Parse();
                save(docsText.ToString(), codeText.ToString());

                output.Add(new OutputUnit()
                {
                    Sections = sections,
                    CodeFile = codefile
                });
            }

            foreach (var v in output)
            {
                Generate(v.Sections, sources.ToArray(), v.CodeFile, templateType);
            }
        }

        // Create docs folder and copy resource files from the list of embedded resources
        static void PrepareOutput()
        {
            Directory.CreateDirectory("docs");

            foreach (string res in _resources)
            {
                if (File.Exists("docs" + Path.DirectorySeparatorChar + res))
                {
                    Verbose("Skipping {0}", res);
                    continue;
                }

                Verbose("Copying {0}...", res);

                try
                {
                    using (var writer = File.CreateText("docs" + Path.DirectorySeparatorChar + res))
                    {
                        string[] names = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames();
                        Stream s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Explain.Resources." + res);

                        using (var reader = new StreamReader(s))
                        {
                            writer.Write(reader.ReadToEnd());
                        }
                    }
                }
                catch
                {
                    File.Delete("docs" + Path.DirectorySeparatorChar + res);
                }
            }
        }

        // Open specified MSBuild file and return relative .cs file paths for each `<Compile>` element
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
                if (!node.Attributes["Include"].Value.EndsWith(".cs"))
                    continue;

                yield return Path.Combine(Path.GetDirectoryName(projfile), node.Attributes["Include"].Value);
            }
        }

        // The tokenizer produces a `Queue<string>` of tokens for a specified file stream.
        // String literals and comments are aggregated into single tokens.
        class Lexer
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

            // Continue adding characters to the specified token until an unescaped character is encountered
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

            // Continue adding characters to the specified token until string is encountered (inclusive of string)
            private static void ChompUntil(string until, StreamReader reader, StringBuilder token)
            {
                do
                {
                    char c = (char)reader.Read();
                    token.Append(c);
                } while (token.Length < until.Length || token.ToString().Substring(token.Length - until.Length, until.Length) != until);
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

                while (reader.Peek() >= 0)
                {
                    char c = (char)reader.Read();

                    if (c == LF)
                    {
                        pushtoken();
                        token.Append(EOL);
                        pushtoken();
                        continue;
                    }

                    // Non-newline white space is the primary signal for the end of a token.
                    if (Char.IsWhiteSpace(c))
                    {
                        pushtoken();
                        continue;
                    }

                    // `"` or `@` signals the start of a string literal
                    if (c == STRING_LITERAL || c == AT)
                    {
                        pushtoken();
                        token.Append(c);
                        if (c == AT)
                            token.Append((char)reader.Read());

                        ChompUntil(STRING_LITERAL, reader, token);
                        pushtoken();
                        continue;
                    }

                    // `'` signals the start of a character literal
                    if (c == CHAR_LITERAL)
                    {
                        pushtoken();
                        token.Append(c);
                        ChompUntil(CHAR_LITERAL, reader, token);
                        pushtoken();
                        continue;
                    }

                    // `/*` signals the start of a multiline comment
                    if (c == FORWARDSLASH && (char)reader.Peek() == SPLAT)
                    {
                        pushtoken();
                        token.Append(c);
                        ChompUntil("*/", reader, token);
                        pushtoken();
                        continue;
                    }

                    // `//` signals the start of a line comment
                    if (c == FORWARDSLASH && (char)reader.Peek() == FORWARDSLASH)
                    {
                        token.Append(c);
                        ChompUntil(LF, reader, token);
                        while (token[token.Length - 1] == CR || token[token.Length - 1] == LF)
                        {
                            token.Remove(token.Length - 1, 1);
                        }
                        pushtoken();
                        token.Append(EOL);
                        pushtoken();
                        continue;
                    }

                    // These characters are necessary to successfully parse type names later on.
                    if (c == BLOCK_BEGIN ||
                        c == BLOCK_END ||
                        c == GREATERTHAN ||
                        c == LESSTHAN ||
                        c == PAREN_BEGIN ||
                        c == PAREN_END ||
                        c == BRACKET_BEGIN ||
                        c == BRACKET_END)
                    {
                        pushtoken();
                        token.Append(c);
                        pushtoken();
                        continue;
                    }

                    // Making a word
                    token.Append(c);
                }

                return result;
            }
        }

        delegate void EmitLineEventHandler(string line, int sourceLineNumber);

        // The file parser reads both the source file AND token stream to deliver
        // both comment and source lines via events.
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
                    bool iscomment = false;

                    // Helper: get the next token and raise event if it's a newline.
                    Func<string> next = new Func<string>(() =>
                    {
                        string tok = tokens.Dequeue();
                        if (tok == System.Environment.NewLine)
                        {
                            sourceLineNumber++;
                            OnEmitLine(reader.ReadLine(), iscomment);
                        }
                        return tok;
                    });

                    // Helper: emit a multiline token
                    Action<string> emitMultilineToken = new Action<string>((string token) =>
                    {
                        using (StringReader sr = new StringReader(token))
                        {
                            while (sr.Peek() >= 0)
                            {
                                sourceLineNumber++;
                                OnEmitLine(reader.ReadLine(), iscomment);
                                // Throw away
                                sr.ReadLine();
                            }
#warning This is a logical error!
                            tokens.Dequeue();
                        }
                    });

                    // Helper: move to last newline
                    Action skipNewlines = new Action(() =>
                    {
                        while (tokens.Peek() == System.Environment.NewLine)
                        {
                            next();
                        }
                    });

                    // Helper: move past block as designated by opening and closing strings. Return the block as a string.
                    Func<string, string, string> skipBlock = new Func<string, string, string>((open, close) =>
                    {
                        StringBuilder result = new StringBuilder();
                        string tok = null;
                        int depth = 0;
                        while (tok != close || depth > 0)
                        {
                            tok = next();
                            result.Append(tok);
                            if (tok == open)
                                depth++;
                            else if (tok == close)
                                depth--;
                        }

                        return result.ToString();
                    });

                    // Helper: move past generic decl block
                    Func<string> skipGeneric = new Func<string>(() =>
                    {
                        if (tokens.Peek() == "<") {
                            return skipBlock("<", ">");
                        }
                        return String.Empty;
                    });

                    // Helper: return the next token plus any possible generic portion
                    Func<string> typeName = new Func<string>(() =>
                    {
                        skipNewlines();
                        return next() + skipGeneric();   
                    });

                    string relpath = _pathHelper.MakeRelativePath(this.path);

                    while (tokens.Count > 0)
                    {
                        string tok = next();
                        if (tok == System.Environment.NewLine)
                            continue;

                        if (tok.StartsWith("//"))
                        {
                            iscomment = true;
                            continue;
                        }
                        else if (tok.StartsWith("/*"))
                        {
                            iscomment = true;
                            emitMultilineToken(tok);
                            continue;
                        }
                        else
                        {
                            iscomment = false;
                        }

                        switch (tok)
                        {
                            case "class":
                                _typeMap.Add(typeName(), relpath, this.sourceLineNumber, TypeMap.TypeHint.Class);
                                break;
                            case "delegate":
                                skipNewlines();
                                string n = next();
                                if(n == "{" || n == "(")
                                {
                                    // Indicates anonymous delegate
                                    break;
                                }
                                skipGeneric();
                                _typeMap.Add(tokens.Peek(), relpath, this.sourceLineNumber, TypeMap.TypeHint.Delegate);
                                break;
                            case "struct":
                                if (tokens.Peek() == "{" || tokens.Peek() == System.Environment.NewLine)
                                {
                                    // Indicates generic constraint
                                    break;
                                }
                                _typeMap.Add(typeName(), relpath, this.sourceLineNumber, TypeMap.TypeHint.Struct);
                                break;
                            case "enum":
                                skipNewlines();
                                _typeMap.Add(tokens.Peek(), relpath, this.sourceLineNumber, TypeMap.TypeHint.Enum);
                                break;
                            case "interface":
                                skipNewlines();
                                _typeMap.Add(tokens.Peek(), relpath, this.sourceLineNumber, TypeMap.TypeHint.Interface);
                                break;
                        }
                    }
                }
            }

            // Emit either a comment or code line.
            protected void OnEmitLine(string line, bool iscomment)
            {
                if (!iscomment && EmitLine != null)
                    EmitLine(line, this.sourceLineNumber);
                else if (iscomment && EmitCommentLine != null)
                    EmitCommentLine(line, this.sourceLineNumber);
            }

            public FileParser(string path)
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    this.tokens = Lexer.Tokenize(reader);
                }

                this.path = path;
            }
        }

        // Prepare sections for html output and execute razor template
        static void Generate(List<Section> sections, string[] sources, string codefile, Type templateType)
        {
            int depth;
            var destination = Path.Combine("docs", _pathHelper.MakeRelativePath(codefile));
            Directory.CreateDirectory(Path.GetDirectoryName(destination));

            string pathToRoot = "";
            depth = Path.GetDirectoryName(destination).Split(new char[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries).Length - 1;
            for (int i = 0; i < depth; i++)
            {
                pathToRoot = Path.Combine("..", pathToRoot);
            }

            Func<string, string> getSourcePath = (string s) => {
                return Path.Combine(pathToRoot, Path.ChangeExtension(s, ".html")).Replace('\\', '/');
            };

            foreach (Section s in sections)
            {
                s.DocsHtml = DownBlouse.DownBlouse.Markdownify(s.DocsHtml, smartypants: false);
                s.CodeHtml = System.Web.HttpUtility.HtmlEncode(s.CodeHtml);

                foreach (TypeMap.TypeInfo type in _typeMap)
                {
                    if (type.File != _pathHelper.MakeRelativePath(codefile)) 
                        s.CodeHtml = s.CodeHtml.GSub(type.GetPattern(), "$1<a href=\"" + getSourcePath(type.File) + "\">$2</a>$3");
                }
            }

            var htmlTemplate = Activator.CreateInstance(templateType) as TemplateBase;
            htmlTemplate.Title = Path.GetFileName(codefile);
            htmlTemplate.GetResourcePath = (string s) => Path.Combine(pathToRoot, s);
            htmlTemplate.GetSourcePath = getSourcePath;
            htmlTemplate.Sections = sections;
            htmlTemplate.Sources = new List<string>(from f in sources
                                                    select _pathHelper.MakeRelativePath(f));

            htmlTemplate.Execute();

            // Overwrite existing file
            File.WriteAllText(Path.ChangeExtension(destination, ".html"), htmlTemplate.Buffer.ToString());
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
            Console.WriteLine("<file> can be a .cs, .vb, .csproj, or .sln");
        }

        // The razor template is embedded inside the assembly.
        static Type SetupRazorTemplate()
        {
            RazorEngineHost host = new RazorEngineHost(new CSharpRazorCodeLanguage());
            host.DefaultBaseClass = typeof(TemplateBase).FullName;
            host.DefaultNamespace = "RazorOutput";
            host.DefaultClassName = "Template";
            host.NamespaceImports.Add("System");

            GeneratorResults razorResult = null;

            using (var reader = new StreamReader(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Explain.Resources.explain.cshtml")))
            {
                razorResult = new RazorTemplateEngine(host).GenerateCode(reader);
            }

            var compilerParams = new CompilerParameters
            {
                GenerateInMemory = true,
                GenerateExecutable = false,
                IncludeDebugInformation = false,
                CompilerOptions = "/target:library /optimize"
            };
            compilerParams.ReferencedAssemblies.Add(typeof(Program).Assembly.CodeBase.Replace("file:///", "").Replace("/", "\\"));

            var codeProvider = new Microsoft.CSharp.CSharpCodeProvider();
            CompilerResults results = codeProvider.CompileAssemblyFromDom(compilerParams, razorResult.GeneratedCode);

            // Check for errors that may have occurred during template generation
            if (results.Errors.HasErrors)
            {
                foreach (var err in results.Errors.OfType<CompilerError>().Where(ce => !ce.IsWarning))
                    Console.WriteLine("explain: Error compiling template: ({0}, {1}) {2}", err.Line, err.Column, err.ErrorText);

                System.Environment.Exit(-1);
            }

            return results.CompiledAssembly.GetType("RazorOutput.Template");
        }
    }
}
