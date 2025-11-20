namespace Domain.DTO
{
    public class CharacterWorkItem(long playerId, string characterId, DateTime lastPlayed)
    {
        public long PlayerId { get; } = playerId;
        public string CharacterId { get; } = characterId;
        public DateTime LastPlayed { get; } = DateTime.UtcNow;
    }
}
