namespace TShirtStore.Api.Options;

public class KeycloakOptions
{
    public required string Authority { get; init; } = string.Empty;
    public required string Audience { get; init; } = string.Empty;
    public required string Issuer { get; init; } = string.Empty;
}