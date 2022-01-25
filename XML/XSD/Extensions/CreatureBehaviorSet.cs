using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Hybrasyl.Xml;

public partial class CreatureBehaviorSet : HybrasylLoadable, IHybrasylLoadable<CreatureBehaviorSet>
{
    public static string DataDirectory => "behaviorsets";

    public static CreatureBehaviorSet operator &(CreatureBehaviorSet cbs1, CreatureBehaviorSet cbs2)
    {
        // Usage: a & b
        // a is intended to be set with a defined import value (eg Import=)
        // b is the set referenced by the import value
        var cbsMerge = cbs2.Clone<CreatureBehaviorSet>();
        // We don't do deep merges for now
        if (cbs1.Behavior != null)
        {
            cbsMerge.Behavior.Assail = cbs1.Behavior.Assail ?? cbs2.Behavior.Assail;
            cbsMerge.Behavior.Casting = cbs1.Behavior.Casting ?? cbs2.Behavior.Casting;
            cbsMerge.Behavior.Hostility = cbs1.Behavior.Hostility ?? cbs2.Behavior.Hostility;
            cbsMerge.Behavior.SetCookies = cbs1.Behavior.SetCookies ?? cbs2.Behavior.SetCookies;
        }
        if (cbs1.Castables != null)
        {
            cbsMerge.Castables.Castable = cbs1.Castables.Castable ?? cbs2.Castables.Castable;
            cbsMerge.Castables.SkillCategories = string.IsNullOrEmpty(cbs1.Castables.SkillCategories) ?
                cbs2.Castables.SkillCategories : cbs1.Castables.SkillCategories;
            cbsMerge.Castables.SpellCategories = string.IsNullOrEmpty(cbs1.Castables.SpellCategories) ?
                cbs2.Castables.SpellCategories : cbs1.Castables.SpellCategories;
            cbsMerge.Castables.Auto = cbs2.Castables.Auto == true || cbs1.Castables.Auto;
        }
        if (cbs1.StatAlloc != null)
        {
            cbsMerge.StatAlloc = string.IsNullOrEmpty(cbs1.StatAlloc) ? cbs2.StatAlloc : cbs1.StatAlloc;            
        }
        return cbsMerge;
    }

    public static XmlLoadResponse<CreatureBehaviorSet> LoadAll(string baseDir)
    {
        var ret = new XmlLoadResponse<CreatureBehaviorSet>();
        var imports = new Dictionary<string, CreatureBehaviorSet>();
        foreach (var xml in GetXmlFiles(Path.Join(baseDir, DataDirectory)))
        {
            if (xml.Contains(".ignore"))
                continue;

            try
            {
                CreatureBehaviorSet set = LoadFromFile(xml);
                if (!string.IsNullOrEmpty(set.Import))
                    imports.Add(xml, set);
                else
                    ret.Results.Add(set);
            }
            catch (Exception e)
            {
                ret.Errors.Add(xml, e.ToString());
            }
        }

        // Now process imports. This could be made nicer
        foreach (var importset in imports)
        {
            var importedSet = ret.Results.FirstOrDefault(s => s.Name.ToLower() == importset.Value.Import.ToLower());
            if (importedSet == null)
            {
                ret.Errors.Add(importset.Key, $"Referenced import set {importset.Value.Import} not found");
                continue;
            }
            var newSet = importedSet.Clone<CreatureBehaviorSet>();
            var resolved = importset.Value & newSet;
            resolved.Name = importset.Value.Name;
            ret.Results.Add(resolved);
        }
        return ret;
    }
}