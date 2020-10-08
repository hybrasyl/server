#!/usr/bin/python
#
#
# Rename all boards to Hybrasyl.Messaging keyspace.
#

import redis
import json
import sys

r = redis.Redis()

MIGRATION_NAME = "20201006-board-rename"

migrations = r.get('Hybrasyl.RedisMigrations')

if (migrations is not None):
    migrations = json.loads(r.get('Hybrasyl.RedisMigrations'))
    if MIGRATION_NAME in migrations['Migrations']:
        print("This migration has already been applied.")
        sys.exit()
    migrations['Migrations'].append(MIGRATION_NAME)
else:
    migrations = {}
    migrations['Migrations'] = [MIGRATION_NAME]
   
for boardkey in r.keys("Hybrasyl.Board:*"):
    storagekey = boardkey.decode().split(':')[1]
    print(f"{MIGRATION_NAME}: moving board {boardkey.decode()} to Hybrasyl.Messaging.Board:{storagekey}")
    r.rename(boardkey, f"Hybrasyl.Messaging.Board:{storagekey}")
    
# Set the migration as having run

r.set('Hybrasyl.RedisMigrations', json.dumps(migrations))

print(f"{MIGRATION_NAME}: Marked migration as complete")
