# Species System Design

Species is a first-class property on User (like Class, Gender). Encoded as an enum in Hybrasyl.Xml, not a cookie.

**Why:** Species drives available body styles (currently called SkinColor). The client needs it for rendering, server needs it for validation. Also usable as a [FormulaVariable] for species-based scaling.

**Where:**

- Enum definition: Hybrasyl.Xml (new `Species` enum, XSD + generated C#)
- User property: `[JsonProperty] public Species Species { get; set; }` on User.cs, near Class/Gender
- Body style mapping: ServerConfig XML — maps each species to its allowed SkinColor/body style values
- Client: needs species sent in a packet for display/body selection

**Species list (50 total, Human = default / ordinal 0):**
Human, Sylarim, Nitharim, Chaya, Vanik, Brukha, Varkha, Ordrak, Orcen, Salaman, Valenni, Rusken, Vosken, Morsken, Bralan, Argan, Aliarim, Dwarf, Triton, Skara, Tharn, Shavren, Morlok, Bealok, Eirald, Lyrien, Veshim, Goblin, Mioren, Bjorin, Drindle, Twickle, Carran, Molgru, Syrrin, Tzurak, Dunrath, Ixon, Aldwen, Irunan, Shirok, Rhavan, Farrani, Semari, Ayllun, Lutan, Kwall, Ogre, Hutan, Skraven

Source: {path}/Ideas - Species.md

**Subspecies are separate species:** Orc splits into Ordrak + Orcen as their own enum values. No nesting. Flat list.

**Sprite mapping:** Lookup table in ServerConfig XML, variable variants per species:

- DisplayUser packet changes from WriteByte to WriteUInt16 (ushort) for body sprite index — could exceed 256 total sprites
- Current SkinColor enum (Basic=0 through Purple=9) is replaced by this scheme
- Server stores Species (enum) + variant index separately on User; looks up sprite index from ServerConfig at packet time
- Client loads mm{spriteIndex:0000}.epf (or .png) based on the ushort value
- Example ServerConfig structure:
  
  ```xml
  <Species Name="Human">
    <Variant Name="Light" SpriteIndex="0" />
    <Variant Name="Medium" SpriteIndex="1" />
    <Variant Name="Dark" SpriteIndex="2" />
    <Variant Name="Asian" SpriteIndex="3" />
    <Variant Name="Hispanic" SpriteIndex="4" />
  </Species>
  <Species Name="Ordrak">
    <Variant Name="Green" SpriteIndex="30" />
    <Variant Name="Brown" SpriteIndex="31" />
  </Species>
  ```

**Admin commands:** Two separate commands, two separate concerns:

- `/species human` — sets species, resets variant to 0. `Enum.TryParse`, case-insensitive. File: `ChatCommands/SpeciesCommand.cs`
- `/skincolor 2` — sets variant index within current species' allowed set. Validates against ServerConfig. File: `ChatCommands/SkinColorCommand.cs`
- Variant count is variable per species (defined in ServerConfig)
- Both privileged = true (admin only)

**How to apply:** This is part of the Hybrasyl.Xml package update pipeline (same as element expansion). Enum goes in XSD, regenerate C#, publish NuGet, then server-side property + validation + packet + slash command.
