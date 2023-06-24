# Hybrasyl build image used by Github Actions

FROM mcr.microsoft.com/dotnet/sdk:7.0

RUN apt-get update && apt-get -yy dist-upgrade && apt-get -yy install zip jq curl redis-server file openssh-client
RUN curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip" && unzip awscliv2.zip && ./aws/install
RUN curl "https://github.com/fieu/discord.sh/releases/download/v2.0.0/discord.sh" > /usr/local/bin/discord.sh && chmod +x /usr/local/bin/discord.sh
