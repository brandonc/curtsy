using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace Explain
{
    public class TypeMap : IEnumerable<TypeMap.TypeInfo>
    {
        public class TypeInfo {
            public string Name { get; set; }
            public TypeHint Type { get; set; }
            public string File { get; set; }
            public int LineNumber { get; set; }

            private string typebounds = @"[\s\(;:,]";

            public string GetPattern()
            {
                if(Name.IndexOf('<') > 0)
                    return "(" + typebounds + ")(" + Name.Sub(@"\<\w+\>", @"&lt;\w+&gt;") + ")(" + typebounds + ")";

                return "(" + typebounds + ")(" + Name + ")(" + typebounds + ")";
            }
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

        public TypeMap()
        {
        }

        IEnumerator<TypeMap.TypeInfo> IEnumerable<TypeMap.TypeInfo>.GetEnumerator()
        {
            return types.Values.ToList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator)types.Values.ToList();
        }
    }
}
