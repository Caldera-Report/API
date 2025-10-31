namespace Domain.DB
{
    public class Player
    {
        public required long Id { get; set; }
        public required int MembershipType { get; set; }
        public required string DisplayName { get; set; }
        public required int DisplayNameCode { get; set; }
        public string? LastPlayedCharacterEmblemPath { get; set; }
        public string? LastPlayedCharacterBackgroundPath { get; set; }
        public DateTime? LastUpdateStarted { get; set; }
        public DateTime? LastUpdateCompleted { get; set; }
        public string? LastUpdateStatus { get; set; }
        public bool NeedsFullCheck { get; set; }

        public ICollection<ActivityReportPlayer> ActivityReportPlayers { get; set; }
    }
}
