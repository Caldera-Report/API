namespace Domain.DestinyApi
{
    public class UserMembershipData
    {
        public Destinymembership[] destinyMemberships { get; set; }
        public Bungienetuser bungieNetUser { get; set; }
    }

    public class Bungienetuser
    {
        public string membershipId { get; set; }
        public string uniqueName { get; set; }
        public string displayName { get; set; }
        public int profilePicture { get; set; }
        public int profileTheme { get; set; }
        public int userTitle { get; set; }
        public string successMessageFlags { get; set; }
        public bool isDeleted { get; set; }
        public string about { get; set; }
        public DateTime firstAccess { get; set; }
        public DateTime lastUpdate { get; set; }
        public string xboxDisplayName { get; set; }
        public bool showActivity { get; set; }
        public string locale { get; set; }
        public bool localeInheritDefault { get; set; }
        public bool showGroupMessaging { get; set; }
        public string profilePicturePath { get; set; }
        public string profileThemeName { get; set; }
        public string userTitleDisplay { get; set; }
        public string statusText { get; set; }
        public DateTime statusDate { get; set; }
        public string twitchDisplayName { get; set; }
        public string cachedBungieGlobalDisplayName { get; set; }
        public int cachedBungieGlobalDisplayNameCode { get; set; }
    }

    public class Destinymembership
    {
        public string LastSeenDisplayName { get; set; }
        public int LastSeenDisplayNameType { get; set; }
        public string iconPath { get; set; }
        public int crossSaveOverride { get; set; }
        public int[] applicableMembershipTypes { get; set; }
        public bool isPublic { get; set; }
        public int membershipType { get; set; }
        public string membershipId { get; set; }
        public string displayName { get; set; }
        public string bungieGlobalDisplayName { get; set; }
        public int bungieGlobalDisplayNameCode { get; set; }
    }
}
