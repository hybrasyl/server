# Hybrasyl Server

[Project website](http://hybrasyl.com/) - [Bug tracker](https://hybrasyl.atlassian.net/secure/Dashboard.jspa) - [Punchlist](https://github.com/hybrasyl/server/wiki/Hybrasyl-Punchlist)

Welcome to Project Hybrasyl! Our aim is to create a well-documented and
exceptionally accurate DOOMVAS v1 emulator (example:
[Dark Ages](http://www.darkages.com)). Look around,
[make an account](https://www.hybrasyl.com/accounts/sign_up), and join us
sometime.

This document is intended for developers; if you're interested in using a
released version of Hybrasyl server, please check our 
[Github releases page](https://github.com/hybrasyl/server/releases). We do 
not currently provide installer packages, although that is in
the works. Generally, if you use the Hybrasyl launcher, our staging server is
almost always online - production will be online once we implement more
features!

Hybrasyl is a work in progress. A lot of the functionality you would expect
from a playable game is not yet implemented. You can see what's currently in
the works on the [bug tracker](https://hybrasyl.atlassian.net/), look at our current
[TODO/punchlist](https://github.com/hybrasyl/server/wiki/Hybrasyl-Punchlist) or check out
recent [project news](https://www.hybrasyl.com/).

The instructions in this document will guide you in setting up and running the
latest version of Hybrasyl and connecting a client for testing purposes. Note
that this project does not provide game content (properly configured items,
maps, warps between maps, etc); it comes with a set of examples that will allow
you to log in but not enough to come close to playing a real game.

It is our hope to release a content editor at some point in the future to make
the process of adding content to a server much easier.

## Requirements

You will need three things to compile and use Hybrasyl:

* [Hybrasyl Launcher](https://github.com/hybrasyl/launcher)
* [Redis](https://github.com/MSOpenTech/redis/releases)
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

2. Do _one_ of the following. Either:
   * Run the included powershell script (`Prep.ps1`) included in the `examples` directory 
   to create the Hybrasyl data directories. Hybrasyl's data is currently located at ```%MYDOCUMENTS\Hybrasyl\world```, normally found at `C:\Users\<yourusername>\Documents\world`.

	* Copy the [example XML and scripting data](https://github.com/hybrasyl/server/tree/master/examples/XML)
   (including subdirectories) from the examples directory into the `world\xml` directory. This 
   will populate the world with enough to login as a user,wander around, and test functionality.
   
   **_or_**
   
   * Unzip the included `examples.zip` into your Hybrasyl folder.
 
3. Examine the Hybrasyl configuration in the Hybrasyl data directory, `config.xml`. In particular, 
   you will want to add the name of your character to `<Privileged>`, which will allow 
   them to use any slash command.  

## Compiling the Game Server

The process for compiling Hybrasyl is detailed below.

1. Install
   [Microsoft Visual Studio](https://www.visualstudio.com/en-us/downloads/visual-studio-2015-downloads-vs.aspx).
   The Community Edition is free and capable of compiling all the needed projects (server, launcher).
2. Clone the [launcher](https://github.com/hybrasyl/launcher) and 
   [server](https://github.com/hybrasyl/server) repositories to your local machine
   using a [git client](https://git-scm.com/downloads/guis), or with Visual Studio's built-in integration. 
   Make sure you clone them into separate directories. 
3. Open the Hybrasyl Server solution (`Hybrasyl.sln`) in Visual Studio and
   update all NuGet packages (just building it will do this). The SDK for
   XML is now included in server to make this process (as well as making changes) 
   easier.
4. Build Hybrasyl. The default settings should be adequate for most system
   setups, assuming you've updated and installed all NuGet packages (which
   should occur automatically).
5. Copy `lod136.map`, `lod500.map`, and `lod300.map` from your Dark Ages directory (or an
   online archive) into `My Documents\Hybrasyl\world\mapfiles` (which should exist, if you followed the 
   directions above).  
6. Give the control service permission to bind to port 4949 (the
   default, this can be changed in config.xml): `netsh http add urlacl
   url=http://+:4949/ user=YOURMACHINENAME\YOURUSERNAME`. Substitute
   your Windows machine name and your username in the command above;
   e.g. `user=LOURES\baughj`.
7. Run `Hybrasyl.exe` either from within Visual Studio or as a standalone
   executable in the `hybrasyl\bin\Debug` folder of your git checkout. This
   should launch the server. You can change any of Hybrasyl’s settings by 
   editing `config.xml` in `My Documents\Hybrasyl`.

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
  require that you be registered as a game master.
  
* Try typing `/item Stick` when you log in to add a stick to your inventory. 
  You can add any item that is a valid Item XML file in `xml/items` in your
  `world` directory.
  
* You can learn skills and spells by using `/spell`, for instance, `/spell Assail`.

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

## Contributing

We welcome contributions to the project! We encourage new developers to visit
us on Discord or send some emails to the developer list and get to know us
first, especially if you plan on tackling a substantial feature or change.
Hybrasyl follows the standard Github fork model, so
[fork us today](https://github.com/hybrasyl/) and submit a PR!


Please note that in order to contribute to the project, you must agree to the
terms of
[our contributor agreement](https://github.com/hybrasyl/server/blob/master/CONTRIBUTING.md).


