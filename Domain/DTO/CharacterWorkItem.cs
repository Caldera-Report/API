namespace Domain.DTO
{
    public class CharacterWorkItem
    {
        public long PlayerId { get; }
        public string CharacterId { get; }

        public CharacterWorkItem(long playerId, string characterId)
        {
            PlayerId = playerId;
            CharacterId = characterId;
        }
    }
}
