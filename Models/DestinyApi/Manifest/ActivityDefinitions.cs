namespace API.Models.DestinyApi.Manifest
{
    public class ActivityDefinitions
    {
        public Dictionary<string, ActivityDefinition> ActivityDefinition { get; set; }
    }
    public class ActivityDefinition
    {
        public DisplayProperties DisplayProperties { get; set; }
        public DisplayProperties OriginalDisplayProperties { get; set; }
        public string ReleaseIcon { get; set; }
        public int ReleaseTime { get; set; }
        public uint CompletionUnlockHash { get; set; }
        public int ActivityLightLevel { get; set; }
        public uint ActivityDestinationHash { get; set; }
        public uint PlaceHash { get; set; }
        public uint ActivityTypeHash { get; set; }
        public int Tier { get; set; }
        public bool IsPlaylist { get; set; }
    }
    public class DisplayProperties
    { 
        public string Description { get; set; }
        public string Name { get; set; }
        public string Icon {  get; set; }
        public string HasIcon { get; set; }
    }
}
