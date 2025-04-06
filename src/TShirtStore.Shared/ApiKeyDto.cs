namespace TShirtStore.Shared;

public record ApiClientDto(string ClientId, string ApiKey, string Secret); // Secret would be hashed in real scenarios