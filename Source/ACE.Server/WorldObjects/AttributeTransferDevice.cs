using System;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Network.GameMessages.Messages;

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
                return;

            var device = player.FindObject(Guid.Full, Player.SearchLocations.MyInventory);
            if (device == null) return;

            var fromAttr = player.Attributes[TransferFromAttribute];
            var toAttr = player.Attributes[TransferToAttribute];

            // CONQUEST: Calculate true innate values by subtracting enlightenment and augmentation bonuses
            // These bonuses add to StartingValue for display, but we don't want them
            // to count towards the 100 cap for stat swapping
            var enlightenmentBonus = (uint)(player.Enlightenment > 0 ? player.Enlightenment : 0);
            var fromAugBonus = (uint)(GetAugmentationBonus(player, TransferFromAttribute));
            var toAugBonus = (uint)(GetAugmentationBonus(player, TransferToAttribute));

            var fromInnate = fromAttr.StartingValue - enlightenmentBonus - fromAugBonus;
            var toInnate = toAttr.StartingValue - enlightenmentBonus - toAugBonus;

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

            if (!confirmed)
            {
                if (!player.ConfirmationManager.EnqueueSend(new Confirmation_AlterAttribute(player.Guid, Guid), $"This action will transfer 10 points from your {fromAttr.Attribute} to your {toAttr.Attribute}."))
                    player.SendWeenieError(WeenieError.ConfirmationInProgress);
                return;
            }

            // CONQUEST: Use true innate values for transfer calculations
            var fromAmount = Math.Min(10, fromInnate - 10);
            var toAmount = Math.Min(100 - toInnate, 10);

            var amount = Math.Min(fromAmount, toAmount);

            fromAttr.StartingValue -= amount;
            toAttr.StartingValue += amount;

            var updateFrom = new GameMessagePrivateUpdateAttribute(player, fromAttr);
            var updateTo = new GameMessagePrivateUpdateAttribute(player, toAttr);

            var msgFrom = new GameMessageSystemChat($"Your base {TransferFromAttribute} is now {fromAttr.Base}!", ChatMessageType.Broadcast);
            var msgTo = new GameMessageSystemChat($"Your base {TransferToAttribute} is now {toAttr.Base}!", ChatMessageType.Broadcast);

            var sound = new GameMessageSound(player.Guid, Sound.RaiseTrait);

            player.Session.Network.EnqueueSend(updateFrom, updateTo, msgFrom, msgTo, sound);

            player.SaveBiotaToDatabase();

            player.TryConsumeFromInventoryWithNetworking(this, 1);
        }

        /// <summary>
        /// CONQUEST: Gets the augmentation bonus for a specific attribute
        /// Each augmentation adds +5 to the attribute's StartingValue
        /// </summary>
        private static int GetAugmentationBonus(Player player, PropertyAttribute attribute)
        {
            int augCount = 0;
            switch (attribute)
            {
                case PropertyAttribute.Strength:
                    augCount = player.AugmentationInnateStrength;
                    break;
                case PropertyAttribute.Endurance:
                    augCount = player.AugmentationInnateEndurance;
                    break;
                case PropertyAttribute.Coordination:
                    augCount = player.AugmentationInnateCoordination;
                    break;
                case PropertyAttribute.Quickness:
                    augCount = player.AugmentationInnateQuickness;
                    break;
                case PropertyAttribute.Focus:
                    augCount = player.AugmentationInnateFocus;
                    break;
                case PropertyAttribute.Self:
                    augCount = player.AugmentationInnateSelf;
                    break;
            }
            return augCount * 5; // Each augmentation adds +5
        }
    }
}
