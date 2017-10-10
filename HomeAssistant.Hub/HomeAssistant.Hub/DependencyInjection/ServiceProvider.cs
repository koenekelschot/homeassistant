using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HomeAssistant.Hub.DependencyInjection
{
    public class ServiceProvider : IServiceProvider
    {
        private readonly IDictionary<Type, Lifetime> _serviceTypes;
        private readonly IDictionary<Type, object> _singletonInstances;

        internal ServiceProvider()
        {
            _serviceTypes = new Dictionary<Type, Lifetime>();
            _singletonInstances = new Dictionary<Type, object>();
        }

        public ServiceType GetService<ServiceType>() where ServiceType : class
        {
            var serviceType = typeof(ServiceType);
            object instance = TryGetInstance(serviceType, new List<Type>());
            return (ServiceType)instance;
        }

        public void RegisterSingleton<ServiceType>(ServiceType instance) where ServiceType : class
        {
            RegisterService<ServiceType>(instance, Lifetime.Singleton);
        }

        public void RegisterSingleton<InterfaceType, ServiceType>(ServiceType instance) where ServiceType : class, InterfaceType where InterfaceType : class
        {
            CheckInterfaceAlreadyRegistered<InterfaceType>();
            RegisterSingleton<ServiceType>(instance);
        }

        public void RegisterTransient<ServiceType>() where ServiceType : class
        {
            RegisterService<ServiceType>(null, Lifetime.Transient);
        }

        public void RegisterTransient<InterfaceType, ServiceType>() where ServiceType : class, InterfaceType where InterfaceType : class
        {
            CheckInterfaceAlreadyRegistered<InterfaceType>();
            RegisterTransient<ServiceType>();
        }

        private void RegisterService<ServiceType>(ServiceType instance, Lifetime lifetime) where ServiceType : class
        {
            var serviceType = typeof(ServiceType);
            _serviceTypes.Keys.ToList().ForEach(st => ServiceHelper.ThrowIfCanBeTreatedAsType(st, serviceType));
            _serviceTypes.Add(serviceType, lifetime);

            if (instance != null && !_singletonInstances.Keys.Any(s => ServiceHelper.CanBeTreatedAsType(s, serviceType)))
            {
                _singletonInstances.Add(serviceType, instance);
            }
        }

        private void CheckInterfaceAlreadyRegistered<InterfaceType>() where InterfaceType : class
        {
            var interfaceType = typeof(InterfaceType);
            foreach (var serviceType in _serviceTypes.Keys)
            {
                var interfaces = serviceType.GetInterfaces();
                interfaces.ToList().ForEach(i => ServiceHelper.ThrowIfCanBeTreatedAsType(interfaceType, i));
            }
        }

        private object TryGetInstance(Type serviceType, IList<Type> typeStack)
        {
            if (typeStack.Contains(serviceType))
            {
                throw new TypeLoadException($"Type {serviceType.Name} contains circular references");
            }
            typeStack.Add(serviceType);
            
            var returnType = _serviceTypes.Keys.FirstOrDefault(s => ServiceHelper.CanBeTreatedAsType(s, serviceType));
            if (returnType == null)
            {
                throw new TypeLoadException($"No type registered for {serviceType.Name}");
            }

            object instance = GetInstanceOf(serviceType, typeStack);
            if (instance == null)
            {
                throw new TypeLoadException($"Could not create instance of type {serviceType.Name}");
            }
            return instance;
        }

        private object GetInstanceOf(Type serviceType, IList<Type> typeStack)
        {
            _serviceTypes.TryGetValue(serviceType, out Lifetime lifetime);
            if (lifetime == Lifetime.Transient)
            {
                return CreateInstanceOf(serviceType, typeStack);
            }

            object instance;
            if (_singletonInstances.Keys.Any(s => ServiceHelper.CanBeTreatedAsType(s, serviceType)))
            {
                _singletonInstances.TryGetValue(serviceType, out instance);
            }
            else
            {
                instance = CreateInstanceOf(serviceType, typeStack);
                _singletonInstances.Add(serviceType, instance);
            }
            return instance;
        }

        private object CreateInstanceOf(Type serviceType, IList<Type> typeStack)
        {
            ConstructorInfo ctor = GetCtorWithFewestArguments(serviceType); //shit I'm lazy
            if (ctor != null)
            {
                var parameterValues = new List<object>();

                var allParameters = ctor.GetParameters();
                var requiredParams = allParameters.Where(p => !p.IsOptional);
                var optionalParams = allParameters.Where(p => p.IsOptional);

                foreach (ParameterInfo requiredParam in requiredParams)
                {
                    parameterValues.Add(TryGetInstance(requiredParam.ParameterType, typeStack));
                }
                foreach (ParameterInfo optionalParam in optionalParams)
                {
                    parameterValues.Add(Type.Missing);
                }

                return ctor.Invoke(parameterValues.ToArray());
            }

            return null;
        }

        private enum Lifetime
        {
            Singleton,
            Transient
        }

        private ConstructorInfo GetCtorWithFewestArguments(Type type)
        {
            var constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                ConstructorInfo[] ctors = type.GetConstructors();
                var optionalCtors =
                    ctors.Where(ctor => ctor.GetParameters().All(p => p.IsOptional))
                        .OrderBy(ctor => ctor.GetParameters().Length);
                constructor = ctors.FirstOrDefault();

                if (constructor == null)
                {
                    ctors.OrderBy(ctor => ctor.GetParameters().Length).FirstOrDefault();
                }
            }
            return constructor;
        }
    }
}
