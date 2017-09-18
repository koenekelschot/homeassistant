namespace HomeAssistant.Hub.DependencyInjection
{
    public interface IServiceProvider
    {
        ServiceType GetService<ServiceType>() where ServiceType : class;

        void RegisterSingleton<ServiceType>(ServiceType instance) where ServiceType : class;
        void RegisterSingleton<InterfaceType, ServiceType>(ServiceType instance) where ServiceType : class, InterfaceType where InterfaceType : class;

        void RegisterTransient<ServiceType>() where ServiceType : class;
        void RegisterTransient<InterfaceType, ServiceType>() where ServiceType : class, InterfaceType where InterfaceType : class;
    }
}
