=======
# Hybrasyl Server

[![Build status](https://ci.appveyor.com/api/projects/status/qx1g0etqkhlt1qw3/branch/master?svg=true)](https://ci.appveyor.com/project/Hybrasyl/server/branch/master)

[Project website](http://hybrasyl.com/) - [Bug tracker](https://hybrasyl.atlassian.net/secure/Dashboard.jspa)

Welcome to Project Hybrasyl! Our aim is to create a well-documented and
exceptionally accurate DOOMVAS v1 emulator (example: [Dark Ages](http://www.darkages.com)).
Look around, [make an account](https://www.hybrasyl.com/accounts/sign_up), and
join us sometime.

This document is intended for developers; if you're interested in using a
released version of Hybrasyl server, please check the
[releases page](https://www.hybrasyl.com/releases) of the project website.
Hybrasyl is a work in progress and many features are not yet implemented. You
can see what's currently in the works on the
[bug tracker](https://hybrasyl.atlassian.net/), or check out recent
[project news](https://www.hybrasyl.com/).

The instructions in this document will guide you in setting up and running the
latest version of Hybrasyl and connecting a client for testing purposes. Note
that this project does not provide a full copy of game content (properly
configured items, maps, warps between maps, etc); it comes with a set of
examples that will allow you to log in but not enough to come close to playing
the full game. It is our hope to release a content editor at some point in the
future.

## Requirements

You will need three things to compile and use Hybrasyl:

* [Hybrasyl Launcher](https://github.com/hybrasyl/launcher)
* [Redis](https://github.com/MSOpenTech/redis/releases)
* [Hybrasyl SDK](https://github.com/hybrasyl/sdk)
* [Dark Ages Client](https://www.darkages.com)

## Terminology and key concepts

There are three processes that need to be properly configured in order to
connect a Dark Ages client to your own Hybrasyl instance: the
*[Hybrasyl launcher](https://github.com/hybrasyl/launcher)* (which modifies the
Dark Ages client), the *[Hybrasyl server](https://github.com/hybrasyl/server)*,
and a *Redis cache*. This tutorial assumes that the launcher (DA client) and
Hybrasyl server will be running on the same machine. Redis can be located anywhere,
as long as Hybrasyl can reach it (it could even be an Amazon Elasticache instance).

## Game Server (Hybrasyl)

The **game server** maintains all game metadata and state, including registered
character names, mobs, towns, spawn points, items, and a bunch of other
game-related data. The Hybrasyl server retrieves state data such as players, player
inventory, messageboards and mailboxes from its companion Redis server at runtime;
XML is processed when the server starts up for actual world data (items, maps,
mobs, etc).

1. Download and install the Windows installer for Redis 2.8 from the
   [MSOpenTech releases page](https://github.com/MSOpenTech/redis/releases).
   There is no requirement for Redis to be local to the server; it can be
   hosted anywhere, though we recommend on the same network segment for better
   security. Redis should work out of the box with Hybrasyl with its default
   settings. You will need to ensure that the Redis port, 6379, can be accessed
   from the server running Hybrasyl; you may need to grant access or open
   ports.
2. Create some directories manually (we swear this will be automatically
   handled soon). You’ll need the Hybrasyl data directory, which, for
   convenience, is currently located at ```%MYDOCUMENTS%\Hybrasyl```. You’ll need
   the following directories:
   | Location                   | Use |
   |----------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------|
   | mapfiles                   | Used to store *.map files. Map files  have a unique ID, which will be referred to in map XML.                                                        |
   | scripts/castable           | Scripts (*.py) that can be called by castables (skills and spells) to trigger actions. Not fully implemented.                                        |
   | scripts/item               | Scripts that can be called by items (e.g. on item use). Not yet implemented.                                                                         |
   | scripts/npc                | Scripts that will be used by NPCs to interact with players (dialogs, etc.)                                                                           |
   | scripts/reactor            | Scripts that will be used by reactors (map tiles that respond to events). Not fully implemented.                                                     |
   | scripts/startup            | Scripts that will be run by Hybrasyl at startup.                                                                                                     |
   | xml/items                  | Items in the game. Fully implemented.                                                                                                                |
   | xml/castables              | Castables (actions). Support is ongoing.                                                                                                             |
   | xml/itemvariants           | Item variants (create modifiers on items, such as Really Cool Stick. Fully implemented for known variants; custom variants will require coding work. |
   | xml/maps                   | Maps, including signpost / messageboard locations, NPCs, etc. Fully implemented.                                                                     |
   | xml/nations                | Nations (citizenship), including spawnpoints. Fully implemented.                                                                                     |
   | xml/worldmaps              | World maps (travelling between areas). Fully implemented.                                                                                            |

3. Put the [example XML data](https://github.com/hybrasyl/server/examples) from the examples directory into each corresponding XML directory.
   This will populate the world with enough to login as a user, wander around, and test functionality.

## Compiling the Game Server
The process for compiling the **game server** is detailed below.

1. Install
   [Microsoft Visual Studio](https://www.visualstudio.com/en-us/downloads/visual-studio-2015-downloads-vs.aspx).
   The Community Edition is free and capable of compiling both projects.
2. Install [IronPython](http://ironpython.codeplex.com/downloads/get/970325).
   Hybrasyl currently uses Python for all of its scripting.
3. Clone the [launcher](https://github.com/hybrasyl/launcher),
   [server](https://github.com/hybrasyl/server), and
   [sdk](https://github.com/hybrasyl/sdk) repositories to your local machine
   using a [git client](https://git-scm.com/downloads/guis). Make sure you
   clone them into separate directories.
4. Open `HybrasylIntegration.sln` from the SDK repository, and build the integration libraries (just click build).
5. Open `Hybrasyl.sln` in Visual Studio and update all NuGet packages.
6. Build Hybrasyl. The default settings should be adequate for most system
   setups, assuming you've updated and installed all NuGet packages (which should occur automatically).
7. Copy `lod136.map` and `lod500.map` from your Dark Ages directory (or an
   online archive) into `My Documents\Hybrasyl\mapfiles` (the directory you
   created above).
8. Run `Hybrasyl.exe` either from within Visual Studio or as a standalone
   executable in the `hybrasyl\bin\Debug` folder of your git repository. This
   should launch the server and run you through a first-launch configuration
   wizard. Here's you'll need to point the **Hybrasyl server** at Redis;
   make sure you provide the right hostname and that the ports are open.
   You can change any of Hybrasyl’s settings later by editing `config.xml`
   in `My Documents\Hybrasyl`.

That completes the Hybrasyl server setup. You should be able to use the
[released version of the launcher](https://www.hybrasyl.com/launcher/Hybrasyl_Launcher_Installer.msi) to
connect to it. In case you have trouble with the latest launcher, open
`Hy-brasyl Launcher.sln` and build the project. Launch the executable and
select `localhost` from the server selection dropdown. You should now be
able to connect to your Hybrasyl server, create a new character, and log in!

## Logging in

Log in to your new server by launching the Hy-brasyl Launcher application,
either compiled as described above or downloaded from
[hybrasyl.com](https://www.hybrasyl.com/). Point it to a local Dark Ages client
installation, select `localhost` from the server configuration dropdown, and
launch. The launcher will ask you for a local Dark Ages client executable; you
must have the latest client installed in order to continue. Once launched, you
should see a Hybrasyl welcome screen in place of the standard Dark Ages welcome
screen. Congratulations -- you're connected!

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

* Some flags require admin-level access to the *Hybrasyl server*. Try typing
  `/item Stick` when you log in to add a stick to your inventory. You can add
  any item that is a valid Item XML file in `xml/items` in your `world` directory.

* Warps are links between locations on the map. You can add or remove warps in
  a map file (e.g. `xml/maps/ExampleVillage.xml`).

* You can add new items by creating new XML files. The example items should
  provide good models to follow; there are also XSD files for the XML structure
  in the SDK repository.

