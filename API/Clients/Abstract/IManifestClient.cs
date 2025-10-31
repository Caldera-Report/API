using Domain.Manifest;

namespace API.Clients.Abstract
{
    public interface IManifestClient
    {
        public Task<Dictionary<string, DestinyActivityDefinition>> GetActivityDefinitions(string url);
    }
}
