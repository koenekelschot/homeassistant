namespace HomeAssistant.Hub.DependencyInjection
{
    public class ServiceCollection : IServiceCollection
    {
        private readonly IServiceProvider _serviceProvider;

        public ServiceCollection()
        {
            _serviceProvider = new ServiceProvider();
        }
        
        public IServiceCollection AddSingleton<IT, T>() where T : class, IT where IT : class
        {
            ServiceHelper.ThrowIfNoInterface<IT>();
            return AddSingleton<T>();
        }

        public IServiceCollection AddSingleton<T>() where T : class
        {
            _serviceProvider.RegisterSingleton<T>();
            return this;
        }

        public IServiceCollection AddSingleton<IT, T>(T instance) where T : class, IT where IT : class
        {
            ServiceHelper.ThrowIfNoInterface<IT>();
            return AddSingleton<T>(instance);
        }

        public IServiceCollection AddSingleton<T>(T instance) where T : class
        {
            _serviceProvider.RegisterSingleton<T>(instance);
            return this;
        }

        public IServiceCollection AddTransient<IT, T>() where T : class, IT where IT : class
        {
            ServiceHelper.ThrowIfNoInterface<IT>();
            return AddTransient<T>();
        }

        public IServiceCollection AddTransient<T>() where T : class
        {
            _serviceProvider.RegisterTransient<T>();
            return this;
        }

        /*public IServiceCollection AddTransient<IT, T>(T instance) where T : class, IT where IT : class
        {
            ServiceHelper.ThrowIfNoInterface<IT>();
            return AddTransient<T>(instance);
        }

        public IServiceCollection AddTransient<T>(T instance) where T : class
        {
            _serviceProvider.RegisterTransient<T>(instance);
            return this;
        }*/

        public IServiceProvider BuildServiceProvider()
        {
            return _serviceProvider;
        }

        public IServiceCollection Configure()
        {
            return this;
        }
    }
}
