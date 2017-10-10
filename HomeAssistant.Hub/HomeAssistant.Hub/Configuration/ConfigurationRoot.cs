namespace HomeAssistant.Hub.Configuration
{
    public class ConfigurationRoot : IConfigurationRoot
    {
        internal ConfigurationRoot() { }
        
        public IOptions<TOptions> GetSection<TOptions>(string sectionName) where TOptions : class, new()
        {
            throw new System.NotImplementedException();
        }
    }
}
