# Hybrasyl Server

[Project website](http://hybrasyl.com/) -
[Bug tracker](https://github.com/hybrasyl/server/issues) -
[Punchlist](https://github.com/hybrasyl/server/wiki/Hybrasyl-Punchlist)

**Welcome to Project Hybrasyl!**

Our aim is to create a well-documented and
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
from a playable game is not yet implemented. You can see open issues in our GitHub 
[issue tracker](https://github.com/hybrasyl/server/issues), look at our current
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

You will need three things to use Hybrasyl:

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

To get started with the server:

1. **Install Redis**

   Hybrasyl uses Redis to store player state and mailboxes. If you are using Ubuntu/Debian,
   `apt install redis-server`. For Windows, you can either run Redis using [WSL](https://docs.microsoft.com/en-us/windows/wsl/install-win10)
   or you can downoad and install the Windows installer for Redis 2.8 from the
   [MSOpenTech releases page](https://github.com/MSOpenTech/redis/releases).
   There is no requirement for Redis to be local to the server; it can be
   hosted anywhere, though we recommend it is located on the same network
   segment for better security. Redis should work out of the box with Hybrasyl
   with its default settings. You will need to ensure that the Redis port,
   TCP/6379, can be accessed from the server running Hybrasyl; you may need to
   grant access or open ports.

2. **Create and populate your base directory**

   On Windows, this is `%userprofile%\documents\Hybrasyl`. On GNU/Linux
   or OSX this is `~/Hybrasyl` for whatever user is running the server.
   Take a look at the
   [community-maintained database](https://github.com/shadowoffice/HybraDB)
   for XML and scripting. This has more than enough XML and scripts to
   get you started. You can put the contents of that repository
   directly into your Hybrasyl data directory and start the server. You
   may wish to modify the default `config.xml`.

3. **Update your configuration**

   Examine the Hybrasyl configuration in the Hybrasyl data directory, `config.xml`. In particular,
   you will want to add the name of your character to `<Privileged>`, which will allow
   them to use any slash command.

4. **Install and run Hybrasyl** 

   (see _Running Hybrasyl_ below).

## Running Hybrasyl

Hybrasyl Server is .NET Core, which means it can be run on a variety of platforms (Windows, GNU/Linux, OSX).

A `systemd` unit file [is provided](./contrib/hybrasyl.unit) to start the server on Ubuntu 18.04+. In any case,
[download the latest release](https://github.com/hybrasyl/server/releases) for your platform. This can be unpacked
into `/srv/hybrasyl` on GNU/Linux or a directory of your choosing on Windows.

Once downloaded, either run the server directly (`Hybrasyl.exe` or
`Hybrasyl` on GNU/Linux) or, if you’re running on GNU/Linux or a WSL
distribution that uses systemd, install the unit file in
`/etc/systemd/system/hybrasyl.service` and start Hybrasyl:

`service hybrasyl start`

To start the server on OSX or GNU/Linux for debugging and testing, you can run `dotnet run` from the `hybrasyl`
directory (e.g. where `Hybrasyl.csproj` lives.

## Compiling the Game Server

The process for compiling Hybrasyl is detailed below.

1. Install either [Microsoft Visual Studio](https://www.visualstudio.com/en-us/downloads/visual-studio-2015-downloads-vs.aspx).
or [Microsoft Visual Studio Code](https://code.visualstudio.com/).

   For Visual Studio, the Community Edition is free and capable of compiling
   all the needed projects (server, launcher), but you don't strictly speaking need this any longer - you can also just edit C# code in VS Code.

2. Download and install a [.NET Core SDK and Runtime](https://dotnet.microsoft.com/download).

   Currently, Hybrasyl uses .NET Core 3.1. In order to do development, you need both the SDK and runtime; to simply run Hybrasyl, you just need the runtime.

2. Clone the [launcher](https://github.com/hybrasyl/launcher) and
   [server](https://github.com/hybrasyl/server) repositories to your
   local machine using a
   [git client](https://git-scm.com/downloads/guis), or with Visual
   Studio or VS Code's built-in integration. **Make sure you clone them into
   separate directories**.
   
3. Update and rebuild packages.

   Open the Hybrasyl Server solution (`Hybrasyl.sln`) in Visual Studio
   and update all NuGet packages (just building it will do this). If
   using VS Code / via command line, run `dotnet restore`
   and `dotnet build` in the same directory as the `Hybrasyl.csproj`
   file. This step will also build the XML/XSD data library.

4. Build Hybrasyl.

   The default settings should be adequate for most system setups.
   Should you wish to compile an distributable / standalone executable, run the
   following from the command line: `dotnet publish -c Debug -r
   win10-x64` or `dotnet publish -c Debug -r ubuntu.18.04-x64`

Now that your setup is complete, you should be able to use the launcher 
to connect to it (after opening `Hy-brasyl Launcher.sln` and building the project). Launch the
executable and select `localhost` from the server selection dropdown. You can also use one of the other community launchers to connect.

You should now be able to connect to your Hybrasyl server, create a
new character, and log in! If not, well, take a look at the section on [getting help](#help).

## Logging in

Log in to your new server by launching the Hy-brasyl Launcher
application, either compiled as described above or downloaded from
[hybrasyl.com](https://www.hybrasyl.com/files/Hybrasyl_Launcher_Installer.msi).
Point it to a local Dark Ages client installation, select `localhost`
from the server configuration dropdown, and launch. The launcher will
ask you for a local Dark Ages client executable; you must have the
latest client installed in order to continue. Once launched, you
should see a Hybrasyl welcome screen in place of the standard Dark
Ages welcome screen. Congratulations -- you're connected!

Create a character and log in the same way you would on a production
server. You should find your Aisling in an inn and ready to explore
the world.

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

*Please note: these restrictions do not apply to in-game Lua scripts
 and/or XML world data you may create for your server*. Whether or not
 you distribute that content is up to you.

## Contributing

We welcome contributions to the project! We encourage new developers to visit
us on Discord or send some emails to the developer list and get to know us
first, especially if you plan on tackling a substantial feature or change.
Hybrasyl follows the standard Github fork model, so
[fork us today](https://github.com/hybrasyl/) and submit a PR!

Please note that in order to contribute to the project, you must agree to the
terms of
[our contributor agreement](https://github.com/hybrasyl/server/blob/master/CONTRIBUTING.md).
