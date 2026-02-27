# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore dependencies (cached layer)
COPY GW2-DonBot/DonBot.csproj GW2-DonBot/
RUN dotnet restore GW2-DonBot/DonBot.csproj

# Build & publish
COPY GW2-DonBot/ GW2-DonBot/
RUN dotnet publish GW2-DonBot/DonBot.csproj -c Release -o /app/publish --no-restore

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "DonBot.dll"]
