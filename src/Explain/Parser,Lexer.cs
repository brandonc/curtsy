using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Explain
{
    // The tokenizer produces a `Queue<string>` of tokens for a specified file stream.
    // String literals and comments are aggregated into single tokens.
    public class Lexer
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
        static void ChompUntil(char until, StreamReader reader, StringBuilder token)
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
        static void ChompUntil(string until, StreamReader reader, StringBuilder token)
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

    public delegate void EmitLineEventHandler(string line, int sourceLineNumber);

    // The file parser reads both the source file AND token stream to deliver
    // both comment and source lines via events.
    public class FileParser
    {
        public event EmitLineEventHandler EmitLine;
        public event EmitLineEventHandler EmitCommentLine;

        readonly Queue<string> tokens;
        readonly string path;

        int sourceLineNumber = 0;
        TypeMap typeMap;
        PathHelper pathHelper;

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
                    token.Scan(System.Environment.NewLine, (s) =>
                    {
                        sourceLineNumber++;
                        OnEmitLine(reader.ReadLine(), iscomment);
                    });
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
                    if (tokens.Peek() == "<")
                    {
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

                string relpath = pathHelper.MakeRelativePath(this.path);

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
                            typeMap.Add(typeName(), relpath, this.sourceLineNumber, TypeMap.TypeHint.Class);
                            break;
                        case "delegate":
                            skipNewlines();
                            string n = next();
                            if (n == "{" || n == "(")
                            {
                                // Indicates anonymous delegate
                                break;
                            }
                            skipGeneric();
                            typeMap.Add(tokens.Peek(), relpath, this.sourceLineNumber, TypeMap.TypeHint.Delegate);
                            break;
                        case "struct":
                            if (tokens.Peek() == "{" || tokens.Peek() == System.Environment.NewLine)
                            {
                                // Indicates generic constraint
                                break;
                            }
                            typeMap.Add(typeName(), relpath, this.sourceLineNumber, TypeMap.TypeHint.Struct);
                            break;
                        case "enum":
                            skipNewlines();
                            typeMap.Add(tokens.Peek(), relpath, this.sourceLineNumber, TypeMap.TypeHint.Enum);
                            break;
                        case "interface":
                            skipNewlines();
                            typeMap.Add(tokens.Peek(), relpath, this.sourceLineNumber, TypeMap.TypeHint.Interface);
                            break;
                    }
                }
                sourceLineNumber++;
                OnEmitLine(reader.ReadLine(), iscomment);
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

        public FileParser(string path, TypeMap typeMap, PathHelper pathHelper)
        {
            using (StreamReader reader = new StreamReader(path))
            {
                this.tokens = Lexer.Tokenize(reader);
            }

            this.typeMap = typeMap;
            this.path = path;
            this.pathHelper = pathHelper;
        }
    }
}
