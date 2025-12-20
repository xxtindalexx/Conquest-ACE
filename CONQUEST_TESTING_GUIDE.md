================================================================================
CONQUEST SERVER - COMPREHENSIVE TESTING GUIDE
================================================================================
Version: 1.0
Date: 2025-12-20
Purpose: Systematic testing of all implemented Conquest features

Legend:
  ✅ = Can test now (code implemented)
  ⚠️ = Partial testing (some aspects need content)
  ❌ = Cannot test (requires content/weenie creation)
  🔧 = Admin/developer tools required

================================================================================
TESTING ENVIRONMENT SETUP
================================================================================

PREREQUISITES:
1. Fresh character created (or use /grantxp to set specific levels)
2. Admin account with access level (for testing admin commands)
3. Test account without admin privileges (for player testing)
4. Multiple IP addresses or VM setup (for multi-account testing)
5. Database access to verify property changes
6. Server logs accessible for error checking

USEFUL ADMIN COMMANDS FOR TESTING:
- /grantxp [amount] - Grant XP to character
- /setlevel [level] - Set character level directly
- /create [wcid] - Create items by weenie ID
- /teleto [coords] - Teleport to specific coordinates
- /god - Enable god mode (invulnerability)
- /ungod - Disable god mode
- /cloak player - Appear as player (for PK testing)

================================================================================
1. PLAYER COMMANDS - BASIC FUNCTIONALITY
================================================================================

-------------------------------------------
TEST 1.1: /qb and /bonus Commands
-------------------------------------------
✅ Can Test Now

PREREQUISITES:
- Character with known quest completion count
- Database access to verify QuestCount property

TEST STEPS:
1. Type `/qb` in-game
2. Type `/bonus` in-game

EXPECTED RESULTS:
- Both commands display identical output
- Shows "=== Quest Bonus (QB) ==="
- Shows "Total Quests Completed: [number]"
- Shows "XP Bonus: [percentage]%"
- Shows "(You gain [percentage]% extra XP from all sources)"

VERIFICATION:
- Formula: 0.01% per quest = (quest_count * 0.0001)
- Example: 1,000 quests = 10% bonus
- Example: 5,000 quests = 50% bonus

EDGE CASES:
[ ] 0 quests completed (should show 0% bonus)
[ ] 1 quest completed (should show 0.01% bonus)
[ ] 10,000 quests (should show 100% bonus)

PASS CRITERIA:
✓ Command executes without errors
✓ Math is correct (quest count × 0.0001)
✓ Both /qb and /bonus show same data

-------------------------------------------
TEST 1.2: /aug and /augs Commands
-------------------------------------------
✅ Can Test Now

PREREQUISITES:
- Character with luminance augmentations purchased

TEST STEPS:
1. Type `/aug` in-game
2. Type `/augs` in-game

EXPECTED RESULTS:
- Both commands display identical output
- Shows "=== Luminance Augmentation Levels ==="
- Lists all 12 augmentation types:
  * Damage Rating
  * Damage Reduction Rating
  * Critical Damage Rating
  * Critical Reduction Rating
  * Surge Chance Rating
  * Healing Rating
  * Item Mana Usage
  * Item Mana Gain
  * Vitality
  * All Skills
  * Skilled Craft
  * Skilled Spec

VERIFICATION:
- Use admin command to set augmentation values
- Verify displayed values match database

EDGE CASES:
[ ] All augmentations at 0
[ ] Mixed augmentation levels
[ ] Maximum augmentation levels

PASS CRITERIA:
✓ Command executes without errors
✓ All 12 augmentation types displayed
✓ Values match database properties

-------------------------------------------
TEST 1.3: /top Leaderboard Commands
-------------------------------------------
✅ Can Test Now

PREREQUISITES:
- Multiple characters with varying stats
- Database stored procedures created (TopQuestBonus, TopLevel, TopEnlightenment, TopBank, TopLum)

TEST STEPS:
1. Type `/top` (no parameters)
2. Type `/top qb`
3. Type `/top level`
4. Type `/top enl` or `/top enlightenment`
5. Type `/top bank`
6. Type `/top lum` or `/top luminance`
7. Type `/top invalid`

EXPECTED RESULTS:
1. Help message: "Specify a leaderboard: /top qb, /top level, /top enl, /top bank, or /top lum"
2. Shows "Top 50 Players by Quest Bonus:"
3. Shows "Top 50 Players by Level:"
4. Shows "Top 50 Players by Enlightenment:"
5. Shows "Top 50 Players by Banked Pyreals:"
6. Shows "Top 50 Players by Banked Luminance:"
7. Error: "Unknown leaderboard 'invalid'. Use: qb, level, enl, bank, or lum"

VERIFICATION:
- Create test characters with known values
- Verify ranking is correct (highest to lowest)
- Verify numbers formatted with thousand separators (1,000,000)

EDGE CASES:
[ ] No data in leaderboard (should show "No data available")
[ ] Only 1 player in leaderboard
[ ] Exactly 50 players
[ ] More than 50 players (should only show top 50)
[ ] Characters with ExcludeFromLeaderboards = 1 (should not appear)

PASS CRITERIA:
✓ All 5 leaderboard types work
✓ Rankings are accurate
✓ Numbers formatted correctly
✓ Cache refreshes after 15 minutes

-------------------------------------------
TEST 1.4: /bank and /b Commands
-------------------------------------------
✅ Can Test Now

PREREQUISITES:
- Pyreals in inventory
- Luminance available
- Event Tokens (Dragon Coins - WCID 13370002) in inventory
- Access to database to verify balances

TEST STEPS - Balance Check:
1. Type `/b b` or `/bank balance`

EXPECTED RESULTS:
- Shows "[BANK] Your balances:"
- Shows "[BANK] Pyreals: [amount]"
- Shows "[BANK] Luminance: [amount]"
- Shows "[BANK] Event Tokens (Dragon Coins): [amount]"
- Shows "[BANK] Conquest Coins (non-transferable): [amount]"
- Shows "[BANK] Soul Fragments (non-transferable): [amount]"

TEST STEPS - Deposit All:
1. Get some pyreals, luminance, event tokens in inventory
2. Type `/b d` or `/bank deposit`

EXPECTED RESULTS:
- Message: "Deposited all Pyreals, Luminance, and Event Tokens!"
- Items removed from inventory
- Bank balance increases accordingly

TEST STEPS - Deposit Specific Amount:
1. Type `/b d p 1000` (deposit 1000 pyreals)
2. Type `/b d l 500` (deposit 500 luminance)
3. Type `/b d e 100` (deposit 100 event tokens)

EXPECTED RESULTS:
- Only specified amount deposited
- Appropriate success messages
- Balances update correctly

TEST STEPS - Withdraw:
1. Type `/b w p 1000` (withdraw 1000 pyreals)
2. Type `/b w l 500` (withdraw 500 luminance)
3. Type `/b w e 100` (withdraw 100 event tokens)

EXPECTED RESULTS:
- Items appear in inventory
- Bank balance decreases
- MMDs created when withdrawing >250,000 pyreals in batches

TEST STEPS - Transfer:
1. Create second character "TestChar2"
2. On first character: `/b t p 1000 TestChar2`
3. On first character: `/b t l 500 TestChar2`
4. On first character: `/b t e 100 TestChar2`

EXPECTED RESULTS:
- Sender sees: "Transferred [amount] [currency] to TestChar2"
- Recipient's bank balance increases
- Works for both online and offline recipients

EDGE CASES:
[ ] Deposit with no items
[ ] Withdraw more than banked amount
[ ] Withdraw while inventory full
[ ] Transfer to non-existent character
[ ] Transfer while busy/teleporting
[ ] Transfer negative amounts
[ ] Transfer 0 amount
[ ] Transfer to character with spaces in name (use quotes)

PASS CRITERIA:
✓ All deposit/withdraw operations work correctly
✓ Transfers work for online and offline players
✓ Error messages appropriate
✓ Balances accurate in database
✓ Conquest Coins and Soul Fragments show in balance but cannot transfer

-------------------------------------------
TEST 1.5: /pk Command - PK Flagging
-------------------------------------------
✅ Can Test Now

PREREQUISITES:
- NPK (non-PK) character
- Second character for PvP testing

TEST STEPS - Check Status:
1. Type `/pk` (no parameters)

EXPECTED RESULTS:
- Shows "Your PK status is currently OFF. Use '/pk on' or '/pk off' to change."

TEST STEPS - Turn PK On:
1. Type `/pk on`
2. Note the warning message
3. Type `/pk on confirm`

EXPECTED RESULTS:
1. Warning message:
   - "=== WARNING ==="
   - "Enabling PK status will allow other PK players to attack you anywhere in the world!"
   - "Type '/pk on confirm' to proceed."
2. Success message: "You are now a Player Killer! Other PK players can attack you anywhere."
3. Server broadcast: "[YourName] is now a Player Killer!"
4. PK status visible to other players

TEST STEPS - Attempt to Turn PK On Again:
1. Type `/pk on`

EXPECTED RESULTS:
- Message: "You are already a player killer."

TEST STEPS - Turn PK Off Too Soon:
1. Immediately after turning PK on, type `/pk off`

EXPECTED RESULTS:
- Message: "You must remain PK for at least 5 minutes after flagging. [X] minute(s) remaining."

TEST STEPS - Turn PK Off After 5 Minutes:
1. Wait 5+ minutes
2. Type `/pk off`

EXPECTED RESULTS:
- Message: "You are no longer a Player Killer. You are now safe from PvP attacks."
- PK status removed

TEST STEPS - PK Death Cooldown:
1. Turn PK on
2. Have another PK player kill you
3. Wait for respawn
4. Type `/pk on`

EXPECTED RESULTS:
- Message: "You must wait [X] more minute(s) before you can flag PK again after dying in PvP combat."
- Must wait 20 minutes after PK vs PK death

TEST STEPS - PK Timer Active:
1. Turn PK on
2. Attack another PK player (don't kill, just engage combat)
3. Immediately type `/pk off`

EXPECTED RESULTS:
- Message: "You cannot disable PK status while your PK timer is active (lasts 20 seconds after PK combat)."

EDGE CASES:
[ ] Try `/pk off` while in PK-only dungeon (needs content setup)
[ ] Try `/pk on` while already PK
[ ] Try `/pk off` while not PK
[ ] Try `/pk invalidparam`

PASS CRITERIA:
✓ 5-minute minimum duration enforced
✓ 20-minute death cooldown enforced
✓ PK timer prevents immediate unflagging
✓ Confirmation required for `/pk on`
✓ Server broadcasts work
✓ LastPKFlagTime and LastPKDeathTime properties set correctly

================================================================================
2. ENLIGHTENMENT (ENL) SYSTEM
================================================================================

-------------------------------------------
TEST 2.1: ENL Cap at 100
-------------------------------------------
✅ Can Test Now

PREREQUISITES:
- Admin access to set Enlightenment value

TEST STEPS:
1. Use admin command to set Enlightenment to 100
2. Verify in stats panel
3. Use admin command to set Enlightenment to 150

EXPECTED RESULTS:
- Setting to 100 works
- Setting to 150 should cap at 100

VERIFICATION:
- Check PropertyInt.Enlightenment = 390 in database
- Value should never exceed 100

PASS CRITERIA:
✓ Cannot exceed 100 ENL
✓ Database enforces cap

-------------------------------------------
TEST 2.2: ENL XP Bonus (+1% per ENL)
-------------------------------------------
✅ Can Test Now

PREREQUISITES:
- Character with 0 ENL
- Character with 50 ENL
- Character with 100 ENL
- Mob to kill for XP testing

TEST STEPS:
1. Record base XP from killing identical mob at 0 ENL
2. Set ENL to 50, kill same mob, record XP
3. Set ENL to 100, kill same mob, record XP

EXPECTED RESULTS:
- 0 ENL: Base XP (e.g., 1,000 XP)
- 50 ENL: Base XP × 1.5 (1,500 XP)
- 100 ENL: Base XP × 2.0 (2,000 XP)

FORMULA:
- XP Modifier = 1.0 + (ENL * 0.01)

PASS CRITERIA:
✓ XP scales linearly with ENL
✓ 100 ENL = double XP

-------------------------------------------
TEST 2.3: ENL Attribute Bonus (+1 per ENL)
-------------------------------------------
✅ Can Test Now

PREREQUISITES:
- Character with known base attributes
- Admin access to modify ENL

TEST STEPS:
1. Record all 6 base attributes (STR, END, COORD, QUICK, FOCUS, SELF) at 0 ENL
2. Set ENL to 50
3. Record all 6 attributes again
4. Set ENL to 100
5. Record all 6 attributes again

EXPECTED RESULTS:
- Each ENL point adds +1 to ALL 6 attributes
- 50 ENL: All attributes +50
- 100 ENL: All attributes +100

VERIFICATION:
- Check character stats panel
- Verify all 6 attributes increase equally

PASS CRITERIA:
✓ All 6 attributes increase by ENL amount
✓ Bonus applies to current stats (base + buffs + ENL)

-------------------------------------------
TEST 2.4: ENL Rating Bonus (+1 DR/DDR per 25 ENL)
-------------------------------------------
✅ Can Test Now

PREREQUISITES:
- Character with 0 ENL
- Access to damage rating stats

TEST STEPS:
1. Record Damage Rating and Damage Resistance Rating at 0 ENL
2. Set ENL to 25, record ratings
3. Set ENL to 50, record ratings
4. Set ENL to 75, record ratings
5. Set ENL to 100, record ratings

EXPECTED RESULTS:
- 0 ENL: Base ratings
- 25 ENL: Base + 1 DR, Base + 1 DRR
- 50 ENL: Base + 2 DR, Base + 2 DRR
- 75 ENL: Base + 3 DR, Base + 3 DRR
- 100 ENL: Base + 4 DR, Base + 4 DRR

FORMULA:
- Rating Bonus = ENL / 25 (integer division)

PASS CRITERIA:
✓ Every 25 ENL adds +1 to both DR and DRR
✓ Partial increments don't add rating (24 ENL = 0 bonus)

-------------------------------------------
TEST 2.5: ENL Leaderboard
-------------------------------------------
✅ Can Test Now (if stored procedure exists)

TEST STEPS:
1. Create characters with different ENL values
2. Type `/top enl`

EXPECTED RESULTS:
- Shows "Top 50 Players by Enlightenment:"
- Ranked highest to lowest ENL
- Proper formatting

PASS CRITERIA:
✓ Accurate rankings
✓ Updates when ENL changes

================================================================================
3. QUEST BONUS (QB) SYSTEM
================================================================================

-------------------------------------------
TEST 3.1: Quest Completion Tracking
-------------------------------------------
✅ Can Test Now

PREREQUISITES:
- Access to quest system
- Multiple unique quests to complete

TEST STEPS:
1. Check initial QB count with `/qb`
2. Complete a quest for the first time
3. Check QB count again
4. Repeat the same quest
5. Check QB count again

EXPECTED RESULTS:
1. Initial count displayed
2. QB count increases by 1
3. Message: "You've stamped [QuestName]!" (first completion)
4. QB count does NOT increase (repeat doesn't count)
5. No stamp message on repeat

VERIFICATION:
- Check PropertyInt64.QuestCount = 9027 in database
- Check account_quest table for quest entries

EDGE CASES:
[ ] Complete quest on alt character (same account)
[ ] Complete same quest after server restart
[ ] Quest with multiple solves (should only stamp once)

PASS CRITERIA:
✓ Only first completion counts toward QB
✓ Repeats don't increment QB
✓ Account-wide tracking works
✓ Stamp message appears on first completion

-------------------------------------------
TEST 3.2: QB XP Bonus Calculation
-------------------------------------------
✅ Can Test Now

PREREQUISITES:
- Character with known QB count
- Mob for XP testing

TEST STEPS:
1. Set QB to 0, kill mob, record XP
2. Set QB to 1000, kill same mob, record XP
3. Set QB to 5000, kill same mob, record XP

EXPECTED RESULTS:
- 0 QB: Base XP
- 1,000 QB: Base XP × 1.10 (+10%)
- 5,000 QB: Base XP × 1.50 (+50%)

FORMULA:
- XP Modifier = 1.0 + (QB * 0.0001)

STACKING:
- QB bonus stacks multiplicatively with ENL bonus
- Example: 100 ENL + 5000 QB = Base × 2.0 × 1.5 = 3.0x total

PASS CRITERIA:
✓ XP scales correctly with QB count
✓ Stacks with ENL bonus

-------------------------------------------
TEST 3.3: QB Leaderboard
-------------------------------------------
✅ Can Test Now (if stored procedure exists)

TEST STEPS:
1. Create characters with different QB counts
2. Type `/top qb`

EXPECTED RESULTS:
- Shows "Top 50 Players by Quest Bonus:"
- Ranked highest to lowest QB
- Proper formatting

PASS CRITERIA:
✓ Accurate rankings
✓ Updates when QB changes

-------------------------------------------
TEST 3.4: Mule Character Exclusion
-------------------------------------------
✅ Can Test Now

PREREQUISITES:
- Character flagged as mule (IsMule = true)

TEST STEPS:
1. Set character IsMule = true in database
2. Complete a quest

EXPECTED RESULTS:
- Quest completes normally
- QB count does NOT increase
- No stamp message

PASS CRITERIA:
✓ Mules don't earn QB
✓ No error messages

================================================================================
4. BANKING SYSTEM
================================================================================

-------------------------------------------
TEST 4.1: Pyreals Banking
-------------------------------------------
✅ Can Test Now

See Test 1.4 for detailed banking tests.

ADDITIONAL TESTS:
[ ] Deposit pyreals when BankedPyreals is null (first use)
[ ] Withdraw creates MMDs for amounts >250k
[ ] Withdraw creates proper stacks (max 25k per pyreal stack)
[ ] Transfer logs to transfer_log table
[ ] Concurrent deposits (thread safety)

-------------------------------------------
TEST 4.2: Luminance Banking
-------------------------------------------
✅ Can Test Now

TEST STEPS:
1. Gain some luminance
2. `/b d l` to deposit all
3. `/b w l 500` to withdraw
4. Verify cannot exceed MaximumLuminance

EXPECTED RESULTS:
- Luminance transfers correctly
- Cannot withdraw if it would exceed max luminance

PASS CRITERIA:
✓ Deposits work
✓ Withdrawals work
✓ Max luminance enforced

-------------------------------------------
TEST 4.3: Event Tokens (Dragon Coins)
-------------------------------------------
❌ Cannot Test (requires WCID 13370002 weenie creation)

Once weenie exists:
[ ] Deposit Dragon Coins
[ ] Withdraw Dragon Coins (max stack 25,000)
[ ] Transfer Dragon Coins to another player
[ ] Verify transfer logs

-------------------------------------------
TEST 4.4: Conquest Coins (Non-Transferable)
-------------------------------------------
⚠️ Partial Test (property exists, no acquisition method yet)

TEST STEPS:
1. Use admin command to set ConquestCoins value
2. Type `/b b` to view balance
3. Attempt to transfer conquest coins

EXPECTED RESULTS:
- Balance displays correctly
- Transfer should fail or not be available

-------------------------------------------
TEST 4.5: Soul Fragments (Non-Transferable)
-------------------------------------------
⚠️ Partial Test (property exists, no acquisition method yet)

TEST STEPS:
1. Use admin command to set SoulFragments value
2. Type `/b b` to view balance
3. Attempt to transfer soul fragments

EXPECTED RESULTS:
- Balance displays correctly
- Transfer should fail or not be available

================================================================================
5. PK SYSTEM & SOUL FRAGMENTS
================================================================================

-------------------------------------------
TEST 5.1: PK Flagging (Already covered in 1.5)
-------------------------------------------
✅ See Test 1.5

-------------------------------------------
TEST 5.2: No Item Loss on PK Death
-------------------------------------------
✅ Can Test Now

PREREQUISITES:
- Two PK characters
- Items in inventory

TEST STEPS:
1. Turn both characters PK
2. Character A fills inventory with items
3. Character B kills Character A
4. Check Character A's corpse

EXPECTED RESULTS:
- Corpse contains NO items
- All items remain in Character A's inventory after death
- Normal death penalties apply (vitae, etc.)

COMPARISON:
- NPK death to mob: Items drop according to level
- PK death to player: NO items drop

PASS CRITERIA:
✓ Zero items drop on PK death
✓ Items remain in inventory
✓ Corpse is empty

-------------------------------------------
TEST 5.3: Soul Fragment Drops on PK Death
-------------------------------------------
❌ Cannot Test (requires WCID 999999998 Soul Fragment weenie)

Once weenie exists:
[ ] PK vs PK death drops 1-3 Soul Fragments
[ ] Fragments go to killer's inventory
[ ] 6-8 hour cooldown enforced (random)
[ ] Second PK death within cooldown: no fragments
[ ] Check PropertyInt64.LastSoulFragmentLootTime

-------------------------------------------
TEST 5.4: PK Dungeon Soul Fragment Drops
-------------------------------------------
❌ Cannot Test (requires PK dungeon setup + weenie)

Once PK dungeons configured:
[ ] 0.75% drop chance per mob kill in PK dungeon
[ ] Daily cap of 20 Soul Fragments
[ ] Message: "You found a Soul Fragment! (X remaining today)"
[ ] Daily reset after 24 hours
[ ] PropertyInt64.DailySoulFragmentCount
[ ] PropertyInt64.LastSoulFragmentResetTime

-------------------------------------------
TEST 5.5: PK Dungeon +10% XP/Lum Bonus
-------------------------------------------
⚠️ Cannot Test (requires PK dungeon landblock configuration)

Once pkDungeonLandblocks populated:
[ ] NPK player: normal XP/lum
[ ] PK player in PK dungeon: +10% XP/lum
[ ] PK player outside PK dungeon: normal XP/lum

-------------------------------------------
TEST 5.6: PK Dungeon Enforcement
-------------------------------------------
⚠️ Cannot Test (requires PK dungeon configuration)

Once configured:
[ ] NPK player enters PK dungeon variant
[ ] Player teleported to lifestone after ~5 seconds
[ ] Message explains PK requirement
[ ] PK players can enter freely
[ ] Admin characters bypass enforcement

-------------------------------------------
TEST 5.7: PK Dungeon Re-Entry Cooldown
-------------------------------------------
⚠️ Cannot Test (requires PK dungeon configuration)

Once configured:
[ ] PK player dies to another PK player in dungeon
[ ] Cooldown starts (20 minutes)
[ ] Attempting re-entry teleports to lifestone
[ ] Message: "You cannot re-enter this dungeon for X minutes and Y seconds after your death."
[ ] Can enter OTHER PK dungeons immediately
[ ] Death to mob: no cooldown
[ ] PropertyInt64.LastPKDungeonDeathLocation
[ ] PropertyInt64.LastPKDungeonDeathTime

================================================================================
6. MARKETPLACE & CHARACTER LIMITS
================================================================================

-------------------------------------------
TEST 6.1: Multiple Characters in Marketplace
-------------------------------------------
✅ Can Test Now

PREREQUISITES:
- Multiple accounts or VM with different IPs
- OR single IP (to test same-IP behavior)

TEST STEPS:
1. Login Character A to Marketplace (Landblock 0x016C)
2. Login Character B (same IP) to Marketplace
3. Login Character C (same IP) to Marketplace

EXPECTED RESULTS:
- All characters can coexist in Marketplace
- No boot messages
- Unlimited characters allowed

PASS CRITERIA:
✓ Unlimited characters in Marketplace
✓ Works from same IP
✓ No connection limits

-------------------------------------------
TEST 6.2: Single Character Outside Marketplace
-------------------------------------------
✅ Can Test Now

PREREQUISITES:
- Two characters from same IP

TEST STEPS:
1. Login Character A, portal OUT of Marketplace to town
2. Login Character B, portal OUT of Marketplace to different town
3. Wait a few seconds

EXPECTED RESULTS:
- One character gets booted
- Message: "Only 1 character per IP allowed outside Marketplace. Please return to Marketplace to switch characters."
- Teleport back to lifestone or disconnect

VERIFICATION:
- Check Config.js: MaximumCharactersOutsideMarketplace = 1

EDGE CASES:
[ ] Both characters in apartments (should work - exempt landblock)
[ ] One in Marketplace, one outside (should work)
[ ] Both outside Marketplace (one gets booted)
[ ] Admin character bypasses limit

PASS CRITERIA:
✓ Only 1 character per IP outside exempt areas
✓ Unlimited in Marketplace
✓ Boot message displayed
✓ Admin characters exempt

-------------------------------------------
TEST 6.3: Apartment Exemption
-------------------------------------------
✅ Can Test Now

TEST STEPS:
1. Login Character A to apartment landblock
2. Login Character B to different apartment
3. Both should stay connected

EXPECTED RESULTS:
- Both characters remain connected
- Apartments are exempt landblocks

VERIFICATION:
- Apartment landblock ranges: 0x7200-0x9900, 0x5360-0x5369

PASS CRITERIA:
✓ Multiple characters in apartments works
✓ No boot messages

================================================================================
7. TINKERING SYSTEM
================================================================================

-------------------------------------------
TEST 7.1: 20x Tinkering Support
-------------------------------------------
✅ Can Test Now

PREREQUISITES:
- Item to tinker
- Salvage materials
- High enough tinkering skill

TEST STEPS:
1. Tinker an item to 1x
2. Continue tinkering to 10x
3. Continue tinkering to 11x
4. Continue tinkering to 20x
5. Attempt 21st tink

EXPECTED RESULTS:
- Tinks 1-10: Standard difficulty progression
- Tinks 11-20: Increased difficulty (+0.5 per attempt)
- Tink 20: Difficulty multiplier = 9.5
- Tink 21: Should fail (not supported)

VERIFICATION:
- Check RecipeManager.cs TinkeringDifficulty list
- Verify 20 entries exist

PASS CRITERIA:
✓ Can tinker to 20x
✓ Cannot tinker beyond 20x
✓ Difficulty increases correctly

================================================================================
8. VPN DETECTION SYSTEM
================================================================================

-------------------------------------------
TEST 8.1: VPN Detection on Login
-------------------------------------------
⚠️ Requires proxycheck.io API key

PREREQUISITES:
- proxycheck_api_key configured in Config.js
- VPN connection active

TEST STEPS:
1. Connect via VPN
2. Attempt to login

EXPECTED RESULTS:
- Login blocked
- Message about VPN usage not allowed
- IP added to blocklist cache

VERIFICATION:
- Check server logs for VPN detection
- Verify IP in blocklist cache

-------------------------------------------
TEST 8.2: VPN Blocklist Management
-------------------------------------------
🔧 Admin Only

TEST STEPS:
1. Admin: `/clearvpnblocklist`
2. Admin: `/removeipfromvpnblocklist [IP]`

EXPECTED RESULTS:
- Blocklist cleared or specific IP removed
- Confirmation messages

PASS CRITERIA:
✓ Commands execute without errors
✓ IPs can be managed

================================================================================
9. LEADERBOARD CACHING
================================================================================

-------------------------------------------
TEST 9.1: Leaderboard Cache Refresh
-------------------------------------------
✅ Can Test Now

TEST STEPS:
1. Type `/top qb` and note timestamp
2. Wait 15+ minutes
3. Type `/top qb` again
4. Check server logs for cache refresh

EXPECTED RESULTS:
- Data served from cache (fast response)
- After 15 minutes + variance: cache refreshes
- New data loaded from database

VERIFICATION:
- Check LeaderboardExtensions.cs for cache expiry logic
- Verify 15-minute base + 15-120 second variance

PASS CRITERIA:
✓ Cache serves data quickly
✓ Auto-refresh after expiry
✓ No database thrashing

================================================================================
10. AUGMENTATION GEM SYSTEM
================================================================================

-------------------------------------------
TEST 10.1: Augmentation Gem Usage
-------------------------------------------
❌ Cannot Test (requires augmentation gem weenies)

Once gems created:
[ ] Purchase +1 gem from vendor (costs Conquest Coins)
[ ] Use gem on character (costs Luminance)
[ ] Verify augmentation increases
[ ] Repeat with +5 gem
[ ] Check luminance consumption rates

Expected consumption (from EmoteManager_AugGems.cs):
- Cost = (current level + increment) ^ 2 * 100
- Example: +1 gem at level 0 = (0 + 1)^2 * 100 = 100 lum
- Example: +5 gem at level 10 = (10 + 5)^2 * 100 = 22,500 lum

================================================================================
11. NETHER WEAPONS
================================================================================

-------------------------------------------
TEST 11.1: Nether Weapons in Loot Tables
-------------------------------------------
❌ Cannot Test (requires nether weapon weenies WCID 999900000-999900089)

Once weenies created:
[ ] Kill mobs in loot tier zones
[ ] Verify nether weapons drop
[ ] Check drop rates: 13% (same as elemental variants)
[ ] Test all weapon types:
    - 24 Heavy Weapons
    - 19 Light Weapons
    - 22 Finesse Weapons
    - 12 Two-Handed Weapons
    - 3 Crossbows
    - 2 Atlatls
    - 7 Bows (heritage-specific)

[ ] Verify damage type is Nether (1024)
[ ] Verify "Corrupted [name]" naming

================================================================================
12. EDGE CASES & STRESS TESTING
================================================================================

-------------------------------------------
TEST 12.1: Concurrent Banking Operations
-------------------------------------------
✅ Can Test Now

TEST STEPS:
1. Login two characters on same account
2. Simultaneously deposit/withdraw from bank
3. Verify no race conditions

EXPECTED RESULTS:
- Operations serialized correctly
- No duplicate deposits/withdrawals
- Balances accurate

-------------------------------------------
TEST 12.2: Property Boundary Values
-------------------------------------------
✅ Can Test Now

TEST STEPS:
1. Set Enlightenment to 99, 100, 101
2. Set negative values for currencies
3. Set extremely large values

EXPECTED RESULTS:
- ENL capped at 100
- Negative values handled gracefully
- No overflows

-------------------------------------------
TEST 12.3: Database Connectivity Loss
-------------------------------------------
✅ Can Test Now

TEST STEPS:
1. Stop database server
2. Attempt banking operations
3. Attempt leaderboard queries

EXPECTED RESULTS:
- Graceful error handling
- No server crashes
- Error messages to player

================================================================================
13. COMMANDS SUMMARY CHECKLIST
================================================================================

Player Commands Implemented:
[ ] /qb - Quest Bonus display
[ ] /bonus - Quest Bonus display (alias)
[ ] /aug - Augmentation levels display
[ ] /augs - Augmentation levels display (alias)
[ ] /top qb - Quest Bonus leaderboard
[ ] /top level - Level leaderboard
[ ] /top enl - Enlightenment leaderboard
[ ] /top bank - Banked pyreals leaderboard
[ ] /top lum - Banked luminance leaderboard
[ ] /bank or /b - Banking commands
  [ ] /b b - Balance
  [ ] /b d - Deposit all
  [ ] /b d p [amt] - Deposit pyreals
  [ ] /b d l [amt] - Deposit luminance
  [ ] /b d e [amt] - Deposit event tokens
  [ ] /b w p [amt] - Withdraw pyreals
  [ ] /b w l [amt] - Withdraw luminance
  [ ] /b w e [amt] - Withdraw event tokens
  [ ] /b t p [amt] [name] - Transfer pyreals
  [ ] /b t l [amt] [name] - Transfer luminance
  [ ] /b t e [amt] [name] - Transfer event tokens
[ ] /pk - PK status toggle
  [ ] /pk (no params) - Check status
  [ ] /pk on - Enable PK (requires confirm)
  [ ] /pk on confirm - Confirm PK enable
  [ ] /pk off - Disable PK

================================================================================
14. TESTING PRIORITY MATRIX
================================================================================

HIGH PRIORITY (Test Immediately):
1. All player commands (/qb, /aug, /bank, /top, /pk)
2. ENL system (cap, bonuses)
3. QB system (tracking, bonuses)
4. Banking (deposit, withdraw, transfer)
5. PK flagging (restrictions, timers)
6. Character limits (marketplace exemption)

MEDIUM PRIORITY (Test After Setup):
1. Leaderboards (requires stored procedures)
2. Tinkering to 20x
3. No item loss on PK death

LOW PRIORITY (Content Work Required):
1. Soul Fragment drops
2. PK dungeon bonuses
3. Nether weapons
4. Augmentation gems
5. VPN detection

CANNOT TEST (Weenies Not Created):
1. Event Tokens (Dragon Coins WCID 13370002)
2. Soul Fragments (WCID 999999998)
3. Nether Weapons (WCIDs 999900000-999900089)
4. Augmentation Gems (+1 and +5 variants)

================================================================================
15. BUG REPORTING TEMPLATE
================================================================================

When reporting issues, include:

ISSUE TITLE: [Brief description]

SEVERITY:
[ ] Critical - Server crash or data loss
[ ] High - Feature broken, impacts gameplay
[ ] Medium - Feature partially working
[ ] Low - Minor issue, cosmetic

STEPS TO REPRODUCE:
1.
2.
3.

EXPECTED RESULT:


ACTUAL RESULT:


ENVIRONMENT:
- Server Version:
- Database Version:
- Character Level:
- Admin/Player Account:

ADDITIONAL INFO:
- Server logs:
- Error messages:
- Screenshots:

================================================================================
16. DISCORD INTEGRATION
================================================================================

-------------------------------------------
TEST 16.1: Discord Chat Relay (Game → Discord)
-------------------------------------------
✅ Can Test Now

PREREQUISITES:
- Discord bot configured and connected
- Access to Discord server channels
- GeneralChannelId and TradeChannelId configured in Config.js

TEST STEPS:
1. Type `/cg Hello from game!` in-game
2. Type `/trade Trading test!` in-game
3. Check Discord General channel for first message
4. Check Discord Trade channel for second message

EXPECTED RESULTS:
- `/cg` messages appear in Discord General channel
- `/trade` messages appear in Discord Trade channel
- Player name is shown before the message
- Message format: "PlayerName : Message"

PASS CRITERIA:
✓ Messages appear in correct Discord channels
✓ Player name is displayed correctly
✓ Message content is accurate
✓ No errors in server logs

-------------------------------------------
TEST 16.2: Discord Chat Relay (Discord → Game)
-------------------------------------------
✅ Can Test Now

PREREQUISITES:
- Discord bot connected
- In-game character logged in

TEST STEPS:
1. Send message in Discord General channel
2. Send message in Discord Trade channel
3. Verify messages appear in-game

EXPECTED RESULTS:
- Discord General messages appear in-game General chat
- Discord Trade messages appear in-game Trade chat
- Discord username is shown
- Bot messages are ignored

PASS CRITERIA:
✓ Bi-directional communication works
✓ Messages sync in real-time
✓ Discord nicknames display correctly

-------------------------------------------
TEST 16.3: Admin Audit Channel
-------------------------------------------
🔧 Admin Tools Required

PREREQUISITES:
- Admin account
- AdminAuditId configured
- Access to Discord admin audit channel

TEST STEPS:
1. Use `@world open` command
2. Use `@world close` command
3. Use `@export-discord 1` command
4. Use `@import-discord test` command
5. Check Discord Admin Audit channel

EXPECTED RESULTS:
- "World is now open" appears in audit channel
- "World is now closed" appears in audit channel
- "Exported weenie X to Discord" appears
- "Imported 'test' from Discord" appears
- System or admin name shown

PASS CRITERIA:
✓ All admin actions logged to audit channel
✓ Correct attribution (admin name or System)

-------------------------------------------
TEST 16.4: Server Events Channel
-------------------------------------------
🔧 Admin Tools Required

PREREQUISITES:
- EventsChannelId configured
- Admin account

TEST STEPS:
1. Use `@world open`
2. Use `@world close`
3. Use `@world close boot`
4. Use `@shutdown 5m`
5. Check Discord Events channel

EXPECTED RESULTS:
- World open: "🌍 **World is now OPEN** - Players can now enter the world!"
- World close: "🔒 **World is now CLOSED** - No new players can enter."
- World close boot: "🔒 **World is now CLOSED** - All players have been booted."
- Shutdown 5m: "📢 **ATTENTION** - Server will shut down in 5 minutes."
- Shutdown 1m: "⚠️ **WARNING** - Server shutting down in X seconds! Please log out!"
- Shutdown now: "🔴 **SERVER SHUTTING DOWN NOW!**"

PASS CRITERIA:
✓ All events appear in Events channel
✓ Correct emoji and formatting
✓ Timing messages accurate

-------------------------------------------
TEST 16.5: Export to Discord
-------------------------------------------
🔧 Admin Tools Required

PREREQUISITES:
- ExportsChannelId configured
- Admin account
- Existing weenie/recipe/quest/spell in database

TEST STEPS:
1. Use `@export-discord 1` (weenie)
2. Use `@export-discord 1 recipe` (recipe)
3. Use `@export-discord questname quest`
4. Use `@export-discord 1 spell`
5. Use `@export-discord A9B4 landblock`
6. Check Discord Exports channel

EXPECTED RESULTS:
- SQL file attached to Discord message
- Filename matches ID (e.g., "1.sql", "A9B4.sql")
- Player name shown in message
- Export message: "Exported weenie 1"
- File contains DELETE and INSERT statements

PASS CRITERIA:
✓ SQL files upload successfully
✓ Correct filename and content
✓ All content types work (weenie, recipe, quest, spell, landblock)
✓ Audit log shows export action

-------------------------------------------
TEST 16.6: Import from Discord
-------------------------------------------
🔧 Admin Tools Required

PREREQUISITES:
- WeenieUploadsChannelId configured
- SQL file uploaded to Discord WeenieUploads channel
- Admin account

TEST STEPS:
1. Upload SQL file to Discord WeenieUploads channel (filename: "test.sql")
2. Use `@import-discord test` in-game
3. Verify import success message
4. Check database for imported content

EXPECTED RESULTS:
- Command finds SQL file in Discord channel
- Imports SQL to database
- Shows "Imported 'test' from Discord."
- Audit log shows import action
- Database contains new content

PASS CRITERIA:
✓ Finds SQL files by identifier
✓ Executes SQL successfully
✓ Audit log created
✓ Error handling if file not found

-------------------------------------------
TEST 16.7: Clothing Export/Import
-------------------------------------------
🔧 Admin Tools Required (Optional - if clothing modding used)

PREREQUISITES:
- ClothingBase ID known (0x10000000 - 0x10FFFFFF range)
- ExportsChannelId configured

TEST STEPS:
1. Use `@export-discord-clothing 268435456` (0x10000000 in decimal)
2. Check Discord Exports channel for JSON file
3. Upload modified JSON to Discord
4. Use `@import-discord-clothing filename`

EXPECTED RESULTS:
- Export: JSON file uploaded with ClothingBase data
- Import: JSON saved to ModsDirectory/Clothing folder
- File format: {ClothingBaseId}.json

PASS CRITERIA:
✓ ClothingBase exported as JSON
✓ JSON import saves to correct folder

-------------------------------------------
TEST 16.8: Error Handling
-------------------------------------------
✅ Can Test Now

TEST STEPS:
1. Try `@export-discord 999999999` (non-existent weenie)
2. Try `@import-discord nonexistent` (file not in Discord)
3. Send chat while Discord bot disconnected
4. Check server logs for graceful error handling

EXPECTED RESULTS:
- Export error: "Couldn't find weenie 999999999"
- Import error: "Couldn't find SQL attachment for 'nonexistent'"
- Chat errors: Warning logged, no crash
- Logs show connection state

PASS CRITERIA:
✓ No server crashes
✓ Clear error messages to user
✓ Logs errors appropriately

================================================================================
17. TEST COMPLETION CHECKLIST
================================================================================

Use this to track your testing progress:

COMMANDS:
[ ] /qb and /bonus
[ ] /aug and /augs
[ ] /top qb
[ ] /top level
[ ] /top enl
[ ] /top bank
[ ] /top lum
[ ] /bank balance
[ ] /bank deposit (all types)
[ ] /bank withdraw (all types)
[ ] /bank transfer (all types)
[ ] /pk (all variations)
[ ] /cg (game to Discord)
[ ] /trade (game to Discord)

ADMIN COMMANDS:
[ ] @export-discord (weenie)
[ ] @export-discord (recipe)
[ ] @export-discord (quest)
[ ] @export-discord (spell)
[ ] @export-discord (landblock)
[ ] @import-discord
[ ] @export-discord-clothing
[ ] @import-discord-clothing
[ ] @world open
[ ] @world close
[ ] @shutdown

SYSTEMS:
[ ] ENL cap at 100
[ ] ENL +1% XP per level
[ ] ENL +1 all stats per level
[ ] ENL +1 DR/DRR per 25 levels
[ ] QB XP bonus (0.01% per quest)
[ ] QB tracking (first completions only)
[ ] QB account-wide tracking
[ ] QB mule exclusion
[ ] Banking thread safety
[ ] Character limits (marketplace)
[ ] No item loss on PK death
[ ] PK 5-minute minimum duration
[ ] PK 20-minute death cooldown
[ ] PK timer prevents unflagging
[ ] Tinkering to 20x
[ ] Leaderboard caching

DISCORD INTEGRATION:
[ ] Game→Discord chat relay (General)
[ ] Game→Discord chat relay (Trade)
[ ] Discord→Game chat relay
[ ] Admin audit logging to Discord
[ ] World open/close events to Discord
[ ] Server shutdown events to Discord
[ ] SQL export to Discord (weenie/recipe/quest/spell/landblock)
[ ] SQL import from Discord
[ ] Clothing export/import
[ ] Discord connection error handling
[ ] Null checks prevent crashes

EDGE CASES:
[ ] Negative values handled
[ ] Null values handled
[ ] Boundary values (0, max)
[ ] Concurrent operations
[ ] Database disconnection
[ ] Invalid command parameters
[ ] Character name with spaces
[ ] Offline player transfers

================================================================================
END OF TESTING GUIDE
================================================================================

For questions or issues during testing, refer to:
- Conquest_Proposal_Review.txt (implementation details)
- Source code (C:\Users\xxtin\Documents\ACEMain\Source\)
- Database schema (authentication and shard databases)

Good luck with testing!
