#!/usr/bin/python
#
#
# Migrate all mailboxes in a Hybrasyl Redis store to having a new key name Hybrasyl.Messaging.Mailbox:<uuid>.
#

import redis
import json
import sys

r = redis.Redis()

MIGRATION_NAME = "20200930-mailbox-uuid"

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
   
for mailboxkey in r.keys("Hybrasyl.Mailbox:*"):
    username = mailboxkey.decode().split(':')[1]
    userjson = r.get(f"User:{username}")
    userobj = json.loads(userjson)
    print(f"{MIGRATION_NAME}: moving {mailboxkey} to Hybrasyl.Mailbox:{userobj['Uuid']}")
    r.rename(mailboxkey, f"Hybrasyl.Messaging.Mailbox:{userobj['Uuid']}")
    
# Set the migration as having run

r.set('Hybrasyl.RedisMigrations', json.dumps(migrations))

print(f"{MIGRATION_NAME}: Marked migration as complete")
