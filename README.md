# GW2-DonBot

![DonBot](https://i.imgur.com/tQ4LD6H.png)

A Discord bot and web application for Guild Wars 2 communities. Processes [Elite Insights](https://github.com/baaron4/GW2-Elite-Insights-Parser) combat logs, tracks player performance, manages raffles, and handles guild administration.

**[Add DonBot to your server](https://discord.com/api/oauth2/authorize?client_id=1021682849797111838&permissions=8&scope=bot)**

---

## Features

### Combat Log Processing

Automatically detects and processes combat log URLs posted in Discord. Supported sources: `dps.report`, `wvw.report`, `gw2wingman.nevermindcreations.de`.

**WvW fights**
Generates a fight summary embed with participant stats, damage, deaths, and builds. Updates player points and rankings. Includes a "Know My Enemy" button to inspect enemy team composition. If an advanced log report channel is configured, a detailed report is also posted there.

**PvE fights**
Generates fight summaries for strikes, fractals, and raids. Supports multi-log aggregation into a combined raid report with a "Best Times" button comparing session clears against all-time bests per boss and mode.

**Rotation analysis**
Inspects player skill rotations in PvE logs for patterns that may indicate macro or bot usage. Detected anomalies are flagged for review.

**Wingman auto-submit**
PvE log URLs are submitted to [gw2wingman](https://gw2wingman.nevermindcreations.de) in the background after processing. WvW logs are excluded. Configurable per guild.

**Posting flow**
When a log URL is detected, the bot can optionally prompt the uploader before posting publicly. Controlled by the `auto_aggregate_logs` and `auto_reply_single_log` guild settings:

1. Bot prompts: **Post Summary** or **Dismiss**
2. If confirmed: **Also submit to Wingman?** (Yes / No)
3. Summary is posted publicly

For logs delivered via webhook (e.g. a dedicated log drop-off channel), the Wingman setting follows `auto_submit_to_wingman` instead.

---

### Points and Rankings

Players earn points by participating in tracked WvW fights. Points can be spent on raffle entries.

- `/gw2_points` - view your balance, spending history, and leaderboard rank
- Role polling (every 30 min) syncs GW2 API data, updates guild membership, and applies or removes Discord roles automatically

---

### Weekly Leaderboards

Posted on a configurable schedule (default: every Monday at 00:00 UTC).

**WvW leaderboard** - top 20 players over the past 7 days across: Damage, Down Contribution, Cleanses, Strips, Stab (avg, 10+ logs minimum), Healing, Barrier, Times Downed, Damage Taken, Kills, Distance From Tag (avg, 10+ logs minimum).

**PvE leaderboard** - top 20 players (6+ logs minimum) across: DPS, Cleave DPS, Avg Res Time, Avg Damage Taken, Avg Times Downed.

- `/gw2_my_rank` - view your personal rank across all leaderboards (ephemeral)
- `/gw2_add_quote` - add a quote to the guild pool; quotes appear randomly in leaderboard footers

---

### Raffle System

Two raffle types with slash commands and one-click entry buttons.

**Standard Raffles** - single winner

| Command | Description |
|---|---|
| `/gw2_create_raffle` | Create a raffle |
| `/gw2_enter_raffle` | Enter by spending points |
| `/gw2_complete_raffle` | Pick a winner |
| `/gw2_reopen_raffle` | Reopen the last raffle |

**Event Raffles** - multiple winners

| Command | Description |
|---|---|
| `/gw2_create_event_raffle` | Create an event raffle |
| `/gw2_enter_event_raffle` | Enter by spending points |
| `/gw2_complete_event_raffle` | Pick N winners |
| `/gw2_reopen_event_raffle` | Reopen the last event raffle |

Raffle messages include buttons to check your balance and enter with 1, 50, 100, 1000, or a random number of points.

---

### Raid Management

- `/gw2_start_raid` - start a raid session; logs posted during the session are aggregated
- `/gw2_close_raid` - close the raid and generate a combined summary report
- `/gw2_start_alliance_raid` - start a raid with a custom alliance alert message

The aggregate summary includes a **Best Times** button comparing session clears against all-time bests per boss and mode.

---

### Scheduled Events

Recurring event posts configured per guild.

| Type | Description |
|---|---|
| **PvE Raid Signup** | Weekly roster embed with Join / Can't Join / Can Fill buttons |
| **WvW Raid Signup** | Weekly roster embed with Join / Can't Join / Will Be Late buttons |
| **WvW Leaderboard** | Weekly WvW stats post |
| **PvE Leaderboard** | Weekly PvE stats post |
| **Wordle** | Daily Wordle starting word hint with definition |

---

### GW2 Account Verification

- `/gw2_verify` - link a GW2 API key to your Discord account
- `/gw2_deverify` - unlink your GW2 account
- Verified accounts receive configurable Discord roles based on primary and alliance guild membership

---

### Spam Protection

Automatically removes Discord invite links and URLs posted by unverified users, and logs the removal to a configurable moderation channel.

---

### Other Commands

| Command | Description |
|---|---|
| `/digut` | Check the current GW2 Pinata event cycle status |
| `/steam_verify` | Link your Steam account |
| `/deadlock_mmr` | View your Deadlock MMR |
| `/deadlock_mmr_history` | View your MMR history |
| `/deadlock_match_history` | View recent match history |

---

### Server Configuration

All per-guild settings are managed via `/gw2_server_config` (Administrator only).

**Channels**

| Subcommand | Description |
|---|---|
| `log_drop_off_channel` | Channel where fight logs are dropped via webhook |
| `log_report_channel` | Channel where fight summaries are posted |
| `advance_log_report_channel` | Channel for detailed WvW log reports |
| `stream_log_channel` | Channel for stream log output |
| `announcement_channel` | Channel for announcements |
| `raid_alert_channel` | Channel for raid alerts |
| `removed_message_channel` | Channel where removed spam messages are logged |
| `wvw_leaderboard_channel` | Channel for the weekly WvW leaderboard |
| `pve_leaderboard_channel` | Channel for the weekly PvE leaderboard |
| `wordle_channel` | Channel for the daily Wordle message |

**Roles**

| Subcommand | Description |
|---|---|
| `guild_member_role` | Role assigned to primary GW2 guild members |
| `secondary_member_role` | Role assigned to alliance guild members |
| `verified_role` | Role assigned to all verified accounts |
| `wordle_role` | Role mentioned in the daily Wordle message |

**GW2 Guild IDs**

| Subcommand | Description |
|---|---|
| `gw2_guild_member_role_id` | GW2 guild UUID for primary membership |
| `gw2_secondary_member_role_ids` | Comma-separated GW2 guild UUIDs for alliance membership |

**Toggles**

| Subcommand | Default | Description |
|---|---|---|
| `raid_alert_enabled` | off | Enable raid alerts |
| `remove_spam_enabled` | off | Auto-remove URLs from unverified users |
| `auto_submit_to_wingman` | on | Submit PvE logs to gw2wingman automatically |
| `auto_aggregate_logs` | on | Post aggregate summary for multiple logs |
| `auto_reply_single_log` | off | Reply with summary for a single log |
| `wvw_leaderboard_enabled` | off | Enable the weekly WvW leaderboard |
| `pve_leaderboard_enabled` | off | Enable the weekly PvE leaderboard |

**Other**

| Subcommand | Description |
|---|---|
| `wordle_hour` | UTC hour (0-23) for the daily Wordle post |

---

## Web Application

A companion web app provides a richer interface for log history, stats, and uploads. Users log in with Discord OAuth2.

**Pages**

| Page | Description |
|---|---|
| Dashboard | Overview of recent activity |
| Logs | Browse and filter personal fight log history |
| Logs: Upload | Upload `.zevtc` files or paste dps.report/wvw.report URLs |
| Bests | All-time best clear times per boss and mode |
| Mechanics | Per-fight mechanic hit counts |
| Leaderboard | Guild-wide leaderboard |
| Points | Points balance and history |
| Progression | Player progression over time |
| Stats | Detailed player stats |

**Log upload**

The upload page supports two modes:

- **URL mode**: paste one or more `dps.report` or `wvw.report` links. The app fetches, parses, and saves them.
- **File mode**: upload raw `.zevtc` ArcDPS combat logs. The app runs Elite Insights locally to parse the file, uploads to dps.report to generate a shareable URL, and saves the result. Progress is streamed live in the browser across stages: Stored, Parsing, Uploading, Saving, Done.

Logs are also submitted to gw2wingman in the background after the dps.report URL is obtained.

---

## Technology Stack

| Layer | Technology |
|---|---|
| Bot and API | .NET 10, C# |
| Discord integration | Discord.Net 3.18 |
| Database | PostgreSQL via Entity Framework Core 10 + Npgsql |
| Logging | Serilog (console + daily rolling file) |
| File uploads | TUS resumable upload protocol |
| Frontend | Nuxt 4, PrimeVue, Chart.js |
| Hosting | Docker, nginx |
| CI/CD | GitHub Actions, Watchtower |

---

## Setup and Deployment

### Prerequisites

- .NET 10 SDK
- PostgreSQL
- Docker and Docker Compose (for containerised deployment)
- [Elite Insights CLI](https://github.com/baaron4/GW2-Elite-Insights-Parser/releases) (.dll or .exe) - required only for `.zevtc` file uploads. EI targets .NET 8, so the .NET 8 runtime must be installed alongside .NET 10.

### Configuration

**Docker**: copy `.env.example` to `.env` and fill in your values.

**Local development**: copy the relevant `appsettings.example.json` to `appsettings.user.json` in the same directory. Both files are gitignored and loaded automatically at startup.

- Bot: `GW2-DonBot/appsettings.example.json`
- API: `DonBot.Api/appsettings.example.json`

**Required** (both bot and API)

| Key | Description |
|---|---|
| `DonBotToken` | Discord bot token |
| `DonBotSqlConnectionString` | PostgreSQL connection string |

**Required** (API only)

| Key | Description |
|---|---|
| `DiscordClientId` | Discord OAuth2 app client ID |
| `DiscordClientSecret` | Discord OAuth2 app client secret |
| `DonBotJwtKey` | Random 32+ character string for JWT signing (`openssl rand -base64 32`) |
| `Discord:RedirectUri` | OAuth2 callback URL - must match the Discord developer portal |
| `Nuxt:BaseUrl` | URL of the Nuxt frontend, used for post-login redirects |

**Optional** (API)

| Key | Default | Description |
|---|---|---|
| `WebApp:BaseUrl` | _(none)_ | If set, WvW embeds include a "View on DonBot" link |
| `CookieDomain` | _(none)_ | Set to `.yourdomain.com` in production for cross-subdomain auth cookies |
| `Cors:AllowedOrigins` | `[]` | Allowed CORS origins; if empty, any `localhost` origin is allowed |
| `EliteInsights:DllPath` | _(none)_ | Path to EI CLI `.dll` or `.exe`. Required for `.zevtc` file uploads. |
| `EliteInsights:OutputBasePath` | `/tmp/donbot/ei-output` | Directory where EI writes per-job output |
| `Upload:StoragePath` | `/tmp/donbot/uploads` | Root directory for uploaded files |
| `Upload:MaxRequestBytes` | `1073741824` (1 GB) | Kestrel max request body size |
| `Upload:MaxConcurrentProcessing` | `3` | Max parallel upload pipeline jobs |
| `DpsReport:UserToken` | _(none)_ | Links dps.report uploads to your account |

**Docker / CI**

| Key | Description |
|---|---|
| `GHCR_USER` | GitHub username for Watchtower to pull images |
| `GHCR_TOKEN` | GitHub PAT with `read:packages` scope |

### Docker (recommended)

```bash
docker-compose up -d
```

The stack runs the bot, API, web frontend (nginx), and Watchtower for automatic updates.

A new image is pushed to `ghcr.io/loganwal/gw2-donbot:latest` by GitHub Actions on every merge to `main`. Watchtower automatically restarts containers when a new image is available.

**Generating a GitHub PAT for Watchtower:**
1. Go to GitHub, Settings, Developer settings, Personal access tokens, Tokens (classic)
2. Tick `read:packages` only
3. Paste the token as `GHCR_TOKEN` in `.env`

### Local Development

```bash
# Bot
dotnet run --project GW2-DonBot/DonBot.csproj

# API
dotnet run --project DonBot.Api/DonBot.Api.csproj

# Frontend
cd DonBot.Web
npm install
npm run dev     # http://localhost:3000
```

### Database Migrations

Migrations are managed with EF Core and apply automatically on startup.

```bash
# Install the EF Core CLI tool (once)
dotnet tool install --global dotnet-ef

# Apply all pending migrations manually
dotnet ef database update --project GW2-DonBot/DonBot.csproj

# Create a new migration after changing an entity
dotnet ef migrations add DescriptiveName --project GW2-DonBot/DonBot.csproj
```

Always commit the entity change and its generated migration together.
