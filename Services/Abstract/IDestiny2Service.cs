using API.Models.Responses;

namespace API.Services.Abstract
{
    public interface IDestiny2Service
    {
        public Task<SearchResponse> SearchForPlayer(string playerName);
    }
}
