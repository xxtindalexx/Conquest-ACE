using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        public TreasureDeath DeathTreasure { get => DeathTreasureType.HasValue ? DatabaseManager.World.GetCachedDeathTreasure(DeathTreasureType.Value) : null; }

        private bool onDeathEntered = false;

        /// <summary>
        /// Called when a monster or player dies, in conjunction with Die()
        /// </summary>
        /// <param name="lastDamager">The last damager that landed the death blow</param>
        /// <param name="damageType">The damage type for the death message</param>
        /// <param name="criticalHit">True if the death blow was a critical hit, generates a critical death message</param>
        public virtual DeathMessage OnDeath(DamageHistoryInfo lastDamager, DamageType damageType, bool criticalHit = false)
        {
            if (onDeathEntered)
                return GetDeathMessage(lastDamager, damageType, criticalHit);

            onDeathEntered = true;

            IsTurning = false;
            IsMoving = false;

            grappleLoopCTS?.Cancel();
            hotspotLoopCTS?.Cancel();

            // Reset fog to Clear upon death only if the creature was enraged
            if (IsEnraged && CurrentLandblock != null)
            {
                var fogResetType = EnvironChangeType.Clear;
                CurrentLandblock.SendEnvironChange(fogResetType);
            }

            //QuestManager.OnDeath(lastDamager?.TryGetAttacker());

            if (KillQuest != null)
                OnDeath_HandleKillTask(KillQuest);
            if (KillQuest2 != null)
                OnDeath_HandleKillTask(KillQuest2);
            if (KillQuest3 != null)
                OnDeath_HandleKillTask(KillQuest3);

            if (!IsOnNoDeathXPLandblock)
                OnDeath_GrantXP();

            return GetDeathMessage(lastDamager, damageType, criticalHit);
        }


        public DeathMessage GetDeathMessage(DamageHistoryInfo lastDamagerInfo, DamageType damageType, bool criticalHit = false)
        {
            var lastDamager = lastDamagerInfo?.TryGetAttacker();

            if (lastDamagerInfo == null || lastDamagerInfo.Guid == Guid || lastDamager is Hotspot || lastDamager is Food)   // !(lastDamager is Creature)?
                return Strings.General[1];

            var deathMessage = Strings.GetDeathMessage(damageType, criticalHit);

            // if killed by a player, send them a message
            if (lastDamagerInfo.IsPlayer)
            {
                if (criticalHit && this is Player)
                    deathMessage = Strings.PKCritical[0];

                var killerMsg = string.Format(deathMessage.Killer, Name);

                if (lastDamager is Player playerKiller)
                    playerKiller.Session.Network.EnqueueSend(new GameEventKillerNotification(playerKiller.Session, killerMsg));
            }
            return deathMessage;
        }

        /// <summary>
        /// Kills a player/creature and performs the full death sequence
        /// </summary>
        public void Die()
        {
            Die(DamageHistory.LastDamager, DamageHistory.TopDamager);
        }

        private bool dieEntered = false;

        /// <summary>
        /// Performs the full death sequence for non-Player creatures
        /// </summary>
        protected virtual void Die(DamageHistoryInfo lastDamager, DamageHistoryInfo topDamager)
        {
            if (dieEntered) return;

            dieEntered = true;

            UpdateVital(Health, 0);

            if (topDamager != null)
            {
                KillerId = topDamager.Guid.Full;

                if (topDamager.IsPlayer)
                {
                    var topDamagerPlayer = topDamager.TryGetAttacker();
                    if (topDamagerPlayer != null)
                        topDamagerPlayer.CreatureKills = (topDamagerPlayer.CreatureKills ?? 0) + 1;
                }
            }

            CurrentMotionState = new Motion(MotionStance.NonCombat, MotionCommand.Ready);
            //IsMonster = false;

            PhysicsObj.StopCompletely(true);

            // broadcast death animation
            var motionDeath = new Motion(MotionStance.NonCombat, MotionCommand.Dead);
            var deathAnimLength = ExecuteMotion(motionDeath);

            // Use topDamager for emotes (fellowship rolls should go to top damager, matching loot rights)
            EmoteManager.OnDeath(topDamager ?? lastDamager);

            var dieChain = new ActionChain();

            // wait for death animation to finish
            //var deathAnimLength = DatManager.PortalDat.ReadFromDat<MotionTable>(MotionTableId).GetAnimationLength(MotionCommand.Dead);
            dieChain.AddDelaySeconds(deathAnimLength);

            dieChain.AddAction(this, ActionType.CreatureDeath_MakeCorpse, () =>
            {
                CreateCorpse(topDamager);
                Destroy();
            });

            dieChain.EnqueueChain();
        }

        /// <summary>
        /// Called when an admin player uses the /smite command
        /// to instantly kill a creature
        /// </summary>
        public void Smite(WorldObject smiter, bool useTakeDamage = false)
        {
            if (useTakeDamage)
            {
                // deal remaining damage
                TakeDamage(smiter, DamageType.Bludgeon, Health.Current);
            }
            else
            {
                OnDeath();
                var smiterInfo = new DamageHistoryInfo(smiter);
                Die(smiterInfo, smiterInfo);
            }
        }

        public void OnDeath()
        {
            OnDeath(null, DamageType.Undef);
        }

        /// <summary>
        /// Grants XP to players in damage history
        /// </summary>
        public void OnDeath_GrantXP()
        {
            if (this is Player && PlayerKillerStatus == PlayerKillerStatus.PKLite)
                return;

            var totalHealth = DamageHistory.TotalHealth;

            if (totalHealth == 0)
                return;

            foreach (var kvp in DamageHistory.TotalDamage)
            {
                var damager = kvp.Value.TryGetAttacker();

                var playerDamager = damager as Player;

                if (playerDamager == null && kvp.Value.PetOwner != null)
                    playerDamager = kvp.Value.TryGetPetOwner();

                if (playerDamager == null)
                    continue;

                var totalDamage = kvp.Value.TotalDamage;

                var damagePercent = totalDamage / totalHealth;

                var totalXP = (XpOverride ?? 0) * damagePercent;

                playerDamager.EarnXP((long)Math.Round(totalXP), XpType.Kill);

                // handle luminance
                if (LuminanceAward != null)
                {
                    var totalLuminance = (long)Math.Round(LuminanceAward.Value * damagePercent);
                    playerDamager.EarnLuminance(totalLuminance, XpType.Kill);
                }
            }
        }

        /// <summary>
        /// Handles the KillTask for a killed creature
        /// </summary>
        public void OnDeath_HandleKillTask(string killQuest)
        {
            /*var receivers = KillTask_GetEligibleReceivers(killQuest);

            foreach (var receiver in receivers)
            {
                var damager = receiver.Value.TryGetAttacker();

                var player = damager as Player;

                if (player == null && receiver.Value.PetOwner != null)
                    player = receiver.Value.TryGetPetOwner();

                if (player != null)
                    player.QuestManager.HandleKillTask(killQuest, this);
            }*/

            // new method

            // with full fellowship support and new config option for capping,
            // building a pre-flattened structure is no longer really necessary,
            // and we can do this more iteratively.

            // one caveat to do this, we need to keep track of player and summoning caps separately
            // this is to prevent ordering bugs, such as a player being processed after a summon,
            // and already being at the 1 cap for players

            var summon_credit_cap = (int)PropertyManager.GetLong("summoning_killtask_multicredit_cap") - 1;

            var playerCredits = new Dictionary<ObjectGuid, int>();
            var summonCredits = new Dictionary<ObjectGuid, int>();

            // this option isn't really needed anymore, but keeping it around for compatibility
            // it is now synonymous with summoning_killtask_multicredit_cap <= 1
            if (!PropertyManager.GetBool("allow_summoning_killtask_multicredit"))
                summon_credit_cap = 0;

            foreach (var kvp in DamageHistory.TotalDamage)
            {
                if (kvp.Value.TotalDamage <= 0)
                    continue;

                var damager = kvp.Value.TryGetAttacker();

                var combatPet = false;

                var playerDamager = damager as Player;

                if (playerDamager == null && kvp.Value.PetOwner != null)
                {
                    playerDamager = kvp.Value.TryGetPetOwner();
                    combatPet = true;
                }

                if (playerDamager == null)
                    continue;

                var killTaskCredits = combatPet ? summonCredits : playerCredits;

                var cap = combatPet ? summon_credit_cap : 1;

                if (cap <= 0)
                {
                    // handle special case: use playerCredits
                    killTaskCredits = playerCredits;
                    cap = 1;
                }

                if (playerDamager.QuestManager.HasQuest(killQuest))
                {
                    TryHandleKillTask(playerDamager, killQuest, killTaskCredits, cap);
                }
                // check option that requires killer to have killtask to pass to fellows
                else if (!PropertyManager.GetBool("fellow_kt_killer"))   
                {
                    continue;
                }

                if (playerDamager.Fellowship == null)
                    continue;

                // share with fellows in kill task range
                var fellows = playerDamager.Fellowship.WithinRange(playerDamager);

                foreach (var fellow in fellows)
                {
                    if (fellow.QuestManager.HasQuest(killQuest))
                        TryHandleKillTask(fellow, killQuest, killTaskCredits, cap);
                }
            }
        }

        public bool TryHandleKillTask(Player player, string killTask, Dictionary<ObjectGuid, int> killTaskCredits, int cap)
        {
            if (killTaskCredits.TryGetValue(player.Guid, out var currentCredits))
            {
                if (currentCredits >= cap)
                    return false;

                killTaskCredits[player.Guid]++;
            }
            else
                killTaskCredits[player.Guid] = 1;

            player.QuestManager.HandleKillTask(killTask, this);

            return true;
        }

        /// <summary>
        /// Returns a flattened structure of eligible Players, Fellows, and CombatPets
        /// </summary>
        public Dictionary<ObjectGuid, DamageHistoryInfo> KillTask_GetEligibleReceivers(string killQuest)
        {
            // http://acpedia.org/wiki/Announcements_-_2012/12_-_A_Growing_Twilight#Release_Notes

            var questName = QuestManager.GetQuestName(killQuest);

            // we are using DamageHistoryInfo here, instead of Creature or WorldObjectInfo
            // WeakReference<CombatPet> may be null for expired CombatPets, but we still need the WeakReference<PetOwner> references

            var receivers = new Dictionary<ObjectGuid, DamageHistoryInfo>();

            foreach (var kvp in DamageHistory.TotalDamage)
            {
                if (kvp.Value.TotalDamage <= 0)
                    continue;

                var damager = kvp.Value.TryGetAttacker();

                var playerDamager = damager as Player;

                if (playerDamager == null && kvp.Value.PetOwner != null)
                {
                    // handle combat pets
                    playerDamager = kvp.Value.TryGetPetOwner();

                    if (playerDamager != null && playerDamager.QuestManager.HasQuest(questName))
                    {
                        // only add combat pet to eligible receivers if player has quest, and allow_summoning_killtask_multicredit = true (default, retail)
                        if (DamageHistory.HasDamager(playerDamager, true) && PropertyManager.GetBool("allow_summoning_killtask_multicredit"))
                            receivers[kvp.Value.Guid] = kvp.Value;  // add CombatPet
                        else
                            receivers[playerDamager.Guid] = new DamageHistoryInfo(playerDamager);   // add dummy profile for PetOwner
                    }

                    // regardless if combat pet is eligible, we still want to continue traversing to the pet owner, and possibly fellows

                    // in a scenario where combat pet does 100% damage:

                    // - regardless if allow_summoning_killtask_multicredit is enabled/disabled, it should continue traversing into pet owner and possibly their fellows

                    // - if pet owner doesn't have kill task, and fellow_kt_killer=false, any fellows with the task should still receive 1 credit
                }

                if (playerDamager == null)
                    continue;

                // factors:
                // - has quest
                // - is killer (last damager, top damager, or any damager? in current context, considering it to be any damager)
                // - has fellowship
                // - server option: fellow_kt_killer
                // - server option: fellow_kt_landblock

                if (playerDamager.QuestManager.HasQuest(questName))
                {
                    // just add a fake DamageHistoryInfo for reference
                    receivers[playerDamager.Guid] = new DamageHistoryInfo(playerDamager);
                }
                else if (PropertyManager.GetBool("fellow_kt_killer"))
                {
                    // if this option is enabled (retail default), the killer is required to have kill task
                    // for it to share with fellowship
                    continue;
                }

                // we want to add fellowship members in a flattened structure
                // in this inner loop, instead of the outer loop

                // scenarios:

                // i am a summoner in a fellowship with 1 other player
                // we both have a killtask

                // - my combatpet does 100% damage to the monster
                // result: i get 1 killtask credit, and my fellow gets 1 killtask credit

                // - my combatpet does 50% damage to monster, and i do 50% damage
                // result: i get 2 killtask credits (1 if allow_summoning_killtask_multicredit server option is disabled), and my fellow gets 1 killtask credit
                // after update should be 2/2, instead of 2/1

                // - my combatpet does 33% damage to monster, i do 33% damage, and fellow does 33% damage
                // result: same as previous scenario
                // after update should be 2/2, instead of 2/1 again

                // 2 players not in a fellowship both have a killtask
                // they each do 50% damage to monster

                // result: both players receive killtask credit

                if (playerDamager.Fellowship == null)
                    continue;

                // share with fellows in kill task range
                var fellows = playerDamager.Fellowship.WithinRange(playerDamager);

                foreach (var fellow in fellows)
                {
                    if (fellow.QuestManager.HasQuest(questName))
                        receivers[fellow.Guid] = new DamageHistoryInfo(fellow);
                }
            }
            return receivers;
        }

        /// <summary>
        /// Create a corpse for both creatures and players currently
        /// </summary>
        protected void CreateCorpse(DamageHistoryInfo killer, bool hadVitae = false)
        {
            if (NoCorpse)
            {
                if (killer != null && killer.IsOlthoiPlayer) return;

                var loot = GenerateTreasure(killer, null);

                foreach(var item in loot)
                {
                    if (!string.IsNullOrEmpty(item.Quest)) // if the item has a Quest string, make the creature a "generator" of the item so that the pickup action applies the quest. 
                        item.GeneratorId = Guid.Full; 
                    item.Location = new Position(Location);
                    LandblockManager.AddObject(item);
                }
                return;
            }

            var cachedWeenie = DatabaseManager.World.GetCachedWeenie("corpse");

            var corpse = WorldObjectFactory.CreateNewWorldObject(cachedWeenie) as Corpse;

            var prefix = "Corpse";

            if (TreasureCorpse)
            {
                // Hardcoded values from PCAPs of Treasure Pile Corpses, everything else lines up exactly with existing corpse weenie
                corpse.SetupTableId  = 0x02000EC4;
                corpse.MotionTableId = 0x0900019B;
                corpse.SoundTableId  = 0x200000C2;
                corpse.ObjScale      = 0.4f;

                prefix = "Treasure";
            }
            else
            {
                corpse.SetupTableId = SetupTableId;
                corpse.MotionTableId = MotionTableId;
                //corpse.SoundTableId = SoundTableId; // Do not change sound table for corpses
                corpse.PaletteBaseDID = PaletteBaseDID;
                corpse.ClothingBase = ClothingBase;
                corpse.PhysicsTableId = PhysicsTableId;

                if (ObjScale.HasValue)
                    corpse.ObjScale = ObjScale;
                if (PaletteTemplate.HasValue)
                    corpse.PaletteTemplate = PaletteTemplate;
                if (Shade.HasValue)
                    corpse.Shade = Shade;
                //if (Translucency.HasValue) // Shadows have Translucency but their corpses do not, videographic evidence can be found on YouTube.
                //corpse.Translucency = Translucency;


                // Pull and save objdesc for correct corpse apperance at time of death
                var objDesc = CalculateObjDesc();

                corpse.Biota.PropertiesAnimPart = objDesc.AnimPartChanges.Clone(corpse.BiotaDatabaseLock);

                corpse.Biota.PropertiesPalette = objDesc.SubPalettes.Clone(corpse.BiotaDatabaseLock);

                corpse.Biota.PropertiesTextureMap = objDesc.TextureChanges.Clone(corpse.BiotaDatabaseLock);
            }

            // use the physics location for accuracy,
            // especially while jumping
            corpse.Location = PhysicsObj.Position.ACEPosition();

            corpse.VictimId = Guid.Full;
            corpse.Name = $"{prefix} of {Name}";

            // set 'killed by' for looting rights
            var killerName = "misadventure";
            if (killer != null)
            {
                if (!(Generator != null && Generator.Guid == killer.Guid) && Guid != killer.Guid)
                {
                    if (!string.IsNullOrWhiteSpace(killer.Name))
                        killerName = killer.Name.TrimStart('+');  // vtank requires + to be stripped for regex matching.

                    corpse.KillerId = killer.Guid.Full;

                    if (killer.PetOwner != null)
                    {
                        var petOwner = killer.TryGetPetOwner();
                        if (petOwner != null)
                            corpse.KillerId = petOwner.Guid.Full;
                    }
                }
            }

            corpse.LongDesc = $"Killed by {killerName}.";

            bool saveCorpse = false;

            var player = this as Player;

            if (player != null)
            {
                corpse.SetPosition(PositionType.Location, corpse.Location);

                var killerIsOlthoiPlayer = killer != null && killer.IsOlthoiPlayer;
                var killerIsPkPlayer = killer != null && killer.IsPlayer && killer.Guid != Guid;

                //var dropped = killer != null && killer.IsOlthoiPlayer ? player.CalculateDeathItems_Olthoi(corpse, hadVitae) : player.CalculateDeathItems(corpse);

                if (killerIsOlthoiPlayer || player.IsOlthoiPlayer)
                {
                    var dropped = player.CalculateDeathItems_Olthoi(corpse, hadVitae, killerIsOlthoiPlayer, killerIsPkPlayer);

                    foreach (var wo in dropped)
                        DoCantripLogging(killer, wo);

                    corpse.RecalculateDecayTime(player);

                    if (dropped.Count > 0)
                        saveCorpse = true;

                    corpse.PkLevel = PKLevel.PK;
                }
                else
                {
                    var dropped = player.CalculateDeathItems(corpse);

                    corpse.RecalculateDecayTime(player);

                    if (dropped.Count > 0)
                        saveCorpse = true;

                    if ((player.Location.Cell & 0xFFFF) < 0x100)
                    {
                        player.SetPosition(PositionType.LastOutsideDeath, new Position(corpse.Location));
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePosition(player, PositionType.LastOutsideDeath, corpse.Location));

                        if (dropped.Count > 0)
                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your corpse is located at ({corpse.Location.GetMapCoordStr()}).", ChatMessageType.Broadcast));
                    }

                    var isPKdeath = player.IsPKDeath(killer);
                    var isPKLdeath = player.IsPKLiteDeath(killer);

                    if (isPKdeath)
                    {
                        corpse.PkLevel = PKLevel.PK;

                        // CONQUEST: Drop 1-3 Soul Fragments on PK death
                        // Victim must be level > 50 to drop soul fragments
                        // Check if killer can loot Soul Fragments (6-8 hour cooldown)
                        var killerPlayer = killer?.TryGetAttacker() as Player;
                        if (killerPlayer != null && (player.Level ?? 1) > 90)
                        {
                            // CONQUEST: Don't drop soul fragments if killer and victim are in same allegiance
                            if (killerPlayer.Allegiance != null && player.Allegiance != null &&
                                killerPlayer.Allegiance.MonarchId == player.Allegiance.MonarchId)
                            {
                                // Same allegiance - no soul fragment drops
                                // No message needed, silently skip
                            }
                            else
                            {
                                // CONQUEST: Per-victim cooldown system
                                // Each victim has their own 6-8 hour cooldown, so you can get trophies from
                                // multiple different players, but not from the same player twice within the cooldown
                                var currentTime = Time.GetUnixTime();
                                var cooldownHours = 6;  // Fixed 6 hour cooldown per victim
                                var cooldownSeconds = (uint)(cooldownHours * 3600);

                                // Use quest system to track per-victim cooldowns
                                // Quest name format: PKSoulLoot_<victimGuid>
                                var victimQuestName = $"PKSoulLoot_{player.Guid.Full:X8}";
                                var questEntry = killerPlayer.QuestManager.GetQuest(victimQuestName);

                                // Check if cooldown has passed for this specific victim
                                var lastLootTime = questEntry?.LastTimeCompleted ?? 0;
                                var cooldownExpired = currentTime >= lastLootTime + cooldownSeconds;

                                if (cooldownExpired)
                                {
                                    // No cooldown for this victim (or cooldown expired), drop Soul Fragments
                                    var soulFragmentCount = ThreadSafeRandom.Next(1, 3);  // 1-3 soul fragments
                                    var soulFragmentWeenieId = 13370003u;  // Soul Fragment weenie ID

                                    for (int i = 0; i < soulFragmentCount; i++)
                                    {
                                        var soulFragment = WorldObjectFactory.CreateNewWorldObject(soulFragmentWeenieId);
                                        if (soulFragment != null)
                                        {
                                            soulFragment.Location = new Position(corpse.Location);
                                            corpse.TryAddToInventory(soulFragment);
                                        }
                                    }

                                    // Stamp the quest to record this loot time
                                    killerPlayer.QuestManager.Update(victimQuestName);

                                    killerPlayer.Session.Network.EnqueueSend(new GameMessageSystemChat(
                                        $"You looted {soulFragmentCount} Soul Fragment{(soulFragmentCount > 1 ? "s" : "")} from {player.Name}!",
                                        ChatMessageType.Broadcast));
                                }
                                else
                                {
                                    // Still on cooldown for this specific victim
                                    var timeRemaining = (lastLootTime + cooldownSeconds) - currentTime;
                                    var hoursRemaining = Math.Ceiling(timeRemaining / 3600.0);
                                    killerPlayer.Session.Network.EnqueueSend(new GameMessageSystemChat(
                                        $"You cannot loot Soul Fragments from {player.Name} for another {hoursRemaining} hour(s).",
                                        ChatMessageType.Broadcast));
                                }
                            }
                        }
                    }

                    if (!isPKdeath && !isPKLdeath)
                    {
                        var miserAug = player.AugmentationLessDeathItemLoss * 5;
                        if (miserAug > 0)
                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your augmentation has reduced the number of items you can lose by {miserAug}!", ChatMessageType.Broadcast));
                    }

                    if (dropped.Count == 0 && !isPKLdeath)
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have retained all your items. You do not need to recover your corpse!", ChatMessageType.Broadcast));
                }
            }
            else
            {
                corpse.IsMonster = true;

                if (killer == null || !killer.IsOlthoiPlayer)
                    GenerateTreasure(killer, corpse);
                else
                    GenerateTreasure_Olthoi(killer, corpse);

                // CONQUEST: Soul Fragment drops in PK-only dungeon variants
                GenerateSoulFragments_PKDungeon(killer, corpse);

                if (killer != null && killer.IsPlayer && !killer.IsOlthoiPlayer)
                {
                    if (Level >= 100)
                    {
                        CanGenerateRare = true;
                    }
                    else
                    {
                        var killerPlayer = killer.TryGetAttacker();
                        if (killerPlayer != null && Level > killerPlayer.Level)
                            CanGenerateRare = true;
                    }
                }
                else
                    CanGenerateRare = false;
            }

            corpse.RemoveProperty(PropertyInt.Value);

            if (CanGenerateRare && killer != null)
                corpse.TryGenerateRare(killer);

            corpse.InitPhysicsObj(Location.Variation);

            // persist the original creature velocity (only used for falling) to corpse
            corpse.PhysicsObj.Velocity = PhysicsObj.Velocity;

            corpse.EnterWorld();

            if (player != null)
            {
                if (corpse.PhysicsObj == null || corpse.PhysicsObj.Position == null)
                    log.InfoFormat("[CORPSE] {0}'s corpse (0x{1}) failed to spawn! Tried at {2}", Name, corpse.Guid, player.Location.ToLOCString());
                else
                    log.InfoFormat("[CORPSE] {0}'s corpse (0x{1}) is located at {2}", Name, corpse.Guid, corpse.PhysicsObj.Position);
            }

            if (saveCorpse)
            {
                corpse.SaveBiotaToDatabase();

                foreach (var item in corpse.Inventory.Values)
                    item.SaveBiotaToDatabase();
            }
        }

        public bool CanGenerateRare
        {
            get => GetProperty(PropertyBool.CanGenerateRare) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.CanGenerateRare); else SetProperty(PropertyBool.CanGenerateRare, value); }
        }

        /// <summary>
        /// Transfers generated treasure from creature to corpse
        /// </summary>
        private List<WorldObject> GenerateTreasure(DamageHistoryInfo killer, Corpse corpse)
        {
            var droppedItems = new List<WorldObject>();

            // Treasure Map Drop Logic (adjust drop rate as needed: 0.01f = 1%, 0.10f = 10%)
            if (IsMonster && ThreadSafeRandom.Next(0.0f, 1.0f) < 0.01f)  // 1% chance
            {
                var map = TreasureMap.TryCreateTreasureMap(this);

                if (map != null)
                {
                    if (corpse != null)
                        corpse.TryAddToInventory(map);
                    else
                        droppedItems.Add(map);
                }
            }

            // CONQUEST: Mystery Egg Drop System
            TryGenerateMysteryEgg(killer);

            // create death treasure from loot generation factory
            if (DeathTreasure != null)
            {
                List<WorldObject> items = LootGenerationFactory.CreateRandomLootObjects(DeathTreasure);
                foreach (WorldObject wo in items)
                {
                    if (corpse != null)
                        corpse.TryAddToInventory(wo);
                    else
                        droppedItems.Add(wo);

                    DoCantripLogging(killer, wo);
                }
            }

            // move wielded treasure over, which also should include Wielded objects not marked for destroy on death.
            // allow server operators to configure this behavior due to errors in createlist post 16py data
            var dropFlags = PropertyManager.GetBool("creatures_drop_createlist_wield") ? DestinationType.WieldTreasure : DestinationType.Treasure;

            var wieldedTreasure = Inventory.Values.Concat(EquippedObjects.Values).Where(i => (i.DestinationType & dropFlags) != 0);
            foreach (var item in wieldedTreasure.ToList())
            {
                if (item.Bonded == BondedStatus.Destroy)
                    continue;

                if (TryDequipObjectWithBroadcasting(item.Guid, out var wo, out var wieldedLocation))
                    EnqueueBroadcast(new GameMessagePublicUpdateInstanceID(item, PropertyInstanceId.Wielder, ObjectGuid.Invalid));

                if (corpse != null)
                {
                    corpse.TryAddToInventory(item);
                    EnqueueBroadcast(new GameMessagePublicUpdateInstanceID(item, PropertyInstanceId.Container, corpse.Guid), new GameMessagePickupEvent(item));
                }
                else
                    droppedItems.Add(item);
            }

            // contain and non-wielded treasure create
            if (Biota.PropertiesCreateList != null)
            {
                var createList = Biota.PropertiesCreateList.Where(i => (i.DestinationType & DestinationType.Contain) != 0 ||
                                (i.DestinationType & DestinationType.Treasure) != 0 && (i.DestinationType & DestinationType.Wield) == 0).ToList();

                var selected = CreateListSelect(createList);

                foreach (var item in selected)
                {
                    var wo = WorldObjectFactory.CreateNewWorldObject(item);

                    if (wo != null)
                    {
                        if (corpse != null)
                            corpse.TryAddToInventory(wo);
                        else
                            droppedItems.Add(wo);
                    }
                }
            }

            return droppedItems;
        }

        /// <summary>
        /// Generates random amounts of slag on a corpse
        /// when an OlthoiPlayer is the killer
        /// </summary>
        private void GenerateTreasure_Olthoi(DamageHistoryInfo killer, Corpse corpse)
        {
            if (DeathTreasure == null) return;

            var slag = LootGenerationFactory.RollSlag(DeathTreasure);

            if (slag == null) return;

            corpse.TryAddToInventory(slag);
        }

        /// <summary>
        /// CONQUEST: Mystery Egg Drop System
        /// Rolls for mystery egg drops based on mob level and configurable rates
        /// Integrates with FellowshipRollManager for distribution and weekly limits
        /// </summary>
        private void TryGenerateMysteryEgg(DamageHistoryInfo killer)
        {
            // Only monsters can drop eggs
            if (!IsMonster)
                return;

            // Must have a valid player killer
            if (killer == null || !killer.IsPlayer)
                return;

            var killerPlayer = killer.TryGetAttacker() as Player;
            if (killerPlayer == null)
                return;

            // Handle pet owners - credit goes to the pet's owner
            if (killer.PetOwner != null)
            {
                killerPlayer = killer.TryGetPetOwner();
                if (killerPlayer == null)
                    return;
            }

            // Check minimum mob level requirement
            var minMobLevel = (int)PropertyManager.GetLong("mystery_egg_min_mob_level", 50);
            var mobLevel = Level ?? 0;
            if (mobLevel < minMobLevel)
                return;

            // Calculate drop rate based on mob level
            var baseDropRate = PropertyManager.GetDouble("mystery_egg_base_drop_rate", 0.002);
            var dropRate = baseDropRate;

            // Apply level multipliers
            if (mobLevel >= 200)
                dropRate *= PropertyManager.GetDouble("mystery_egg_level_mult_200", 2.0);
            else if (mobLevel >= 150)
                dropRate *= PropertyManager.GetDouble("mystery_egg_level_mult_150", 1.5);
            else if (mobLevel >= 100)
                dropRate *= PropertyManager.GetDouble("mystery_egg_level_mult_100", 1.25);
            // Level 50-99 uses base rate (1.0x multiplier)

            // Roll for drop
            var dropRoll = ThreadSafeRandom.Next(0.0f, 1.0f);
            if (dropRoll > dropRate)
                return;

            // Egg drop succeeded - determine rarity using weighted random
            var rarity = RollMysteryEggRarity();

            // Get the mystery egg WCID from config
            var eggWcid = (uint)PropertyManager.GetLong("mystery_egg_wcid", 801502);

            // Use FellowshipRollManager to handle distribution (respects weekly limits and fellowship rolls)
            if (killerPlayer.Fellowship != null)
            {
                // In a fellowship - initiate roll among eligible members
                FellowshipRollManager.InitiateRoll(this, eggWcid, rarity, killerPlayer, killerPlayer.Fellowship);
            }
            else
            {
                // Solo player - award directly with weekly limit check
                FellowshipRollManager.AwardItemToPlayerDirect(killerPlayer, eggWcid, rarity);
            }
        }

        /// <summary>
        /// Rolls for mystery egg rarity based on configured weights
        /// </summary>
        private static string RollMysteryEggRarity()
        {
            // Get weights from config
            var weightCommon = PropertyManager.GetDouble("mystery_egg_weight_common", 85.25);
            var weightRare = PropertyManager.GetDouble("mystery_egg_weight_rare", 13.0);
            var weightLegendary = PropertyManager.GetDouble("mystery_egg_weight_legendary", 1.5);
            var weightMythic = PropertyManager.GetDouble("mystery_egg_weight_mythic", 0.25);

            var totalWeight = weightCommon + weightRare + weightLegendary + weightMythic;
            var roll = ThreadSafeRandom.Next(0.0f, (float)totalWeight);

            // Determine rarity based on roll
            if (roll < weightMythic)
                return "mythic";
            if (roll < weightMythic + weightLegendary)
                return "legendary";
            if (roll < weightMythic + weightLegendary + weightRare)
                return "rare";

            return "common";
        }

        /// <summary>
        /// CONQUEST: Generates Soul Fragments on mob deaths in PK-only dungeon variants
        /// Small chance to drop 1 Soul Fragment (~1-2 per hour)
        /// Daily cap of 20 Soul Fragments per player
        /// </summary>
        private void GenerateSoulFragments_PKDungeon(DamageHistoryInfo killer, Corpse corpse)
        {
            // Must have a player killer
            if (killer == null || !killer.IsPlayer)
                return;

            var killerPlayer = killer.TryGetAttacker() as Player;
            if (killerPlayer == null || corpse == null)
                return;

            // Check if in a PK-only dungeon variant
            if (killerPlayer.CurrentLandblock == null || killerPlayer.Location == null)
                return;

            var currentLandblock = (ushort)killerPlayer.CurrentLandblock.Id.Landblock;
            var currentVariation = killerPlayer.Location.Variation ?? 0;

            if (!Landblock.pkDungeonLandblocks.Contains((currentLandblock, currentVariation)))
            {
                //Console.WriteLine($"[SOUL_FRAGMENT] Player {killerPlayer.Name} not in PK dungeon (LB: 0x{currentLandblock:X4}, Var: {currentVariation})");
                return;
            }
            //Console.WriteLine($"[SOUL_FRAGMENT] Player {killerPlayer.Name} in PK dungeon 0x{currentLandblock:X4} Var {currentVariation}");

            // Check and reset daily Soul Fragment count if needed
            var currentTime = Time.GetUnixTime();
            var lastResetTime = killerPlayer.LastSoulFragmentResetTime ?? 0;
            var dailyCount = killerPlayer.DailySoulFragmentCount ?? 0;

            // Reset if more than 24 hours have passed
            if (currentTime - lastResetTime > 86400) // 86400 seconds = 24 hours
            {
                dailyCount = 0;
                killerPlayer.SetProperty(PropertyInt64.DailySoulFragmentCount, (long)0);
                killerPlayer.SetProperty(PropertyInt64.LastSoulFragmentResetTime, (long)currentTime);
            }

            // Roll for Soul Fragment drop (0.75% chance for ~1-2 per hour at 100-200 kills/hour)
            var dropChance = ThreadSafeRandom.Next(0.0f, 1.0f);
            //Console.WriteLine($"[SOUL_FRAGMENT] Roll: {dropChance:F6} (need <= 0.0075), Daily: {dailyCount}/20");
            if (dropChance > 0.0075f)  // 0.75% drop rate
                return;

            // Check daily cap (20 fragments per day) AFTER the roll succeeds
            // This way we only notify when they would have gotten a fragment
            if (dailyCount >= 20)
            {
                log.Debug($"[SOUL_FRAGMENT] Player {killerPlayer.Name} at daily cap ({dailyCount}/20)");

                // Notify player about cap (with throttling to avoid spam)
                var lastCapNotify = killerPlayer.GetProperty(PropertyInt64.LastSoulFragmentCapNotifyTime) ?? 0;
                var timeSinceLastNotify = currentTime - lastCapNotify;

                if (timeSinceLastNotify > 300)  // Only notify once every 5 minutes
                {
                    var timeUntilReset = 86400 - (currentTime - lastResetTime);
                    var hours = (int)(timeUntilReset / 3600);
                    var minutes = (int)((timeUntilReset % 3600) / 60);
                    var seconds = (int)(timeUntilReset % 60);

                    var resetTimeMsg = hours > 0
                        ? $"{hours}h {minutes}m {seconds}s"
                        : minutes > 0
                            ? $"{minutes}m {seconds}s"
                            : $"{seconds}s";

                    killerPlayer.Session.Network.EnqueueSend(new GameMessageSystemChat(
                        $"You have reached your daily Soul Fragment limit (20/20). Your limit will reset in {resetTimeMsg}.",
                        ChatMessageType.Broadcast));

                    killerPlayer.SetProperty(PropertyInt64.LastSoulFragmentCapNotifyTime, (long)currentTime);
                }

                return;
            }

            // Create Soul Fragment
            // TODO: Replace 999999998 with actual Soul Fragment weenie ID
            var soulFragment = WorldObjectFactory.CreateNewWorldObject(13370003);
            if (soulFragment == null)
            {
                //Console.WriteLine($"[SOUL_FRAGMENT] Failed to create Soul Fragment weenie 13370003!");
                return;
            }

            // Try to add directly to player inventory
            if (killerPlayer.TryCreateInInventoryWithNetworking(soulFragment))
            {
                // Increment daily count
                killerPlayer.SetProperty(PropertyInt64.DailySoulFragmentCount, dailyCount + 1);

                // Notify player
                var remaining = 20 - (dailyCount + 1);
                killerPlayer.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"You found a Soul Fragment! ({remaining} remaining today)",
                    ChatMessageType.Broadcast));
            }
            else
            {
                // Inventory full - destroy the fragment and notify
                soulFragment.Destroy();
                killerPlayer.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    "A Soul Fragment appeared but your inventory is full!",
                    ChatMessageType.Broadcast));
            }
        }

        public void DoCantripLogging(DamageHistoryInfo killer, WorldObject wo)
        {
            var epicCantrips = wo.EpicCantrips;
            var legendaryCantrips = wo.LegendaryCantrips;

            if (epicCantrips.Count > 0 && log.IsDebugEnabled)
                log.Debug($"[LOOT][EPIC] {Name} ({Guid}) generated item with {epicCantrips.Count} epic{(epicCantrips.Count > 1 ? "s" : "")} - {wo.Name} ({wo.Guid}) - {GetSpellList(epicCantrips)} - killed by {killer?.Name} ({killer?.Guid})");

            if (legendaryCantrips.Count > 0 && log.IsDebugEnabled)
                log.Debug($"[LOOT][LEGENDARY] {Name} ({Guid}) generated item with {legendaryCantrips.Count} legendar{(legendaryCantrips.Count > 1 ? "ies" : "y")} - {wo.Name} ({wo.Guid}) - {GetSpellList(legendaryCantrips)} - killed by {killer?.Name} ({killer?.Guid})");
        }

        public static string GetSpellList(Dictionary<int, float> spellTable)
        {
            var spells = new List<Server.Entity.Spell>();

            foreach (var kvp in spellTable)
                spells.Add(new Server.Entity.Spell(kvp.Key, false));

            return string.Join(", ", spells.Select(i => i.Name));
        }

        public bool IsOnNoDeathXPLandblock => Location != null ? NoDeathXP_Landblocks.Contains(Location.LandblockId.Landblock) : false;

        /// <summary>
        /// A list of landblocks the player gains no xp from creature kills
        /// </summary>
        public static HashSet<ushort> NoDeathXP_Landblocks = new HashSet<ushort>()
        {
            0x00B0,     // Colosseum Arena One
            0x00B1,     // Colosseum Arena Two
            0x00B2,     // Colosseum Arena Three
            0x00B3,     // Colosseum Arena Four
            0x00B4,     // Colosseum Arena Five
            0x5960,     // Gauntlet Arena One (Celestial Hand)
            0x5961,     // Gauntlet Arena Two (Celestial Hand)
            0x5962,     // Gauntlet Arena One (Eldritch Web)
            0x5963,     // Gauntlet Arena Two (Eldritch Web)
            0x5964,     // Gauntlet Arena One (Radiant Blood)
            0x5965,     // Gauntlet Arena Two (Radiant Blood)
            0x596B,     // Gauntlet Staging Area (All Societies)
        };
    }
}
