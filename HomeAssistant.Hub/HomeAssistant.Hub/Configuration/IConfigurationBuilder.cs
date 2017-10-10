namespace HomeAssistant.Hub.Configuration
{
    public interface IConfigurationBuilder
    {
        IConfigurationBuilder SetBasePath(string path);

        IConfigurationBuilder AddJsonFile(string filename);
        IConfigurationBuilder AddJsonFile(string filename, bool optional);

        IConfigurationRoot Build();
    }
}
