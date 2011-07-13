﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Explain
{
    public class Section
    {
        public string CodeHtml;
        public string DocsHtml;
        public int StartLine;
        public int EndLine;
        public int CodeStartLine;
    }

    public class OutputUnit
    {
        public string CodeFile;
        public string Name;
        public List<Section> Sections;
        public string SizeFormatted;
    }
}
