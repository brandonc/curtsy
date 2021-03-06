﻿using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using System.Web.Razor;
using System.Text;

namespace Curtsy
{
    public class EmbeddedResources
    {
        Type templateType = null;
        string[] clientFiles = { "prettify.js", "curtsy.css" };

        // Writes each file designated as a client file from the embedded resources folder
        // into the specified directory, unless that file already exists.
        public void WriteClientFilesTo(string path)
        {
            Directory.CreateDirectory(path);

            foreach (string res in clientFiles)
            {
                string outputFile = Path.Combine(path, res);

                if (File.Exists(outputFile))
                    continue;

                try
                {
                    using (var writer = File.CreateText(outputFile))
                    {
                        Stream s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Curtsy.Resources." + res);
                        if (s == null)
                            throw new InvalidDataException("Could not find embedded resource 'Curtsy.Resources." + res + "'");

                        using (var reader = new StreamReader(s))
                        {
                            writer.Write(reader.ReadToEnd());
                        }
                    }
                }
                catch
                {
                    try { File.Delete(outputFile); } catch { }
                }
            }
        }

        // Returns an instance of the razor template, compiled from the file stored as an embedded resource.
        // The first time this method is executed, the Razor template is compiled and stored.
        // This method will throw an InvalidDataException if the template contains syntax errors.
        public TemplateBase CreateRazorTemplateInstance()
        {
            if (templateType == null)
            {
                var host = new RazorEngineHost(new CSharpRazorCodeLanguage());
                host.DefaultBaseClass = typeof(TemplateBase).FullName;
                host.DefaultNamespace = "RazorOutput";
                host.DefaultClassName = "Template";
                host.NamespaceImports.Add("System");

                GeneratorResults razorResult = null;

                var templateStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Curtsy.Resources.curtsy.cshtml");

                if (templateStream == null)
                    throw new FileNotFoundException("Could not find embedded resource 'Curtsy.Resources.curtsy.cshtml'");

                using (var reader = new StreamReader(templateStream))
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
                compilerParams.ReferencedAssemblies.Add(typeof(Program).Assembly.CodeBase.Replace("file:///", "").Replace('/', Path.DirectorySeparatorChar));

                var codeProvider = new Microsoft.CSharp.CSharpCodeProvider();
                var results = codeProvider.CompileAssemblyFromDom(compilerParams, razorResult.GeneratedCode);

                if (results.Errors.HasErrors)
                {
                    StringBuilder errors = new StringBuilder();
                    foreach (var err in results.Errors.OfType<CompilerError>().Where(ce => !ce.IsWarning))
                        errors.AppendFormat("Error compiling template: ({0}, {1}) {2}", err.Line, err.Column, err.ErrorText);

                    throw new InvalidDataException(errors.ToString());
                }

                templateType = results.CompiledAssembly.GetType("RazorOutput.Template");
            }

            return (TemplateBase)Activator.CreateInstance(templateType);
        }
    }
}
