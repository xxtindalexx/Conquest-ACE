using System;
using System.Collections.Generic;

using log4net;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    public class DyeMixture : CraftTool
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public const uint DyeMixtureWcid = 13370669;
        public const int CompleteMask = 511;

        private static readonly Dictionary<uint, int> VialBits = new Dictionary<uint, int>()
        {
            { 7977,   1   },   // Red
            { 7976,   2   },   // Green
            { 8641,   4   },   // Blue
            { 11471,  8   },   // Purple
            { 7975,   16  },   // Yellow
            { 8642,   32  },   // Mint
            { 8643,   64  },   // White
            { 11469,  128 },   // Black
            { 11470,  256 },   // Dark Blue
        };

        public DyeMixture(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
        }

        public DyeMixture(Biota biota) : base(biota)
        {
        }

        public static bool TryAddVial(Player player, WorldObject source, WorldObject target)
        {
            if (!TryResolveVialAndMixture(source, target, out var vial, out var mixture, out var bit))
                return false;

            var current = mixture.GetProperty(PropertyInt.DyeMixtureVialsBitfield) ?? 0;

            if ((current & bit) != 0 && current != CompleteMask)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat("That vial has already been added to the mixture.", ChatMessageType.Craft));
                player.SendUseDoneEvent();
                return true;
            }

            var allowCraftInCombat = PropertyManager.GetBool("allow_combat_mode_crafting");
            var motionCommand = MotionCommand.ClapHands;
            var actionChain = new ActionChain();
            var nextUseTime = 0.0f;

            player.IsBusy = true;

            if (allowCraftInCombat && player.CombatMode != CombatMode.NonCombat)
            {
                var stanceTime = player.SetCombatMode(CombatMode.NonCombat);
                actionChain.AddDelaySeconds(stanceTime);
                nextUseTime += stanceTime;
            }

            var currentStance = player.CurrentMotionState.Stance;
            var clapTime = Physics.Animation.MotionTable.GetAnimationLength(player.MotionTableId, currentStance, motionCommand);

            actionChain.AddAction(player, ActionType.PlayerMotion_SendMotionAsCommands, () => player.SendMotionAsCommands(motionCommand, currentStance));
            actionChain.AddDelaySeconds(clapTime);
            nextUseTime += clapTime;

            actionChain.AddAction(player, ActionType.DyeMixture_AddVial, () =>
            {
                try
                {
                    if (player.FindObject(vial.Guid.Full, Player.SearchLocations.MyInventory) == null ||
                        player.FindObject(mixture.Guid.Full, Player.SearchLocations.MyInventory) == null)
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat("The vial and dye mixture must remain in your possession.", ChatMessageType.Craft));
                        return;
                    }

                    var bits = mixture.GetProperty(PropertyInt.DyeMixtureVialsBitfield) ?? 0;

                    // Mixture already complete — retry granting Mystery Dye (e.g. after a prior failed handoff)
                    if (bits == CompleteMask)
                    {
                        TryGrantMysteryDye(player, mixture);
                        return;
                    }

                    if ((bits & bit) != 0)
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat("That vial has already been added to the mixture.", ChatMessageType.Craft));
                        return;
                    }

                    if (!player.TryConsumeFromInventoryWithNetworking(vial, 1))
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat("The vial could not be consumed.", ChatMessageType.Craft));
                        return;
                    }

                    var newBits = bits | bit;

                    player.UpdateProperty(mixture, PropertyInt.DyeMixtureVialsBitfield, newBits);
                    mixture.SaveBiotaToDatabase();

                    if (newBits == CompleteMask)
                    {
                        TryGrantMysteryDye(player, mixture);
                        return;
                    }

                    var count = CountBits(newBits);
                    player.SendMessage($"You add the {vial.Name} to the dye mixture. ({count}/9)", ChatMessageType.Craft);
                }
                finally
                {
                    player.SendUseDoneEvent();
                    player.IsBusy = false;
                }
            });

            actionChain.EnqueueChain();
            player.NextUseTime = DateTime.UtcNow.AddSeconds(nextUseTime);

            return true;
        }

        private static bool TryResolveVialAndMixture(WorldObject source, WorldObject target, out WorldObject vial, out WorldObject mixture, out int bit)
        {
            vial = null;
            mixture = null;
            bit = 0;

            if (target.WeenieClassId == DyeMixtureWcid && VialBits.TryGetValue(source.WeenieClassId, out bit))
            {
                vial = source;
                mixture = target;
                return true;
            }

            if (source.WeenieClassId == DyeMixtureWcid && VialBits.TryGetValue(target.WeenieClassId, out bit))
            {
                vial = target;
                mixture = source;
                return true;
            }

            return false;
        }

        private static void TryGrantMysteryDye(Player player, WorldObject mixture)
        {
            var mysteryDye = WorldObjectFactory.CreateNewWorldObject(RandomDye.RandomDyeWcid);
            if (mysteryDye == null)
            {
                log.Warn($"DyeMixture.TryGrantMysteryDye({player.Name}): Mystery Dye weenie {RandomDye.RandomDyeWcid} not found in world database.");
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    "The dye mixture is ready, but Mystery Dye could not be created. Weenie 13370549 may be missing from the world database.",
                    ChatMessageType.Craft));
                return;
            }

            if (!player.TryCreateInInventoryWithNetworking(mysteryDye))
            {
                mysteryDye.Destroy();
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    "The dye mixture is ready, but you do not have enough pack space for the Mystery Dye. Free up space and use any vial on the mixture again.",
                    ChatMessageType.Craft));
                return;
            }

            if (!player.TryConsumeFromInventoryWithNetworking(mixture, 1))
            {
                log.Warn($"DyeMixture.TryGrantMysteryDye({player.Name}): granted Mystery Dye but failed to consume mixture.");
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    "You receive a Mystery Dye, but the dye mixture could not be removed from your inventory.",
                    ChatMessageType.Craft));
                return;
            }

            player.SendMessage("The dye mixture finishes brewing and becomes a Mystery Dye!", ChatMessageType.Craft);
        }

        private static int CountBits(int value)
        {
            var count = 0;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }
            return count;
        }
    }
}
