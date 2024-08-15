using DestinyLoadoutTool;
using DotNetBungieAPI.Models.Destiny;
using DotNetBungieAPI.Models.Destiny.Components;
using DotNetBungieAPI.Models.Destiny.Definitions.InventoryItems;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

internal static class D2LTHelpers
{
    public static IEnumerable<DestinyInventoryItemDefinition> ItemDefinitions = [];

    public static CharacterEquippingBlockSlot GetSlot(uint? Hash)
    {
        if (Hash is null)
            return CharacterEquippingBlockSlot.None;
        uint EquipHash = ItemDefinitions.FirstOrDefault(x => x.Hash == Hash)?.EquippingBlock?.EquipmentSlotType.Hash ?? 0;
        if (Enum.IsDefined(typeof(CharacterEquippingBlockSlot), EquipHash)) return (CharacterEquippingBlockSlot)EquipHash;
        return CharacterEquippingBlockSlot.None;
    }

    public static string GetName(uint? Hash)
    {
        if (Hash is null)
            return string.Empty;
        return ItemDefinitions.FirstOrDefault(x => x.Hash == Hash)?.DisplayProperties?.Name ?? string.Empty;
    }

    public static bool GetIsExotic(uint? Hash)
    {
        if (Hash is null)
            return false;
        return ItemDefinitions.FirstOrDefault(x => x.Hash == Hash)?.Inventory?.TierTypeEnumValue == ItemTierType.Exotic;
    }

    public static DestinyClass GetClass(uint? Hash)
    {
        if (Hash is null)
            return DestinyClass.Unknown;
        return ItemDefinitions.FirstOrDefault(x => x.Hash == Hash)?.ClassType ?? DestinyClass.Unknown;
    }

    public static List<D2LTLoadoutItem> GetD2LTLoadoutItem(DIMDestinyItem[] DIMItems)
    {
        List<D2LTLoadoutItem> items = [];
        foreach (DIMDestinyItem item in DIMItems)
        {
            items.Add(new D2LTLoadoutItem(item));
        }
        return items;
    }


    public static List<D2LTInventoryItem> GetD2LTInventoryItems(IEnumerable<DestinyItemComponent> Inventory, ReadOnlyDictionary<long, DestinyItemSocketsComponent> Sockets)
    {
        List<D2LTInventoryItem> items = [];
        foreach (DestinyItemComponent item in Inventory)
        {
            D2LTInventoryItem inventoryItem = new D2LTInventoryItem(Sockets, item) { Quantity = item.Quantity, TransferStatus = item.TransferStatus };
            if (inventoryItem.Slot == CharacterEquippingBlockSlot.Subclass ||(inventoryItem.TransferStatus & TransferStatuses.NotTransferrable) == 0 &&
                (item.Bucket.Hash == 138197802 || /* 138197802 Safe storage for general items. Accessible to all your characters.*/
                (item.Location == ItemLocation.Inventory && item.Bucket.Hash != 215593132 && inventoryItem.Slot != CharacterEquippingBlockSlot.None))) /*215593132 Lost Items*/
                items.Add(inventoryItem);
        }
        return items;
    }
}