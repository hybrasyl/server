using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hybrasyl;
using Hybrasyl.Casting;
using Hybrasyl.Objects;
using Hybrasyl.Xml;

namespace Hybrasyl.Casting;

public class CastableController : IEnumerable<Rotation>
{
    public BookSlot LastCastableUsed { get; set; }
    private Dictionary<CreatureCastingSet, Rotation> Rotations = new();
    private HashSet<CreatureCastingSet> CastingSets = new();
    private HashSet<RotationEntry> ThresholdCasts = new();
    public Dictionary<string, BookSlot> Castables { get; set; } = new();

    public Guid MonsterGuid { get; set; }
    public Monster MonsterObj => Game.World.WorldData.GetWorldObject<Monster>(MonsterGuid);
    public bool HasAssailSkills { get; set; }

    public bool Enabled { get; set; }

    private string DebugLogHeader => $"{MonsterObj.Name} ({MonsterObj.Map.Name}@{MonsterObj.X},{MonsterObj.Y})";

    public CastableController(Guid id)
    {
        MonsterGuid = id;
    }

    public bool ContainsCastable(string castable) => Castables.ContainsKey(castable);

    public bool TryGetCastable(string castable, out BookSlot slot) => Castables.TryGetValue(castable, out slot);

    /// <summary>
    /// Given an already specified behaviorset for the monster, learn all the castables possible at 
    /// their level; or the castables specifically enumerated in the set.
    /// </summary>
    public void LearnCastables()
    {
        // All monsters get assail. TODO: hardcoded
        if (Game.World.WorldData.TryGetValueByIndex("Assail", out Castable assail))
            Castables.Add("Assail", new BookSlot { Castable = assail });

        if (CastingSets.Count == 0 || MonsterObj.BehaviorSet?.Castables == null)
            // Behavior set either doesn't exist or doesn't specify castables; no action needed
            return;

        // Default to automatic assignation if unsetF
        if (MonsterObj.BehaviorSet.Castables.Auto == true)
        {
            // If categories are present, use those. Otherwise, learn everything we can
            foreach (var category in MonsterObj.BehaviorSet.LearnSpellCategories)
            {
                foreach (var castable in Game.World.WorldData.GetSpells(MonsterObj.Stats.BaseStr,
                             MonsterObj.Stats.BaseInt, MonsterObj.Stats.BaseWis,
                             MonsterObj.Stats.BaseCon, MonsterObj.Stats.BaseDex, category))
                {
                    if (!Castables.ContainsKey(castable.Name))
                        Castables.Add(castable.Name, new BookSlot {Castable = castable});
                }
            }

            foreach (var category in MonsterObj.BehaviorSet.LearnSkillCategories)
            {
                foreach (var castable in Game.World.WorldData.GetSkills(MonsterObj.Stats.BaseStr, MonsterObj.Stats.BaseInt, MonsterObj.Stats.BaseWis,
                             MonsterObj.Stats.BaseCon, MonsterObj.Stats.BaseDex, category))
                {
                    if (!Castables.ContainsKey(castable.Name))
                        Castables.Add(castable.Name, new BookSlot { Castable = castable});
                }
            }

            if (MonsterObj.BehaviorSet.LearnSkillCategories.Count == 0 && MonsterObj.BehaviorSet.LearnSpellCategories.Count == 0)
            {
                // Auto add according to stats
                foreach (var castable in Game.World.WorldData.GetCastables(MonsterObj.Stats.BaseStr, MonsterObj.Stats.BaseInt, MonsterObj.Stats.BaseWis,
                             MonsterObj.Stats.BaseCon, MonsterObj.Stats.BaseDex))
                {
                    if (!Castables.ContainsKey(castable.Name))
                    {

                        if (castable.IsSkill)
                            Castables.Add(castable.Name, new BookSlot {Castable = castable});
                        else
                            Castables.Add(castable.Name, new BookSlot {Castable = castable});
                    }
                }
            }
        }

        // Handle any specific additions. Note that specific additions *ignore stat requirements*, 
        // to allow a variety of complex behaviors.
        foreach (var castable in MonsterObj.BehaviorSet.Castables.Castable)
        {
            if (Game.World.WorldData.TryGetValue(castable, out Castable xmlCastable))
            {
                if (xmlCastable.IsSkill)
                    Castables.Add(xmlCastable.Name, new BookSlot {Castable = xmlCastable});
                else
                    Castables.Add(xmlCastable.Name, new BookSlot {Castable = xmlCastable});
            }
        }

        foreach (var kvp in Castables)
            GameLog.SpawnInfo($"{MonsterObj.Name}: CastableController: learned {kvp.Key}");

    }


    /// <summary>
    /// Given a list of creature casting sets, process each one into a rotation.  
    /// </summary>
    /// <param name="sets">A list of CreatureCastingSets which will be evaluated and stored as rotations</param>
    public void ProcessCastingSets(List<CreatureCastingSet> sets)
    {
        if (sets.Count == 0)
        {
            Enabled = false;
            return;
        }
        foreach (var set in sets)
        {
            CastingSets.Add(set);
            if (set.Type == RotationType.Assail) HasAssailSkills = true;
            var newRotation = new Rotation(set);
            foreach (var entry in set.Castable)
            {
                if (Castables.TryGetValue(entry.Value, out var slot))
                {
                    var newEntry = new RotationEntry(slot, entry);
                    newRotation.Add(newEntry);

                    if (entry.UseOnce)
                        ThresholdCasts.Add(newEntry);
                }
                else
                    GameLog.SpawnError($"{MonsterObj.Name}: processing rotation: missing castable {entry.Value}");
            }

            // Now add categories
            foreach (var category in set.CategoryList)
            {
                var castableMatches = Castables.Where(x => x.Value.Castable.CategoryList.Contains(category));
                foreach (var match in castableMatches)
                {

                    var directive = new CreatureCastable
                    {
                        Value = match.Value.Castable.Name,
                        HealthPercentage = set.HealthPercentage,
                        Interval = set.Interval,
                        TargetPriority = set.TargetPriority,
                    };
                    newRotation.Add(new RotationEntry(match.Value, directive));
                }
            }

            Rotations[set] = newRotation;
            GameLog.SpawnInfo($"{MonsterObj.Name} Rotation resolved to: {Rotations[set]}");
            newRotation.Active = true;
        }
    }

    /// <summary>
    /// Calculate the next castable to be used.
    /// </summary>
    /// <returns>A RotationEntry structure indicating the rotation and castable to be used next (along with targeting), or null, if no castable is to be used.</returns>
    public RotationEntry GetNextCastable()
    {
        // Evaluate all UseOnce castables to see if one has triggered. If it has, return that immediately
        // If no UseOnce triggered, order rotations by priority(SecondsSinceLastUse - Interval) and select top rotation. 
        // For winning rotation, find RotationEntry with GetNextCastable()
        
        // Rotations have been pre-calculated to include categories / etc, so if there's nothing here we can't do anything

        if (Rotations.Count == 0 || Castables.Count == 0)
        {
            GameLog.SpawnDebug($"{DebugLogHeader}: no rotations exist, or no castables known");
            return null;
        }

        // Find the "most expired" rotation
        var rotation = Rotations.Values.Where(x => x.Active && x.SecondsSinceLastUse >= x.Interval)
            .OrderByDescending(y => y.Interval).FirstOrDefault();
        if (rotation == null)
        {
            GameLog.SpawnDebug($"{DebugLogHeader}: no active rotations or not enough time elapsed since last use");
            return null;

        }

        // Always handle UseOnce trigger thresholds (Rule #1)
        foreach (var threshold in ThresholdCasts.Where(c =>
                     c.Directive.HealthPercentage > 0 && c.Directive.HealthPercentage <= MonsterObj.Stats.HpPercentage))
        {
            if (!Castables.ContainsKey(threshold.Name))
                // Threshold references a skill or spell that the mob doesn't know; ignore
                continue;
            // Is this a use once trigger with a percentage defined? If so, it hits and returns immediately IF the
            // corresponding slot hasn't seen a trigger.              
            if (!threshold.UseOnce || threshold.ThresholdTriggered) continue;

            GameLog.SpawnDebug(
                $"{DebugLogHeader}: one-time threshold triggered: {threshold.Name}, {threshold.Threshold}%, priority {threshold.CurrentPriority}");
            threshold.ThresholdTriggered = true;
            return threshold;
        }

        return rotation.GetNextCastable();

    }

    public bool CanCast(string castable) => Castables.Keys.Contains(castable);

    public Rotation GetNextRotation() => Rotations.Count == 0 ? null : Rotations.Values.Where(x => x.Active).OrderBy(x => x.Priority).FirstOrDefault();

    public IEnumerator<Rotation> GetEnumerator() => Rotations.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
