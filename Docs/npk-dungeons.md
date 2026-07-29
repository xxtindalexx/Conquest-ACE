# NPK-Only Dungeons

NPK-only dungeons are safe PvE areas where players **must remain Non-Player Killers (NPK)**. They are the inverse of PK-only dungeons: instead of blocking unflagging, they block flagging.

## Overview

| | PK Dungeon | NPK Dungeon |
|---|---|---|
| Who can enter | PK players only | NPK players only |
| Blocked while inside | `/pk off`, NPK switches | `/pk on`, PK switches |
| If wrong status inside | Booted every ~5s | Booted every ~5s |
| XP/Lum bonus | +10% | None |
| Soul fragments | Yes | None |
| Aug stripping | Yes | None |
| Death/logout lockouts | Yes | None |
| No-log on logout | Yes | None |

NPK dungeons enforce **core PK-status rules only**. They do not include the rewards, penalties, or lockout systems that PK dungeons have.

## Setup

### 1. Run the database migration

Apply the SQL migration against your **world** database:

```
Database/Updates/World/2026-07-24-00-Npk-Dungeon-Landblocks.sql
```

This creates the `npk_dungeon_landblocks` table.

### 2. Start or reload the server

On startup, the server loads all NPK dungeon configs from the database into memory automatically.

To reload without restarting:

```
/npkdungeon reload
```

## Admin Commands

All commands require Admin access.

### Add an NPK dungeon

```
/npkdungeon add <landblock> <variation> [description]
```

Examples:

```
/npkdungeon add 0x002B 2 Safe Egg Orchard
/npkdungeon add 43 0 Starter Dungeon Base
```

- **landblock** — hex (`0x002B`) or decimal (`43`)
- **variation** — variant number (`0` = base, `1+` = variants)
- **description** — optional label shown in admin lists

### Remove an NPK dungeon

```
/npkdungeon remove <landblock> <variation>
```

Example:

```
/npkdungeon remove 0x002B 2
```

### List all NPK dungeons

```
/npkdungeon list
```

### Reload from database

```
/npkdungeon reload
```

### Mutual exclusivity

A landblock+variation pair **cannot** be both a PK dungeon and an NPK dungeon. Attempting to add a conflicting entry is rejected with an error message.

The PK-only counterpart uses `/pkdungeon` with the same subcommands (`add`, `remove`, `list`, `reload`).

## Player Behavior

### Who can enter

- **NPK players** — allowed through portals
- **PK players** — blocked at the portal with: *"This dungeon is NPK-only. You must be a non-Player Killer to enter."*
- **PKLite players** — also blocked (they are not NPK)

### While inside an NPK dungeon

- `/pk on` is blocked: *"You cannot enable PK status while in an NPK-only dungeon. Leave the dungeon first."*
- **PK switches** are blocked with the same message
- NPK switches still work normally (player is already NPK)

### If a PK player gets inside anyway

If a player enters with PK status (e.g., via recall or admin teleport), the server boots them on the next heartbeat (~5 seconds) to their lifestone, last portal, or Holtburg as a fallback.

Message: *"You have been removed from this NPK-only dungeon. You must be a non-Player Killer to remain here."*

### Admin bypass

Admins bypass the periodic enforcement tick and are not booted from NPK dungeons regardless of PK status.

## Enforcement Flow

```
Player uses portal
  └─ Destination is NPK dungeon?
       ├─ Player is NPK → allow entry
       └─ Player is PK/PKLite → block at portal

Player inside NPK dungeon (heartbeat ~every 5s)
  └─ Player is not NPK and not admin?
       └─ Boot to lifestone / last portal / Holtburg

Player tries /pk on or PK switch inside
  └─ Block with message, must leave first
```

## Troubleshooting

### Dungeon rules not applying

1. Confirm the dungeon is listed: `/npkdungeon list`
2. Verify the **variation number** matches the dungeon variant (not just the landblock)
3. Run `/npkdungeon reload` after manual database edits
4. Check server startup logs for `Loaded NPK dungeon:` entries

### Player enters but gets booted immediately

- Check their PK status — PK and PKLite are not allowed
- Only `PlayerKillerStatus.NPK` is permitted

### Cannot add a dungeon

- Check if the same landblock+variation is already a PK dungeon: `/pkdungeon list`
- Remove the conflicting entry from one system before adding to the other

### Portal allows wrong players through

- Portal checks only apply when the **destination** landblock+variation is in the NPK dungeon registry
- Make sure the portal's destination variation matches the configured variation exactly

## Related Files

| File | Purpose |
|------|---------|
| `Source/ACE.Database/Models/World/NpkDungeonLandblock.cs` | Database model |
| `Source/ACE.Server/Entity/Landblock.cs` | Runtime cache and load |
| `Source/ACE.Server/WorldObjects/Portal.cs` | Entry gate |
| `Source/ACE.Server/WorldObjects/Player_Tick.cs` | Periodic enforcement |
| `Source/ACE.Server/Command/Handlers/PlayerCommands.cs` | `/pk on` block |
| `Source/ACE.Server/WorldObjects/PKModifier.cs` | PK switch block |
| `Source/ACE.Server/Command/Handlers/AdminCommands.cs` | `/npkdungeon` admin command |
