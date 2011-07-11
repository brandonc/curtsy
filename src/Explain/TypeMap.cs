using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace Explain
{
    public class FoundTypes : IEnumerable<FoundTypes.TypeInfo>
    {
        public class TypeInfo {
            public string Name { get; set; }
            public TypeHint Type { get; set; }
            public string File { get; set; }
            public int LineNumber { get; set; }
        }

        public enum TypeHint
        {
            Class,
            Enum,
            Struct,
            Delegate,
            Interface
        }

        Dictionary<Tuple<string, string>, TypeInfo> types = new Dictionary<Tuple<string, string>, TypeInfo>();

        public void Add(string name, string file, int line, TypeHint typehint)
        {
            types.Add(new Tuple<string, string>(name, file), new TypeInfo() { File = file, LineNumber = line, Name = name, Type = typehint });
        }

        public FoundTypes()
        {
        }

        IEnumerator<FoundTypes.TypeInfo> IEnumerable<FoundTypes.TypeInfo>.GetEnumerator()
        {
            return types.Values.ToList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator)types.Values.ToList();
        }
    }
}
