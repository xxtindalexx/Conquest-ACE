# CONQUEST - Discord Integration Setup Guide

## Overview

The Discord integration allows content creators to contribute to the server without needing direct VM/FTP access. Developers can import SQL/JSON files uploaded to Discord channels and export content for creators to modify.

## Features

### Import Commands
- `/import-discord <identifier>` (alias: `/id`) - Import SQL from Discord
- `/import-discord-clothing <filename>` (alias: `/idc`) - Import clothing JSON from Discord

### Export Commands
- `/export-discord <wcid> [type]` (alias: `/ed`) - Export content to Discord as SQL
- `/export-discord-clothing <id>` (alias: `/edc`) - Export clothing JSON to Discord

### Supported Content Types
- **Weenies** - Items, creatures, NPCs
- **Recipes** - Crafting recipes
- **Landblocks** - Landblock instances
- **Quests** - Quest definitions
- **Spells** - Spell definitions
- **Clothing** - ClothingBase modifications (JSON)

---

## Discord Bot Setup

### Step 1: Create Discord Application
1. Go to https://discord.com/developers/applications
2. Click "New Application"
3. Name it (e.g., "Conquest Bot")
4. Click "Create"

### Step 2: Create Bot User
1. Click "Bot" in left sidebar
2. Click "Add Bot"
3. Under "Privileged Gateway Intents", enable:
   - Message Content Intent
   - Server Members Intent
4. Click "Reset Token" and copy the token (save it securely!)

### Step 3: Invite Bot to Server
1. Click "OAuth2" → "URL Generator"
2. Select scopes:
   - `bot`
3. Select bot permissions:
   - Read Messages/View Channels
   - Send Messages
   - Attach Files
   - Read Message History
4. Copy generated URL and open in browser
5. Select your Discord server and authorize

### Step 4: Get Server and Channel IDs
1. In Discord: User Settings → Advanced → Enable "Developer Mode"
2. Right-click your server name → Copy ID (this is ServerId)
3. Create channels (or use existing):
   - `#content-uploads` - For creators to upload SQL
   - `#content-exports` - For developers to export SQL
   - `#clothing-uploads` - For clothing mod JSON uploads
   - `#clothing-exports` - For clothing mod JSON exports
   - `#admin-audit` - For audit logging (optional)
4. Right-click each channel → Copy ID

---

## Server Configuration

### Edit Config.js

Add the following section to your `Config.js` file:

```json
{
  "Chat": {
    "EnableDiscordConnection": true,
    "DiscordToken": "YOUR_BOT_TOKEN_HERE",
    "ServerId": 1234567890123456789,
    "GeneralChannelId": 0,
    "TradeChannelId": 0,
    "AdminAuditId": 1234567890123456789,
    "EventsChannelId": 0,
    "ExportsChannelId": 1234567890123456789,
    "WeenieUploadsChannelId": 1234567890123456789,
    "RaffleChannelId": 0,
    "AdminChannelId": 0,
    "WebhookURL": "",
    "ClothingModUploadChannelId": 1234567890123456789,
    "ClothingModExportChannelId": 1234567890123456789,
    "PerformanceAlertsChannelId": 0
  }
}
```

**Replace:**
- `YOUR_BOT_TOKEN_HERE` with your actual bot token
- Channel IDs with the IDs you copied
- Set unused channels to `0`

### Required NuGet Packages

The following packages are required (should auto-restore):
- `Discord.Net` (or `Discord.Net.WebSocket`)
- `Newtonsoft.Json`

---

## Two-Way Chat Integration

The Discord integration includes **bidirectional chat relay** between Discord and in-game chat channels.

### Supported Chat Channels

| In-Game Channel | Discord Channel | Config Setting |
|----------------|-----------------|----------------|
| General Chat | `#general` | `GeneralChannelId` |
| Trade Chat | `#trade` | `TradeChannelId` |
| Admin Channel | `#admin-channel` | `AdminChannelId` |
| Events Broadcast | `#events` | `EventsChannelId` |

### How It Works

**Discord → In-Game:**
- Messages sent in Discord `#general` appear in in-game General chat
- Messages sent in Discord `#trade` appear in in-game Trade chat
- Messages sent in Discord `#admin-channel` appear on Admin channel (for admins with `/channel admin` enabled)
- Messages sent in Discord `#events` appear as World Broadcast messages to all players

**In-Game → Discord:**
- Messages sent to in-game General chat are relayed to Discord `#general`
- Messages sent to in-game Trade chat are relayed to Discord `#trade`

### Player Experience

**In-Game Players:**
- See Discord messages in their respective chat channels (General, Trade)
- Discord username appears as the sender name
- Must have the chat channel enabled (default settings apply)
- Can squelch/ignore Discord users if needed

**Discord Users:**
- Can participate in General and Trade chat without being in-game
- See real-time messages from in-game players
- Can use Events channel for server-wide announcements (with proper permissions)

### Configuration Notes

- Bot must have **Message Content Intent** enabled in Discord Developer Portal
- Each chat relay requires the corresponding `ChannelId` to be configured in `Config.js`
- Set unused channel IDs to `0` to disable that specific relay
- Discord bot messages are ignored (prevents message loops)

---

## Content Import/Export Workflows

### Workflow 1: Content Creator Creates New Weenie

**Creator Side:**
1. Create/edit weenie in local editor (Lifestoned, ACE.Adapter, etc.)
2. Export weenie to SQL file (e.g., `12345.sql`)
3. Upload `12345.sql` to Discord `#content-uploads` channel
4. In the message body, type only: `12345` (the WCID without .sql extension)
5. Notify developer that content is ready

**Developer Side:**
1. Log into server as Developer+
2. Run command: `/import-discord 12345` (or `/id 12345`)
3. Server downloads SQL from Discord and executes against database
4. Notify creator that weenie is live
5. Creator can test in-game

### Workflow 2: Developer Exports Weenie for Modification

**Developer Side:**
1. Run command: `/export-discord 12345` (or `/ed 12345`)
2. Server exports weenie to SQL and uploads to Discord `#content-exports`
3. Notify creator in Discord

**Creator Side:**
1. Download SQL file from `#content-exports`
2. Import into local editor
3. Make modifications
4. Follow Workflow 1 to upload changes

### Workflow 3: Clothing Mod Creation

**Creator Side:**
1. Create/edit ClothingBase JSON file
2. Upload JSON to Discord `#clothing-uploads` channel
3. In message body, type: `0x1000ABCD` (the hex ID)

**Developer Side:**
1. Run command: `/import-discord-clothing 0x1000ABCD` (or `/idc 0x1000ABCD`)
2. JSON file saved to Content folder
3. Server may need restart to load new clothing

### Workflow 4: Export Clothing for Modification

**Developer Side:**
1. Run command: `/export-discord-clothing 268439741` (or `/edc 268439741`)
   - Note: Use decimal ID, not hex
2. JSON file uploaded to Discord `#clothing-exports`

---

## Command Reference

### Import Commands

#### `/import-discord <identifier>` (alias: `/id`)
**Access Level:** Developer
**Description:** Downloads SQL file from Discord WeenieUploadsChannelId and executes against database

**Usage:**
```
/import-discord 12345
/import-discord questname
/id 12345
```

**How it works:**
1. Searches last 20 messages in WeenieUploadsChannelId
2. Finds message with content matching `<identifier>`
3. Downloads SQL attachment from that message
4. Executes SQL against world database

**Notes:**
- Message content must EXACTLY match identifier
- Only first SQL attachment is processed
- SQL errors will prevent import

#### `/import-discord-clothing <filename>` (alias: `/idc`)
**Access Level:** Developer
**Description:** Downloads JSON file from Discord ClothingModUploadChannelId

**Usage:**
```
/import-discord-clothing 0x1000ABCD
/idc 0x1000ABCD
```

### Export Commands

#### `/export-discord <wcid> [content-type]` (alias: `/ed`)
**Access Level:** Developer
**Description:** Exports content from database to SQL and uploads to Discord ExportsChannelId

**Usage:**
```
/export-discord 12345
/export-discord 12345 weenie
/export-discord 0xAB94 landblock
/export-discord Assassin quest
/export-discord 1234 recipe
/export-discord 1 spell
/ed 12345
```

**Content Types:**
- `weenie` (default)
- `recipe`
- `landblock`
- `quest`
- `spell`

#### `/export-discord-clothing <id>` (alias: `/edc`)
**Access Level:** Developer
**Description:** Exports ClothingBase from DAT file to JSON and uploads to Discord

**Usage:**
```
/export-discord-clothing 268439741
/edc 268439741
```

**Notes:**
- ID must be in decimal format
- ID range: 0x10000000 - 0x10FFFFFF (268435456 - 285212671)

---

## Security Considerations

### Access Control
- Only Developer (AccessLevel.Developer) can use import/export commands
- Content creators should NOT have in-game Developer access
- Content creators only need Discord channel access

### Discord Permissions
- Restrict upload channels to trusted content creators
- Use Discord roles to control who can upload
- Consider approval workflow for imports

### SQL Validation
- SQL executes with full database permissions
- Malicious SQL can damage database
- Review SQL files before importing if creator is untrusted
- Consider implementing approval queue

### Best Practices
1. Keep bot token secret (use environment variables if possible)
2. Regularly audit imports (use AdminAuditId channel)
3. Backup database before importing large changes
4. Test imports on dev server first
5. Version control your SQL files

---

## Troubleshooting

### Bot Doesn't Connect
- Check DiscordToken is correct
- Check ServerId is correct
- Check bot is in Discord server
- Check bot has required permissions
- Check "EnableDiscordConnection": true in Config.js

### Can't Find SQL Attachment
- Message content must EXACTLY match identifier
- Only searches last 20 messages
- File must be .sql attachment
- Upload to correct channel (WeenieUploadsChannelId)

### Import Fails with SQL Error
- Check SQL syntax
- Check weenie/recipe/etc. doesn't already exist
- Check foreign key constraints
- Use DELETE statements before INSERT

### Export Does Nothing
- Check content exists in database
- Check ExportsChannelId is correct
- Check bot has permission to send files in channel
- Check for errors in server log

---

## Channel Setup Recommendations

### Minimal Setup (Required)
```
#content-uploads     → WeenieUploadsChannelId
#content-exports     → ExportsChannelId
```

### Recommended Setup
```
#content-uploads     → WeenieUploadsChannelId
#content-exports     → ExportsChannelId
#clothing-uploads    → ClothingModUploadChannelId
#clothing-exports    → ClothingModExportChannelId
#admin-audit         → AdminAuditId
```

### Full Setup
```
#general             → GeneralChannelId (in-game chat relay)
#trade               → TradeChannelId (in-game trade chat)
#content-uploads     → WeenieUploadsChannelId
#content-exports     → ExportsChannelId
#clothing-uploads    → ClothingModUploadChannelId
#clothing-exports    → ClothingModExportChannelId
#admin-audit         → AdminAuditId
#admin-channel       → AdminChannelId
#events              → EventsChannelId
#performance         → PerformanceAlertsChannelId
#raffles             → RaffleChannelId
```

---

## Advanced: Automation Ideas

### Auto-Import on Upload
- Use Discord event listener to watch for uploads
- Automatically execute import when SQL file uploaded
- Send confirmation message when complete

### Approval Workflow
- Uploads go to #content-pending channel
- Admin reviews and approves with reaction
- Auto-import on approval

### Version Tracking
- Log all imports to database
- Track who created/modified each weenie
- Allow rollback to previous versions

### Batch Import
- Upload multiple SQL files
- Import all with single command

---

## Testing Checklist

- [ ] Bot connects to Discord successfully
- [ ] `/export-discord 12345` exports weenie to Discord
- [ ] `/import-discord 12345` imports SQL from Discord
- [ ] `/export-discord-clothing 268439741` exports ClothingBase
- [ ] `/import-discord-clothing 0x1000ABCD` imports clothing JSON
- [ ] Error handling for missing files works
- [ ] Error handling for invalid SQL works
- [ ] Error handling for Discord connection failures works
- [ ] Permissions check (Developer access) works
- [ ] Export works for all content types (weenie, recipe, landblock, quest, spell)
- [ ] Aliases work (/id, /ed, /idc, /edc)

---

## Support

For issues or questions:
1. Check server log for errors
2. Verify Discord configuration
3. Test with a simple weenie export/import first
4. Check the Discord_Integration_Analysis.txt for technical details

---

## Credits

Discord integration system originally developed for ILT (Infinite Legends of Temitri) server.
Adapted for Conquest server.
