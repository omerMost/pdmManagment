using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace LeanVault.AddIn.Services
{
    public class BomRow
    {
        public string PartNumber { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
        public string Material { get; set; }
        public string Revision { get; set; }
    }

    public class SwBomExtractor
    {
        private readonly ISldWorks _sw;
        private readonly SwAssemblyWalker _walker;

        public SwBomExtractor(ISldWorks sw)
        {
            _sw = sw;
            _walker = new SwAssemblyWalker();
        }

        public IReadOnlyList<BomRow> Extract(IAssemblyDoc assembly)
        {
            var refs = _walker.GetAllReferencedFiles(assembly);
            var partCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // In M4 flat list, it just gives us unique parts. But wait, if we want quantity, 
            // the flat list in SwAssemblyWalker deduplicates files by path.
            // If the user wants quantity, we should count occurrences from GetComponents(false) without deduplication.
            object[] components = (object[])assembly.GetComponents(false);
            if (components != null)
            {
                foreach (var compObj in components)
                {
                    var comp = compObj as IComponent2;
                    if (comp != null && !comp.IsSuppressed())
                    {
                        var path = comp.GetPathName();
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            if (partCounts.ContainsKey(path))
                                partCounts[path]++;
                            else
                                partCounts[path] = 1;
                        }
                    }
                }
            }

            var results = new List<BomRow>();

            foreach (var kvp in partCounts)
            {
                var path = kvp.Key;
                var qty = kvp.Value;

                var doc = _sw.GetOpenDocumentByName(path) as IModelDoc2;
                if (doc != null)
                {
                    var props = ReadSwProperties(doc);
                    results.Add(new BomRow
                    {
                        PartNumber = props.TryGetValue("PartNumber", out var pn) ? pn : Path.GetFileNameWithoutExtension(path),
                        Description = props.TryGetValue("Description", out var desc) ? desc : "",
                        Material = props.TryGetValue("Material", out var mat) ? mat : "",
                        Revision = props.TryGetValue("Revision", out var rev) ? rev : "",
                        Quantity = qty
                    });
                }
                else
                {
                    // Fallback if not loaded
                    results.Add(new BomRow
                    {
                        PartNumber = Path.GetFileNameWithoutExtension(path),
                        Description = "",
                        Material = "",
                        Revision = "",
                        Quantity = qty
                    });
                }
            }

            return results.OrderBy(r => r.PartNumber).ToList();
        }

        private static Dictionary<string, string> ReadSwProperties(IModelDoc2 doc)
        {
            var props = new Dictionary<string, string>();
            if (doc == null) return props;

            var customPropMgr = doc.Extension.CustomPropertyManager[""];
            if (customPropMgr == null) return props;

            string[] names = (string[])customPropMgr.GetNames() ?? Array.Empty<string>();
            foreach (var name in names)
            {
                customPropMgr.Get4(name, false, out string val, out string resolvedVal);
                props[name] = resolvedVal ?? val ?? "";
            }
            return props;
        }
    }
}
