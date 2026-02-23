using System;

using ACE.Database;
using ACE.Entity.Models;
using ACE.Server.WorldObjects;

namespace ACE.Server.Network.GameEvent.Events
{
    public class GameEventApproachVendor : GameEventMessage
    {
        // Pyreal coin stack WCID for bank mode hack
        private const uint PYREAL_WCID = 273;

        public GameEventApproachVendor(Session session, Vendor vendor, uint altCurrencySpent)
            : base(GameEventType.ApproachVendor, GameMessageGroup.UIQueue, session)
        {
            Writer.Write(vendor.Guid.Full);

            // the types of items vendor will purchase
            Writer.Write((uint)vendor.MerchandiseItemTypes);
            Writer.Write((uint)vendor.MerchandiseMinValue);
            Writer.Write((uint)vendor.MerchandiseMaxValue);

            Writer.Write(Convert.ToUInt32(vendor.DealMagicalItems ?? false));

            Writer.Write((float)vendor.BuyPrice);
            Writer.Write((float)vendor.SellPrice);

            // CONQUEST: Determine alternate currency WCID
            // If VendorBankMode is enabled and this is a pyreal vendor, fake it as alternate currency
            // so the client uses our provided total instead of local CoinValue
            uint alternateCurrencyWcid;
            if (vendor.AlternateCurrency != null)
            {
                alternateCurrencyWcid = vendor.AlternateCurrency.Value;
            }
            else if (session.Player.VendorBankMode && (session.Player.BankedPyreals ?? 0) > 0)
            {
                // Trick: Send pyreal WCID as alternate currency so client uses our total
                alternateCurrencyWcid = PYREAL_WCID;
            }
            else
            {
                alternateCurrencyWcid = 0;
            }

            Writer.Write(alternateCurrencyWcid);

            // if this vendor accepts items as alternate currency, instead of pyreals
            if (vendor.AlternateCurrency != null)
            {
                var altCurrency = DatabaseManager.World.GetCachedWeenie(vendor.AlternateCurrency.Value);
                var pluralName = altCurrency.GetPluralName();

                // the total amount of alternate currency the player currently has
                var altCurrencyInInventory = (uint)session.Player.GetNumInventoryItemsOfWCID(vendor.AlternateCurrency.Value);

                // CONQUEST: If VendorBankMode is enabled, include banked currency in total sent to client
                if (session.Player.VendorBankMode)
                {
                    var bankedAltCurrency = session.Player.GetBankedAlternateCurrency(vendor.AlternateCurrency.Value);
                    var totalFunds = altCurrencyInInventory + (uint)bankedAltCurrency;
                    Writer.Write(totalFunds + altCurrencySpent);
                }
                else
                {
                    Writer.Write(altCurrencyInInventory + altCurrencySpent);
                }

                // the plural name of alt currency
                Writer.WriteString16L(pluralName);
            }
            else if (alternateCurrencyWcid == PYREAL_WCID)
            {
                // CONQUEST: VendorBankMode hack - send pyreals as alternate currency
                var bankedPyreals = session.Player.BankedPyreals ?? 0;
                var inventoryPyreals = session.Player.CoinValue ?? 0;
                var totalFunds = (uint)(inventoryPyreals + bankedPyreals);

                Writer.Write(totalFunds);
                Writer.WriteString16L("Pyreals");
            }
            else
            {
                Writer.Write(0);
                Writer.WriteString16L(string.Empty);
            }

            var numItems = vendor.DefaultItemsForSale.Count + vendor.UniqueItemsForSale.Count;

            Writer.Write(numItems);

            vendor.forEachItem((obj) =>
            {
                int stackSize = obj.VendorShopCreateListStackSize ?? obj.StackSize ?? 1; // -1 = unlimited supply

                // packed value: (stackSize & 0xFFFFFF) | (pwdType << 24)
                // pwdType: flag indicating whether the new or old PublicWeenieDesc is used; -1 = PublicWeenieDesc, 1 = OldPublicWeenieDesc; -1 always used.
                Writer.Write(stackSize & 0xFFFFFF | -1 << 24);

                obj.SerializeGameDataOnly(Writer);
            });

            Writer.Align();
        }
    }
}
