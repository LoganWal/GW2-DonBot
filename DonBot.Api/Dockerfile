FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore DonBot.Api/DonBot.Api.csproj
RUN dotnet publish DonBot.Api/DonBot.Api.csproj -c Release -o /app/publish --no-restore /p:ErrorOnDuplicatePublishOutputFiles=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update && apt-get install -y curl && \
    curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0 --runtime dotnet --install-dir /usr/share/dotnet && \
    apt-get remove -y curl && apt-get autoremove -y && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DonBot.Api.dll"]
