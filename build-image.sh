#!/bin/bash

# Pull latest data from ceridwen
git submodule update --init --recursive

# Build server
cd hybrasyl && dotnet build -c Release --sc -r linux-x64 && cd ..

# Build docker image
docker build . -t baughj/hybrasyl:quickstart


