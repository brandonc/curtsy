using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Curtsy
{
    // PathHelper is a helper class that accepts a root directory
    // and can produce relative paths to that directory given another path.
    public class PathHelper
    {
        public string RootPath { get; set; }

        public string MakeRelativePath(string toPath)
        {
            if (String.IsNullOrEmpty(toPath)) throw new ArgumentNullException("toPath");

            Uri fromUri = new Uri(RootPath);
            Uri toUri = new Uri(toPath);

            return fromUri.MakeRelativeUri(toUri).ToString().Replace('/', Path.DirectorySeparatorChar);
        }

        public PathHelper(string rootPath)
        {
            if (String.IsNullOrEmpty(rootPath)) throw new ArgumentNullException("absoluteTargetPath");

            RootPath = rootPath.EndsWith(Path.DirectorySeparatorChar.ToString()) ? rootPath : rootPath + Path.DirectorySeparatorChar;
        }
    }
}
