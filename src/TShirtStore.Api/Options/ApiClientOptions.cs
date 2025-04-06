namespace TShirtStore.Api.Options;

public class ApiClientOptions
{
    public required string ClientId { get; init; } = string.Empty;
    public required string ApiKey { get; init; } = string.Empty;
    public required string SecretHash { get; init; } = string.Empty;
}
