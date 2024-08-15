using DotNetBungieAPI.Models.Destiny.Components;
using DotNetBungieAPI.Models.Destiny.Definitions.InventoryItems;
using DotNetBungieAPI.Models.Destiny;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DestinyLoadoutTool
{
    public enum CharacterEquippingBlockSlot : uint
    {
        None,
        Subclass = 3284755031,
        KineticWeapon = 1498876634,
        EnergyWeapon = 2465295065,
        PowerWeapon = 953998645,
        Helmet = 3448274439,
        Gauntlet = 3551918588,
        ChestArmor = 14239492,
        LegArmor = 20886954,
        ClassArmor = 1585787867
    }

    public class StringPlugCategoryIdentifier
    {
        public static readonly string Head = "enhancements.v2_head";
        public static readonly string Arms = "enhancements.v2_arms";
        public static readonly string Chest = "enhancements.v2_chest";
        public static readonly string Legs = "enhancements.v2_legs";
        public static readonly string ClassItem = "enhancements.v2_class_item";
        public static readonly string General = "enhancements.v2_general";
        public static readonly string Artifice = "enhancements.artifice";

        public static readonly string[] RelevantPlugs =
        {
            Head,
            Arms,
            Chest,
                Legs,
            ClassItem,
            General,
            Artifice
        };
        public static string GetCategoryFromEquippingBlock(CharacterEquippingBlockSlot slot)
        {
            switch (slot)
            {

                case CharacterEquippingBlockSlot.Helmet: return Head;
                case CharacterEquippingBlockSlot.Gauntlet: return Arms;
                case CharacterEquippingBlockSlot.ChestArmor: return Chest;
                case CharacterEquippingBlockSlot.LegArmor: return Legs;
                case CharacterEquippingBlockSlot.ClassArmor: return ClassItem;
                default: return string.Empty;
            }
        }
    }


    public class D2LTItem
    {
        public long? InstanceId { get; }
        public uint Hash { get; }
        public string Name { get => D2LTHelpers.GetName(Hash); }
        public CharacterEquippingBlockSlot Slot { get => D2LTHelpers.GetSlot(Hash); }
        public DestinyClass ClassRestriction { get => D2LTHelpers.GetClass(Hash); }
        public bool IsExotic { get => D2LTHelpers.GetIsExotic(Hash); }

        public D2LTItem(DIMDestinyItem item)
        {
            Hash = item.Hash;
            InstanceId = item.Id;
        }

        public D2LTItem(DestinyItemComponent item)
        {
            Hash = (uint)item.Item.Hash!;
            InstanceId = item.ItemInstanceId;
        }
    }

    public class D2LTLoadoutItem : D2LTItem
    {
        public Dictionary<ulong, uint>? SocketOverrides { get; }

        public D2LTLoadoutItem(DIMDestinyItem item):base(item)
        {

            SocketOverrides = item.SocketOverrides;
        }

    }

    public class D2LTInventoryItem : D2LTItem
    {
        public int Quantity { get; init; }
        public TransferStatuses TransferStatus { get; set; }
        public uint?[] PlugsHashes { get; set; } = [];
        public D2LTPlugInfo?[] Plugs { get => PlugsHashes.Select(x => (x is not null) ? new D2LTPlugInfo((uint)x) : null).ToArray(); }

        public D2LTInventoryItem(ReadOnlyDictionary<long, DestinyItemSocketsComponent> Sockets, DestinyItemComponent item) : base(item)
        {
            if (InstanceId is not null && Sockets.ContainsKey((long)InstanceId))
            {
                PlugsHashes = Sockets[(long)InstanceId].Sockets.Select(x => x.Plug.Hash).ToArray();
            }

        }
    }

    public class D2LTPlugInfo
    {
        public string Name { get => D2LTHelpers.ItemDefinitions.FirstOrDefault(y => y.Hash == Hash)!.DisplayProperties.Name; }
        public string? PlugCategoryIdentifier { get => D2LTHelpers.ItemDefinitions.FirstOrDefault(y => y.Hash == Hash)!.Plug.PlugCategoryIdentifier; }
        public uint Hash { get; }
        public int Cost { get => D2LTHelpers.ItemDefinitions.FirstOrDefault(y => y.Hash == Hash)!.Plug.EnergyCost?.EnergyCost ?? 0; }

        public D2LTPlugInfo(uint PlugHash)
        {
            Hash = PlugHash;
        }
    }
}