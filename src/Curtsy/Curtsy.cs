/*
## Curtsy ##

> A hyperlinked, readable, C#-to-annotated-html documentation generator.

Curtsy is a fork of [nocco][] that does some lexical analysis in order to provide type hyperlinking for c#.

This page is the result of running Curtsy against its own source file. The source for Curtsy is [available on GitHub][project], and released under the MIT license.

Running this: `curtsy WindowsApplication1.csproj`

will generate linked HTML documentation for the named source files, saving it into a new folder called "docs".

[prettify]: http://code.google.com/p/google-code-prettify/
[project]: https://github.com/brandonc/curtsy
[nocco]: http://dontangg.github.com/nocco/
[docco]: http://jashkenas.github.com/docco/
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace Curtsy
{
    public class Curtsy
    {
        public List<string> Sources { get; set; }
        public PathHelper RootPathHelper { get; set; }
        public EmbeddedResources Resources { get; set; }

        // Generates html documents and writes them to the specified destination directory.
        public void Generate(string destinationDirectory)
        {
            FoundTypes typeMap = new FoundTypes();
            List<OutputUnit> output = new List<OutputUnit>();

            foreach (string codefile in this.Sources)
            {
                List<Section> sections = new List<Section>();
                var hasCode = false;
                var docsText = new StringBuilder();
                var codeText = new StringBuilder();

                Action<string, string, int, int, int> save = (string docs, string code, int codeStartLine, int s, int e) =>
                {
                    sections.Add(new Section() { DocsHtml = docs, CodeHtml = code, CodeStartLine = codeStartLine, StartLine = s, EndLine = e });
                };

                int? codeStart = null;
                int? start = null;
                int? end = null;

                FileParser parser = new FileParser(codefile, typeMap, this.RootPathHelper);
                parser.EmitCommentLine += delegate(string line, int sourceLineNumber)
                {
                    if (!start.HasValue)
                        start = sourceLineNumber;

                    if (hasCode)
                    {
                        end = sourceLineNumber;

                        save(docsText.ToString(), codeText.ToString(), codeStart.HasValue ? codeStart.Value : 1, start.Value, end.Value);
                        docsText.Clear();
                        codeText.Clear();
                        hasCode = false;
                        codeStart = start = end = null;
                    } else
                    {
                        end = sourceLineNumber;
                    }

                    if (line.IndexOfPattern(@"//\s*#(if|else|elif|endif|define|undef|warning|error|line|region|endregion)") == 0)
                        return; // Discard commented preprocessor commands

                    docsText.AppendLine(line.TrimStart(' ', '\t', '/', '*'));
                };

                parser.EmitLine += delegate(string line, int sourceLineNumber)
                {
                    if (!codeStart.HasValue)
                        codeStart = sourceLineNumber;

                    if (!start.HasValue)
                        start = sourceLineNumber;

                    end = sourceLineNumber;
                    codeText.AppendLine(line);
                    hasCode = true;
                };

                parser.Parse();
                save(docsText.ToString(), codeText.ToString(), codeStart.HasValue ? codeStart.Value : 0, start.Value, end.Value);

                output.Add(new OutputUnit()
                {
                    Sections = sections,
                    CodeFile = codefile,
                    Name = RootPathHelper.MakeRelativePath(codefile),
                    SizeFormatted = GetFileSizeFormatted(codefile)
                });
            }

            Resources.WriteClientFilesTo(destinationDirectory);

            foreach (var v in output)
            {
                GenerateInternal(v.Sections, output, v.CodeFile, typeMap, destinationDirectory);
            }
        }

        // For the specified file, create a formatted text string describing the
        // length of the file using these units:
        static string[] sizes = { "B", "KB", "MB", "GB" };

        string GetFileSizeFormatted(string path)
        {
            int order = 0;
            double len = new FileInfo(path).Length;
            while (len >= 1024 && order + 1 < len)
            {
                order++;
                len = len / 1024;
            }

            return String.Format("{0:0.##} {1}", len, sizes[order]);
        }

        // Write the output for each file into the specified destination directory
        // and prepare the razor template instance with the data it needs.
        void GenerateInternal(List<Section> sections, List<OutputUnit> sources, string codefile, FoundTypes typeMap, string destinationDirectory)
        {
            var output = this.RootPathHelper.MakeRelativePath(codefile);
            var subdestination = Path.Combine(destinationDirectory, output);
            var mdexts = DownBlouse.MarkdownExtensions.AutoLink;

            Directory.CreateDirectory(Path.GetDirectoryName(subdestination).ToLower());

            string clientPathToRoot = String.Concat(Enumerable.Repeat<string>("../", output.Split(Path.DirectorySeparatorChar).Length - 1));

            Func<string, string> getSourcePath = (string s) =>
            {
                return Path.Combine(clientPathToRoot, Path.ChangeExtension(s, ".html").ToLower()).Replace(Path.DirectorySeparatorChar, '/');
            };

            string typebounds = @"[\s\(;:,]+";

            foreach (Section s in sections)
            {
                // This block handles the possibility of XML embedded into the comments, signalling
                // visual studio style xml documentation. Only `remarks` and `summary` are used and everything else
                // is discarded.
                MatchData xmlmatches = s.DocsHtml.MatchesPattern(
                    @"\<summary\>(.+)\<\/summary\>|\<remarks\>(.+)\<\/remarks\>", "s"
                );

                if (xmlmatches != null && xmlmatches.Count > 3) {
                    s.DocsHtml = DownBlouse.DownBlouse.Markdownify(xmlmatches[1] + System.Environment.NewLine + System.Environment.NewLine + xmlmatches[3], mdexts);
                } else if (xmlmatches != null && xmlmatches.Count > 1) {
                    s.DocsHtml = DownBlouse.DownBlouse.Markdownify(xmlmatches[1], mdexts);
                } else if (s.DocsHtml.HasPattern(@"\<(example|exception|param|permission|returns|seealso|include)\>")) {
                    s.DocsHtml = String.Empty;
                } else {
                    s.DocsHtml = DownBlouse.DownBlouse.Markdownify(s.DocsHtml, mdexts);
                }

                s.CodeHtml = System.Web.HttpUtility.HtmlEncode(s.CodeHtml);

                foreach (FoundTypes.TypeInfo type in typeMap)
                {
                    string pattern;

                    if (type.Name.IndexOf('<') > 0)
                        pattern = "(namespace)?(" + typebounds + ")(" + type.Name.Sub(@"\<\w+\>", @"&lt;\w+&gt;") + ")(" + typebounds + ")";

                    pattern = "(namespace)?(" + typebounds + ")(" + type.Name + ")(" + typebounds + ")";

                    if (type.File != RootPathHelper.MakeRelativePath(codefile))
                        s.CodeHtml = s.CodeHtml.GSub(pattern, (Match m) => {
                            if (String.IsNullOrEmpty(m.Groups[1].Value))
                                return String.Format("{0}<a href=\"" + getSourcePath(type.File) + "\">{1}</a>{2}", m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value);
                            else
                                return m.Groups[0].Value;
                        });
                }
            }

            var htmlTemplate = Resources.CreateRazorTemplateInstance();
            htmlTemplate.Title = Path.GetFileName(codefile);
            htmlTemplate.GetResourcePath = (string s) => Path.Combine(clientPathToRoot, s);
            htmlTemplate.GetSourcePath = getSourcePath;
            htmlTemplate.Sections = sections;
            htmlTemplate.Sources = sources;

            htmlTemplate.Execute();

            File.WriteAllText(Path.ChangeExtension(subdestination, ".html").ToLower(), htmlTemplate.Buffer.ToString()); // Overwrites existing file
        }

        // Create a new instance of the Curtsy engine. You have to specify the individual files to be used as well
        // as the root directory from which to resolve them and write the output "docs" folder
        public Curtsy(List<string> sources, string rootDirectory)
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
