using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

namespace Hybrasyl.Xml;

public class XmlLoadResponse<T>
{
    public List<T> Results { get; set; } = new List<T>();
    public Dictionary<string, string> Errors { get; set; } = new Dictionary<string, string>();
}

public interface IHybrasylLoadable<T>
{
    public static string DataDirectory { get; }
    public static XmlLoadResponse<T> LoadAll(string baseDir) => throw new NotImplementedException();
}

[Serializable]
public abstract class HybrasylLoadable
{
    public T Clone<T>()
    {
        MemoryStream ms = new MemoryStream();
        BinaryFormatter bf = new BinaryFormatter();
        bf.Serialize(ms, this);
        ms.Position = 0;
        object obj = bf.Deserialize(ms);
        ms.Close();
        return (T)obj;
    }

    public static List<string> GetXmlFiles(string Path)
    {
        try
        {
            if (Directory.Exists(Path))
            {
                return Directory.GetFiles(Path, "*.xml", SearchOption.AllDirectories)
                    .Where(x => !x.Contains(".ignore") || x.StartsWith("\\_")).ToList();
            }
        }
        catch (Exception)
        {
            return null;
        }
        return new List<string>();
    }
}