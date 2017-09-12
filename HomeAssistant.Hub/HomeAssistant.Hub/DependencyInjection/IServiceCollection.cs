namespace HomeAssistant.Hub.DependencyInjection
{
    public interface IServiceCollection
    {
        IServiceCollection Configure();

        //Singleton lifetime services are created the first time they are requested 
        //and then every subsequent request will use the same instance.
        IServiceCollection AddSingleton<T>() where T : class;
        IServiceCollection AddSingleton<T>(T instance) where T : class;
        IServiceCollection AddSingleton<IT, T>() where T : class, IT where IT : class;
        IServiceCollection AddSingleton<IT, T>(T instance) where T : class, IT where IT : class;

        //Scoped lifetime services are created once per request.
        //IServiceCollection AddScoped<T>() where T : class;
        //IServiceCollection AddScoped<IT, T>() where T : class, IT;

        //Transient lifetime services are created each time they are requested. This 
        //lifetime works best for lightweight, stateless services.
        IServiceCollection AddTransient<T>() where T : class;
        //IServiceCollection AddTransient<T>(T instance) where T : class;
        IServiceCollection AddTransient<IT, T>() where T : class, IT where IT : class;
        //IServiceCollection AddTransient<IT, T>(T instance) where T : class, IT where IT : class;

        IServiceProvider BuildServiceProvider();
    }
}
