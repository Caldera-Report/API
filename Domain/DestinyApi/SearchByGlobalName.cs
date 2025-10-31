namespace Domain.DestinyApi
{
    public class UserSearchPrefixRequest
    {
        public string displayNamePrefix { get; set; }
    }

    public class UserSearchPrefixResponse
    {
        public List<UserSearchResponseDetail> searchResults { get; set; }
        public int page { get; set; }
        public bool hasMore { get; set; }
    }

    public class UserSearchResponseDetail
    {
        public string bungieGlobalDisplayName { get; set; }
        public int bungieGlobalDisplayNameCode { get; set; }
        public List<UserInfoCard> destinyMemberships { get; set; }
    }
}

