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
        var newCbs = new CreatureBehaviorSet
        {
            Name = cbs1.Name,
            StatAlloc = string.IsNullOrEmpty(cbs1.StatAlloc) ? cbs2.StatAlloc : cbs1.StatAlloc,
            Behavior = new CreatureBehavior(),
            Castables = new CreatureCastables()
        };

        newCbs.Behavior.CastableSets = new List<CreatureCastingSet>();
        newCbs.Castables.Castable = new List<string>();

        if (cbs1.Behavior != null)
        {
            newCbs.Behavior.CastableSets.AddRange(cbs1.Behavior.CastableSets);
            newCbs.Behavior.Hostility = cbs1.Behavior.Hostility;
            newCbs.Behavior.SetCookies = cbs1.Behavior.SetCookies;
        }

        if (cbs2.Behavior != null)
        {
            newCbs.Behavior.CastableSets.AddRange(cbs2.Behavior.CastableSets);
            newCbs.Behavior.Hostility = cbs2.Behavior.Hostility;
            newCbs.Behavior.SetCookies = cbs2.Behavior.SetCookies;
        }

        if (cbs1.Castables != null)
        {
            newCbs.Castables.Castable.AddRange(cbs1.Castables.Castable);
            if (!string.IsNullOrEmpty(cbs1.Castables.SkillCategories))
                newCbs.Castables.SkillCategories = cbs1.Castables.SkillCategories;
            if (!string.IsNullOrEmpty(cbs1.Castables.SpellCategories))
                newCbs.Castables.SpellCategories = cbs1.Castables.SpellCategories;
            newCbs.Castables.Auto = cbs1.Castables.Auto;
        }

        if (cbs2.Castables != null)
        {
            newCbs.Castables.Castable.AddRange(cbs2.Castables.Castable);
            if (!string.IsNullOrEmpty(cbs2.Castables.SkillCategories))
                newCbs.Castables.SkillCategories += $" {cbs2.Castables.SkillCategories}";
            if (!string.IsNullOrEmpty(cbs2.Castables.SpellCategories))
                newCbs.Castables.SpellCategories += $" {cbs2.Castables.SpellCategories}";
        }

        return newCbs;
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