    using System;
    using System.Collections.Generic;
    using System.Globalization;
using DotNetBungieAPI.Models.Destiny;
using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;


namespace DestinyLoadoutTool
{

    public partial class DIMLoadout
    {
        [JsonProperty("platformMembershipId")]
        public required long PlatformMembershipId { get; set; }

        [JsonProperty("destinyVersion")]
        public ulong DestinyVersion { get; set; }

        [JsonProperty("loadout")]
        public required DIMLoadoutDefinition Loadout { get; set; }
    }

    public partial class DIMLoadoutDefinition
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("name")]
        public required string Name { get; set; }

        [JsonProperty("classType")]
        public DestinyClass ClassType { get; set; }

        [JsonProperty("clearSpace")]
        public bool ClearSpace { get; set; }

        [JsonProperty("equipped")]
        public required DIMDestinyItem[] Equipped { get; set; } = [];

        [JsonProperty("unequipped")]
        public required DIMDestinyItem[] Unequipped { get; set; } = [];

        [JsonProperty("createdAt")]
        public ulong CreatedAt { get; set; }

        [JsonProperty("lastUpdatedAt")]
        public ulong LastUpdatedAt { get; set; }

        [JsonProperty("parameters")]
        public Parameters? Parameters { get; set; }
    }

    public partial class DIMDestinyItem
    {
        [JsonProperty("id")]
        public required long Id { get; set; }

        [JsonProperty("hash")]
        public uint Hash { get; set; }

        [JsonProperty("socketOverrides", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<ulong, uint>? SocketOverrides { get; set; }

        [JsonProperty("craftedDate", NullValueHandling = NullValueHandling.Ignore)]
        public ulong? CraftedDate { get; set; }
    }

    public partial class Parameters
    {
        [JsonProperty("modsByBucket", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<ulong, uint[]>? ModsByBucket { get; set; }

        [JsonProperty("mods", NullValueHandling = NullValueHandling.Ignore)]
        public uint[]? Mods { get; set; }

        [JsonProperty("exoticArmorHash", NullValueHandling = NullValueHandling.Ignore)]
        public ulong? ExoticArmorHash { get; set; }

        [JsonProperty("statConstraints", NullValueHandling = NullValueHandling.Ignore)]
        public StatConstraint[]? StatConstraints { get; set; }

        [JsonProperty("assumeArmorMasterwork", NullValueHandling = NullValueHandling.Ignore)]
        public uint? AssumeArmorMasterwork { get; set; }

        [JsonProperty("autoStatMods", NullValueHandling = NullValueHandling.Ignore)]
        public bool? AutoStatMods { get; set; }

        [JsonProperty("includeRuntimeStatBenefits", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IncludeRuntimeStatBenefits { get; set; }

        [JsonProperty("lockArmorEnergyType", NullValueHandling = NullValueHandling.Ignore)]
        public uint? LockArmorEnergyType { get; set; }
    }

    public partial class StatConstraint
    {
        [JsonProperty("maxTier", NullValueHandling = NullValueHandling.Ignore)]
        public short? MaxTier { get; set; }

        [JsonProperty("minTier")]
        public short? MinTier { get; set; }

        [JsonProperty("statHash")]
        public uint StatHash { get; set; }
    }
}
