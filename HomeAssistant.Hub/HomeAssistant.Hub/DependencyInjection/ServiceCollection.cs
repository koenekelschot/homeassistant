namespace HomeAssistant.Hub.DependencyInjection
{
    public class ServiceCollection : IServiceCollection
    {
        private readonly IServiceProvider _serviceProvider;

        public ServiceCollection()
        {
            _serviceProvider = new ServiceProvider();
        }

        public IServiceCollection AddSingleton<ServiceType>() where ServiceType : class
        {
            return AddSingleton<ServiceType>(null);
        }

        public IServiceCollection AddSingleton<InterfaceType, ServiceType>() where ServiceType : class, InterfaceType where InterfaceType : class
        {
            return AddSingleton<InterfaceType, ServiceType>(null);
        }

        public IServiceCollection AddSingleton<ServiceType>(ServiceType instance) where ServiceType : class
        {
            ServiceHelper.ThrowIfInterface<ServiceType>();
            _serviceProvider.RegisterSingleton<ServiceType>(instance);
            return this;
        }

        public IServiceCollection AddSingleton<InterfaceType, ServiceType>(ServiceType instance) where ServiceType : class, InterfaceType where InterfaceType : class
        {
            ServiceHelper.ThrowIfNoInterface<InterfaceType>();
            ServiceHelper.ThrowIfInterface<ServiceType>();
            _serviceProvider.RegisterSingleton<InterfaceType, ServiceType>(instance);
            return this;
        }

        public IServiceCollection AddTransient<ServiceType>() where ServiceType : class
        {
            ServiceHelper.ThrowIfInterface<ServiceType>();
            _serviceProvider.RegisterTransient<ServiceType>();
            return this;
        }

        public IServiceCollection AddTransient<InterfaceType, ServiceType>() where ServiceType : class, InterfaceType where InterfaceType : class
        {
            ServiceHelper.ThrowIfNoInterface<InterfaceType>();
            ServiceHelper.ThrowIfInterface<ServiceType>();
            _serviceProvider.RegisterTransient<InterfaceType, ServiceType>();
            return this;
        }

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
