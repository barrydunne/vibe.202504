using Microsoft.Extensions.Options;

namespace TShirtStore.Api.Tests.TestUtils;

// Helper to mock IOptionsMonitor
public class TestOptionsMonitor<T> : IOptionsMonitor<T>
{
    public T CurrentValue { get; private set; }
    public TestOptionsMonitor(T currentValue) => CurrentValue = currentValue;
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
    public void UpdateOption(T value) => CurrentValue = value;
}
