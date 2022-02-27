using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Hybrasyl.Xml;

public partial class CreatureBehaviorSet : HybrasylLoadable, IHybrasylLoadable<CreatureBehaviorSet>
{
    public static string DataDirectory => "behaviorsets";

    /// <summary>
    /// Merge two behavior sets together
    /// </summary>
    /// <param name="cbs1">Target behavior set</param>
    /// <param name="cbs2">Source behavior set (import)</param>
    /// <returns></returns>
    public static CreatureBehaviorSet operator &(CreatureBehaviorSet cbs1, CreatureBehaviorSet cbs2)
    {
        // Usage: a & b
        // a is intended to be set with a defined import value (eg Import=)
        // b is the set referenced by the import value
        var newCbs = new CreatureBehaviorSet();

        newCbs.Name = cbs1.Name;
        newCbs.StatAlloc = string.IsNullOrEmpty(cbs1.StatAlloc) ? cbs2.StatAlloc ? cbs1.StatAlloc;

        if (cbs1.Behavior != null)
        {
            if (cbs2.Behavior != null)
            {
                cbsMerge.Behavior.CastableSets.AddRange(cbs1.Behavior.CastableSets);
                cbsMerge.Behavior.Hostility = cbs1.Behavior.Hostility ?? cbs2.Behavior.Hostility;
                cbsMerge.Behavior.SetCookies = cbs1.Behavior.SetCookies ?? cbs2.Behavior.SetCookies;
            }
        }
        else
            cbsMerge.Behavior = cbs2.Behavior;
        if (cbs1.Castables != null)
        {
            cbsMerge.Castables.Castable = cbs1.Castables.Castable ?? cbs2.Castables.Castable;
            cbsMerge.Castables.SkillCategories = string.IsNullOrEmpty(cbs1.Castables.SkillCategories) ?
                cbs2.Castables.SkillCategories : cbs1.Castables.SkillCategories;
            cbsMerge.Castables.SpellCategories = string.IsNullOrEmpty(cbs1.Castables.SpellCategories) ?
                cbs2.Castables.SpellCategories : cbs1.Castables.SpellCategories;
            cbsMerge.Castables.Auto = cbs2.Castables.Auto == true || cbs1.Castables.Auto;
        }
        else
        {
            cbsMerge.Castables = cbs2.Castables;
        }
        if (cbs1.StatAlloc != null)
        {
            cbsMerge.StatAlloc = string.IsNullOrEmpty(cbs1.StatAlloc) ? cbs2.StatAlloc : cbs1.StatAlloc;            
        }
        else
        {
            cbsMerge.StatAlloc = cbs2.StatAlloc;
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