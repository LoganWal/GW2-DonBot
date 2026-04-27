FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore DonBot.Api/DonBot.Api.csproj
RUN dotnet publish DonBot.Api/DonBot.Api.csproj -c Release -o /app/publish --no-restore /p:ErrorOnDuplicatePublishOutputFiles=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DonBot.Api.dll"]
