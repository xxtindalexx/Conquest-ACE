using System;
using System.Collections.Generic;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;

using MaterialTypeEnum = ACE.Entity.Enum.MaterialType;
using TinkerLogHelper = ACE.Server.Entity.TinkerLog;

namespace ACE.Server.WorldObjects
{
    public class ImbueStripTool : CraftTool
    {
        // CONQUEST: Powder of Purging
        public const uint ImbueStripToolWcid = 13370550;

        public const ImbuedEffectType PrimaryImbueMask =
            ImbuedEffectType.CriticalStrike |
            ImbuedEffectType.CripplingBlow |
            ImbuedEffectType.ArmorRending |
            ImbuedEffectType.SlashRending |
            ImbuedEffectType.PierceRending |
            ImbuedEffectType.BludgeonRending |
            ImbuedEffectType.AcidRending |
            ImbuedEffectType.ColdRending |
            ImbuedEffectType.ElectricRending |
            ImbuedEffectType.FireRending;

        private static readonly Dictionary<MaterialTypeEnum, ImbuedEffectType> MaterialPrimaryImbue = new Dictionary<MaterialTypeEnum, ImbuedEffectType>
        {
            { MaterialTypeEnum.BlackOpal, ImbuedEffectType.CriticalStrike },
            { MaterialTypeEnum.FireOpal, ImbuedEffectType.CripplingBlow },
            { MaterialTypeEnum.Sunstone, ImbuedEffectType.ArmorRending },
            { MaterialTypeEnum.Aquamarine, ImbuedEffectType.ColdRending },
            { MaterialTypeEnum.Jet, ImbuedEffectType.ElectricRending },
            { MaterialTypeEnum.RedGarnet, ImbuedEffectType.FireRending },
            { MaterialTypeEnum.BlackGarnet, ImbuedEffectType.PierceRending },
            { MaterialTypeEnum.WhiteSapphire, ImbuedEffectType.BludgeonRending },
            { MaterialTypeEnum.ImperialTopaz, ImbuedEffectType.SlashRending },
            { MaterialTypeEnum.Emerald, ImbuedEffectType.AcidRending },
        };

        public ImbueStripTool(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        public ImbueStripTool(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
            if (WeenieClassId != ImbueStripToolWcid)
                return;

            TargetedConsumableTool.ApplyUseOnTargetDefaults(this,
                ItemType.MeleeWeapon | ItemType.MissileWeapon | ItemType.Caster);
        }

        public static bool IsValidImbueStripTarget(WorldObject target)
        {
            if (target == null)
                return false;

            if (target.WeenieType != WeenieType.MeleeWeapon &&
                target.WeenieType != WeenieType.MissileLauncher &&
                target.WeenieType != WeenieType.Caster)
                return false;

            if (target.Workmanship == null)
                return false;

            if ((target.ImbuedEffect & PrimaryImbueMask) == 0)
                return false;

            if (target.NumTimesTinkered < 1)
                return false;

            return true;
        }

        public static MaterialTypeEnum? GetPrimaryImbueMaterial(ImbuedEffectType imbuedEffect)
        {
            var primary = imbuedEffect & PrimaryImbueMask;

            if (primary.HasFlag(ImbuedEffectType.CriticalStrike))
                return MaterialTypeEnum.BlackOpal;
            if (primary.HasFlag(ImbuedEffectType.CripplingBlow))
                return MaterialTypeEnum.FireOpal;
            if (primary.HasFlag(ImbuedEffectType.ArmorRending))
                return MaterialTypeEnum.Sunstone;
            if (primary.HasFlag(ImbuedEffectType.ColdRending))
                return MaterialTypeEnum.Aquamarine;
            if (primary.HasFlag(ImbuedEffectType.ElectricRending))
                return MaterialTypeEnum.Jet;
            if (primary.HasFlag(ImbuedEffectType.FireRending))
                return MaterialTypeEnum.RedGarnet;
            if (primary.HasFlag(ImbuedEffectType.PierceRending))
                return MaterialTypeEnum.BlackGarnet;
            if (primary.HasFlag(ImbuedEffectType.BludgeonRending))
                return MaterialTypeEnum.WhiteSapphire;
            if (primary.HasFlag(ImbuedEffectType.SlashRending))
                return MaterialTypeEnum.ImperialTopaz;
            if (primary.HasFlag(ImbuedEffectType.AcidRending))
                return MaterialTypeEnum.Emerald;

            return null;
        }

        public static ImbuedEffectType? GetPrimaryImbueType(ImbuedEffectType imbuedEffect)
        {
            var primary = imbuedEffect & PrimaryImbueMask;
            if (primary == 0)
                return null;

            if (primary.HasFlag(ImbuedEffectType.CriticalStrike))
                return ImbuedEffectType.CriticalStrike;
            if (primary.HasFlag(ImbuedEffectType.CripplingBlow))
                return ImbuedEffectType.CripplingBlow;
            if (primary.HasFlag(ImbuedEffectType.ArmorRending))
                return ImbuedEffectType.ArmorRending;
            if (primary.HasFlag(ImbuedEffectType.ColdRending))
                return ImbuedEffectType.ColdRending;
            if (primary.HasFlag(ImbuedEffectType.ElectricRending))
                return ImbuedEffectType.ElectricRending;
            if (primary.HasFlag(ImbuedEffectType.FireRending))
                return ImbuedEffectType.FireRending;
            if (primary.HasFlag(ImbuedEffectType.PierceRending))
                return ImbuedEffectType.PierceRending;
            if (primary.HasFlag(ImbuedEffectType.BludgeonRending))
                return ImbuedEffectType.BludgeonRending;
            if (primary.HasFlag(ImbuedEffectType.SlashRending))
                return ImbuedEffectType.SlashRending;
            if (primary.HasFlag(ImbuedEffectType.AcidRending))
                return ImbuedEffectType.AcidRending;

            return null;
        }

        public static uint? GetIconUnderlayForImbueType(ImbuedEffectType imbueType)
        {
            if (RecipeManager.IconUnderlay.TryGetValue(imbueType, out var icon))
                return icon;

            return null;
        }

        /// <summary>
        /// After stripping a primary imbue, set icon underlay from any remaining primary imbue on the item,
        /// else the most recent prior primary-imbue material in TinkerLog, else clear it.
        /// </summary>
        public static void UpdateIconUnderlayAfterStrip(Player player, WorldObject target)
        {
            uint? icon = null;

            var remainingPrimary = GetPrimaryImbueType(target.ImbuedEffect);
            if (remainingPrimary != null)
                icon = GetIconUnderlayForImbueType(remainingPrimary.Value);
            else if (!string.IsNullOrEmpty(target.TinkerLog))
            {
                var log = new TinkerLog(target.TinkerLog);
                for (var i = log.Tinkers.Count - 1; i >= 0; i--)
                {
                    if (!MaterialPrimaryImbue.TryGetValue(log.Tinkers[i], out var imbueType))
                        continue;

                    icon = GetIconUnderlayForImbueType(imbueType);
                    if (icon != null)
                        break;
                }
            }

            player.UpdateProperty(target, PropertyDataId.IconUnderlay, icon);
        }

        public static bool TryStripPrimaryImbue(WorldObject target)
        {
            if (!IsValidImbueStripTarget(target))
                return false;

            var material = GetPrimaryImbueMaterial(target.ImbuedEffect);
            if (material == null)
                return false;

            var imbueType = GetPrimaryImbueType(target.ImbuedEffect);
            if (imbueType == null)
                return false;

            target.ImbuedEffect &= ~imbueType.Value;
            target.NumTimesTinkered -= 1;
            target.TinkerLog = TinkerLogHelper.RemoveLast(target.TinkerLog, material.Value);

            return true;
        }

        public override void HandleActionUseOnTarget(Player player, WorldObject target)
        {
            if (WeenieClassId != ImbueStripToolWcid)
            {
                base.HandleActionUseOnTarget(player, target);
                return;
            }

            if (target is Player)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat("You cannot strip imbues from that.", ChatMessageType.Tell));
                player.SendUseDoneEvent();
                return;
            }

            if (target.WeenieType != WeenieType.MeleeWeapon &&
                target.WeenieType != WeenieType.MissileLauncher &&
                target.WeenieType != WeenieType.Caster)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat("This item cannot have its imbue stripped.", ChatMessageType.Tell));
                player.SendUseDoneEvent();
                return;
            }

            if (target.Workmanship == null)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat("This item cannot have its imbue stripped.", ChatMessageType.Tell));
                player.SendUseDoneEvent();
                return;
            }

            if ((target.ImbuedEffect & PrimaryImbueMask) == 0)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat("This item has no removable imbue.", ChatMessageType.Tell));
                player.SendUseDoneEvent();
                return;
            }

            if (target.NumTimesTinkered < 1)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat("This item has no tinkers to remove.", ChatMessageType.Tell));
                player.SendUseDoneEvent();
                return;
            }

            var animTime = 0.0f;

            var actionChain = new ActionChain();

            if (player.CombatMode != CombatMode.NonCombat)
            {
                var stanceTime = player.SetCombatMode(CombatMode.NonCombat);
                actionChain.AddDelaySeconds(stanceTime);
                animTime += stanceTime;
            }

            animTime += player.EnqueueMotion(actionChain, MotionCommand.ClapHands);

            actionChain.AddAction(player, ActionType.ImbueStripTool_ApplyStrip, () =>
            {
                try
                {
                    if (player.FindObject(Guid.Full, Player.SearchLocations.MyInventory) == null ||
                        player.FindObject(target.Guid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems) == null)
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat("The tool and target item must remain in your possession.", ChatMessageType.Tell));
                        player.SendUseDoneEvent();
                        return;
                    }

                    if (!IsValidImbueStripTarget(target))
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat("This item can no longer have its imbue stripped.", ChatMessageType.Tell));
                        player.SendUseDoneEvent();
                        return;
                    }

                    if (!player.TryConsumeFromInventoryWithNetworking(this, 1))
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat("The tool could not be consumed.", ChatMessageType.Tell));
                        player.SendUseDoneEvent();
                        return;
                    }

                    if (!TryStripPrimaryImbue(target))
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat("The imbue could not be stripped.", ChatMessageType.Tell));
                        player.SendUseDoneEvent();
                        return;
                    }

                    UpdateIconUnderlayAfterStrip(player, target);

                    player.EnqueueBroadcast(new GameMessageUpdateObject(target));

                    if (target.CurrentWieldedLocation != null)
                        player.EnqueueBroadcast(new GameMessageObjDescEvent(player));

                    player.SendMessage($"You strip the imbue from the {target.Name}.", ChatMessageType.Craft);
                }
                catch (Exception ex)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat("An error occurred while stripping the imbue.", ChatMessageType.Tell));
                    Console.WriteLine($"ImbueStripTool error: {ex}");
                }

                player.SendUseDoneEvent();
            });

            actionChain.EnqueueChain();

            player.NextUseTime = DateTime.UtcNow.AddSeconds(animTime);
        }
    }
}
