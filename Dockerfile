FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG GIT_HASH=unknown
WORKDIR /src
COPY hybrasyl/Hybrasyl.csproj hybrasyl/
RUN dotnet restore hybrasyl/Hybrasyl.csproj
COPY hybrasyl/ hybrasyl/
RUN dotnet publish hybrasyl/Hybrasyl.csproj -c Release -r linux-x64 --self-contained -o /app \
    /p:MSBuildGitHashValue=$GIT_HASH

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0
WORKDIR /app
RUN groupadd -r hybrasyl && useradd -r -g hybrasyl hybrasyl
COPY --from=build /app .
USER hybrasyl
EXPOSE 2610/tcp
EXPOSE 2611/tcp
EXPOSE 2612/tcp
ENTRYPOINT ["./Hybrasyl"]
