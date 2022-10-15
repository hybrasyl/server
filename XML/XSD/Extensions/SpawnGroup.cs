using System;
using System.IO;

namespace Hybrasyl.Xml;

public partial class SpawnGroup : HybrasylLoadable, IHybrasylLoadable<SpawnGroup>
{
    public static string Directory => "spawngroups";

    public static XmlLoadResponse<SpawnGroup> LoadAll(string baseDir)
    {
        var ret = new XmlLoadResponse<SpawnGroup>();
        foreach (var xml in GetXmlFiles(Path.Join(baseDir, Directory)))
            try
            {
                var group = LoadFromFile(xml);
                ret.Results.Add(group);
            }
            catch (Exception e)
            {
                ret.Errors.Add(xml, e.ToString());
            }

        return ret;
    }

    public static SpawnGroup operator +(SpawnGroup sg1, SpawnGroup sg2)
    {
        var merged = sg1.Clone<SpawnGroup>();
        merged.Spawns.AddRange(sg2.Spawns);
        return merged;
    }
}