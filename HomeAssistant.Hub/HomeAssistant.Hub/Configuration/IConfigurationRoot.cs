namespace HomeAssistant.Hub.Configuration
{
    public interface IConfigurationRoot
    {
        IOptions<TOptions> GetSection<TOptions>(string sectionName) where TOptions : class, new();
    }
}
