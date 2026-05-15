# Hybrasyl Server
[![Create a Game Account](https://img.shields.io/badge/-Create%20A%20Game%20Account-red?style=plastic)](http://accounts.hybrasyl.com/)   
[![Project website](https://img.shields.io/badge/-Project%20Website-blue?style=plastic)](http://hybrasyl.com/) 
[![Bug Tracker](https://img.shields.io/badge/-Bug%20Tracker-blue?style=plastic)](https://www.hybrasyl.com/bugs/) 
[![Server](https://img.shields.io/badge/-Server-blue?style=plastic)](https://github.com/hybrasyl/server)
[![Public World Data](https://img.shields.io/badge/-Public%20World%20Data-blue?style=plastic)](https://github.com/hybrasyl/ceridwen/)
[![Road Map](https://img.shields.io/badge/-Road%20Map-blue?style=plastic)](https://hybrasyl.github.io/cernunnos/)

**Welcome to Project Hybrasyl!**

Our aim is to create a well-documented and
exceptionally accurate DOOMVAS v1 emulator (example:
[Dark Ages](http://www.darkages.com)). 

### For Players
>**If you are looking to play Hybrasyl, you're in the wrong spot!**  

[Create an account](https://accounts.hybrasyl.com), and join us as we
implement Kennerly's vision with new technology and better gameplay.

### For Developers, Contributors, Server Hosts
This document is intended for developers; if you're interested in
using a released version of Hybrasyl server, please check our
[Github releases page](https://github.com/hybrasyl/server/releases).
Although we do not currently provide installer packages, we do provide
[Docker images](https://hub.docker.com/r/baughj/hybrasyl/tags) which
can be quickly used to get started. Generally, if you use the Hybrasyl
launcher, our staging server is almost always online - production will
be online once we implement more features!

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

To edit the data for your world, check out our XML editor [Creidhne](https://github.com/hybrasyl/creidhne)
and our world map / client asset editor [Taliesin](https://github.com/hybrasyl/taliesin). We also maintain
[Epona](https://github.com/eriscorp/epona), a client launcher and server orchestrator, which is guaranteed
to be an easy path to getting started.

## Requirements

You will need three things to use Hybrasyl, in addition to the server itself:

* A launcher - we recommend you use [Epona](https://github.com/eriscorp/epona) or 
  [Spark](https://www.hybrasyl.com/media/launcher/Spark.zip) 
* [Valkey](https://valkey.io)
* [Dark Ages Client](https://www.darkages.com)

## Terminology and key concepts

There are three processes that need to be properly configured in order to
connect a Dark Ages client to your own Hybrasyl instance:

* The launcher - Spark or Epona, which modifies the Dark Ages client to get it to connect to the server. 
* The [game server](https://github.com/hybrasyl/server) (what you're looking at
  now)
* A running instance of Valkey, which will be used for storing state data.

This tutorial assumes that the launcher (DA client) and Hybrasyl server will be
running on the same machine. Valkey/Redis can be located anywhere, as long as Hybrasyl
can reach it (it could even be an Amazon Elasticache instance).

## Game Server (Hybrasyl)

The **game server** maintains all game metadata and state, including registered
character names, mobs, towns, spawn points, items, and a bunch of other
game-related data. The Hybrasyl server retrieves state data such as players,
player inventory, messageboards and mailboxes from its companion Redis server
at runtime; XML is processed when the server starts up for actual world data
(items, maps, mobs, etc).

The server runs on three TCP ports (2610, 2611, and 2612 by default).

To get started with the server, either use the provided [Helm chart](./chart) if using 
Docker Desktop or Kubernetes, or use `docker-compose`.

1. Using [Docker Compose](https://docs.docker.com/compose/install/)

   If you have `docker-compose`, starting a working server involves the following steps:
   
   a. Clone the Hybrasyl server repository: `git clone https://github.com/hybrasyl/server.git`
   	     	   
   b. Edit the config.xml as needed (it's in `contrib/config.xml`)
   
   N.B. the protocol Darkages uses isn't NAT-aware (Hybrasyl provides `ExternalAddress` in order to 
   make this possible) so you need to provide it with the right address. For a local dev environment, it comes
   pre-set to localhost (127.0.0.1), which should Just Work.

   For a public-facing server, `ExternalAddress` will be the publicly routable IP address. 
   You may also need to edit your `config.xml` so that `DataStore` points to the right IP / hostname, if you
   don't want to use the built-in Valkey.

   c. Create the `hybrasyl` docker network

   `docker network create hybrasyl`

   d. Start the servers
   
   `docker-compose up`

   This will download Valkey, Ceridwen (our public getting-started XML
   repository) and Hybrasyl's GHCR image. You'll be able to login to it
   immediately using Spark or another launcher.

## Running Hybrasyl

Hybrasyl Server is a .NET 10 console application, which means it can
be run on a variety of platforms (Windows, Linux, OSX).

If you aren't using Docker, a `systemd` unit file [is
provided](./contrib/hybrasyl.unit) to start the server on modern
distributions. In any case, you can [download the latest
release](https://github.com/hybrasyl/server/releases) for your
platform. We also maintain a [release browser](https://releases.hybrasyl.com) that is 
automatically built from checkins to `develop` (the active development branch). 
Binaries are provided for MacOS, Linux and Windows.

Once downloaded, either run the server directly (`Hybrasyl.exe` or
`Hybrasyl` on Linux / MacOS) or, if running on Linux or a WSL
distribution that uses systemd, install the unit file in
`/etc/systemd/system/hybrasyl.service` and start Hybrasyl after reloading systemd units:

`service hybrasyl start`

To start the server on OSX or Linux for debugging and testing, you can run `dotnet run` from the `hybrasyl`
directory (e.g. where `Hybrasyl.csproj` lives.

## Compiling the Game Server

The process for compiling Hybrasyl is detailed below.

1. Install either [Microsoft Visual Studio](https://visualstudio.microsoft.com/vs) 
   or [Microsoft Visual Studio Code](https://code.visualstudio.com/).

   For Visual Studio, the Community Edition is free and capable of
   compiling all the needed projects. VS Code also works.

2. Download and install the [.NET 10 SDK](https://dotnet.microsoft.com/download).

   Currently, Hybrasyl uses .NET 10. In order to do development, you
   need both the SDK and runtime; to simply run Hybrasyl, you just need
   the runtime.

2. Download Spark (the launcher from above) and clone the
   [server](https://github.com/hybrasyl/server) repository to your local
   machine using a [git client](https://git-scm.com/downloads/guis), with
   Visual Studio or VS Code's built-in integration, or with [GitHub
   Desktop](https://desktop.github.com)
      
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
   win10-x64` or `dotnet publish -c Debug -r linux-x64`

Now that your setup is complete, you should be able to use Spark to
connect to it (after opening `Hy-brasyl Launcher.sln` and building the
project). Launch Spark and type in `localhost` in the "Server
Hostname" field.

You should now be able to connect to your Hybrasyl server, create a
new character, and log in! If not, well, take a look at the section on
[getting help](#help).

## Logging in

Log in to your new server by launching Spark. Point it to a local Dark
Ages client installation, enter `localhost` into the server hostname
field (if running locally), or the IP address of your Docker host, and
launch. Spark will ask you for a local Dark Ages client executable;
you must have the latest client installed in order to continue. Once
launched, you should see a Hybrasyl welcome screen in place of the
standard Dark Ages welcome screen. 

Congratulations -- you're connected!

Create a character and log in the same way you would on a production
server. You should find your Aisling in an inn and ready to explore
the world.

## Testing tips and other notable resources

* There are a number of admin flags that can be used for common tasks like
  creating items, teleporting, or changing level and class. Certain flags
  require that you be registered as a game master.
  
* Try typing `/item Stick` when you log in to add a stick to your inventory. 
  You can add any item that is a valid Item XML file in `xml/items` in your
  `world` directory (you did use [ceridwen](https://github.com/hybrasyl/ceridwen) to start, right?)
  
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

## A Note on Licensing

**Please note that Hybrasyl Server, along with most of its components, is
  licensed under the GNU Affero General Public License, version 3 (AGPLv3)**.
  This means that, *if you use this software to run a server that other users
  can connect to, you are required by the license to release the corresponding
  source code, which means that any and all modifications you make to the
  server software are also licensed under the AGPLv3*.
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

