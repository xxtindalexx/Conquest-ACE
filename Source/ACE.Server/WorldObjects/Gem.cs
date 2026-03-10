using ACE.Common;
using ACE.Database;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Physics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ACE.Server.WorldObjects
{
    public class Gem : Stackable
    {
        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public Gem(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public Gem(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
        }

        /// <summary>
        /// This is raised by Player.HandleActionUseItem.<para />
        /// The item should be in the players possession.
        /// 
        /// The OnUse method for this class is to use a contract to add a tracked quest to our quest panel.
        /// This gives the player access to information about the quest such as starting and ending NPC locations,
        /// and shows our progress for kill tasks as well as any timing information such as when we can repeat the
        /// quest or how much longer we have to complete it in the case of at timed quest.   Og II
        /// </summary>
        public override void ActOnUse(WorldObject activator)
        {
            ActOnUse(activator, false);
        }

        public void ActOnUse(WorldObject activator, bool confirmed)
        {
            if (!(activator is Player player))
                return;

            // CONQUEST: Arena observers cannot use items (especially portal gems)
            if (player.IsArenaObserver)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat("You cannot use items while observing an arena match.", ChatMessageType.Broadcast));
                return;
            }

            // CONQUEST: Gem of Soul Recovery - Teleports player to their last corpse location
            // Gem is consumed on use, 20-hour cooldown before another gem can be used
            if (WeenieClassId == 13370301)
            {
                player.TeleportToLastCorpse(this);
                return;
            }

            if (player.IsBusy || player.Teleporting || player.suicideInProgress)
            {
                player.SendWeenieError(WeenieError.YoureTooBusy);
                return;
            }

            if (player.IsJumping)
            {
                player.SendWeenieError(WeenieError.YouCantDoThatWhileInTheAir);
                return;
            }

            if (!string.IsNullOrWhiteSpace(UseSendsSignal))
            {
                player.CurrentLandblock?.EmitSignal(player, UseSendsSignal);
                return;
            }

            // CONQUEST: Block rare gem usage in arena landblocks
            if (RareId != null && player.CurrentLandblock?.IsArenaLandblock == true)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat("Rare gems cannot be used in arena matches.", ChatMessageType.Broadcast));
                return;
            }

            // CONQUEST: Block rare gem usage during PvP combat (PK timer active)
            if ((RareId != null || RareUsesTimer) && player.PKTimerActive)
            {
                player.SendWeenieError(WeenieError.YouHaveBeenInPKBattleTooRecently);
                return;
            }

            // handle rare gems
            if (RareId != null && player.GetCharacterOption(CharacterOption.ConfirmUseOfRareGems) && !confirmed)
            {
                var msg = $"Are you sure you want to use {Name}?";
                var confirm = new Confirmation_Custom(player.Guid, () => ActOnUse(activator, true));
                if (!player.ConfirmationManager.EnqueueSend(confirm, msg))
                    player.SendWeenieError(WeenieError.ConfirmationInProgress);
                return;
            }

            if (RareUsesTimer)
            {
                var currentTime = Time.GetUnixTime();

                var timeElapsed = currentTime - player.LastRareUsedTimestamp;

                if (timeElapsed < RareTimer)
                {
                    // TODO: get retail message
                    var remainTime = (int)Math.Ceiling(RareTimer - timeElapsed);
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You may use another timed rare in {remainTime}s", ChatMessageType.Broadcast));
                    return;
                }
            }

            if (UseUserAnimation != MotionCommand.Invalid)
            {
                // some gems have UseUserAnimation and UseSound, similar to food
                // eg. 7559 - Condensed Dispel Potion

                // the animation is also weird, and differs from food, in that it is the full animation
                // instead of stopping at the 'eat/drink' point... so we pass 0.5 here?

                var animMod = (UseUserAnimation == MotionCommand.MimeDrink || UseUserAnimation == MotionCommand.MimeEat) ? 0.5f : 1.0f;

                player.ApplyConsumable(UseUserAnimation, () => UseGem(player), animMod);
            }
            else
                UseGem(player);
        }

        public void UseGem(Player player)
        {
            if (player.IsDead) return;

            // verify item is still valid
            if (player.FindObject(Guid.Full, Player.SearchLocations.MyInventory) == null)
            {
                //player.SendWeenieError(WeenieError.ObjectGone);   // results in 'Unable to move object!' transient error
                player.SendTransientError($"Cannot find the {Name}");   // custom message
                return;
            }

            // Handle Mystery Egg hatching
            var eggRarity = GetProperty(PropertyInt.EggRarity);
            if (eggRarity != null)
            {
                HatchMysteryEgg(player, eggRarity.Value);
                return;
            }

            // CONQUEST: Handle Enlightenment Gems
            var enlightenmentGemType = GetProperty(PropertyInt.EnlightenmentGemType);
            if (enlightenmentGemType != null)
            {
                // HandleEnlightenmentGem returns true if we should continue normal gem processing
                // (for benediction gems that use UseCreateItem to give the reusable gem)
                if (!HandleEnlightenmentGem(player, enlightenmentGemType.Value))
                    return;
                // For benediction gems (12/13), fall through to UseCreateItem processing below
            }

            // trying to use a dispel potion while pk timer is active
            // send error message and cancel - do not consume item
            if (SpellDID != null)
            {
                var spell = new Spell(SpellDID.Value);

                if (spell.MetaSpellType == SpellType.Dispel && !VerifyDispelPKStatus(this, player))
                    return;
            }

            if (RareUsesTimer)
            {
                var currentTime = Time.GetUnixTime();

                player.LastRareUsedTimestamp = currentTime;

                // local broadcast usage
                player.EnqueueBroadcast(new GameMessageSystemChat($"{player.Name} used the rare item {Name}", ChatMessageType.Broadcast));
            }

            if (SpellDID.HasValue)
            {
                var spell = new Spell((uint)SpellDID);

                // should be 'You cast', instead of 'Item cast'
                // omitting the item caster here, so player is also used for enchantment registry caster,
                // which could prevent some scenarios with spamming enchantments from multiple gem sources to protect against dispels

                // TODO: figure this out better
                if (spell.MetaSpellType == SpellType.PortalSummon)
                    TryCastSpell(spell, player, this, tryResist: false);
                else if (spell.IsImpenBaneType || spell.IsItemRedirectableType)
                    player.TryCastItemEnchantment_WithRedirects(spell, player, this);
                else
                    player.TryCastSpell(spell, player, this, tryResist: false);
            }

            if (UseCreateContractId > 0)
            {
                if (!player.ContractManager.Add(UseCreateContractId.Value))
                    return;

                // this wasn't in retail, but the lack of feedback when using a contract gem just seems jarring so...
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{Name} accepted. Click on the quill icon in the lower right corner to open your contract tab to view your active contracts.", ChatMessageType.Broadcast));
            }

            if (UseCreateItem > 0)
            {
                if (!HandleUseCreateItem(player))
                    return;
            }

            if (UseSound > 0)
                player.Session.Network.EnqueueSend(new GameMessageSound(player.Guid, UseSound));

            if ((GetProperty(PropertyBool.UnlimitedUse) ?? false) == false)
                player.TryConsumeFromInventoryWithNetworking(this, 1);
        }

        public bool HandleUseCreateItem(Player player)
        {
            var amount = UseCreateQuantity ?? 1;

            var itemsToReceive = new ItemsToReceive(player);

            itemsToReceive.Add(UseCreateItem.Value, amount);

            if (itemsToReceive.PlayerExceedsLimits)
            {
                if (itemsToReceive.PlayerExceedsAvailableBurden)
                    player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, "You are too encumbered to use that!"));
                else if (itemsToReceive.PlayerOutOfInventorySlots)
                    player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, "You do not have enough pack space to use that!"));
                else if (itemsToReceive.PlayerOutOfContainerSlots)
                    player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, "You do not have enough container slots to use that!"));

                return false;
            }

            if (itemsToReceive.RequiredSlots > 0)
            {
                var remaining = amount;

                while (remaining > 0)
                {
                    var item = WorldObjectFactory.CreateNewWorldObject(UseCreateItem.Value);

                    if (item is Stackable)
                    {
                        var stackSize = Math.Min(remaining, item.MaxStackSize ?? 1);

                        item.SetStackSize(stackSize);
                        remaining -= stackSize;
                    }
                    else
                        remaining--;

                    player.TryCreateInInventoryWithNetworking(item);
                }
            }
            else
            {
                player.SendTransientError($"Unable to use {Name} at this time!");
                return false;
            }
            return true;
        }

        public int? RareId
        {
            get => GetProperty(PropertyInt.RareId);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.RareId); else SetProperty(PropertyInt.RareId, value.Value); }
        }

        public bool RareUsesTimer
        {
            get => GetProperty(PropertyBool.RareUsesTimer) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.RareUsesTimer); else SetProperty(PropertyBool.RareUsesTimer, value); }
        }

        public override void HandleActionUseOnTarget(Player player, WorldObject target)
        {
            // CONQUEST: Check for morph gems first
            if (MorphGem.IsMorphGem(WeenieClassId))
            {
                Tailoring.UseObjectOnTarget(player, this, target);
                return;
            }

            // should tailoring kit / aetheria be subtyped?
            if (Tailoring.IsTailoringKit(WeenieClassId))
            {
                Tailoring.UseObjectOnTarget(player, this, target);
                return;
            }

            // fallback on recipe manager?
            base.HandleActionUseOnTarget(player, target);
        }

        /// <summary>
        /// For Rares that use cooldown timers (RareUsesTimer),
        /// any other rares with RareUsesTimer may not be used for 3 minutes
        /// Note that if the player logs out, this cooldown timer continues to tick/expire (unlike enchantments)
        /// </summary>
        public static int RareTimer = 180;

        public string UseSendsSignal
        {
            get => GetProperty(PropertyString.UseSendsSignal);
            set { if (value == null) RemoveProperty(PropertyString.UseSendsSignal); else SetProperty(PropertyString.UseSendsSignal, value); }
        }

        public override void OnActivate(WorldObject activator)
        {
            if (ItemUseable == Usable.Contained && activator is Player player)
            {               
                var containedItem = player.FindObject(Guid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems);
                if (containedItem != null) // item is contained by player
                {
                    if (player.IsBusy || player.Teleporting || player.suicideInProgress)
                    {
                        player.Session.Network.EnqueueSend(new GameEventWeenieError(player.Session, WeenieError.YoureTooBusy));
                        player.EnchantmentManager.StartCooldown(this);
                        return;
                    }

                    if (player.IsDead)
                    {
                        player.Session.Network.EnqueueSend(new GameEventWeenieError(player.Session, WeenieError.Dead));
                        player.EnchantmentManager.StartCooldown(this);
                        return;
                    }
                }
                else
                    return;
            }

            base.OnActivate(activator);
        }

        private void HatchMysteryEgg(Player player, int eggRarityValue)
        {
            // Check if 7 days have passed since creation
            var creationTime = GetProperty(PropertyInt.CreationTimestamp) ?? 0;
            var currentTime = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            var daysElapsed = (currentTime - creationTime) / 86400.0; // 86400 seconds in a day
            var daysRemaining = 7.0 - daysElapsed;

            if (daysRemaining > 0)
            {
                player.SendMessage($"This egg is not ready to hatch yet. It needs {daysRemaining:F1} more days to mature.");
                return;
            }

            // Get all pet WCIDs with matching rarity (FAST - only queries one table!)
            var eligiblePetWcids = DatabaseManager.World.GetPetsByRarity(eggRarityValue);

            if (eligiblePetWcids.Count == 0)
            {
                player.SendMessage($"No pets found for this rarity tier. Please contact an administrator.");
                return;
            }

            // Randomly select a pet WCID
            var randomIndex = ThreadSafeRandom.Next(0, eligiblePetWcids.Count - 1);
            var selectedPetWcid = eligiblePetWcids[randomIndex];

            // Create the pet and give to player
            var pet = WorldObjectFactory.CreateNewWorldObject(selectedPetWcid);
            if (pet == null)
            {
                player.SendMessage($"Error creating pet (WCID: {selectedPetWcid}). Please contact an administrator.");
                return;
            }

            // Set pet rarity on the device so it can be read when summoning
            pet.SetProperty(PropertyInt.PetRarity, eggRarityValue);

            // Set icon underlay based on rarity
            // Common: 0x06003355, Rare: 0x06003353, Legendary: 0x06003356, Mythic: 0x06003354
            pet.IconUnderlayId = eggRarityValue switch
            {
                1 => 0x06003355, // Common
                2 => 0x06003353, // Rare
                3 => 0x06003356, // Legendary
                4 => 0x06003354, // Mythic
                _ => null
            };

            // Randomize pet rating bonuses based on rarity (Rare/Legendary/Mythic only)
            AssignRandomPetRatings(pet, eggRarityValue);

            // Try to add to inventory
            if (player.TryCreateInInventoryWithNetworking(pet))
            {
                var rarityName = eggRarityValue switch
                {
                    1 => "Common",
                    2 => "Rare",
                    3 => "Legendary",
                    4 => "Mythic",
                    _ => "Unknown"
                };

                player.SendMessage($"Your {rarityName} Mystery Egg hatched into {pet.Name}!");

                // Global broadcast for Legendary and Mythic
                if (eggRarityValue >= 3)
                {
                    var broadcastMsg = $"{player.Name}'s {rarityName} Mystery Egg hatched into {pet.Name}!";
                    PlayerManager.BroadcastToAll(new GameMessageSystemChat(broadcastMsg, ChatMessageType.Broadcast));
                }

                // Consume the egg (only on successful hatch)
                player.TryConsumeFromInventoryWithNetworking(this, 1);
            }
            else
            {
                // Inventory full - don't consume egg, let player try again
                player.SendMessage($"Your inventory is full! Make space before hatching this egg.");
                pet.Destroy();
                // Note: Egg is NOT consumed, player can try again when they have space
            }
        }

        /// <summary>
        /// Randomly assigns rating bonuses to a pet based on its rarity tier
        /// Rare: +1 to one random rating
        /// Legendary/Mythic: 50% chance for +2 to one rating, 50% chance for +1 to two different ratings
        /// </summary>
        private void AssignRandomPetRatings(WorldObject pet, int rarity)
        {
            // Common pets get no rating bonuses
            if (rarity == 1)
                return;

            // Available ratings to choose from
            var availableRatings = new List<PropertyInt>
            {
                PropertyInt.PetBonusDamageRating,
                PropertyInt.PetBonusDamageReductionRating,
                PropertyInt.PetBonusCritDamageRating,
                PropertyInt.PetBonusCritDamageReductionRating
            };

            if (rarity == 2) // Rare: +1 to one random rating
            {
                var chosenRating = availableRatings[ThreadSafeRandom.Next(0, availableRatings.Count - 1)];
                pet.SetProperty(chosenRating, 1);
            }
            else if (rarity == 3 || rarity == 4) // Legendary or Mythic
            {
                // 50/50 chance: +2 to one rating OR +1 to two different ratings
                var option = ThreadSafeRandom.Next(0, 1); // 0 or 1

                if (option == 0) // +2 to one rating
                {
                    var chosenRating = availableRatings[ThreadSafeRandom.Next(0, availableRatings.Count - 1)];
                    pet.SetProperty(chosenRating, 2);
                }
                else // +1 to two different ratings
                {
                    // Pick first rating
                    var firstIndex = ThreadSafeRandom.Next(0, availableRatings.Count - 1);
                    var firstRating = availableRatings[firstIndex];
                    pet.SetProperty(firstRating, 1);

                    // Remove first rating from list and pick second
                    availableRatings.RemoveAt(firstIndex);
                    var secondRating = availableRatings[ThreadSafeRandom.Next(0, availableRatings.Count - 1)];
                    pet.SetProperty(secondRating, 1);
                }
            }
        }

        /// <summary>
        /// CONQUEST: Handles enlightenment gem usage based on gem type
        /// Gem types:
        /// 1 = Cleave, 2 = Arrow Split, 3 = Spell Chain, 4 = Aetheria Surge, 14 = Void Contagion (Combat Trophies)
        /// 5 = +1 DR, 6 = +1 DRR, 7 = +1 CD, 8 = +1 CDR (Tokens, max 4 each)
        /// 9 = +1% Imbue (Tokens, max 10), 10 = +1% Salvage (Tokens, max 10)
        /// 11 = +1 Skill Credit (Tokens, max 5), 12 = Stamina Benediction, 13 = Mana Benediction
        /// Returns true if normal gem processing should continue (for UseCreateItem), false to stop
        /// </summary>
        private bool HandleEnlightenmentGem(Player player, int gemType)
        {
            string gemName = Name ?? "Enlightenment Gem";
            bool success = false;

            switch (gemType)
            {
                // Combat Trophy Gems (one-time purchases)
                case 1: // Cleave
                    success = TryApplyEnlightenmentPerk(player, PropertyInt.EnlightenmentCleaveBonus, 1, 1, gemName,
                        "Your melee weapons now cleave to hit an additional target!");
                    break;
                case 2: // Arrow Split
                    success = TryApplyEnlightenmentPerk(player, PropertyInt.EnlightenmentSplitArrowBonus, 1, 1, gemName,
                        "Your missile weapons now split to hit an additional target!");
                    break;
                case 3: // Spell Chain
                    success = TryApplyEnlightenmentPerk(player, PropertyInt.EnlightenmentSpellChainBonus, 1, 1, gemName,
                        "Your war magic spells now chain to a nearby target for 30% damage!");
                    break;
                case 4: // Aetheria Surge
                    success = TryApplyEnlightenmentPerk(player, PropertyInt.EnlightenmentAetheriaSurgeBonus, 1, 1, gemName,
                        "Your aetheria now surge as if they were 1 level higher!");
                    break;
                case 14: // Void Contagion (DoT spread on death)
                    if (!PropertyManager.GetBool("void_contagion_enabled"))
                    {
                        player.Session?.Network.EnqueueSend(new GameMessageSystemChat("This ability is not yet available.", ChatMessageType.Broadcast));
                        return false;
                    }
                    success = TryApplyEnlightenmentPerk(player, PropertyInt.EnlightenmentVoidDotSpreadBonus, 1, 1, gemName,
                        "Your void magic DoTs now spread to nearby enemies when your target dies!");
                    break;

                // Rating Gems (max 4 each)
                case 5: // +1 Damage Rating
                    success = TryApplyEnlightenmentPerk(player, PropertyInt.EnlightenmentBonusDamageRating, 1, 4, gemName,
                        "You have gained +1 Damage Rating!");
                    break;
                case 6: // +1 Damage Reduction
                    success = TryApplyEnlightenmentPerk(player, PropertyInt.EnlightenmentBonusDamageReduction, 1, 4, gemName,
                        "You have gained +1 Damage Reduction Rating!");
                    break;
                case 7: // +1 Crit Damage
                    success = TryApplyEnlightenmentPerk(player, PropertyInt.EnlightenmentBonusCritDamageRating, 1, 4, gemName,
                        "You have gained +1 Critical Damage Rating!");
                    break;
                case 8: // +1 Crit Damage Reduction
                    success = TryApplyEnlightenmentPerk(player, PropertyInt.EnlightenmentBonusCritDamageReduction, 1, 4, gemName,
                        "You have gained +1 Critical Damage Reduction Rating!");
                    break;

                // Crafting Gems (max 10 each)
                case 9: // +1% Imbue
                    success = TryApplyEnlightenmentPerk(player, PropertyInt.EnlightenmentImbueBonus, 1, 10, gemName,
                        "You have gained +1% Imbue Success Chance!");
                    break;
                case 10: // +1 Salvage Unit
                    success = TryApplyEnlightenmentPerk(player, PropertyInt.EnlightenmentSalvageBonus, 1, 10, gemName,
                        "You have gained +1 Salvage Unit per item!");
                    break;

                // Skill Credit Gem (max 5)
                case 11: // +1 Skill Credit
                    success = TryApplySkillCreditGem(player, gemName);
                    break;

                // Benediction Gems (one-time purchase - weenie handles UseCreateItem to give reusable gem)
                case 12: // Stamina Benediction
                case 13: // Mana Benediction
                    var trackingProp = gemType == 12 ? PropertyInt.EnlightenmentStaminaBenediction : PropertyInt.EnlightenmentManaBenediction;
                    var benedictionName = gemType == 12 ? "Stamina Benediction" : "Mana Benediction";
                    if (TryMarkBenedictionObtained(player, trackingProp, benedictionName))
                    {
                        player.PlayParticleEffect(PlayScript.EnchantUpBlue, player.Guid);
                        // Return true to continue normal gem processing (UseCreateItem will give the reusable gem)
                        return true;
                    }
                    return false; // Already has it, stop processing

                default:
                    player.SendMessage($"Unknown enlightenment gem type: {gemType}");
                    return false;
            }

            if (success)
            {
                // Consume the gem and play effect
                player.TryConsumeFromInventoryWithNetworking(this, 1);
                player.PlayParticleEffect(PlayScript.EnchantUpBlue, player.Guid);
            }

            return false; // Stop normal processing, we handled everything
        }

        /// <summary>
        /// Attempts to apply an enlightenment perk, checking max limits
        /// </summary>
        private bool TryApplyEnlightenmentPerk(Player player, PropertyInt property, int amount, int maxValue, string gemName, string successMessage)
        {
            var currentValue = player.GetProperty(property) ?? 0;

            if (currentValue >= maxValue)
            {
                player.SendMessage($"You have already reached the maximum benefit from {gemName}.");
                return false;
            }

            var newValue = Math.Min(currentValue + amount, maxValue);
            player.SetProperty(property, newValue);
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, property, newValue));
            player.SendMessage(successMessage, ChatMessageType.Broadcast);
            player.SaveBiotaToDatabase();

            return true;
        }

        /// <summary>
        /// Special handling for skill credit gem - adds to available skill credits
        /// </summary>
        private bool TryApplySkillCreditGem(Player player, string gemName)
        {
            var purchased = player.GetProperty(PropertyInt.EnlightenmentSkillCreditsPurchased) ?? 0;
            const int maxPurchases = 5;

            if (purchased >= maxPurchases)
            {
                player.SendMessage($"You have already purchased the maximum number of skill credits from enlightenment ({maxPurchases}).");
                return false;
            }

            // Increment purchased count
            player.SetProperty(PropertyInt.EnlightenmentSkillCreditsPurchased, purchased + 1);

            // Add skill credit
            player.AvailableSkillCredits = (player.AvailableSkillCredits ?? 0) + 1;
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.AvailableSkillCredits, player.AvailableSkillCredits ?? 0));
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.EnlightenmentSkillCreditsPurchased, purchased + 1));

            player.SendMessage("You have gained +1 Skill Credit!", ChatMessageType.Broadcast);
            player.SaveBiotaToDatabase();

            return true;
        }

        /// <summary>
        /// Marks that the player has obtained a benediction gem (prevents re-purchase)
        /// The actual reusable gem is created via the weenie's UseCreateItem property
        /// </summary>
        private bool TryMarkBenedictionObtained(Player player, PropertyInt trackingProperty, string benedictionName)
        {
            var alreadyHas = (player.GetProperty(trackingProperty) ?? 0) > 0;

            if (alreadyHas)
            {
                player.SendMessage($"You have already received the {benedictionName}.");
                return false;
            }

            // Mark as obtained so vendor won't sell another
            player.SetProperty(trackingProperty, 1);
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, trackingProperty, 1));
            player.SaveBiotaToDatabase();

            // Return true - let the normal gem UseCreateItem flow handle giving the reusable gem
            return true;
        }
    }
}
