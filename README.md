# Hybrasyl server

[Project website](http://hybrasyl.com/) - [Bug tracker](https://hybrasyl.atlassian.net/secure/Dashboard.jspa)

Welcome to Project Hybrasyl! Our aim is to create a well-documented and exceptionally accurate private server for [Dark Ages](http://www.darkages.com). Look around, [make an account](https://www.hybrasyl.com/accounts/sign_up), and join us sometime.

This document is intended for developers; if you're interested in using a released version of Hybrasyl server, please check the [releases page](https://www.hybrasyl.com/releases) of the project website. Hybrasyl is a work in progress and many features are not yet implemented. You can see what's currently in the works on the [bug tracker](https://hybrasyl.atlassian.net/), or check out recent [project news](https://www.hybrasyl.com/).

The instructions in this document will guide you in setting up and running the latest version of Hybrasyl and connecting a Dark Ages client for testing purposes. Note that this project does not provide a full copy of game content (properly configured items, maps, warps between maps, etc); it comes with a very basic set that will allow you to log in but not enough to come close to playing the full game. [Ealagad](https://github.com/hybrasyl/ealagad) can be used for easy content editing.

## Terminology and key concepts
There are three processes that need to be properly configured in order to connect a Dark Ages client to your own Hybrasyl instance: the *[Hybrasyl launcher](https://github.com/hybrasyl/launcher)* (which modifies the Dark Ages client), the *[Hybrasyl server](https://github.com/hybrasyl/server)*, and a *MySQL server*. This tutorial assumes that the launcher (DA client) and Hybrasyl server will be running on the same machine, but the MySQL server can either be either colocated or installed on another reachable server. We'll call the machine running the Hybrasyl server and launcher client the **Hybrasyl server,** and the machine running the MySQL server is the **data server**. If you're running all of this on the same machine then just do everything there.

## Data server
The **data server** maintains all game metadata and state, including registered character names, mobs, towns, spawn points, items, and a bunch of other game-related data. The Hybrasyl server retrieves game data from the MySQL server at runtime; you can also use [Ealagad](https://github.com/hybrasyl/ealagad) as a Hybrasyl-specific administration interface once the base server is configured.

1. Instructions should be readily available for installing the latest MySQL server on your operating system of choice ([Windows and Mac OS installer](https://dev.mysql.com/downloads/installer/), [Ubuntu](https://help.ubuntu.com/12.04/serverguide/mysql.html)). Install the server and ensure that you can connect to it from the **Hybrasyl server**; this may require you to grant other hosts access to your MySQL server process or potentially opening ports on the **data server**.
2. Import the [schema](https://github.com/hybrasyl/server/blob/master/schema.sql) from this repository, which will create two databases: `dev_hybrasyl` and `hybrasyl`.
3. Import the [sample data](https://github.com/hybrasyl/server/blob/master/seed.sql) from this repository once the schema has been added. This will populate the database with enough information to properly log a user in and allow for a bit of wandering, but not much else.

If you can connect to the **data server** from the **Hybrasyl server** and retrieve a short list of items from the `items` table, your data server is in good shape.

## Hybrasyl server
The **Hybrasyl server** is a bit more involved and requires compiling both Hybrasyl server and (potentially) launcher code from scratch.

1. Install [Microsoft Visual Studio](https://www.visualstudio.com/en-us/downloads/visual-studio-2015-downloads-vs.aspx). The Community Edition is free and capable of compiling both projects.
2. Clone both the [launcher](https://github.com/hybrasyl/launcher) and [server](https://github.com/hybrasyl/server) repositories to your local machine using a [git client](https://git-scm.com/downloads/guis). Make sure you clone them into separate directories.
3. Copy `lod136.map` and `lod500.map` from your Dark Ages directory (or an online archive) into `My Documents\Hybrasyl\Maps`. Create the directory if it doesn't exist.
4. Open `Hybrasyl.sln` in Visual Studio and update all NuGet packages.
5. Build Hybrasyl. The default settings should be adequate for most system setups, assuming you've updated and installed all NuGet packages (which is supposed to happen automatically but sometimes has issues).
  - Note that you may get an error about an invalid ADO.NET MySQL connector. In my experience the build still completes without issues.
6. Run Hybrasyl.exe either from within Visual Studio or as a standalone executable in the `hybrasyl\bin\Debug` folder of your git repository. This should launch the server and run you through a first-launch configuration wizard. Here's you'll need to point the **Hybrasyl server** to the **data server**; make sure you provide the right hostname and also use the `dev_hybrasyl` database (not `hybrasyl`, even though both will exist).
    - Note that you can change these settings later by editing `config.xml` in `My Documents\Hybrasyl`.

That completes Hybrasyl server setup. You should be able to use the [released version of the launcher](https://www.hybrasyl.com/releases) to connect to it. In case you have trouble with the latest launcher, open `Hy-brasyl Launcher.sln` and build the project. Launch the executable and select `localhost` from the server configuration dropdown. You should now be able to connect to your Hybrasyl server, create a new character, and log in!

## Logging in

Log in to your new server by launching the Hy-brasyl Launcher application, either compiled as described above or downloaded from [hybrasyl.com](https://www.hybrasyl.com/). Point it to a local Dark Ages client installation, select `localhost` from the server configuration dropdown, and launch. The launcher will ask you for a local Dark Ages client executable; you must have the latest client installed in order to continue. Once launched, you should see a Hybrasyl welcome screen in place of the standard Dark Ages welcome screen. Congratulations -- you're connected!

Create a character and log in the same way you would on a production server. You should find your Aisling in an inn and ready to explore the world.

## Testing tips and other notable resources

* There are a number of admin flags that can be used for common tasks like creating items, teleporting, or changing level and class. Certain flags require that you be registered as a game master. The fastest way to configure this is to add a record in the `flags_players` table that contains the player ID (which you can find in the `players` table) and the flag ID (which should be `0` in the case of the game master flag).

* Some flags require admin-level access to the *Hybrasyl server*. Try typing `/item Shirt 1` when you log in to add a shirt to your inventory. You can add any item that's in the `item` table of your `dev_hybrasyl` database.

* Warps are links between locations on the map. You can add or remove warps in the `warps` table.

* You can add new items by modifying the `items` table. There are a couple of sample items included in `seed.sql` that should provide good models to follow.