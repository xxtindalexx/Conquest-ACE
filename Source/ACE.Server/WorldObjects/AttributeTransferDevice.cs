using System;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects.Entity;

namespace ACE.Server.WorldObjects
{
    public class AttributeTransferDevice : WorldObject
    {
        public PropertyAttribute TransferFromAttribute
        {
            get => (PropertyAttribute)(GetProperty(PropertyInt.TransferFromAttribute) ?? 0);
            set { if (value == 0) RemoveProperty(PropertyInt.TransferFromAttribute); else SetProperty(PropertyInt.TransferFromAttribute, (int)value); }
        }

        public PropertyAttribute TransferToAttribute
        {
            get => (PropertyAttribute)(GetProperty(PropertyInt.TransferToAttribute) ?? 0);
            set { if (value == 0) RemoveProperty(PropertyInt.TransferToAttribute); else SetProperty(PropertyInt.TransferToAttribute, (int)value); }
        }

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public AttributeTransferDevice(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public AttributeTransferDevice(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
        }

        public override void ActOnUse(WorldObject activator)
        {
            ActOnUse(activator, false);
        }

        public void ActOnUse(WorldObject activator, bool confirmed)
        {
            var player = activator as Player;
            if (player == null) return;

            if (TransferFromAttribute == PropertyAttribute.Undef || TransferToAttribute == PropertyAttribute.Undef)
            {
                player.SendWeenieError(WeenieError.YouHaveFailedToAlterAttributes);
                return;
            }

            // CONQUEST: Block transferring to the same attribute
            if (TransferFromAttribute == TransferToAttribute)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Cannot transfer from {TransferFromAttribute} to itself.", ChatMessageType.Broadcast));
                return;
            }

            var device = player.FindObject(Guid.Full, Player.SearchLocations.MyInventory);
            if (device == null)
            {
                player.SendWeenieError(WeenieError.YouHaveFailedToAlterAttributes);
                return;
            }

            var fromAttr = player.Attributes[TransferFromAttribute];
            var toAttr = player.Attributes[TransferToAttribute];

            // CONQUEST: Calculate true innate values by subtracting ONLY enlightenment bonus
            // Enlightenment is a separate bonus that doesn't count toward the 100 cap
            // Augmentation bonuses DO count toward the 100 cap (they are part of innate)
            var enlightenmentBonus = (uint)(player.Enlightenment > 0 ? player.Enlightenment : 0);

            // CONQUEST: Calculate innate with underflow protection
            var fromTotal = fromAttr.StartingValue;
            var toTotal = toAttr.StartingValue;

            // Prevent underflow - innate can't be negative
            var fromInnate = fromTotal > enlightenmentBonus ? fromTotal - enlightenmentBonus : 0;
            var toInnate = toTotal > enlightenmentBonus ? toTotal - enlightenmentBonus : 0;

            if (fromInnate <= 10)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your innate {TransferFromAttribute} must be above 10 to use the {Name}.", ChatMessageType.Broadcast));
                return;
            }

            if (toInnate >= 100)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your innate {TransferToAttribute} must be below 100 to use the {Name}.", ChatMessageType.Broadcast));
                return;
            }

            // ACE: block transfer while wielding items that would violate requirements
            if (CheckWieldedItems(player))
            {
                player.SendWeenieError(WeenieError.CannotTransferAttributesWhileWieldingItem);
                return;
            }

            // CONQUEST: Calculate how many points can actually be transferred
            // - Can't take more than (fromInnate - 10) to keep from attribute above 10
            // - Can't add more than (100 - toInnate) to keep to attribute at/below 100
            var fromAmount = Math.Min(10, (int)fromInnate - 10);
            var toAmount = Math.Min(10, 100 - (int)toInnate);
            var amount = Math.Min(fromAmount, toAmount);

            // CONQUEST: Hard cap - ensure we never exceed 100 innate on destination
            if (amount <= 0)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Cannot transfer any points - {TransferToAttribute} is already at the 100 innate cap.", ChatMessageType.Broadcast));
                return;
            }

            // CONQUEST: Verify the transfer won't exceed 100 (safety check)
            if (toInnate + amount > 100)
            {
                amount = (int)(100 - toInnate);
                if (amount <= 0)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Cannot transfer any points - {TransferToAttribute} is already at the 100 innate cap.", ChatMessageType.Broadcast));
                    return;
                }
            }

            if (!confirmed)
            {
                var transferMsg = amount < 10
                    ? $"This action will transfer {amount} points (capped) from your {fromAttr.Attribute} to your {toAttr.Attribute}."
                    : $"This action will transfer 10 points from your {fromAttr.Attribute} to your {toAttr.Attribute}.";
                if (!player.ConfirmationManager.EnqueueSend(new Confirmation_AlterAttribute(player.Guid, Guid), transferMsg))
                    player.SendWeenieError(WeenieError.ConfirmationInProgress);
                return;
            }

            // CONQUEST: Recalculate EVERYTHING after confirmation in case attributes changed
            // Re-fetch current attribute values
            fromAttr = player.Attributes[TransferFromAttribute];
            toAttr = player.Attributes[TransferToAttribute];

            // Recalculate enlightenment bonus and innate values
            enlightenmentBonus = (uint)(player.Enlightenment > 0 ? player.Enlightenment : 0);

            fromTotal = fromAttr.StartingValue;
            toTotal = toAttr.StartingValue;

            // Prevent underflow - innate can't be negative
            fromInnate = fromTotal > enlightenmentBonus ? fromTotal - enlightenmentBonus : 0;
            toInnate = toTotal > enlightenmentBonus ? toTotal - enlightenmentBonus : 0;

            // Recalculate transfer amount
            fromAmount = Math.Min(10, (int)fromInnate - 10);
            toAmount = Math.Min(10, 100 - (int)toInnate);
            amount = Math.Min(fromAmount, toAmount);

            // Final safety check - must be positive and not exceed 100 cap
            if (amount <= 0)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Transfer cancelled - cannot transfer any points.", ChatMessageType.Broadcast));
                return;
            }

            if (toInnate + amount > 100)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Transfer cancelled - would exceed 100 innate cap on {TransferToAttribute}.", ChatMessageType.Broadcast));
                return;
            }

            // CONQUEST: Calculate what the new innate would be after transfer and verify it doesn't exceed 100
            var newToInnate = toInnate + (uint)amount;
            if (newToInnate > 100)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Transfer cancelled - would result in {newToInnate} innate {TransferToAttribute} (max 100).", ChatMessageType.Broadcast));
                return;
            }

            // CONQUEST: Also verify the from attribute won't go below 10
            var newFromInnate = fromInnate - (uint)amount;
            if (newFromInnate < 10)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Transfer cancelled - would result in {newFromInnate} innate {TransferFromAttribute} (min 10).", ChatMessageType.Broadcast));
                return;
            }

            fromAttr.StartingValue -= (uint)amount;
            toAttr.StartingValue += (uint)amount;

            var updateFrom = new GameMessagePrivateUpdateAttribute(player, fromAttr);
            var updateTo = new GameMessagePrivateUpdateAttribute(player, toAttr);

            //// begin things not seen in pcaps?
            //var msgFrom = new GameMessageSystemChat($"Your base {TransferFromAttribute} is now {fromAttr.Base}!", ChatMessageType.Broadcast);
            //var msgTo = new GameMessageSystemChat($"Your base {TransferToAttribute} is now {toAttr.Base}!", ChatMessageType.Broadcast);

            //var sound = new GameMessageSound(player.Guid, Sound.RaiseTrait);
            //// end things not seen in pcaps?
            //// the above provides better feedback to player but it wasn't seen in pcaps...

            //player.Session.Network.EnqueueSend(updateFrom, updateTo, msgFrom, msgTo, sound);
            player.Session.Network.EnqueueSend(updateFrom, updateTo);

            // this should be a UseDone(WeenieError.YouHaveSucceededTransferringAttributes) but Player.TryUseItem has built in UseDone(WeenieError.None) which would conflict.
            player.SendWeenieError(WeenieError.YouHaveSucceededTransferringAttributes);

            player.SaveBiotaToDatabase();

            player.TryConsumeFromInventoryWithNetworking(this, 1);
        }

        public bool VerifyRequirements(Player player, CreatureAttribute transferFromAttribute, CreatureAttribute transferToAttribute)
        {
            // Check for equipped items that have requirements in skill or attributes
            if (CheckWieldedItems(player))
            {
                // Items are wielded which might be affected by a transfer operation
                //player.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(player.Session, WeenieErrorWithString.CannotLowerSkillWhileWieldingItem, skill.Skill.ToSentence()));
                player.SendWeenieError(WeenieError.CannotTransferAttributesWhileWieldingItem);
                return false;
            }

            if (transferFromAttribute.StartingValue <= 10)
            {
                //player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your innate {TransferFromAttribute} must be above 10 to use the {Name}.", ChatMessageType.Broadcast));
                player.SendWeenieErrorWithString(WeenieErrorWithString.AttributeTransferFromTooLow, $"Your innate level of {TransferFromAttribute} is already as low as it can be. You may not reduce it any further.");
                return false;
            }

            if (transferToAttribute.StartingValue >= 100)
            {
                //player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your innate {TransferToAttribute} must be below 100 to use the {Name}.", ChatMessageType.Broadcast));
                player.SendWeenieErrorWithString(WeenieErrorWithString.AttributeTransferToTooHigh, $"Your innate level of {TransferFromAttribute} is already as high as it can be. You may not increase it any further.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks wielded items and their requirements to see if they'd be violated by an impending attribute transfer operation
        /// </summary>
        private bool CheckWieldedItems(Player player)
        {
            foreach (var equippedItem in player.EquippedObjects.Values)
            {
                if (CheckWieldRequirement(player, equippedItem.WieldRequirements, equippedItem.WieldSkillType, equippedItem.WieldDifficulty) ||
                    CheckWieldRequirement(player, equippedItem.WieldRequirements2, equippedItem.WieldSkillType2, equippedItem.WieldDifficulty2) ||
                    CheckWieldRequirement(player, equippedItem.WieldRequirements3, equippedItem.WieldSkillType3, equippedItem.WieldDifficulty3) ||
                    CheckWieldRequirement(player, equippedItem.WieldRequirements4, equippedItem.WieldSkillType4, equippedItem.WieldDifficulty4)) //||
                    //CheckActivationRequirements(player, equippedItem))
                {
                    return true;
                }
            }
            return false;
        }

        private bool CheckWieldRequirement(Player player, WieldRequirement itemWieldReq, int? wieldSkillType, int? wieldSkillDifficulty)
        {
            //if (itemWieldReq == WieldRequirement.RawSkill || itemWieldReq == WieldRequirement.Skill
            //    || itemWieldReq == WieldRequirement.RawAttrib || itemWieldReq == WieldRequirement.Attrib
            //    || itemWieldReq == WieldRequirement.RawSecondaryAttrib || itemWieldReq == WieldRequirement.SecondaryAttrib
            //    || itemWieldReq == WieldRequirement.Training)
            //if ((itemWieldReq >= WieldRequirement.Skill && itemWieldReq <= WieldRequirement.RawSecondaryAttrib) || itemWieldReq == WieldRequirement.Training)
            if (itemWieldReq == WieldRequirement.RawAttrib || itemWieldReq == WieldRequirement.Attrib)
            {
                return true;
            }

            return false;
        }

        private bool CheckActivationRequirements(Player player, WorldObject equippedItem)
        {
            if (equippedItem.ItemDifficulty > 0)
                return true;

            if (player.ConvertToMoASkill(equippedItem.ItemSkillLimit ?? 0) != Skill.None)
                return true;

            if (player.ConvertToMoASkill(equippedItem.ItemSpecializedOnly ?? 0) != Skill.None)
                return true;

            if (player.ConvertToMoASkill((Skill)(equippedItem.UseRequiresSkill ?? 0)) != Skill.None)
                return true;

            if (player.ConvertToMoASkill((Skill)(equippedItem.UseRequiresSkillSpec ?? 0)) != Skill.None)
                return true;

            return false;
        }
    }
}
