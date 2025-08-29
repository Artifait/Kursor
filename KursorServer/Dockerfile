# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# копируем и восстанавливаем
COPY *.csproj ./
RUN dotnet restore

# копируем всЄ и публикуем release
COPY . ./
RUN dotnet publish -c Release -o /app

# Final runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app ./

# ѕо умолчанию порты (можно перекрыть в docker run через -e)
ENV UDP_PORT=55555
ENV MONITOR_PORT=8080
ENV DOTNET_ENVIRONMENT=Production

# ƒокументируем пробрасываемые порты:
# UDP: порт дл€ обмена координатами
# TCP: HTTP монитор
EXPOSE 55555/udp
EXPOSE 8080/tcp

ENTRYPOINT ["dotnet", "KursorServer.dll"]
