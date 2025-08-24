# Этап сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем csproj и восстанавливаем зависимости
COPY *.csproj ./
RUN dotnet restore

# Копируем остальное и собираем
COPY . ./
RUN dotnet publish -c Release -o /app

# Этап рантайма
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app .

# Задаём порт
ENV ASPNETCORE_URLS=http://+:5254
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5254
ENTRYPOINT ["dotnet", "KursorServer.dll"]
