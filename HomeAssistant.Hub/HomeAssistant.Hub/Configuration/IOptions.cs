namespace HomeAssistant.Hub.Configuration
{
    public interface IOptions<TOptions> where TOptions : class, new()
    {
        TOptions Value { get; }
    }
}
