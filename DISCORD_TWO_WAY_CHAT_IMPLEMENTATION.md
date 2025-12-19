# Two-Way Discord Chat Integration - Implementation Summary

## Overview

Implemented bidirectional chat relay between Discord and ACE server, allowing Discord users to communicate with in-game players in real-time through General and Trade chat channels.

---

## Files Modified

### 1. DiscordChatManager.cs
**Location:** `C:\Users\xxtin\Documents\ACEMain\Source\ACE.Server\Managers\DiscordChatManager.cs`

**Changes Made:**
- Added missing `using` statements for new functionality
- Added `ConcurrentQueue<string> outgoingMessages` for message queue system
- Updated `Initialize()` method:
  - Added `GatewayIntents` configuration (Guilds, GuildMessages, MessageContent)
  - Registered `MessageReceived` event handler (`OnDiscordChat`)
- Added `OnDiscordChat()` event handler:
  - Discord → In-Game relay for Admin, General, Trade, and Events channels
  - Filters bot messages to prevent loops
  - Routes messages to appropriate in-game channels
- Added `SendTurbineChat()` method:
  - Sends Discord messages to in-game Turbine Chat system
  - Respects player chat preferences (ListenToGeneralChat, ListenToTradeChat)
- Added `SendAuctionNotification()` method:
  - DM specific Discord users for auction notifications
- Added `SendDiscordDM()` method:
  - Generic Discord DM functionality
- Added `QueueMessageForDiscord()` method:
  - Message queue system for outgoing Discord messages

**Key Features:**
- ✅ Discord → In-Game relay
- ✅ Bot message filtering
- ✅ Channel routing (Admin, General, Trade, Events)
- ✅ Player preference respect
- ✅ DM functionality for notifications

---

### 2. TurbineChatHandler.cs
**Location:** `C:\Users\xxtin\Documents\ACEMain\Source\ACE.Server\Network\Handlers\TurbineChatHandler.cs`

**Changes Made:**
- Added In-Game → Discord relay after line 112
- Added General chat relay:
  ```csharp
  if (adjustedchatType == ChatType.General)
  {
      DiscordChatManager.SendDiscordMessage(session.Player.Name, message, ConfigManager.Config.Chat.GeneralChannelId);
  }
  ```
- Added Trade chat relay:
  ```csharp
  if (adjustedchatType == ChatType.Trade)
  {
      DiscordChatManager.SendDiscordMessage(session.Player.Name, message, ConfigManager.Config.Chat.TradeChannelId);
  }
  ```

**Key Features:**
- ✅ In-Game → Discord relay
- ✅ General chat support
- ✅ Trade chat support
- ✅ Real-time message forwarding

---

### 3. PlayerManager.cs
**Location:** `C:\Users\xxtin\Documents\ACEMain\Source\ACE.Server\Managers\PlayerManager.cs`

**Changes Made:**
- Added `BroadcastFromDiscord()` method after line 644:
  ```csharp
  public static void BroadcastFromDiscord(Channel channel, string senderName, string message)
  {
      foreach (var player in GetAllOnline().Where(p => (p.ChannelsActive ?? 0).HasFlag(channel)))
      {
          player.Session.Network.EnqueueSend(new GameEventChannelBroadcast(
              player.Session,
              channel,
              senderName, // Directly use senderName from Discord
              message
          ));
      }

      LogBroadcastChat(channel, null, message); // Log without a Player object
  }
  ```

**Key Features:**
- ✅ Broadcasts Discord messages to in-game channels
- ✅ Uses Discord username as sender name
- ✅ Respects player channel subscriptions
- ✅ Logs broadcast chat without Player object

---

### 4. DISCORD_SETUP_GUIDE.md
**Location:** `C:\Users\xxtin\Documents\ACEMain\DISCORD_SETUP_GUIDE.md`

**Changes Made:**
- Added new "Two-Way Chat Integration" section
- Documented supported chat channels (General, Trade, Admin, Events)
- Added "How It Works" section explaining Discord → In-Game and In-Game → Discord flows
- Added "Player Experience" section for both in-game and Discord users
- Added configuration notes about Message Content Intent requirement

---

## Channel Mapping

| In-Game Channel | Discord Channel | Config Setting | Direction |
|----------------|-----------------|----------------|-----------|
| General Chat | `#general` | `GeneralChannelId` | ↔️ Bidirectional |
| Trade Chat | `#trade` | `TradeChannelId` | ↔️ Bidirectional |
| Admin Channel | `#admin-channel` | `AdminChannelId` | ← Discord → In-Game only |
| Events Broadcast | `#events` | `EventsChannelId` | ← Discord → In-Game (World Broadcast) |

---

## Technical Implementation Details

### Discord → In-Game Flow

1. User sends message in Discord channel
2. `OnDiscordChat` event handler in DiscordChatManager receives message
3. Bot message filter prevents loops
4. Channel ID is matched to determine destination
5. Depending on channel:
   - **Admin:** `PlayerManager.BroadcastFromDiscord(Channel.Admin, sender, content)`
   - **General:** `SendTurbineChat(ChatType.General, sender, content)`
   - **Trade:** `SendTurbineChat(ChatType.Trade, sender, content)`
   - **Events:** `PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast))`
6. Message appears in-game with Discord username as sender

### In-Game → Discord Flow

1. Player sends message in General or Trade chat
2. `TurbineChatHandler.TurbineChatReceived` processes message
3. After creating `gameMessageTurbineChat`, checks `adjustedchatType`
4. If General or Trade:
   - `DiscordChatManager.SendDiscordMessage(player.Name, message, channelId)`
5. Message appears in Discord with player name

### Message Content Intent Requirement

**Critical:** Discord requires the "Message Content Intent" to be enabled in the Discord Developer Portal:
1. Go to https://discord.com/developers/applications
2. Select your application
3. Click "Bot" in sidebar
4. Under "Privileged Gateway Intents", enable:
   - ✅ Message Content Intent
   - ✅ Server Members Intent

Without this, the bot cannot read message content and the relay will not work.

---

## Configuration Requirements

### Config.js Example

```json
{
  "Chat": {
    "EnableDiscordConnection": true,
    "DiscordToken": "YOUR_BOT_TOKEN_HERE",
    "ServerId": 1234567890123456789,
    "GeneralChannelId": 1234567890123456789,
    "TradeChannelId": 1234567890123456789,
    "AdminChannelId": 1234567890123456789,
    "EventsChannelId": 1234567890123456789,
    "ExportsChannelId": 1234567890123456789,
    "WeenieUploadsChannelId": 1234567890123456789,
    "AdminAuditId": 1234567890123456789,
    "RaffleChannelId": 0,
    "WebhookURL": "",
    "ClothingModUploadChannelId": 1234567890123456789,
    "ClothingModExportChannelId": 1234567890123456789,
    "PerformanceAlertsChannelId": 0
  }
}
```

**Note:** Set unused channel IDs to `0` to disable that specific feature.

---

## Testing Checklist

### Discord → In-Game

- [ ] Send message in Discord `#general`, verify it appears in-game General chat
- [ ] Send message in Discord `#trade`, verify it appears in-game Trade chat
- [ ] Send message in Discord `#admin-channel`, verify it appears on Admin channel
- [ ] Send message in Discord `#events`, verify it broadcasts to all players
- [ ] Verify bot messages are ignored (no loops)
- [ ] Verify Discord username appears as sender name in-game
- [ ] Verify players with chat channels disabled don't receive messages

### In-Game → Discord

- [ ] Send message in-game General chat, verify it appears in Discord `#general`
- [ ] Send message in-game Trade chat, verify it appears in Discord `#trade`
- [ ] Verify player name appears correctly in Discord
- [ ] Verify message formatting is preserved

### Error Handling

- [ ] Disconnect bot, verify no server crashes
- [ ] Invalid channel IDs, verify graceful failure
- [ ] Bot lacks permissions, verify error logging
- [ ] Empty messages, verify proper filtering

---

## Additional Features Implemented

### SendAuctionNotification(ulong discordUserId, string message)
- Sends DM to specific Discord user
- Used for auction system notifications
- Graceful error handling if user not found

### SendDiscordDM(string playerName, string message, long userId)
- Generic DM functionality
- Supports any player → Discord user DM
- Detailed error logging

### QueueMessageForDiscord(string message)
- Message queue system for outgoing messages
- Thread-safe ConcurrentQueue implementation
- Prevents message loss during high traffic

---

## Source Code References

All implementation based on **Infinite-BeefTide** server codebase:
- `C:\Linux Server Backup\ACE\Infinite-BeefTide\Source\ACE.Server\Managers\DiscordChatManager.cs`
- `C:\Linux Server Backup\ACE\Infinite-BeefTide\Source\ACE.Server\Network\Handlers\TurbineChatHandler.cs` (lines 116-124)
- `C:\Linux Server Backup\ACE\Infinite-BeefTide\Source\ACE.Server\Managers\PlayerManager.cs` (lines 618-631)

---

## Known Limitations

1. **Allegiance Chat:** Not relayed to Discord (intentional for privacy)
2. **Society Chat:** Not relayed to Discord (intentional for faction privacy)
3. **LFG/Roleplay:** Not relayed to Discord (can be added if needed)
4. **Emotes/Actions:** Not relayed (technical limitation)
5. **Private Messages:** Not relayed (intentional for privacy)

---

## Future Enhancements

### Potential Additions:
- [ ] LFG channel relay
- [ ] Roleplay channel relay
- [ ] Discord slash commands for server status
- [ ] Webhook support for rich embeds
- [ ] Message rate limiting
- [ ] Profanity filter integration
- [ ] Discord role-based permissions for in-game commands

---

## Troubleshooting

### Messages Not Appearing In-Game
1. Check `EnableDiscordConnection` is `true` in Config.js
2. Check `GeneralChannelId`/`TradeChannelId` are correct
3. Check bot has "Message Content Intent" enabled
4. Check player has General/Trade chat enabled in-game
5. Check server logs for errors

### Messages Not Appearing in Discord
1. Check bot has "Send Messages" permission in channel
2. Check channel IDs are correct
3. Check bot is online in Discord server
4. Check server logs for connection errors

### Bot Not Connecting
1. Verify DiscordToken is correct
2. Verify ServerId is correct
3. Verify bot has been invited to server
4. Check for firewall/network issues

---

## Performance Considerations

- **Message Queue:** ConcurrentQueue prevents blocking on Discord API calls
- **Event Handler:** Async/await pattern prevents thread blocking
- **Player Filtering:** LINQ `Where()` efficiently filters by channel subscription
- **Bot Message Filter:** Prevents infinite message loops
- **Error Handling:** Try-catch blocks prevent crashes from Discord API failures

---

## Security Considerations

- **Bot Token:** Keep `DiscordToken` secret, never commit to version control
- **Channel Permissions:** Restrict Discord channels to trusted community members
- **Spam Protection:** Consider implementing rate limiting in future updates
- **Squelch Support:** Players can squelch Discord users via in-game squelch system
- **Admin Channel:** Only visible to players with Admin channel enabled

---

## Credits

Two-way Discord chat integration originally developed for **Infinite-BeefTide** server.
Adapted for **Conquest** server by integrating with existing Discord import/export system.

Implementation Date: 2025-12-17
