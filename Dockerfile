# Сборка отдельным слоем: в готовый образ не попадают ни SDK, ни исходники.
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Сначала только csproj и восстановление пакетов: правка кода не сбрасывает
# слой с зависимостями, и пересборка занимает секунды вместо минут.
COPY src/Domain/*.csproj     src/Domain/
COPY src/Logic/*.csproj      src/Logic/
COPY src/Overpass/*.csproj   src/Overpass/
COPY src/Database/*.csproj   src/Database/
COPY src/CheckIn.Api/*.csproj src/CheckIn.Api/
RUN dotnet restore src/CheckIn.Api/CheckIn.Api.csproj

COPY src/ src/
RUN dotnet publish src/CheckIn.Api/CheckIn.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine

ENV ASPNETCORE_HTTP_PORTS=8080 \
    Database__Path=/data/checkin.db \
    DataProtection__KeysPath=/data/keys

WORKDIR /app
COPY --from=build /app .

# В /data лежат и база, и ключи, которыми подписываются cookie. Ключи обязаны
# пережить пересборку: иначе каждый деплой разлогинивал бы всех разом.
RUN mkdir -p /data && chown -R $APP_UID /data
VOLUME /data

USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "CheckIn.Api.dll"]
