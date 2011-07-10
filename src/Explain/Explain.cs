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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Explain
{
    public class Explain
    {
        class OutputUnit
        {
            public string CodeFile;
            public List<Section> Sections;
        }

        public List<string> Sources { get; set; }
        public PathHelper RootPathHelper { get; set; }
        public EmbeddedResources Resources { get; set; }

        public void Generate(string destinationDirectory)
        {
            TypeMap typeMap = new TypeMap();
            List<OutputUnit> output = new List<OutputUnit>();

            foreach (string codefile in this.Sources)
            {
                List<Section> sections = new List<Section>();
                var hasCode = false;
                var docsText = new StringBuilder();
                var codeText = new StringBuilder();

                Action<string, string> save = (string docs, string code) => sections.Add(new Section() { DocsHtml = docs, CodeHtml = code });

                FileParser parser = new FileParser(codefile, typeMap, this.RootPathHelper);
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

            Resources.WriteClientFilesTo(destinationDirectory);

            foreach (var v in output)
            {
                GenerateInternal(v.Sections, this.Sources.ToArray(), v.CodeFile, typeMap, destinationDirectory);
            }
        }

        // Prepare sections for html output and execute razor template
        void GenerateInternal(List<Section> sections, string[] sources, string codefile, TypeMap typeMap, string destinationDirectory)
        {
            var output = this.RootPathHelper.MakeRelativePath(codefile);
            var subdestination = Path.Combine(destinationDirectory, output);
            Directory.CreateDirectory(Path.GetDirectoryName(subdestination));

            string clientPathToRoot = String.Concat(Enumerable.Repeat<string>("../", output.Split(Path.DirectorySeparatorChar).Length - 1));

            Func<string, string> getSourcePath = (string s) =>
            {
                return Path.Combine(clientPathToRoot, Path.ChangeExtension(s, ".html").ToLower()).Replace(Path.DirectorySeparatorChar, '/');
            };

            foreach (Section s in sections)
            {
                s.DocsHtml = DownBlouse.DownBlouse.Markdownify(s.DocsHtml);
                s.CodeHtml = System.Web.HttpUtility.HtmlEncode(s.CodeHtml);

                foreach (TypeMap.TypeInfo type in typeMap)
                {
                    if (type.File != RootPathHelper.MakeRelativePath(codefile))
                        s.CodeHtml = s.CodeHtml.GSub(type.GetPattern(), "$1<a href=\"" + getSourcePath(type.File) + "\">$2</a>$3");
                }
            }

            var htmlTemplate = Resources.CreateRazorTemplateInstance();
            htmlTemplate.Title = Path.GetFileName(codefile);
            htmlTemplate.GetResourcePath = (string s) => Path.Combine(clientPathToRoot, s);
            htmlTemplate.GetSourcePath = getSourcePath;
            htmlTemplate.Sections = sections;
            htmlTemplate.Sources = new List<string>(from f in sources
                                                    select RootPathHelper.MakeRelativePath(f));

            htmlTemplate.Execute();

            // Overwrite existing file
            File.WriteAllText(Path.ChangeExtension(subdestination, ".html").ToLower(), htmlTemplate.Buffer.ToString());
        }

        public Explain(List<string> sources, string rootDirectory)
        {
            if (string.IsNullOrEmpty(rootDirectory))
                rootDirectory = ".\\";
            else if (rootDirectory[rootDirectory.Length - 1] != Path.DirectorySeparatorChar)
                rootDirectory += Path.DirectorySeparatorChar;
            
            if (sources == null)
                throw new ArgumentNullException("filePaths");

            if ((from f in sources
                 where !Path.IsPathRooted(f)
                 select f).Count() > 0)
            {
                throw new ArgumentException("source file paths must be rooted (absolute) paths.");
            }

            this.RootPathHelper = new PathHelper(rootDirectory);
            this.Resources = new EmbeddedResources();
            this.Sources = sources;
        }
    }
}
