using API.Models.Responses;

namespace Domain.DTO.Responses
{
    public class LeaderboardResponse
    {
        public PlayerDto Player { get; set; } = new PlayerDto();
        public int Rank { get; set; }
        public string Data { get; set; } = string.Empty;
    }
}
