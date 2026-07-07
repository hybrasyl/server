# Future work

## Board post responses disable the Prev button (confirmed vs retail client)
`MessagingResponse.cs` (`GetBoardMessage` branch, ~line 116) hardcodes the post response's
leading flag byte to `0x00`, which clients (retail confirmed, Brigid) read as "disable the
Prev button" — so Prev renders in its disabled/dithered state and post paging backward is
impossible on public boards. The mail branch (`GetMailMessage`) correctly sends `0x03` and
Prev works there. The 0x3B offset navigation (`MessagingController.GetMessage`, offset 1 =
prev/newer) already supports backward paging for any store, so the flag is disabling a
feature the server implements. Fix: send `0x03` in the board branch too (or compute a real
"has newer" value).

## Mail/board index is oldest-first and truncates away the newest messages
`MessageStore.GetEnumerator` yields messages in insertion order (oldest first) and both it
and `GetIndex` apply `Take(BoardMessageResponseSize)` *before* any ordering — so the index
shows oldest-first (clients display it as-received; retail expects newest-first), and once
a store exceeds the response size the *newest* messages are silently excluded from the
index entirely. Fix: enumerate newest-first and take the newest N.

## 0x34 profile: legend count desyncs from emitted marks
`User.OnClick` (Objects/User.cs ~986) writes `(byte)Legend.Count` as the legend count but
then emits only the marks where `m.Public`. Any non-public legend mark makes the count
disagree with the emitted rows, desynchronizing the client's parse of everything after the
legend block (portrait/profile-text tail). Fix: count the same filtered set that gets
emitted.

## 0x33 morph (display-as-monster) never renders — sentinel not sent
The 0x33 aisling-display packet writes the player's real helmet sprite where a morphed
player should get the `0xFFFF` creature-form sentinel followed by `MonsterSprite`. Clients
(retail wire model, DALib `CreatureSpriteAppearance`) only enter creature-form rendering on
the sentinel, so display-as-monster is dead on the wire. Fix: emit `0xFFFF` + the monster
sprite when a morph is active.

## VERIFY: GameMasterA|B flags reportedly set in every Full stat update
A client-side review claimed `StatUpdateFlags.Full` unconditionally includes
`GameMasterA|GameMasterB`, which would make every client treat every player as a GM
(wall-clip etc.) since clients derive GM state from those movement-mode bits (0x40/0x80 in
the 0x08 packet). This contradicts observed behavior, so it likely conflates the update
mask with the emitted values — needs a source read of the 0x08 writer before it's
actionable. If real, gate the bits on actual privileges.

## NaturalDocs / mono-runtime dependency
NaturalDocs is the only reason `mono-runtime` is in the CI build. It adds significant time to the install step. Worth investigating:
- Does NaturalDocs have a .NET Core / modern .NET version yet?
- Is there a containerized NaturalDocs that could run as a separate step?
- Could docs generation be a separate workflow that doesn't block the main build?
- Is NaturalDocs still the right tool, or has something better emerged?
