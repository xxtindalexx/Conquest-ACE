using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class Container
    {
        public override void Heartbeat(double currentUnixTime)
        {
            // TODO: fix bug for landblock containers w/ no heartbeat
            Inventory_Tick(this);

            var subcontainers = Inventory.Values.OfType<Container>().ToList();
            foreach (var subcontainer in subcontainers)
                subcontainer.Inventory_Tick(this);

            // for landblock containers
            if (IsOpen && CurrentLandblock != null)
            {
                var viewer = CurrentLandblock.GetObject(Viewer) as Player;
                if (viewer == null)
                {
                    Close(null);
                    return;
                }
                var withinUseRadius = CurrentLandblock.WithinUseRadius(viewer, Guid, out var targetValid);
                if (!withinUseRadius)
                {
                    Close(viewer);
                    return;
                }
            }
            base.Heartbeat(currentUnixTime);
        }

        public void Inventory_Tick(Container rootOwner)
        {
            var expireItems = new List<WorldObject>();

            // added where clause
            var items = Inventory.Values
                                 .Where(i => i.EnchantmentManager.HasEnchantments || i.Lifespan.HasValue)
                                 .ToList();

            foreach (var wo in items)
            {
                // FIXME: wo.NextHeartbeatTime is double.MaxValue here
                //if (wo.NextHeartbeatTime <= currentUnixTime)
                    //wo.Heartbeat(currentUnixTime);

                // just go by parent heartbeats, only for enchantments?
                if (wo.EnchantmentManager.HasEnchantments)
                    wo.EnchantmentManager.HeartBeat(CachedHeartbeatInterval);

                if (wo.IsLifespanSpent)
                    expireItems.Add(wo);
            }

            // delete items when RemainingLifespan <= 0
            foreach (var expireItem in expireItems)
            {
                // CONQUEST: Auto-hatch mystery eggs instead of deleting them
                // Check for EggRarity property since MysteryEgg class may not be used
                var eggRarity = expireItem.GetProperty(PropertyInt.EggRarity);
                if (eggRarity != null && rootOwner is Player eggOwner)
                {
                    MysteryEgg.TryAutoHatch(expireItem, eggOwner);
                    continue;
                }

                expireItem.DeleteObject(rootOwner);

                if (rootOwner is Player player)
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Its lifespan finished, your {expireItem.Name} crumbles to dust.", ChatMessageType.Broadcast));
            }
        }
    }
}
