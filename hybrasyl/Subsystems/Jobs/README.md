# All About Jobs

Jobs (each one is a class) to be scheduled at startup by Hybrasyl's timers go here.
NOTE: ONLY JOB CLASSES GO HERE.

Each class needs an Interval (which represents how often it should run, in seconds)
and a void Execute() which will do the work.

Jobs run in their own thread, so there are some important considerations.

1) Never modify game state or call game logic directly. That's what message passing is for!
   Reading state is OK, unless you're using it for something critical (player state)
   in which case consistency (obviously) isn't guaranteed.
   Example: a job that, say, occasionally reported the number of logged in players to
   somewhere else via an external API call: probably fine. A job that checked to see if
   a player had a certain item and then took action based on that - BAD. The only exception
   to this is if the object implements some kind of locking (see Mailbox for an example).

2) You can send packets to clients from a job (since doing so is intended to be
   thread safe). An instance of how to use this is in the AutoSnoreJob, where we can
   send snore packets to idle clients without being a bother to anything else. Since this
   is effectively a threadsafe operation that doesn't change game state or logic - this
   is fine.
