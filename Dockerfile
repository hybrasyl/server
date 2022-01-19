# Hybrasyl Server dockerfile
#
# This is a work in progress! It is mostly intended to be used for the quickstart
# and not a viable production server.
#

FROM mcr.microsoft.com/dotnet/aspnet:6.0

COPY HybrasylTests/world /HybrasylData/world/
COPY /hybrasyl/bin/Release/net6.0/linux-x64 /App
COPY /contrib/config.xml /root/Hybrasyl/config.xml

ENTRYPOINT ["dotnet", "/App/Hybrasyl.dll"]
