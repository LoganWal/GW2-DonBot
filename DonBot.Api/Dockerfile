FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY DonBot.Api/DonBot.Api.csproj DonBot.Api/packages.lock.json DonBot.Api/
COPY GW2-DonBot/DonBot.csproj GW2-DonBot/packages.lock.json GW2-DonBot/
COPY DonBot.Core/DonBot.Core.csproj DonBot.Core/packages.lock.json DonBot.Core/
RUN dotnet restore DonBot.Api/DonBot.Api.csproj --locked-mode

COPY . .
RUN dotnet publish DonBot.Api/DonBot.Api.csproj -c Release -o /app/publish --no-restore /p:ErrorOnDuplicatePublishOutputFiles=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update && apt-get install -y curl && \
    curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0 --runtime dotnet --install-dir /usr/share/dotnet && \
    apt-get remove -y curl && apt-get autoremove -y && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DonBot.Api.dll"]
