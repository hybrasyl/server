#!/usr/bin/python
#
#
# Rename all boards to Hybrasyl.Messaging keyspace.
#

import redis
import json
import sys

r = redis.Redis()

MIGRATION_NAME = "20201210-rename-cookies"

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

for userkey in r.keys("User:*"):
    userobj = json.loads(r.get(userkey))
    userobj['Cookies'] = userobj['UserCookies']
    del userobj['UserCookies']
    r.set(userkey, json.dumps(userobj))
    
# Set the migration as having run

r.set('Hybrasyl.RedisMigrations', json.dumps(migrations))

print(f"{MIGRATION_NAME}: Marked migration as complete")
