namespace API.Models.Responses
{
    public class TimeLeaderboardResponse
    {
        public PlayerDto Player { get; set; }
        public TimeSpan Time { get; set; }
    }
}
