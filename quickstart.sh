#!/bin/bash

# Pull image from DockerHub
docker pull baughj/hybrasyl:quickstart
docker run -it -p 2610:2610 -p 2611:2611 -p 2612:2612 --add-host=host.docker.internal:host-gateway baughj/hybrasyl:quickstart
