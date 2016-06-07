# Hybrasyl Server Changelog

*This is the changelog for the server project. Releases that have names
 generally add fairly significant features, whereas ones without are primarily
 for bugfixing and other updates.*

# Hybrasyl Server 0.5.2 ("Dar")

*Released: June 6, 2016* - [View this release on GitHub](https://github.com/hybrasyl/server/releases/tag/0.5.2)

### Features

* Implement usage support for items. Items now respect their XML definitions
  for usage. For instance:

 ```xml
 <item>
 <use consumed="true">
 <teleport x="3" y="3">Test Village</teleport>
 <playereffect mp="50" hp="50" xp="50" gold="50"/>
 <effect id="3" speed="1"/>
 <soundeffect id="6"/>
 <scriptname>mycoolitem.py</scriptname>
 ...
 </item>
 ```

 Any of these use cases can be combined with one another, opening up a lot of
 possibilities for item scripting.

* General concept of healing (increasing player/creature HP) implemented.
* Python API scripting now supports damage/healing/etc.
* Legend marks implemented, along with Python scripting API that can be used by
  in-game scripts to manage marks.
* Example NPC script added, to help users who wish to learn more about the
  Python scripting API. Documentation is coming - we promise.

### Bugfixes

* Item quantities are now appropriately displayed in the exchange window.
* Level and experience overflows corrected, maximum level is now set as a
  compile-time constant (`Constants.MAX_LEVEL`) and defaults to 99.
* Item bonuses (AC, magic resistance, etc) are now correctly recalculated at
  login (thanks to @woghks123 for reporting this issue).
* `README` updated.

# Hybrasyl Server 0.5.1

*Released: May 30, 2016* - [View this release on GitHub](https://github.com/hybrasyl/server/releases/tag/0.5.1)


### Features

* Sample XML data updated.
* Documentation updates.
* Added support for server (Aisling) time.

### Bugfixes

* Bug fixed where the server would write out incorrect XML, causing the
  login/world port to be the same.

# Hybrasyl Server 0.5.0 ("Riona")

*Released: May 23, 2016* - [View this release on GitHub](https://github.com/hybrasyl/server/releases/tag/0.5.0)


### Features

* Redis is now a requirement, and is used for storing state data such as users,
  board posts, and mailboxes.
* All world data is now read in from XML files. A new integration library
  (called, appropriately, SDK) has been created to manage the parsing,
  validation and writing of XML files.
* As a result of the above, the server no longer uses MySQL, and all references
  / dependencies for it have been removed.
* Thanks to Michael Norris (@norrismiv), we now have a new launcher, which
  displays news and should support a lot of new features in the future.
* Several item, item display and exchange bugs fixed.
* Experience implemented in a real way (with leveling support).
* Nation support (citizenship) implemented, along with logging in to expected
  spawn locations (national spawnpoints).
* Character info panel now functions as expected.
* Mailbox / forums fully implemented.
* Grouping support implemented.
* Unique / unique equip item flags, negative weight, master restrictions
  implemented.
* Networking stack completely reimplemented using async sockets with separate
  send/receive threads.
* Beginning of skill/spell implementation - /skill <skillName> will give your
  character a skill. Still a lot to be done here.
* Assail / spacebar support for assail implemented.
* The beginning of spawning support has been implemented. Unfortunately, at the
  moment, this simply means that wolves and bees have infested Mileth.

### Bugfixes

* Poor, long-suffering Riona in Mileth, critically wounded in a prior release,
  will now respond to Aislings again.
