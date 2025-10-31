using Domain.DestinyApi;

namespace API.Clients.Abstract
{
    public interface IDestiny2ApiClient
    {
        public Task<DestinyApiResponse<DestinyProfileResponse>> GetCharactersForPlayer(long membershipId, int membershipType, CancellationToken ct);
        public Task<DestinyApiResponse<DestinyActivityHistoryResults>> GetHistoricalStatsForCharacter(long membershipId, int membershipType, string characterId, int page, int activityCount, CancellationToken ct);
        public Task<DestinyApiResponse<PostGameCarnageReportData>> GetPostGameCarnageReport(long activityId, CancellationToken ct);
    }
}
