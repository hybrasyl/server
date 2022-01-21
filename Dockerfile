# Hybrasyl Server dockerfile
#
# This is a work in progress! It is mostly intended to be used for the quickstart
# and not a viable production server.
#

FROM mcr.microsoft.com/dotnet/aspnet:6.0

COPY /hybrasyl/bin/Release/net6.0/linux-x64 /App

ENTRYPOINT ["dotnet", "/App/Hybrasyl.dll"]

EXPOSE 2610/tcp
EXPOSE 2611/tcp
EXPOSE 2612/tcp
