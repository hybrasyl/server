# Redis Migrations

Occasionally there are changes to serialized data structures that are
too complex or error prone to be done live.  These scripts take care
of those migrations.

Generally these migrations are one-way, so you will want to backup
Redis (eg copy `dump.rdb` / `appendonly.aof` from `/var/lib/redis` to
a separate directory before running.

## Adding Migrations

Add the migration name to `active.json`. Hybrasyl's build process
embeds this file as a resource, and it will be consulted for
migrations needing to be run before server start. Hybrasyl will refuse
to start unless all migrations are applied.

Individual scripts go in `Scripts` in this directory. They should
define a migration name that will be used to tell if the migration has
run or not.

Look at `Scripts/20200929-authinfo.py` for examples. Note that your migration
must be idempotent.

## Running Migrations

Migrations will check to see if they have been run, and will not
re-run.  It is safe to run them all any number of times using the
included `run_migrations.sh` script in the `Scripts` directory.



