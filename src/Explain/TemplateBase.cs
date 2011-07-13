using System.Text;
using System.Collections.Generic;
using System;
namespace Explain
{
    // TemplateBase serves as the base class for the object that will generated
    // by compiling the embedded razor template. The template itself will use the
    // public members to render its contents.
    public abstract class TemplateBase
    {
        public string Title { get; set; }
        public Func<string, string> GetResourcePath { get; set; }
        public Func<string, string> GetSourcePath { get; set; }
        public List<Section> Sections { get; set; }
        public List<OutputUnit> Sources { get; set; }

        public StringBuilder Buffer { get; set; }

        public TemplateBase()
        {
            Buffer = new StringBuilder();
        }

        // This `Execute` function will be defined in the inheriting template
        // class. It generates the HTML by calling `Write` and `WriteLiteral`.
        public abstract void Execute();

        public virtual void Write(object value)
        {
            WriteLiteral(value);
        }

        public virtual void WriteLiteral(object value)
        {
            Buffer.Append(value);
        }
    }
}