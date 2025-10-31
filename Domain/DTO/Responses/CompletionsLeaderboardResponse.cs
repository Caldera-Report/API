using API.Models.Responses;

namespace Domain.DTO.Responses
{
    public class CompletionsLeaderboardResponse
    {
        public PlayerDto Player { get; set; }
        public int Completions { get; set; }
    }
}
