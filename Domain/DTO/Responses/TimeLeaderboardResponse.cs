using API.Models.Responses;

namespace Domain.DTO.Responses
{
    public class TimeLeaderboardResponse
    {
        public PlayerDto Player { get; set; }
        public TimeSpan Time { get; set; }
    }
}
