using DotNetBungieAPI.Models.Destiny;
using DotNetBungieAPI.Models.Destiny.Definitions.InventoryItems;
using System.Collections.Generic;
using System.Linq;


namespace DestinyLoadoutTool
{
    public class D2LTLoadout
    {
        public bool Selected { get; set; }
        public string Name { get; }
        public DestinyClass ClassType { get; }
        public long PlatformMembershipId { get;  }
        public IEnumerable<D2LTLoadoutItem> Equipped { get; }
        public IEnumerable<D2LTLoadoutItem> Unequipped { get; }
        public Dictionary<ulong, uint[]> Fashion { get;  }
        public uint[] Mods { get; }

        public bool ArmorLoadout { get => Equipped.Where(x => x.Slot == CharacterEquippingBlockSlot.Helmet).Any() && Equipped.Where(x => x.Slot == CharacterEquippingBlockSlot.Gauntlet).Any() && Equipped.Where(x => x.Slot == CharacterEquippingBlockSlot.ChestArmor).Any() && Equipped.Where(x => x.Slot == CharacterEquippingBlockSlot.LegArmor).Any() && Equipped.Where(x => x.Slot == CharacterEquippingBlockSlot.ClassArmor).Any(); }
        public bool WeaponsLoadout { get => Equipped.Where(x => x.Slot == CharacterEquippingBlockSlot.KineticWeapon).Any() && Equipped.Where(x => x.Slot == CharacterEquippingBlockSlot.EnergyWeapon).Any() && Equipped.Where(x => x.Slot == CharacterEquippingBlockSlot.PowerWeapon).Any(); }
        public bool FullLoadout { get => ArmorLoadout && WeaponsLoadout; }
        public D2LTLoadout(DIMLoadout loadout)
        {
            Name = loadout.Loadout.Name;
            PlatformMembershipId = loadout.PlatformMembershipId;
            ClassType = loadout.Loadout.ClassType;
            Equipped = D2LTHelpers.GetD2LTLoadoutItem( loadout.Loadout.Equipped);
            Unequipped = D2LTHelpers.GetD2LTLoadoutItem(loadout.Loadout.Unequipped);
            Mods = loadout.Loadout.Parameters?.Mods ?? [];
            Fashion = loadout.Loadout.Parameters?.ModsByBucket ?? [];
        }
    }
}
