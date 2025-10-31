namespace Domain.DestinyApi
{
    public class ExactSearchRequest
    {
        public string displayName { get; set; }
        public int displayNameCode { get; set; }
    }

    public class UserInfoCard
    {
        public List<int> applicableMembershipTypes { get; set; }
        public bool isPublic { get; set; }
        public int membershipType { get; set; }
        public string membershipId { get; set; }
        public string bungieGlobalDisplayName { get; set; }
        public int bungieGlobalDisplayNameCode { get; set; }
    }
}
