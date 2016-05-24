# Hybrasyl Server 0.5.0 ("Riona")

* Redis is now a requirement, and is used for storing state data such as users, board posts, and mailboxes.
* All world data is now read in from XML files. A new integration library (called, appropriately, SDK) has been created to manage the parsing, validation and writing of XML files.
* As a result of the above, the server no longer uses MySQL, and all references / dependencies for it have been removed.
* Thanks to Michael Norris (@norrismiv), we now have a new launcher, which displays news and should support a lot of new features in the future.
* Several item, item display and exchange bugs fixed.
* Experience implemented in a real way (with leveling support).
* Nation support (citizenship) implemented, along with logging in to expected spawn locations (national spawnpoints).
* Character info panel now functions as expected.
* Mailbox / forums fully implemented.
* Grouping support implemented.
* Unique / unique equip item flags, negative weight, master restrictions implemented.
* Networking stack completely reimplemented using async sockets with separate send/receive threads.
* Beginning of skill/spell implementation - /skill <skillName> will give your character a skill. Still a lot to be done here.
* Assail / spacebar support for assail implemented.
* Poor, long-suffering Riona in Mileth, critically wounded in a prior release, will now respond to Aislings again.
* The beginning of spawning support has been implemented. Unfortunately, at the moment, this simply means that wolves and bees have infested Mileth.