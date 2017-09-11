namespace HomeAssistant.Hub.DependencyInjection
{
    public interface IServiceProvider
    {
        T GetService<T>() where T : class;
        T GetService<IT, T>() where T : class, IT where IT : class;

        //T GetService<IT>() where T : class, IT;

        void RegisterSingleton<T>() where T : class;
        void RegisterSingleton<T>(T service) where T : class;
        void RegisterTransient<T>() where T : class;
        void RegisterTransient<T>(T service) where T : class;
    }
}
