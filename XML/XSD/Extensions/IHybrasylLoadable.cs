using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Hybrasyl.Xml
{
    public class XmlLoadResponse<T>
    {
        public List<T> Results { get; set; } = new List<T>();
        public Dictionary<string, string> Errors { get; set; } = new Dictionary<string, string>();
    }

    public interface IHybrasylLoadable<T>
    {
        public static string DataDirectory { get; }
        public static XmlLoadResponse<T> LoadAll(string baseDir) => throw new NotImplementedException();
        public T Clone();
    }

    public abstract class HybrasylLoadable
    {
        public static List<string> GetXmlFiles(string Path)
        {
            var ret = new List<string>();
            try
            {
                if (Directory.Exists(Path))
                {
                    var wef = new List<string>();

                    foreach (var asdf in Directory.GetFiles(Path, "*.xml", SearchOption.AllDirectories))
                        wef.Add(asdf.Replace(Path, ""));

                    return Directory.GetFiles(Path, "*.xml", SearchOption.AllDirectories).
                        Where(e => !e.Replace(Path, "").
                        StartsWith("\\_")).ToList();
                }
            }
            catch (Exception)
            {
                return null;
            }
            return ret;
        }
    }
}
