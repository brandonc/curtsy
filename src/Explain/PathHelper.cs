using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Explain
{
    class PathHelper
    {
        public string FromPath { get; set; }

        /// <summary>
        /// Creates a relative path from one file or folder to another.
        /// </summary>
        /// <param name="toPath">Contains the path that defines the endpoint of the relative path.</param>
        /// <returns>The relative path from the start directory to the end path.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public string MakeRelativePath(string toPath)
        {
            if (String.IsNullOrEmpty(toPath)) throw new ArgumentNullException("toPath");

            Uri fromUri = new Uri(FromPath);
            Uri toUri = new Uri(toPath);

            return fromUri.MakeRelativeUri(toUri).ToString().Replace('/', Path.DirectorySeparatorChar);
        }

        public PathHelper(string absoluteTargetPath)
        {
            if (String.IsNullOrEmpty(absoluteTargetPath)) throw new ArgumentNullException("absoluteTargetPath");

            FromPath = absoluteTargetPath;
        }
    }
}
