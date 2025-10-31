namespace Domain.Manifest
{
    public class DestinyActivityDefinition
    {
        public DestinyDisplayPropertiesDefinition displayProperties { get; set; }
        public DestinyDisplayPropertiesDefinition originalDisplayProperties { get; set; }
        public string releaseIcon { get; set; }
        public int releaseTime { get; set; }
        public uint completionUnlockHash { get; set; }
        public int activityLightLevel { get; set; }
        public uint destinationHash { get; set; }
        public uint placeHash { get; set; }
        public uint activityTypeHash { get; set; }
        public int tier { get; set; }
        public string pgcrImage { get; set; }
        public List<DestinyActivityReward> rewards { get; set; }
        public List<DestinyActivityModifier> modifiers { get; set; }
        public bool isPlaylist { get; set; }
        public List<object> challenges { get; set; }
        public List<Dictionary<string, string>> optionalUnlockStrings { get; set; }
        public bool inheritFromFreeRoam { get; set; }
        public List<uint> activityFamilyHashes { get; set; }
        public List<uint> traitHashes { get; set; }
        public bool suppressOtherRewards { get; set; }
        public List<uint> selectableSkullCollectionHashes { get; set; }
        public List<object> playlistItems { get; set; }
        public DestinyActivityMatchmaking matchmaking { get; set; }
        public uint? directActivityModeHash { get; set; }
        public int? directActivityModeType { get; set; }
        public List<uint> activityModeHashes { get; set; }
        public List<int> activityModeTypes { get; set; }
        public bool isPvP { get; set; }
        public List<object> insertionPoints { get; set; }
        public List<object> activityLocationMappings { get; set; }
        public uint hash { get; set; }
        public int index { get; set; }
        public bool redacted { get; set; }
        public bool blacklisted { get; set; }
    }

    public class DestinyDisplayPropertiesDefinition
    {
        public string description { get; set; }
        public string name { get; set; }
        public string icon { get; set; }
        public bool hasIcon { get; set; }
    }

    public class DestinyActivityReward
    {
        public string rewardText { get; set; }
        public List<DestinyActivityRewardItem> rewardItems { get; set; }
    }

    public class DestinyActivityRewardItem
    {
        public uint itemHash { get; set; }
        public int quantity { get; set; }
        public bool hasConditionalVisibility { get; set; }
    }

    public class DestinyActivityModifier
    {
        public uint activityModifierHash { get; set; }
    }

    public class DestinyActivityMatchmaking
    {
        public bool isMatchmade { get; set; }
        public int minParty { get; set; }
        public int maxParty { get; set; }
        public int maxPlayers { get; set; }
        public bool requiresGuardianOath { get; set; }
    }
}
