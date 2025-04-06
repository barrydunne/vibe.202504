namespace TShirtStore.Api.Options;

public class StripeOptions
{
    public required string SecretKey { get; init; } = string.Empty;
    public required string ApiBase { get; init; } = string.Empty;
}
