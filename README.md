# GW2-DonBot

![DonBot](https://i.imgur.com/tQ4LD6H.png)

DonBot is a Discord bot and web app for Guild Wars 2 communities. It turns combat logs into summaries, tracks player stats, manages points and raffles, and gives server admins a web interface for common guild settings.

![Website](https://i.imgur.com/JAsd9LI.png)

[Add DonBot to your server](https://discord.com/api/oauth2/authorize?client_id=1021682849797111838&permissions=8&scope=bot)

## What It Does

- Reads `dps.report`, `wvw.report`, and GW2 Wingman log links posted in Discord.
- Creates WvW and PvE fight summaries from Elite Insights data.
- Adds WvW tools such as advanced reports and Know My Enemy.
- Flags suspicious PvE rotation patterns for review.
- Tracks player stats, best times, progression, mechanics, and leaderboards.
- Aggregates raid sessions started from Discord.
- Links Discord users to Guild Wars 2 API keys.
- Awards WvW participation points and lets players spend them on raffles.
- Posts recurring raid signups and leaderboard messages.
- Uploads `.zevtc` logs through the web app when Elite Insights CLI is configured.
- Helps admins manage guild channels, roles, feature toggles, and scheduled events.

## Discord Commands

### Account

| Command | Purpose |
|---|---|
| `/verify` | Link a Guild Wars 2 API key to your Discord account |
| `/deverify` | Remove your linked account |
| `/points` | View your points, history, and rank |
| `/my_rank` | View your leaderboard ranks |

### Raffles

| Command | Purpose |
|---|---|
| `/create_raffle` | Create a single-winner raffle |
| `/enter_raffle` | Spend points on a raffle entry |
| `/complete_raffle` | Draw a raffle winner |
| `/reopen_raffle` | Reopen the last raffle |
| `/create_event_raffle` | Create a multi-winner event raffle |
| `/enter_event_raffle` | Spend points on an event raffle entry |
| `/complete_event_raffle` | Draw event raffle winners |
| `/reopen_event_raffle` | Reopen the last event raffle |

### Raids and Logs

| Command | Purpose |
|---|---|
| `/start_raid` | Start collecting logs for a raid session |
| `/close_raid` | Close the session and post an aggregate report |
| `/start_alliance_raid` | Start a raid with a custom alert message |
| `/add_quote` | Add a quote used in DonBot message footers |
| `/digut` | Check the current GW2 Pinata event cycle |

### Server Admin

Use `/server_config` to set Discord channels, roles, Guild Wars 2 guild IDs, and feature toggles.

Common settings include:

- Log drop-off, report, advanced report, announcement, raid alert, removed-message, and leaderboard channels.
- Guild member, secondary member, and verified roles.
- Primary and secondary Guild Wars 2 guild IDs.
- Auto Wingman submission.
- Auto log aggregation and single-log replies.
- Raid alerts.
- Spam and art-spam filtering.
- WvW and PvE leaderboard posting.

## Web App

The web app uses Discord login and gives players and admins a richer interface than Discord commands.

Pages include:

- Dashboard
- Fight Logs
- Live Raid
- My Stats
- Personal Bests
- Progression
- Mechanics
- Leaderboard
- Raffles and Points
- Account linking
- Log upload
- Scheduling
- Server Admin

Log upload supports:

- URL uploads from `dps.report` and `wvw.report`.
- `.zevtc` file uploads when Elite Insights CLI is configured.
- Live progress while logs are stored, parsed, uploaded, and saved.
- Optional Wingman submission for PvE logs.

## Self-Hosting

### Requirements

- .NET 10 SDK
- PostgreSQL
- Node.js and npm for local frontend development
- Docker and Docker Compose for container deployment
- Elite Insights CLI for `.zevtc` file uploads

Elite Insights currently requires a .NET runtime compatible with its release. If you use file uploads, install the runtime required by the Elite Insights version you deploy.

### Configuration

For local development, copy the example settings files and fill in local values:

- `GW2-DonBot/appsettings.example.json` to `GW2-DonBot/appsettings.user.json`
- `DonBot.Api/appsettings.example.json` to `DonBot.Api/appsettings.user.json`

For Docker, use `.env` or real environment variables.

Required for the bot and API:

| Key | Purpose |
|---|---|
| `DonBotToken` | Discord bot token |
| `DonBotSqlConnectionString` | PostgreSQL connection string |

Required for the API:

| Key | Purpose |
|---|---|
| `DiscordClientId` | Discord OAuth2 client ID |
| `DiscordClientSecret` | Discord OAuth2 client secret |
| `DonBotJwtKey` | 32+ character JWT signing key |
| `Discord:RedirectUri` | OAuth callback URL from the Discord Developer Portal |
| `Nuxt:BaseUrl` | Web app URL used after login |

Optional:

| Key | Purpose |
|---|---|
| `WebApp:BaseUrl` | Adds web links to Discord messages |
| `CookieDomain` | Cookie domain for shared production subdomains |
| `Cors:AllowedOrigins` | Allowed API origins |
| `EliteInsights:DllPath` | Path to Elite Insights CLI |
| `EliteInsights:OutputBasePath` | Elite Insights output directory |
| `Upload:StoragePath` | Uploaded log storage directory |
| `Upload:MaxRequestBytes` | Max upload size |
| `Upload:MaxConcurrentProcessing` | Parallel upload processing limit |
| `DpsReport:UserToken` | Optional dps.report upload token |

### Docker

```bash
docker-compose up -d
```

The compose stack runs:

- Discord bot
- API
- Web frontend
- Watchtower for image updates

Published images:

- `ghcr.io/loganwal/gw2-donbot:latest`
- `ghcr.io/loganwal/gw2-donbot-api:latest`
- `ghcr.io/loganwal/gw2-donbot-web:latest`

Set `GHCR_USER` and `GHCR_TOKEN` if Watchtower needs credentials to pull packages.

## Local Development

Run the bot:

```bash
dotnet run --project GW2-DonBot/DonBot.csproj
```

Run the API:

```bash
dotnet run --project DonBot.Api/DonBot.Api.csproj
```

Run the web app:

```bash
cd DonBot.Web
npm install
npm run dev
```

The web app runs at `http://localhost:3000` by default. The API runs at `http://localhost:5001` by default.

## Tests

```bash
dotnet test
```

Useful focused runs:

```bash
dotnet test --filter "FullyQualifiedName~DpsReportGetJsonMapperTests"
dotnet test --filter "FullyQualifiedName~PointsEndpointsIntegrationTests"
```

Frontend production build:

```bash
cd DonBot.Web
npm run build
```

## Database

Migrations are managed by EF Core and apply on startup.

Manual commands:

```bash
dotnet tool install --global dotnet-ef
dotnet ef database update --project GW2-DonBot/DonBot.csproj
dotnet ef migrations add DescriptiveName --project GW2-DonBot/DonBot.csproj
```

Commit entity changes and generated migrations together.

## Tech Stack

- .NET 10
- Discord.Net
- ASP.NET Core minimal APIs
- Entity Framework Core and PostgreSQL
- Nuxt 4
- PrimeVue
- Chart.js
- Docker
