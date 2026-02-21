# GW2-DonBot

![Imgur Image](https://i.imgur.com/tQ4LD6H.png)

A Discord bot for Guild Wars 2 communities that processes [EliteInsights](https://github.com/baaron4/GW2-Elite-Insights-Parser) combat logs, tracks player performance, manages raffles, and handles guild administration.

---

## Features

### Combat Log Processing

Automatically detects and processes combat log URLs posted in Discord:

- **Supported sources**: `dps.report`, `wvw.report`, `gw2wingman.nevermindcreations.de`
- **WvW fights**: Generates fight summaries with participant stats, damage, deaths, and builds. Posts advanced reports to a configurable channel. Updates player rankings and points. Includes a "Know My Enemy" button to view enemy team composition.
- **PvE fights**: Generates fight summaries for strikes, fractals, and raids. Supports multi-log aggregation raid reports.
- **Rotation analysis**: Automatically inspects player skill rotations in PvE logs to detect suspiciously consistent cast intervals that may indicate macro or bot usage. Detected anomalies are recorded in the database for review.

### Points & Rankings

- Players earn points by participating in tracked WvW fights
- `/gw2_points`: View your points balance, spending points, and leaderboard rank
- Background polling (every 30 min) syncs GW2 API data, updates guild membership, and applies/removes Discord roles

### Raffle System

Two raffle types, each with slash commands and one-click entry buttons:

**Standard Raffles**
- `/gw2_create_raffle`: Create a raffle
- `/gw2_enter_raffle`: Enter by spending points
- `/gw2_complete_raffle`: Pick a winner
- `/gw2_reopen_raffle`: Reopen the last raffle

**Event Raffles** (support multiple winners)
- `/gw2_create_event_raffle`: Create an event raffle
- `/gw2_enter_event_raffle`: Enter by spending points
- `/gw2_complete_event_raffle`: Pick N winners
- `/gw2_reopen_event_raffle`: Reopen the last event raffle

Raffle messages include buttons to check your points and enter with 1, 50, 100, 1000, or a random number of points.

### Raid Management

- `/gw2_start_raid`: Start a raid session; all logs posted during the session are aggregated
- `/gw2_close_raid`: Close the raid and generate a summary report
- `/gw2_start_alliance_raid`: Start a raid with a custom alliance alert message

### Scheduled Events

- Posts recurring event messages with **Join / Can't Join / Can Fill** roster buttons
- Tracks attendance per event; updates the roster embed as players respond

### GW2 Account Verification

- `/gw2_verify`: Link a GW2 API key to your Discord account
- `/gw2_deverify`: Unlink your GW2 account
- Verified accounts receive configurable Discord roles based on primary/alliance guild membership

### Spam Protection

- Automatically removes Discord invite links posted by unverified users
- Logs removed messages to a configurable moderation channel

### Wordle Integration

- Posts a daily NYT Wordle starting word hint (with word definition) to a configured channel at 15:01 UTC

### Steam / Deadlock Integration

- `/steam_verify`: Link your Steam account ID
- `/deadlock_mmr`: View your current Deadlock MMR and rank
- `/deadlock_mmr_history`: View your last 5 MMR records
- `/deadlock_match_history`: View recent match history

### Server Configuration

`/gw2_set_log_channel`: Set which channel receives combat log summaries.

Additional per-guild settings (configured in the database):
- Announcement, raid alert, advanced log, player report, and moderation channels
- GW2 verified role, primary guild ID & role, secondary/alliance guild IDs
- Spam removal toggle

---

## Technology Stack

- **.NET 10.0**, C#
- **Discord.Net 3.18.0**
- **Entity Framework Core 10.0.3** with SQL Server
- **Serilog**: structured logging to console and daily rolling files
- **Microsoft.Extensions.Hosting**: runs as a Windows Service

## Build & Deploy

```bash
# Build
dotnet build --configuration Release

# Publish
dotnet publish -c Release -o ./publish
```

Configuration is provided via `appsettings.json` at deploy time (excluded from the repo). Secrets are managed at runtime via `ISecretService`. Logs are written to `Logs/DonBot-*.txt`.