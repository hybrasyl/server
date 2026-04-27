# Chair Sitting System Design

Players can sit in chairs by walking into wall tiles that are flagged as chairs. Sprite is folded client-side post-composite (no new art needed).

**Why:** Existing RestPosition poses are cross-legged floor sits, not chair sits. Creating new sitting frames for every armor across 50 species is impractical. Chairs are currently impassable wall tiles.

**Interaction model:**

- Player walks toward a chair wall tile
- Server detects the tile sprite ID is in the "chairs" config list
- Instead of blocking movement, server sets `RestPosition.Seated` and faces player toward the chair
- Player stays on their current tile — no position change
- While seated: directional input **turns in place** (changes facing direction without moving or leaving the chair)
- **Assail key (spacebar)** exits the chair — resets RestPosition to Standing
- Any other movement key just turns

**Server changes:**

- `RestPosition` enum: add `Seated = 0x04`
- Walk handler (`User.cs:1916-2115`): when walk hits a wall tile in the chairs list, set Seated + direction instead of blocking
- Direction handler: if Seated, allow direction change (turn in place) without clearing seated state
- Assail handler: if Seated, clear seated state to Standing instead of performing assail. Broadcast DisplayUser update.
- Config: list of chair tile sprite IDs with optional seat height metadata (for client)

**Client changes (Chaos.Client):**

- Post-composite sprite transform when `RestPosition == Seated`:
  - Crop lower portion of assembled sprite (legs below waist line)
  - Shift upper body down by seat height pixels
  - Per-facing-direction crop/shift rules (4 directions)
- Crop line (Y pixel for waist) is roughly consistent across armors — tune once, accept minor variance
- Seat height configurable per chair type (bar stool vs throne vs bench)

**How to apply:** Server work is small (~20 lines in walk/direction/assail handlers + enum value + config). Client work is the post-composite transform in the rendering pipeline — needs investigation into where Chaos.Client assembles sprite layers to find the right intercept point.
