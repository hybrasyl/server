using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace Hybrasyl.Xml;

public class XmlLoadResponse<T>
{
    public List<T> Results { get; set; } = new();
    public Dictionary<string, string> Errors { get; set; } = new();
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
        var ms = new MemoryStream();
        var writer = new BsonWriter(ms);
        var reader = new BsonReader(ms);
        var serializer = new JsonSerializer();
        serializer.Serialize(writer, this);
        ms.Position = 0;
        var obj = serializer.Deserialize<T>(reader);
        ms.Close();
        return obj;
    }

    public static List<string> GetXmlFiles(string Path)
    {
        try
        {
            if (Directory.Exists(Path))
                return Directory.GetFiles(Path, "*.xml", SearchOption.AllDirectories)
                    .Where(predicate: x => !x.Contains(".ignore") || x.StartsWith("\\_")).ToList();
        }
        catch (Exception)
        {
            return null;
        }

        return new List<string>();
    }
}