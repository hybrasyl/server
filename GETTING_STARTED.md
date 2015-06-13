#Hybrasyl server: getting started

This document is intended for developers; if you're interested in using a released version of Hybrasyl server, please check the [releases page](https://www.hybrasyl.com/releases) of the project website. Hybrasyl is a work in progress and many features are not yet implemented. You can see what's currently in the works on the [bug tracker](https://hybrasyl.atlassian.net/).

The instructions in this document will guide you in setting up and running the latest version of Hybrasyl and connecting a Dark Ages client for testing purposes.

## Terminology and key concepts
There are three processes that need to be properly configured in order to connect a Dark Ages client to your own Hybrasyl instance: the *[Hybrasyl launcher](https://github.com/hybrasyl/launcher)* (which modifies the Dark Ages client), the *[Hybrasyl server](https://github.com/hybrasyl/server)*, and a *MySQL server*. This tutorial assumes that the launcher (DA client) and Hybrasyl server will be running on the same machine, but the MySQL server can either be either colocated or installed on another reachable server.

This document will refer to the machine running the Hybrasyl server and launcher client as the **Hybrasyl server.** The machine running the MySQL server is the **data server**. If you're running all of this on the same machine then just do everything there.

## Data server
The **data server** maintains all game metadata and state, including registered character names, mobs, towns, spawn points, and a bunch of other game-related data. The Hybrasyl server retrieves game data from the MySQL server at runtime; you can also use [Ealagad](https://github.com/hybrasyl/ealagad) as a Hybrasyl-specific administration interface once the base server is configured.

1. Instructions should be readily available for installing the latest MySQL server on your operating system of choice ([Windows and Mac OS installer](https://dev.mysql.com/downloads/installer/), [Ubuntu](https://help.ubuntu.com/12.04/serverguide/mysql.html)). Install the server and ensure that you can connect to it from the **Hybrasyl server**; this may require you to grant access to additional hosts.
2. Import the [schema](https://github.com/hybrasyl/server/blob/master/schema.sql) from this repository, which will establish two tables: dev_hybrasyl and hybrasyl.
3. [[somehow get content in there - pending resolution of  [issue #1](https://github.com/hybrasyl/server/issues/1)]]

If you can connect to the **data server** from the **Hybrasyl server** and retrieve a list of [[something once content is in there]], your data server is in good shape.

## Hybrasyl server
The **Hybrasyl server** is a bit more involved and requires compiling both Hybrasyl server and launcher code from scratch.

1. Install [Microsoft Visual Studio](https://www.visualstudio.com/en-us/downloads/visual-studio-2015-downloads-vs.aspx). The Community Edition is free and capable of compiling both projects.
2. Clone both the [launcher](https://github.com/hybrasyl/launcher) and [server](https://github.com/hybrasyl/server) repositories to your local machine using a [git client](https://git-scm.com/downloads/guis). Make sure you clone them into separate directories.
3. Open *Hybrasyl.sln* in Visual Studio and update all NuGet packages.
4. Build Hybrasyl. The default settings should be adequate for most system setups, assuming you've updated and installed all NuGet packages (which is supposed to happen automatically but sometimes has issues).
  - Note that you may get an error about an invalid ADO.NET MySQL connector. In my experience the build still completes without issues.
5. Run Hybrasyl.exe either from within Visual Studio or as a standalone executable in the hybrasyl/bin/Debug folder of your git repository. This should launch the server and run you through a first-launch configuration wizard. Here's you'll need to point the **Hybrasyl server** to the **data server**; make sure you provide the right hostname and also use the *dev_hybrasyl* database (not *hybrasyl*, even though both will exist).
    - Note that you can change these settings later by editing *config.xml* in *My Documents/Hybrasyl/*.
6. That completes Hybrasyl server setup. As far as I can tell, you should be able to use the [released version of the launcher](https://www.hybrasyl.com/releases) but I had trouble getting it to connect; feel free to try that and only use the next step if required.
7. In order to set up the launcher, open *Hy-brasyl Launcher.sln* and build the project. Launch the executable and select *localhost* from server configuration. You should now be able to connect to your Hybrasyl server, create a new character, and log in!