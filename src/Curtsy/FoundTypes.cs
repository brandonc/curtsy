using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace Curtsy
{
    // Maintains meta data about types encountered during the parsing process.
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

        Dictionary<Tuple<string, string>, TypeInfo> types;

        public FoundTypes()
        {
            types = new Dictionary<Tuple<string, string>, TypeInfo>();
        }

        // The combination of type name and file name is used
        // as the key for the type. This is weak because technically 
        // namespacing or partial classes could create a duplicate key

        // To fix this, it would be necessary to maintain a stack of
        // namespaces encountered during the file parsing and use that for this method
        public void Add(string name, string file, int line, TypeHint typehint)
        {
            types.Add(new Tuple<string, string>(name, file), new TypeInfo() { File = file, LineNumber = line, Name = name, Type = typehint });
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
