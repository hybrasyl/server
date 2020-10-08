#!/usr/bin/python
#
#
# Migrate all serialized users in a Hybrasyl Redis store to having a separate AuthInfo construct.
#
# Run with python 3, e.g. `python 20200929-authinfo.py`. Assumes running redis on localhost.
#

import redis
import json
import sys

r = redis.Redis()

MIGRATION_NAME = "20200929-authinfo"

MIGRATION_FIELDS = {'LastLogin': 'Login',
                    'LastLogoff': 'Login',
                    'LastLoginFailure': 'Login',
                    'LastLoginFrom': 'Login',
                    'LoginFailureCount': 'Login',
                    'CreatedTime': 'Login',
                    'FirstLogin': 'Login'}

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
    username = userkey.decode().split(':')[1]
    authinfo = {}
    userobj = json.loads(r.get(userkey))
    print(f"{MIGRATION_NAME}: Running migration for {username}, uuid {userobj['Uuid']}")
    for k,v in MIGRATION_FIELDS.items():
        authinfo[k] = userobj[v][k]
    # Handle fields that were renamed
    authinfo['LastPasswordChange'] = userobj['Password']['LastChanged']
    authinfo['LastPasswordChangeFrom'] = userobj['Password']['LastChangedFrom']
    authinfo['PasswordHash'] = userobj['Password']['Hash']
    authinfo['UserUuid'] = userobj['Uuid']   
    userobj.pop('Password')
    userobj.pop('Login')
    authinfo_json = json.dumps(authinfo)
    #print(authinfo_json)
    authinfo_key = f"Hybrasyl.AuthInfo:{userobj['Uuid']}"
    r.set(authinfo_key, authinfo_json)
    print(f"{MIGRATION_NAME}: AuthInfo converted, saved {authinfo_key}")
    r.set(f"User:{username}", json.dumps(userobj))
    print(f"{MIGRATION_NAME}: User object for {username} pruned and saved")

# Set the migration as having run

migrations['Migrations'].append(MIGRATION_NAME)
r.set('Hybrasyl.RedisMigrations', json.dumps(migrations))

print(f"{MIGRATION_NAME}: Marked migration as complete")
