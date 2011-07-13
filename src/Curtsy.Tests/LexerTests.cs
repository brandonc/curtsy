using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.IO;

namespace Curtsy.Tests
{
    [TestFixture]
    public class LexerTests
    {
        private static readonly string NL = System.Environment.NewLine;

        [TestCase]
        public void Multiline_comments_are_one_token()
        {
            Queue<string> tokens = Lexer.Tokenize(new StreamReader("TestSource.cs"));

            string comment = tokens.Dequeue();
            Assert.AreEqual(4, comment.MatchesPattern(NL, "m").MatchCount);

            Assert.AreEqual(NL, tokens.Dequeue());
            Assert.AreEqual(NL, tokens.Dequeue());
        }
    }
}
