using Classes.DestinyApi;

namespace API.Clients.Abstract
{
    public interface IDestiny2ApiClient
    {
        public Task<DestinyApiResponse<UserSearchPrefixResponse>> PerformSearchByPrefix(UserSearchPrefixRequest player, int page);
        public Task<DestinyApiResponse<List<UserInfoCard>>> PerformSearchByBungieName(ExactSearchRequest player, int membershipTypeId);
        public Task<DestinyApiResponse<DestinyProfileResponse>> GetCharactersForPlayer(long membershipId, int membershipType);
        public Task<DestinyApiResponse<DestinyAggregateActivityResults>> GetActivityAggregateForCharacter(long membershipId, int membershipType, string characterId);
        public Task<DestinyApiResponse<DestinyActivityHistoryResults>> GetHistoricalStatsForCharacter(long membershipId, int membershipType, string characterId, int page, int activityCount);
    }
}
