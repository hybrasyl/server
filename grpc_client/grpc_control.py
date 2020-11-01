#!/bin/env python
#
# grpc_control.py - Control or query a running Hybrasyl server using gRPC.
#

import Patron_pb2
import Patron_pb2_grpc
from grpc import ssl_channel_credentials, secure_channel
import os.path
import sys

cert = None
key = None

if (len(sys.argv) != 3):
    print("grpc_control.py: Control a running Hybrasyl server using GRPC")
    print("usage:           grpc_control.py shutdown <minutes>")
    sys.exit(1)

if os.path.exists(os.path.expanduser("~/.grpc/hybrasylCert.pem")):
    cert = open(os.path.expanduser("~/.grpc/hybrasylCert.pem"), "rb").read()
else:
    print("Authentication certificate not found, expecting ~/.grpc/hybrasylCert.pem")

if os.path.exists(os.path.expanduser("~/.grpc/hybrasylKey.pem")):
    key = open(os.path.expanduser("~/.grpc/hybrasylKey.pem"), "rb").read()
else:
    print("Authentication key not found, expecting ~/.grpc/hybrasylKey.pem")

if os.path.exists(os.path.expanduser("~/.grpc/eriscoca.pem")):
    cacert = open(os.path.expanduser("~/.grpc/eriscoca.pem"), "rb").read()
else:
    print("CA chain certificate not found, expecting ~/.grpc/eriscoca.pem")

cc = ssl_channel_credentials(root_certificates=cacert,
                             private_key=key,
                             certificate_chain=cert)

channel = secure_channel("staging.hybrasyl.com:2613", cc)

stub = Patron_pb2_grpc.PatronStub(channel)

f = stub.BeginShutdown(patron_pb2.BeginShutdownRequest(Delay=sys.argv[2]))

print("Shutdown request submitted")
