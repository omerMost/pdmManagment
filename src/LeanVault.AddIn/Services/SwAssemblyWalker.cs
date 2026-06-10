using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace LeanVault.AddIn.Services
{
    public class AssemblyFileRef
    {
        public string FilePath { get; }
        public bool IsMissing { get; }
        public bool IsVirtual { get; }
        public int Depth { get; }

        public AssemblyFileRef(string filePath, bool isMissing, bool isVirtual, int depth)
        {
            FilePath = filePath;
            IsMissing = isMissing;
            IsVirtual = isVirtual;
            Depth = depth;
        }
    }

    public class SwAssemblyWalker
    {
        public IReadOnlyList<AssemblyFileRef> GetAllReferencedFiles(IAssemblyDoc assembly)
        {
            var result = new List<AssemblyFileRef>();
            if (assembly == null) return result;

            object[] components = (object[])assembly.GetComponents(false); // false gets all components recursively (flat list)
            if (components == null) return result;

            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var compObj in components)
            {
                var comp = compObj as IComponent2;
                if (comp == null) continue;

                string path = comp.GetPathName();
                if (string.IsNullOrWhiteSpace(path)) continue;

                if (uniquePaths.Add(path))
                {
                    result.Add(new AssemblyFileRef(
                        filePath: path,
                        isMissing: false,
                        isVirtual: false,
                        depth: 0 // Using flat list for M4
                    ));
                }
            }
            return result;
        }
    }
}
