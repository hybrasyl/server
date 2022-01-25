using System;
using System.IO;

namespace Hybrasyl.Xml;

public partial class Creature : HybrasylLoadable, IHybrasylLoadable<Creature>
{
    // eventually calculate this from type name
    public static string Directory => "creatures";

    public static XmlLoadResponse<Creature> LoadAll(string baseDir)
    {
        var ret = new XmlLoadResponse<Creature>();

        foreach (var xml in GetXmlFiles(Path.Join(baseDir, Directory)))
        {
            try
            {
                Creature c = LoadFromFile(xml);
                // Resolve subtypes
                foreach (var subtype in c.Types)
                {
                    var creatureVariant = c & subtype;
                    // xml is really annoying sometimes
                    if (string.IsNullOrEmpty(creatureVariant.Name))
                        ret.Errors.Add(xml, "subtype found with no name");
                    else
                        ret.Results.Add(c);
                }
                if (!string.IsNullOrEmpty(c.Name))
                    ret.Results.Add(c);
                else
                    ret.Errors.Add(xml, "Creature has no name");
            }
            catch (Exception e)
            {
                ret.Errors.Add(xml, e.ToString());
            }
        }
        return ret;
    }

    public static Creature operator &(Creature c1, Creature c2)
    {
        var creatureMerge = c1.Clone<Creature>();
        creatureMerge.BehaviorSet = string.IsNullOrEmpty(c2.BehaviorSet) ? c1.BehaviorSet : c2.BehaviorSet;
        creatureMerge.Description = string.IsNullOrEmpty(c2.Description) ? c1.Description : c2.Description;
        creatureMerge.Name = c2.Name;
        creatureMerge.Hostility = c2.Hostility ?? c1.Hostility;
        creatureMerge.Loot = c2.Loot + c1.Loot;
        return c1;
    }
}