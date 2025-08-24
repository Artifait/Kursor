# syntax=docker/dockerfile:1
ARG DOTNET_SDK_VERSION=8.0
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_VERSION} AS build
WORKDIR /src

# copy csproj(s) and restore
COPY *.sln ./
COPY ./Server/*.csproj ./Server/
RUN dotnet restore

# copy everything and publish
COPY . .
WORKDIR /src/Server
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000/tcp
EXPOSE 50000/udp

WORKDIR /app
COPY --from=build /app/publish .

# Optional env defaults (can override at runtime)
ENV CURSOR_UDP_PORT=50000
ENV ROOM_TTL_MINUTES=5
ENV ALLOWED_ORIGINS=*

ENTRYPOINT ["dotnet", "Server.dll"]
