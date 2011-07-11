using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using NUnit.Framework;

namespace Explain.Tests
{
    [TestFixture]
    class ParserTests
    {
        [TestCase]
        public void Multiline_comments_parses_correct_number_of_lines()
        {
            FileInfo file = new FileInfo("TestSource.cs");
            FileParser p = new FileParser(file.FullName, new FoundTypes(), new PathHelper(file.DirectoryName));
            int comments = 0;
            int code = 0;
            int lastline = 0;
            string text = String.Empty;
            p.EmitCommentLine += delegate(string line, int number)
            {
                comments++;
                text += line + System.Environment.NewLine;
            };

            p.EmitLine += delegate(string line, int number)
            {
                code++;
                lastline = number;
            };

            p.Parse();

            Assert.AreEqual(6, comments);
            Assert.AreEqual(17, lastline);
            Assert.AreEqual(11, code);
        }
    }
}
