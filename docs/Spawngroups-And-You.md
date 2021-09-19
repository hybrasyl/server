# Spawngroups and You

This documentation introduces Hybrasyl's new spawning system, and hopes to be a guide for how it works and how to create and maintain spawns, creatures, behaviorsets and more using it.

## Overview

The old spawngroup system was a hacky mess. It made many compromises from a design standpoint and resulted in an extremely difficult to use system with not a lot of reusable parts. Each spawngroup, due to attempts at programmatic generation from questionably useful or correct sources, ended up being handcrafted, extremely repetitive, and hard to edit; it was basically impossible to disable specific spawns or groups on maps; there was no way to define spawns on individual maps without creating a group; etc. 

On top of that, creatures had no real meaning besides defining sprites and creature names. Oh, and terrible descriptions that were mostly Mitch Hedberg quotes. Only the best data.

All of these problems led us to starting over completely.

## Doctor Spawngroups, or How I Learned To Put A Goblin Butler On Exactly One Map

Maps are the new "root" for spawns. Maps now have the ability to define spawns in their own internal group:

```xml
<Map Id="500" Name="Goblin Village">
<Spawns>
  <Spawn Name="Goblin Butler"/>
  ...
</Spawns>
</Map>
```

Spawngroups are now a reusable type. `<Spawns>` as defined in our Goblin Village, here, is the same exact underlying type (`SpawnGroup`) as a defined spawngroup in a standalone file. This means it is particularly easy to add or remove from spawngroups, and the groups themselves can be easily disabled. 

The intention with this refactor is for all of these new types to be reusable components which are (mostly) additive - but maps now become the *primary* way to add spawns to, you know. 

A map.

## Spawngroups

```xml
  <xs:complexType name="SpawnGroup">
    <xs:sequence>
      <xs:element name="Spawn" type="Spawn" minOccurs="0" maxOccurs="unbounded"/>
    </xs:sequence>
    <xs:attribute name="BaseLevel" type="xs:string" use="optional"/>
    <xs:attribute name="Disabled" type="xs:boolean" default="false"/>
    <xs:attribute name="Name" type="xs:string" use="optional"/>
  </xs:complexType>
```

A spawngroup is pretty simple, by comparison to its previous structure.

It can have a `Name` that can be referenced for importing or the purposes of disablement. A `Map` spawngroup defined via `<Spawns>` can _also_ have a name, and can _also_ be imported (mindblown.gif). If not specified, it receives a default which can be later used and referenced in tooling or other maps.

`BaseLevel` can be defined at a group level. This specifies the base level of spawns within the group. If no other levels are specified, the base level will be used. The base level can also be referenced in `Level` definitions within spawns themselves in a formula (see `Spawns` below). _Group question: seems like Creature itself should be able to define a base level, as a fallback in case neither spawn nor map defines it?_

`Disabled` is pretty simple, right? A spawn either is or is not disabled.

Finally, a spawngroup can obviously have a whole lot of spawns! Huzzah!

## Spawns

Spawns actually specify the who, what, why and when of monsters appearing on maps. 

```xml
<xs:complexType name="Spawn">
  <xs:sequence>
    <xs:element name="Loot" type="hyb:LootList" minOccurs="0" maxOccurs="1"/>
    <xs:element name="Coordinates" minOccurs="0" maxOccurs="1" type="hyb:SpawnCoordinatesList"/>
    <xs:element name="Damage" minOccurs="0" maxOccurs="1" type="hyb:SpawnDamage"/>
    <xs:element name="Defense" minOccurs="0" maxOccurs="1" type="hyb:SpawnDefense"/>
    <xs:element name="Spec" minOccurs="0" maxOccurs="1" type="hyb:SpawnSpec"/>
    <xs:element name="Base" minOccurs="0" maxOccurs="1" type="hyb:SpawnBase"/>
    <xs:element name="Hostility" minOccurs="0" maxOccurs="1" type="CreatureHostilitySettings"/>
    <xs:element name="SetCookies" minOccurs="0" maxOccurs="1" type="CreatureCookies"/>
  </xs:sequence>
  <xs:attribute name="Import" type="xs:string" use="optional"/>
  <xs:attribute name="Name" type="xs:string" use="optional"/>
  <xs:attribute name="Flags" type="hyb:SpawnFlags" use="optional"/>
</xs:complexType>
```

`Import` allows a named spawngroup to be imported wholesale. All of its spawns will be added to the parent container (generally, a map's spawngroup). `Name` is the name of the creature that will be spawned. 

`Flags` is an enumeration supporting, currently, `MovementDisabled` to disable monster movement; `AiDisabled` to disable AI from running on a monster, and `DeathDisabled` making it impossible for a spawn / monster to die.

### Spawns: Loot

Spawns can define individual `Loot` (tables or lists), which is (generally speaking) additive to any base `Loot` defined elsewhere. Since no changes have largely been made to `Loot` we simply refer to it as "the existing loot structure" and call it a day. The big change here is that `Loot` can be easily reused between creatures and spawns. For instance, a Goblin might define a loot table dropping goblin skulls within its `Creature` specification. A subtype of Goblin, say, a Goblin Butler, could have an additional loot table dropping `dainty leather gloves` or something equally ridiculous. Lastly, a spawn for a `Badass Goblin Butler` could add an additional loot table which might drop `steel tophat`. This allows loot to be abstracted into more easily managed groups (base creature loot such as skulls, fiors, sausages, whatever, spawn loot specifying special oneoffs and bosses) and significantly reduces repetitiveness.

### Spawns: Coordinates

`Coordinates` can be used to define fixed X,Y coordinates where a monster will be spawned:

```xml
<xs:complexType name="SpawnCoordinatesList">
  <xs:sequence>
    <xs:element name="Coordinate" type="hyb:SpawnCoordinate" minOccurs="1" maxOccurs="unbounded"/>
  </xs:sequence>
</xs:complexType>

<xs:complexType name="SpawnCoordinate">
  <xs:attribute name="X" type="xs:unsignedByte" use="required"/>
  <xs:attribute name="Y" type="xs:unsignedByte" use="required"/>
</xs:complexType>
```

For instance, within a spawn:

```xml
<Coordinates>
  <Coordinate X="4" Y="10"/>
</Coordinates>
```

would ensure a mob is spawned at (4,10) and ONLY (4,10).

### Spawns: Damage

`Damage` gives some fine-grained control over min/max damage (used to effectively implement an equipped "weapon"), as well as the ability to specify an outgoing damage element:

```xml
  <xs:complexType name="SpawnDamage">
    <xs:attribute name="MinDmg" type="xs:string" use="optional"/>
    <xs:attribute name="MaxDmg" type="xs:string" use="optional"/>
    <xs:attribute name="Element" type="hyb:ElementType" use="optional" default="None"/>
  </xs:complexType>
```

For instance:

```xml
<Damage MinDmg="3" MaxDmg="9" Element="Ham"/>
```

would create a monster with weapon minimum damage of 3, maximum 9, attacking with the fierce power of `Ham`. Watch out, `Potato Belt` users!

`ElementType` has been expanded somewhat:

```xml
<!-- Elements -->
<xs:simpleType name="ElementType">
  <xs:restriction base="xs:token">
    <xs:enumeration value="None"/>
    <xs:enumeration value="Fire" />
    <xs:enumeration value="Water" />
    <xs:enumeration value="Wind" /
    <xs:enumeration value="Earth" />
    <xs:enumeration value="Light" />
    <xs:enumeration value="Dark" />
    <xs:enumeration value="Wood" />
    <xs:enumeration value="Metal" /> 
    <xs:enumeration value="Undead" />
    <xs:enumeration value="RandomFour" />
    <xs:enumeration value="RandomEight"/>
    <xs:enumeration value="Random"/>
  </xs:restriction>
</xs:simpleType>
```

`RandomFour` will select a random element from the "prime" elements: Earth, Air, Fire, Water. `RandomEight` will select a random element from the "prime" elements plus Light, Dark, Wood, Metal. `Random` will pick any of the elements at random.

### Spawns: Defense

`Defense` allows AC/MR to be defined (can be a formula) and also allows a defensive element to be specified.

```xml  
<xs:complexType name="SpawnDefense">
  <xs:attribute name="Ac" type="xs:string" use="optional"/>
  <xs:attribute name="Mr" type="xs:string" use="optional"/>
  <xs:attribute name="Element" type="hyb:ElementType" use="optional" default="None"/>
</xs:complexType>
```

### Spawns: Spec

`Spec` allows the nuts and bolts of spawning to be defined (when, how many, maximum number on a map, etc):

```xml
  <xs:complexType name="SpawnSpec">   
    <xs:attribute name="MinCount" type="xs:string" use="optional"/>
    <xs:attribute name="MaxCount" type="xs:string" use="optional"/>
    <xs:attribute name="MaxPerInterval" type="xs:string" use="optional"/>
    <xs:attribute name="Interval" type="xs:string" use="optional"/>
    <xs:attribute name="Limit" type="xs:string" use="optional"/>
    <xs:attribute name="When" type="xs:string" use="optional"/>
    <xs:attribute name="Percentage" type="xs:string" use="optional"/>
    <xs:attribute name="Disabled" type="xs:boolean" use="optional" default="false"/>
  </xs:complexType>
```

`MinCount` and `MaxCount` define the minimum and maximum number of spawns that will be created. `MinCount` will ensure that the minimum number always exists; `MaxCount` defines a maximum number. Both can be formulas.

In the absence of a defined maximum, Hybrasyl will default to something sane: 10% of the tiles on a map. For instance, a 30x30 map with an uncapped spawn would have a default maximum of 90. `MinCount` always defaults to 1.

`MaxPerInterval` defines a cap on the number of spawns that will occur during the specified spawn interval. For instance, if `MaxPerInterval` is 2, a maximum of 2 spawns will be created during the specified `Interval`.

`Interval` is pretty obvious: it's the number of seconds between this spawn being evaluated. In the absence of a specified value, it defaults to 60 seconds.

`Limit` *can probably be removed* since I believe it is obviated by `MaxCount` and `MaxPerInterval`.

`When` controls when the spawn is active: a specific time (`16:00`) or date (`August 25`) or a general season (`Spring`, `Summer`, `Fall`, `Winter`). **Implementation TBD**

`Percentage` can be used instead of `MaxCount` to represent a fixed percentage of tiles. If present it takes precedence over `MaxCount`.

`Disabled` - pretty straightforward. An individual spawn can be disabled via script or slash command (eg `/disablespawn "Goblin Butler"`)

### Spawns: Base

`Base` defines `BehaviorSet` and `Level` for a spawn. Creatures can have default behavior sets, but spawns can also override this for more complex / unique behavior. `Level` can be used either as a raw level number, or, can be used with `BaseLevel` defined in its container spawngroup to easily create higher level monsters. For instance, a map might define `BaseLevel` as 10, then a spawn can have a formula such as `$BASELEVEL+5`. (_See also: Group Question in spawngroups_)

```xml
<xs:complexType name="SpawnBase">
  <xs:attribute name="BehaviorSet" type="xs:string" use="optional"/>
  <xs:attribute name="Level" type="xs:string" use="optional"/>
</xs:complexType>
```

For instance:

```xml
<Spawn Name="Goblin Butler">
  <Base BehaviorSet="butler" Level="$BASELEVEL+5"/>
</Spawn>
```

would spawn a Goblin Butler with the `butler` behavior set, with a level of 15 (assuming the containing spawngroup defined the base level as 10 as discussed above).

### Spawns: Hostility

`Hostility` can be inherited from the behaviorset definition, or can be overriden in a spawn for more complex actions. `Spawns` will always default to hostile in the absence of any `Hostility` settings, because generally speaking, monsters are monsters. The idea of reformed rats and idle centipedes is the work of madmen.

```xml
<xs:complexType name="CreatureHostility">
  <xs:attribute name="ExceptCookie" type="xs:token" use="optional"/>
  <xs:attribute name="OnlyCookie" type="xs:token" use="optional"/>
</xs:complexType>
  
<xs:complexType name="CreatureHostilitySettings">
  <xs:sequence>
    <xs:element name="Players" type="CreatureHostility" minOccurs="0" maxOccurs="1"/>
    <xs:element name="Monsters" type="CreatureHostility" minOccurs="0" maxOccurs="1"/>
    <xs:element name="Neutral" type="CreatureHostility" minOccurs="0" maxOccurs="1"/>
  </xs:sequence>
</xs:complexType>
```

For instance:

```xml
<Hostility>
  <Players ExceptCookie="Goblin-Friendly"/>
</Hostility>
``` 

would make a monster hostile to all players *except* those with the specified scripting cookie `Goblin-Friendly`. By comparison, `OnlyCookie` would instead make a monster hostile only to players with the specified cookie (for instance `OnlyCookie="BurnedDownGoblinVillage"`). *The butler is out for revenge*.

### Spawn: SetCookies

New functionality is included in `Spawn` and `BehaviorSet` types to set scripting cookies automatically when a creature is spawned in the world. These can be used to tune a variety of behaviors in-game (such as making monsters hostile to other monsters, etc).

The SetCookies structure is intentionally simple, just allowing cookies to be set arbitrarily with name/value pairs:

```xml
<xs:complexType name="CreatureCookie">
  <xs:attribute name="Name" type="xs:string" use="required"/>
  <xs:attribute name="Value" type="xs:string" use="optional"/>
</xs:complexType>

<xs:complexType name="CreatureCookies">
  <xs:sequence>
    <xs:element name="Cookie" type="CreatureCookie" minOccurs="0" maxOccurs="unbounded"/>
  </xs:sequence>
</xs:complexType>
```

As an example, in a spawn or behaviorset:

```xml
<SetCookies>
  <Cookie Name="OfferTea" Value="true"/>
</SetCookies>
```
  
Now our butler can be a proper host, if only we give him some scripting.

## Behavior Sets

*Behavior sets* are a completely new way to begin to encapsulate and extract AI behavior and settings into easily configurable XML, rather than using arbitrarily defined hardcoded elements.

```xml
<!-- BehaviorSet structure for AI tweakable settings -->
<xs:complexType name="CreatureBehaviorSet">
  <xs:sequence>
    <xs:element name="StatAlloc" minOccurs="0" maxOccurs="1" type="CreatureStatAlloc"/>
    <xs:element name="Castables" minOccurs="0" maxOccurs="1" type="CreatureCastables"/>
    <xs:element name="Behavior" minOccurs="0" maxOccurs="1" type="CreatureBehavior"/>
  </xs:sequence>
  <xs:attribute name="Name" type="xs:string" use="required"/>
  <xs:attribute name="Import" type="xs:string" use="optional"/>
</xs:complexType>
```

### BehaviorSet: StatAlloc

Previously, castables associated with creatures were weird, artisanal values that had individually coded damage levels, and were completely separate from player formulas. For what should be some extremely obvious reasons, this ended up being very annoying to maintain - especially because there was no way to abstract this out to a reusable component besides `Spawngroups` themselves.

Now, monsters are closer to players in that they still have levels, but also auto-allocate their stat points. A `BehaviorSet` can control this allocation, for instance, to make stronger Goblin Warriors or weaker Butlers, and so on. And monsters, at long last, use the same formulas as players (generally speaking) for castable damage calculations.

`StatAlloc` has an extremely simple definition - it is a just list of stats.

```xml
<xs:simpleType name="CreatureStatAlloc">
  <xs:list itemType="hyb:StatType"/>
</xs:simpleType>
```

For instance:

```xml
<StatAlloc>Str Str Con Con Dex Int Wis</StatAlloc>
```

would allocate level points to Strength (2), Constitution (2), followed by Dexterity, Intelligence and Wisdom.

By way of example, let's say we have a Goblin Butler with the above allocation, and his base level is 20. He's a pretty beefy butler, which I suppose makes sense given his Goblin heritage. At level 20, his stats would be Str 9, Con 9, Dex 6, Int 6, Wis 5. StatAlloc doesn't do any checking because it wants to be as flexible as possible - so if you don't put any other stats here besides, say, `Str`, everything will be allocated to Strength.

...He'll be one `Badass Goblin Butler`.

### Behavior Sets: Castables

`Castables` determines what castables the monster knows. There's some significant flexibility included here - a monster can either auto-learn based on what is available for its allocated stat points; it can be given specific castables regardles of points; it can also auto-learn, but narrowed into castable categories:

```xml
<xs:complexType name="CreatureCastables">
  <xs:sequence>
    <xs:element name="Castable" type="xs:string" minOccurs="0" maxOccurs="unbounded"/>
  </xs:sequence>
  <xs:attribute name="Auto" type="xs:boolean" default="true"/>
  <xs:attribute name="SkillCategories" type="xs:token" use="optional"/>
  <xs:attribute name="SpellCategories" type="xs:token" use="optional"/>                  
</xs:complexType>
```

For instance:

```xml
<Castables>
  <Castable>ard ham</Castable>
  <Castable>Ham Slam</Castable>
</Castable>
```

`Castable` here is just a list of castables that will be automatically given to a monster regardless of points. In the presence of any `Castable` defined, `Auto` defaults to false.

If we wanted to allow the monster to pick up castables automatically, we can either not define the tag at all (default is to assign automaticaly based on points, but this can potentially create some weird behavior where a creature will learn things like `Mentor`. So we can narrow it to categories (existing categories which are in castables):

```xml
<Castables SkillCategories="HamAndHamRelated" SpellCategories="Heal"/>
```

In the example above the monster would _only_ learn skills from the Ham & Ham Related category, and would learn heals.

Sadly, no more `Mentor`. I mean....I _guess_ the butler needs a way to pass on his craft, but NOT while he's on our clock. Get to work, you lazy goblin!

  
### Behavior Sets: Behavior

`Behavior` provides some tunables for AI settings on monsters. As of this writing, its intended use is to define a _casting behavior_, hostility settings, cookies, and an assail rotation:

```xml
<xs:complexType name="CreatureBehavior"> 
  <xs:sequence>
    <xs:element name="Casting" minOccurs="0" maxOccurs="1" type="CreatureCastingBehavior"/>
    <xs:element name="Assail" minOccurs="0" maxOccurs="1" type="CreatureCastingSet"/>
    <xs:element name="Hostility" minOccurs="0" maxOccurs="1" type="CreatureHostilitySettings"/>
    <xs:element name="SetCookies" minOccurs="0" maxOccurs="1" type="CreatureCookies "/>
  </xs:sequence>
</xs:complexType>
```

### Behavior: Casting & Assail

`Casting` allows a casting definition to be expressed for the monster. This inherits most of the old setup for when monsters will use spells, but removes any hardcoding from various elements. `Offense`, `Defense`, `OnDeath` and `NearDeath` are supported here as tags which represent _casting sets_. Note that our assail rotation (defined by `Assail`) is, simultaneously, the same type (`CastingSet`).

`Offense` controls a monster's spell rotation. `Defense` are spells that can be cast under certain conditions (**Not currently implemented - intent being defense spells can have triggers, or be a selection of spells the monster "keeps up"**). `OnDeath` represents castables that will be used when a creature dies (think of our poor, much beleaguered Goblin Butler casting a curse the moment it dies); `NearDeath` represents castables that can be used when a creature is approaching death (such as a really powerful counterattack - our Butler might yet have some `ard biscuits` up his sleeve).
  
```xml
<xs:complexType name="CreatureCastingBehavior">
  <xs:sequence>
    <xs:element name="Offense" type="CreatureCastingSet" minOccurs="0" maxOccurs="1"/>
    <xs:element name="Defense" type="CreatureCastingSet" minOccurs="0" maxOccurs="1"/>
    <xs:element name="OnDeath" type="CreatureCastingSet" minOccurs="0" maxOccurs="1"/>
    <xs:element name="NearDeath" type="CreatureCastingSet" minOccurs="0" maxOccurs="1"/>
  </xs:sequence>
</xs:complexType>
```

`CastingSets`, meanwhile, define the actual casting behavior:

```xml  
<xs:complexType name="CreatureCastingSet">
  <xs:sequence>
    <xs:element name="Castable" minOccurs="0" maxOccurs="unbounded" type="CreatureCastable"/>
  </xs:sequence>
  <xs:attribute name="Categories" type="xs:token" use="optional"/>
  <!-- KTN: interval should be integer -->
  <xs:attribute name="Interval" type="xs:token" use="optional" default="15"/>
  <xs:attribute name="Priority" type="hyb:CreatureAttackPriority" use="optional" default="HighThreat"/>
  <xs:attribute name="HealthPercentage" type="xs:int" use="optional" default="0"/>
  <xs:attribute name="Random" type="xs:boolean" use="optional" default="true"/>
</xs:complexType>
```

A casting set can contain `Castable` tags, indicating which spells are in the rotation. `Categories` can also be used for less specific behavior (e.g. our Butler friend should cast `Ham & Ham Related` spells).

`Interval` allows an interval to be set between spell casts. You can make something trigger happy, or have it allow oneoffs; default is 15 seconds which is probably, if I am being honest, _way_ too aggro. We want to give our butler boi some heft but the fucker shouldn't be able to burn down Piet or bury us in three feet of biscuits.

`Priority` allows an _attack priority_ to be set. This allows some interesting behaviors to be built up: for instance, a monster may assail / use attack skills on highest threat, but can, say, use `ard biscuit` to shower casters or healers in biscuits. The following priority types are supported:

```xml
<!-- Attack priority types for behaviorsets -->
<xs:simpleType name="CreatureAttackPriority">
  <xs:restriction base="xs:token">
    <xs:enumeration value="Attacker" />
    <xs:enumeration value="HighThreat" />
    <xs:enumeration value="LowThreat" />
    <xs:enumeration value="AttackingCaster" />
    <xs:enumeration value="AttackingHealer" />
    <xs:enumeration value="SimilarNearby" />
    <xs:enumeration value="Nearby" />
    <xs:enumeration value="Random" />
    <xs:enumeration value="Group"/>
  </xs:restriction>
</xs:simpleType>
```

`Attacker` is pretty obvious, it will always default to the players actively attacking (assailing) the monsters. In the presence of more than one, the target is selected randomly. `HighThreat` and `LowThreat` allow attacks to be targeted to the player with the highest or lowest threat. `AttackingCaster` allows attacks to target only players using magic; `AttackingHealer` allows healers to be targeted; `SimilarNearby` and `Nearby` are (I think) for MOB ON MOB COMBAT, `Random` well, is random, and `Group`....man idek.

Casting sets also allow a `HealthPercentage` to be defined as a threshold for casting. The set won't fire without the health percentage being reached, regardless of any other directives. This is _most_ useful in `NearDeath` but can be retained elsewhere for interesting behaviors (such as only cursing under 20% health, etc).

Lastly, `Random`, I think, is intended to just randomize the shit out of everything. If the set hits (`Interval`), it'l just use whatever and target whatever. Maybe? I dunno. We should probably discuss it.

Example:

```xml
<Casting>
  <Offense Interval="90" Priority="LowThreat"> 
    <Castable>ard biscuits</Castable>
  </Offense>
  <Defense Interval="60" Categories="Heal" HealthPercentage="10"/>
  <OnDeath Priority="HighThreat">
    <Castable>mor strioch pian tea cozy</Castable>
  </OnDeath>
</Casting>
<Assail>
  <Castable>Biscuit Hurl</Castable>
  <Castable>Ham Slap</Castable>
</Assail>
```

Our butler would cast `ard biscuits` every 90 seconds on the lowest threat player. It would start to use any of its heal spells every 60 seconds once its health was below 10%. It would cast the fearsome `mor strioch pian tea cozy` on death, and lastly its assail rotation would include `Biscuit Hurl` and `Ham Slap`. Truly a fierce combatant.

### Behavior: Hostility

See _Spawns: Hostility_. BehaviorSet hostility functions as "default" settings which can be overriden inside `Spawn`.

### Behavior: SetCookies

See _Spawns: SetCookies_. Similar to `Hostility`, `SetCookies` functions as "default" settings which can be overriden inside `Spawn`.

## Creatures

Creatures have been expanded significantly to allow them to define more base structures (which significantly reduces repetitiveness) and more complex settings.

Creatures can have _subtypes_ that inherit characteristics from their base. For instance, a `Goblin Butler` might be a subtype of `Goblin`.


```xml
<xs:complexType name="Creature">
  <xs:sequence>
    <xs:element name="Description" type="hyb:String8" minOccurs="0" maxOccurs="1" />
    <xs:element name="Loot" type="LootList" minOccurs="0" maxOccurs="1"/>
    <xs:element name="Hostility" minOccurs="0" maxOccurs="1" type="CreatureHostilitySettings"/>
    <xs:element name="SetCookies" minOccurs="0" maxOccurs="1" type="CreatureCookies"/>
    <xs:element name="Types" minOccurs="0" maxOccurs="1" type="CreatureTypes"/>
  </xs:sequence>
  <xs:attribute name="Name" use="required" type="xs:string"/>
  <xs:attribute name="Sprite" use="required" type="xs:unsignedShort" />
  <xs:attribute name="BehaviorSet" use="required" type="xs:string"/>
  <xs:attribute name="MinDmg" type="xs:int" use="optional" default="0"/>
  <xs:attribute name="MaxDmg" type="xs:int" use="optional" default="0"/>
</xs:complexType>
```

`Name` is pretty straightforward - it's the name of the monster. This will (generally) be the name displayed when a user clicks on a monster, and is the name referenced by spawns. `Sprite` is the sprite of the monster (For Hyb1, this is a reference to datfiles). `BehaviorSet` specifies a defined behavior default for the monster. `MinDmg` / `MaxDmg` is similarly a base setting for damage.

### Creature: Description

Description is just text, that can be used by varying types of lore. Existing descriptions for most creatures are Heiler-created trash.

### Creature: Loot

Same as existing Loot structure.

### Creature: Hostility

See _Hostility_ in spawns / behavior sets.

### Creature: SetCookies

See _SetCookies_ in spawns / behavior sets.

### Creature: Types

Subtypes of creatures can be defined here, and they are simply identical to `Creature`. All the normal attributes and elements of a `Creature` are supported. Astute observers will note that because `Creature` subtypes are in fact, still `Creature` type, infinite recursion (subtype of subtypes) is possible. This isn't supported. Just because something is permissible does not mean it should be done, you degenerate.

```xml
<xs:complexType name="CreatureTypes">
  <xs:sequence>
    <xs:element name="Type" type="Creature" minOccurs="0" maxOccurs="unbounded"/>
  </xs:sequence>
</xs:complexType>
```

For example, we could define a `Goblin Butler` subtype:

```
<Type Name="Goblin Butler" Sprite="66" BehaviorSet="GButler">
...
</Type>
```

## A Note on Inheritance / Order of Operations

* Loot is always _additive_. That is to say: if a creature type, subtype, and spawn all define loot, the `LootList` of the resulting spawn will include all three tables.

* SetCookies is similarly _additive_. In the case of a cookie being defined more than once, the most specific setting wins (for instance, if `HateGliocaWorshippers` was set in a creature type to a specific value, but set by a spawn to a different value - the spawn wins. If, however, the creature spec set one cookie (`HateGliocaWorshippers`) and the subtype or spawn set another (`HateLuathasWorshippers`) the resulting spawn would have _both_ cookies set.

* Hostility settings are _most specific wins_. If a spawn's hostility settings are set, it will **override** all other settings, including anything set by the behavior set.

* The _most specific_ behavior set specified for a spawn wins (creature -> creature subtype -> spawn). Behavior sets are **never additive**.

* Damage settings are also _most specific_. `MinDmg` / `MaxDmg` defined in a spawn will override any base settings.

## Formula Sets

*NOTE: THIS IS A WORK IN PROGRESS AND IS NOT YET IMPLEMENTED*

Formulas are expressible in XML, and are an attempt to remove most, if not all hardcoding in terms of damage, TNI calculations, etc. This allows these values to be changed and tuned easily without requiring code updates. They are defined in `formulas.xml` in the root of the world directory and follow a simple structure. The `Formulas` tag / `GameFormulas` type can define three types of formulas: `PlayerFormulas`, `MonsterFormulas` and `VendorFormulas`:

```xml
<xs:complexType name="GameFormulas">
  <xs:sequence>
    <xs:element name="Player" type="PlayerFormulas" minOccurs="0" maxOccurs="1"/>
    <xs:element name="Monster" type="MonsterFormulas" minOccurs="0" maxOccurs="1"/>
    <xs:element name="Vendor" type="VendorFormulas" minOccurs="0" maxOccurs="1"/>    
  </xs:sequence>
</xs:complexType>
```

These groupings basically boil down to a list of `Formula` types. `Formula` must have a `FormulaTarget` which tells Hybrasyl how to interpret and use the formula. This is represented by the following enum:

```xml
<xs:simpleType name="FormulaTarget">
  <xs:restriction base="xs:token">
    <xs:enumeration value="Damage" />
    <xs:enumeration value="BonusCritChance"/>
    <xs:enumeration value="BaseCritChance"/>
    <xs:enumeration value="Hit"/>
    <xs:enumeration value="MagicResistance"/>
    <xs:enumeration value="XpToNextLevel"/>
    <xs:enumeration value="HpGainPerLevel"/>
    <xs:enumeration value="MpGainPerLevel"/>
    <xs:enumeration value="ArmorClassEffect"/>
    <xs:enumeration value="Hp"/>
    <xs:enumeration value="Xp"/>
    <xs:enumeration value="Mp"/>
    <xs:enumeration value="Regen"/>                                                                                                     
    <xs:enumeration value="ItemSellDiscount"/>                                                                                          
    <xs:enumeration value="DepositCost"/>                                                                                               
    <xs:enumeration value="RepairCost"/>
  </xs:restriction>
</xs:simpleType>
```

We're still working on how formulas will work for these in practice but the intent is to allow most things to be affected / controlled by formulas. For monster variants in particular, damage, crit chance, hp/xp/mp and regen are intended to be supported.
Note that some of these combinations make sense and some don't - for instance, `ToNextLevel` is meaningless when applied to a vendor or a monster. 

Rather than contort the structure of XML and create multiple types with very small differences, we have (as a general philosophy) decided that _XML schema cannot and must not be the only gatekeeper_ for sane data. 
You can, of course, create combinations of formulas that are nonsensical; it's not up to the parser to stop you - but it also isn't up to the data structure to contort itself to prevent you from doing strange or frankly stupid things. GOT IT?

### Formula Sets: Monster Formulas

`MonsterFormulas` are currently used to define monster variance formula sets. _Formula sets_ can be used to apply a set of known changes to a spawn - for instance, a "weak" or "strong" Goblin butler:

```xml
<xs:complexType name="MonsterFormulas">
  <xs:sequence>
    <xs:element name="FormulaSet" type="MonsterFormulaSet" minOccurs="0" maxOccurs="unbounded"/>
  </xs:sequence>
</xs:complexType>

 <xs:complexType name="MonsterFormulaSet">
  <xs:sequence>
    <xs:element name="Formula" type="MonsterFormula" minOccurs="0" maxOccurs="unbounded"></xs:element>
  </xs:sequence>
  <xs:attribute name="Name" type="xs:token" use="required"/>
</xs:complexType>

<xs:complexType name="MonsterFormula">
  <xs:simpleContent>
    <xs:extension base="xs:string">
      <xs:attribute name="Target" type="FormulaTarget" use="required"/>
    </xs:extension>
  </xs:simpleContent>
</xs:complexType>
```

Here's an example:

```xml
<Monster>
  <FormulaSet Name="Weak">
    <Formula Target="Damage">$OUTGOINGDAMAGE * 0.8</Formula>
    <Formula Target="Hp">$BASEHP * 0.5</Formula>
  </FormulaSet>
</Monster>
```

When applied to a spawn, the `Weak` variance would reduce outgoing damage by 20% and HP by 50%.

### Formula Sets: Player Formulas

Player formulas define and modify players mostly used to calculate values related to players. The current intention here is to support, at a minimum, TNI (XP required to level), HP/MP gained per level, crit bonus, base crit chance, and the impact of 
hit / MR / AC. XML-wise, they look very similar to Monster, except they can also define an `Interval` for certain formula types (e.g. hp/mp regeneration), as well as a `Class` to support the ability to have different formulas for classes (e.g. a wizard might 
gain more MP per level)

```xml
<xs:complexType name="PlayerFormulas">
  <xs:sequence>
    <xs:element name="Formula" type="PlayerFormula" minOccurs="0" maxOccurs="unbounded"></xs:element>
  </xs:sequence>
</xs:complexType>

<xs:complexType name="PlayerFormula">
  <xs:simpleContent>
    <xs:extension base="xs:string">
      <xs:attribute name="Target" type="FormulaTarget" use="required"/>
      <xs:attribute name="Class" type="hyb:Class" use="optional" default="None"/>
      <xs:attribute name="Interval" type="xs:int" use="optional" default="0"/>
    </xs:extension>
  </xs:simpleContent>
</xs:complexType>
```

Example:

```xml
<Player>
  <Formula Target="ToNextLevel">$CURRLEVEL^3 * 250</Formula>
  <Formula Target="HpGainPerLevel" Class="Warrior">$CURRLEVEL * 5</Formula>
<Player>
```

Here, we set a default for `ToNextLevel` for all classes (most specific wins, but in the absence of a class-specific formula, we will default to classless). We also set the amount of HP gained for warriors per level: in this case, their current level times five.

### Formula Sets: Vendor Formulas

Lastly, we can define _vendor formulas_, which are mostly used to change vendor repair / sell / deposit costs. Same format as `MonsterFormula` at the time of this writing. This may change as we start to implement it.

Example:

```xml
<Vendor>
  <Formula Target="DepositCost">$ITEMVALUE * 0.10</Formula>
</Vendor>
```

This would set a fixed deposit cost of 10% of the item value.
