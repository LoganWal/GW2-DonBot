# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY GW2-DonBot/DonBot.csproj GW2-DonBot/packages.lock.json GW2-DonBot/
COPY DonBot.Core/DonBot.Core.csproj DonBot.Core/packages.lock.json DonBot.Core/
RUN dotnet restore GW2-DonBot/DonBot.csproj --locked-mode

COPY . .
RUN dotnet publish GW2-DonBot/DonBot.csproj -c Release -o /app/publish --no-restore

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "DonBot.dll"]
