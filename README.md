# GW2-DonBot

![Imgur Image](https://i.imgur.com/tQ4LD6H.png)

A Discord bot for Guild Wars 2 communities that processes [EliteInsights](https://github.com/baaron4/GW2-Elite-Insights-Parser) combat logs, tracks player performance, manages raffles, and handles guild administration.

**[Add DonBot to your server](https://discord.com/api/oauth2/authorize?client_id=1021682849797111838&permissions=8&scope=bot)**

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

All per-guild settings are configured via `/gw2_server_config` (Administrator only). Each setting is a subcommand:

| Subcommand | Type | Description |
|---|---|---|
| `log_drop_off_channel` | Channel | Channel where fight logs are dropped off via webhook |
| `log_report_channel` | Channel | Channel where fight summaries are posted |
| `advance_log_report_channel` | Channel | Channel for advanced WvW log reports |
| `stream_log_channel` | Channel | Channel for stream log output |
| `player_report_channel` | Channel | Channel for player ranking reports |
| `wvw_activity_report_channel` | Channel | Channel for WvW activity reports |
| `announcement_channel` | Channel | Channel for announcements |
| `raid_alert_channel` | Channel | Channel for raid alerts |
| `removed_message_channel` | Channel | Channel where removed spam messages are logged |
| `guild_member_role` | Role | Discord role assigned to primary GW2 guild members |
| `secondary_member_role` | Role | Discord role assigned to alliance guild members |
| `verified_role` | Role | Discord role assigned to verified members |
| `gw2_guild_member_role_id` | String | GW2 guild UUID for primary membership |
| `gw2_secondary_member_role_ids` | String | Comma-separated GW2 guild UUIDs for alliance membership |
| `raid_alert_enabled` | Boolean | Enable or disable raid alerts |
| `remove_spam_enabled` | Boolean | Auto-remove links posted by unverified users |
| `auto_submit_to_wingman` | Boolean | Submit dps.report logs to gw2wingman automatically (default: on) |
| `auto_aggregate_logs` | Boolean | Post an aggregate summary when multiple logs are shared at once (default: on) |
| `auto_reply_single_log` | Boolean | Reply with a fight summary when a single log is shared (default: off) |

---

## Technology Stack

- **.NET 10.0**, C#
- **Discord.Net 3.18.0**
- **Entity Framework Core 10.0.3** with SQL Server
- **Serilog**: structured logging to console and daily rolling files
- **Microsoft.Extensions.Hosting**: supports Windows Service, Linux systemd, and Docker

## Build & Deploy

```bash
# Build
dotnet build --configuration Release

# Publish
dotnet publish -c Release -o ./publish
```

**Deployment options:**
- **Docker**: copy `.env.example` to `.env`, fill in your values, then `docker-compose up`
- **Linux systemd**: use `deploy/donbot.service` — copy to `/etc/systemd/system/`, place the published output at `/opt/donbot/`
- **Windows Service**: register the published executable with `sc.exe`

**Configuration** is via environment variables (see `.env.example`):

| Variable | Description |
|---|---|
| `DonBotToken` | Discord bot token |
| `DonBotSqlConnectionString` | SQL Server connection string |

`appsettings.json` is committed and contains base Serilog configuration. For local overrides, create `appsettings.user.json` in the project folder - it is loaded automatically and can override any setting from `appsettings.json`. Logs are written to `Logs/DonBot-*.txt` by default.