# Hybrasyl build image used by Github Actions

FROM mcr.microsoft.com/dotnet/sdk:8.0

RUN apt-get update && apt-get -yy dist-upgrade && apt-get -yy install ca-certificates gnupg zip jq curl redis-server file openssh-client rsync mono-runtime
RUN mkdir -p /etc/apt/keyrings && curl -fsSL https://download.docker.com/linux/debian/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg && \
  echo "deb [arch="$(dpkg --print-architecture)" signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/debian \
  "$(. /etc/os-release && echo "$VERSION_CODENAME")" stable" > /etc/apt/sources.list.d/docker.list && apt-get update && \
  apt-get -yy install docker.io docker-buildx-plugin
RUN curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip" && unzip awscliv2.zip && ./aws/install
RUN curl "https://github.com/fieu/discord.sh/releases/download/v2.0.0/discord.sh" > /usr/local/bin/discord.sh && chmod +x /usr/local/bin/discord.sh
RUN curl -L "https://github.com/getsops/sops/releases/download/v3.7.3/sops_3.7.3_amd64.deb" > /tmp/sops_3.7.3_amd64.deb && dpkg -i /tmp/sops_3.7.3_amd64.deb
RUN curl "https://www.naturaldocs.org/download/natural_docs/2.2/Natural_Docs_2.2.zip" > /tmp/NaturalDocs.zip && \ 
    cd /tmp && unzip /tmp/NaturalDocs.zip && mv "/tmp/Natural Docs" /usr/local/bin/naturaldocs
