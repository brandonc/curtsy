﻿using System.Text;
using System.Collections.Generic;
using System;
namespace Explain
{
    public abstract class TemplateBase
    {

        // Properties available from within the template
        public string Title { get; set; }
        public Func<string, string> GetResourcePath { get; set; }
        public Func<string, string> GetSourcePath { get; set; }
        public List<Section> Sections { get; set; }
        public List<string> Sources { get; set; }

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