=======
# Hybrasyl Server

[![Build status](https://ci.appveyor.com/api/projects/status/qx1g0etqkhlt1qw3/branch/master?svg=true)](https://ci.appveyor.com/project/Hybrasyl/server/branch/master) - *please ignore this for now*

[Project website](http://hybrasyl.com/) - [Bug tracker](https://hybrasyl.atlassian.net/secure/Dashboard.jspa)

Welcome to Project Hybrasyl! Our aim is to create a well-documented and
exceptionally accurate DOOMVAS v1 emulator (example:
[Dark Ages](http://www.darkages.com)). Look around,
[make an account](https://www.hybrasyl.com/accounts/sign_up), and join us
sometime.

This document is intended for developers; if you're interested in using a
released version of Hybrasyl server, please check the
[Github releases page](https://www.hybrasyl.com/releases) of the project
website. We do not currently provide installer packages, although that is in
the works. Generally, if you use the Hybrasyl launcher, our staging server is
almost always online - production will be online once we implement more
features!

Hybrasyl is a work in progress. A lot of the functionality you would expect
from a playable game is not yet implemented. You can see what's currently in
the works on the [bug tracker](https://hybrasyl.atlassian.net/), or check out
recent [project news](https://www.hybrasyl.com/).

The instructions in this document will guide you in setting up and running the
latest version of Hybrasyl and connecting a client for testing purposes. Note
that this project does not provide game content (properly configured items,
maps, warps between maps, etc); it comes with a set of examples that will allow
you to log in but not enough to come close to playing a real game.

It is our hope to release a content editor at some point in the future to make
the process of adding content to a server much easier.

## A Note on Licensing

**Please note that Hybrasyl Server, along with most of its components, is
  licensed under the GNU Affero General Public License, version 3 (AGPLv3)**.
  This means that, if you use this software to run a server that other users
  can connect to, you are required by the license to release the corresponding
  source code, which means that any and all modifications you make to the
  server software are also licensed under the AGPLv3.
  [Read more at gnu.org](http://www.gnu.org/licenses/why-affero-gpl.en.html).

By using this license for Hybrasyl, our intent is to foster a vibrant community
whose development and progress are open and available to all.

*Please note: these restrictions do not apply to in-game Python scripts and/or
 world data you may create for your server*. Whether or not you distribute that
 content is up to you.

## Requirements

You will need four things to compile and use Hybrasyl:

* [Hybrasyl Launcher](https://github.com/hybrasyl/launcher)
* [Redis](https://github.com/MSOpenTech/redis/releases)
* [Hybrasyl SDK](https://github.com/hybrasyl/sdk)
* [Dark Ages Client](https://www.darkages.com)

## Terminology and key concepts

There are three processes that need to be properly configured in order to
connect a Dark Ages client to your own Hybrasyl instance:

* The [launcher](https://github.com/hybrasyl/launcher) (which modifies the Dark
  Ages client to get it to connect to the server)
* The [game server](https://github.com/hybrasyl/server) (what you're looking at
  now)
* A running instance of Redis, which will be used for storing state data.

This tutorial assumes that the launcher (DA client) and Hybrasyl server will be
running on the same machine. Redis can be located anywhere, as long as Hybrasyl
can reach it (it could even be an Amazon Elasticache instance).

## Game Server (Hybrasyl)

The **game server** maintains all game metadata and state, including registered
character names, mobs, towns, spawn points, items, and a bunch of other
game-related data. The Hybrasyl server retrieves state data such as players,
player inventory, messageboards and mailboxes from its companion Redis server
at runtime; XML is processed when the server starts up for actual world data
(items, maps, mobs, etc).

1. Download and install the Windows installer for Redis 2.8 from the
   [MSOpenTech releases page](https://github.com/MSOpenTech/redis/releases).
   There is no requirement for Redis to be local to the server; it can be
   hosted anywhere, though we recommend it is located on the same network
   segment for better security. Redis should work out of the box with Hybrasyl
   with its default settings. You will need to ensure that the Redis port,
   TCP/6379, can be accessed from the server running Hybrasyl; you may need to
   grant access or open ports.

2. Create some directories manually (we swear this will be automatically
   handled soon). You’ll need the Hybrasyl data directory, which, for
   convenience, is currently located at ```%MYDOCUMENTS%\Hybrasyl\world```.
   You’ll need the following directories under `world`:

   | Location                   | Use | Status |
   | -------------------------- | --- | ------ |
   | `mapfiles`                   | Used to store *.map files. Map files  have a unique ID, which will be referred to in map XML. | Fully implemented |
   | `scripts/castable`           | Scripts (*.py) that can be called by castables (skills and spells) to trigger actions. | Work in progress |
   | `scripts/item`               | Scripts that can be called by items (e.g. on item use). |  Not implemented |
   | `scripts/npc`                | Scripts that will be used by NPCs to interact with players (dialogs, etc.)  | Fully implemented |
   | `scripts/reactor`            | Scripts that will be used by reactors (map tiles that respond to events). | Not implemented  |
   | `scripts/startup`            | Scripts that will be run by Hybrasyl at startup. | Fully implemented |
   | `xml/items`                  | Items in the game. | Fully implemented.  |
   | `xml/castables`              | Castables (actions). | Work in progress. Basic support |
   | `xml/itemvariants`           | Item variants (create modifiers on items, such as Really Cool Stick. | Fully implemented for known variants; custom variants will require coding work. |
   | `xml/maps`                   | Maps, including signpost / messageboard locations, NPCs, etc. | Fully implemented.   | 
   | `xml/nations`                | Nations (citizenship), including spawnpoints. | Fully implemented. |
   | `xml/worldmaps`              | World maps (travelling between areas). | Fully implemented. |

3. Copy the
   [example XML data](https://github.com/hybrasyl/server/tree/master/examples/XML)
   (including subdirectories) from the examples directory into the `world\xml`
   directory. This will populate the world with enough to login as a user,
   wander around, and test functionality.

## Compiling the Game Server

The process for compiling Hybrasyl is detailed below.

1. Install
   [Microsoft Visual Studio](https://www.visualstudio.com/en-us/downloads/visual-studio-2015-downloads-vs.aspx).
   The Community Edition is free and capable of compiling all the needed projects (server, launcher and sdk).
2. Install [IronPython](http://ironpython.codeplex.com/downloads/get/970325).
   Hybrasyl currently uses Python for all of its scripting.
3. Clone the [launcher](https://github.com/hybrasyl/launcher),
   [server](https://github.com/hybrasyl/server), and
   [sdk](https://github.com/hybrasyl/sdk) repositories to your local machine
   using a [git client](https://git-scm.com/downloads/guis). Make sure you
   clone them into separate directories.
4. Open the Hybrasyl SDK solution (`HybrasylIntegration.sln`) from the SDK
   repository, and build the integration libraries (just click build).
5. Open the Hybrasyl Server solution (`Hybrasyl.sln`) in Visual Studio and
   update all NuGet packages (just building it will do this).
6. Build Hybrasyl. The default settings should be adequate for most system
   setups, assuming you've updated and installed all NuGet packages (which
   should occur automatically).
7. Copy `lod136.map` and `lod500.map` from your Dark Ages directory (or an
   online archive) into `My Documents\Hybrasyl\mapfiles` (the directory you
   created above).
8. Run `Hybrasyl.exe` either from within Visual Studio or as a standalone
   executable in the `hybrasyl\bin\Debug` folder of your git checkout. This
   should launch the server and run you through a first-launch configuration
   wizard. Here's you'll need to point the game server at Redis;
   make sure you provide the right hostname and that the ports are open.
   You can change any of Hybrasyl’s settings later by editing `config.xml`
   in `My Documents\Hybrasyl`.

Now that your setup is complete, you should be able to use the
[released version of the launcher](https://www.hybrasyl.com/files/Hybrasyl_Launcher_Installer.msi)
to connect to it. In case you have trouble with the latest launcher, open
`Hy-brasyl Launcher.sln` and build the project. Launch the executable and
select `localhost` from the server selection dropdown. You should now be able
to connect to your Hybrasyl server, create a new character, and log in!

If not, well, take a look at the section on [getting help](#help).

## Logging in

Log in to your new server by launching the Hy-brasyl Launcher application,
either compiled as described above or downloaded from
[hybrasyl.com](https://www.hybrasyl.com/files/Hybrasyl_Launcher_Installer.msi).
Point it to a local Dark Ages client installation, select `localhost` from the
server configuration dropdown, and launch. The launcher will ask you for a
local Dark Ages client executable; you must have the latest client installed in
order to continue. Once launched, you should see a Hybrasyl welcome screen in
place of the standard Dark Ages welcome screen. Congratulations -- you're
connected!

Create a character and log in the same way you would on a production server.
You should find your Aisling in an inn and ready to explore the world.

## Testing tips and other notable resources

* There are a number of admin flags that can be used for common tasks like
  creating items, teleporting, or changing level and class. Certain flags
  require that you be registered as a game master. You can update `config.xml`
  to add your character as a privileged user (GM) by adding the following stanza:
  ```xml
  <access>
    <privileged>MyUser</privileged>
  </access>
  ```

* Try typing `/item Stick` when you log in to add a stick to your inventory. 
  You can add any item that is a valid Item XML file in `xml/items` in your
  `world` directory.

* Warps are links between locations on the map. You can add or remove warps
  in a map file (e.g. `xml/maps/ExampleVillage.xml`).

* You can add new items by creating new XML files. The example items should
  provide good models to follow; there are also XSD files for the XML
  structure in the SDK repository. We hope to make a world editor available
  eventually.

## <a name="help"></a>Help! Something isn't working!

We're here to help!

The project maintains a
[public Discord server](https://discord.gg/0ziUhzij2THMqU7B) and it should be
your first go-to for asking questions. **Remember that Hybrasyl is a volunteer
project, not a job; we'll try to get to your questions as soon as we see
them**.

We also maintain two Google Groups, one
[for developers](https://groups.google.com/forum/#!forum/hybrasyl-devel) and
one [for users](https://groups.google.com/forum/#!forum/hybrasyl-users).

## Contributing

We welcome contributions to the project! We encourage new developers to visit
us on Discord or send some emails to the developer list and get to know us
first, especially if you plan on tackling a substantial feature or change.
Hybrasyl follows the standard Github fork model, so
[fork us today](https://github.com/hybrasyl/) and submit a PR!

Please note that in order to contribute to the project, you must agree to the
terms of
[our contributor agreement](https://github.com/hybrasyl/server/blob/master/CONTRIBUTING.md).


