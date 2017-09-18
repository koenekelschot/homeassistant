namespace HomeAssistant.Hub.DependencyInjection
{
    public interface IServiceCollection
    {
        IServiceCollection Configure();

        //Singleton lifetime services are created the first time they are requested 
        //and then every subsequent request will use the same instance.
        IServiceCollection AddSingleton<ServiceType>() where ServiceType : class;
        IServiceCollection AddSingleton<ServiceType>(ServiceType instance) where ServiceType : class;
        IServiceCollection AddSingleton<InterfaceType, ServiceType>() where ServiceType : class, InterfaceType where InterfaceType : class;
        IServiceCollection AddSingleton<InterfaceType, ServiceType>(ServiceType instance) where ServiceType : class, InterfaceType where InterfaceType : class;

        //Transient lifetime services are created each time they are requested. This 
        //lifetime works best for lightweight, stateless services.
        IServiceCollection AddTransient<ServiceType>() where ServiceType : class;
        IServiceCollection AddTransient<InterfaceType, ServiceType>() where ServiceType : class, InterfaceType where InterfaceType : class;

        IServiceProvider BuildServiceProvider();
    }
}
