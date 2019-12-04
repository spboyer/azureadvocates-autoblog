FROM mcr.microsoft.com/dotnet/core/sdk:3.1 as build-env
WORKDIR /app

COPY autoblog/*.csproj ./
RUN dotnet restore

COPY ./autoblog ./
RUN dotnet publish -c Release -o out

# FINAL IMAGE
FROM mcr.microsoft.com/dotnet/core/runtime:3.1
WORKDIR /app
COPY --from=build-env /app/out .

# install git
RUN apt-get update && \
  apt-get upgrade -y && \
  apt-get install -y git-core

COPY ./autoblog/startup.sh .
RUN chmod 777 startup.sh
ENTRYPOINT ["/bin/bash", "./startup.sh"]