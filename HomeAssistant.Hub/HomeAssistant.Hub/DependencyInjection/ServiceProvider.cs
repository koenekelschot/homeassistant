using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HomeAssistant.Hub.DependencyInjection
{
    public class ServiceProvider : IServiceProvider
    {
        private readonly IDictionary<Type, Lifetime> _services;
        private readonly IDictionary<Type, object> _singletons;

        internal ServiceProvider()
        {
            _services = new Dictionary<Type, Lifetime>();
            _singletons = new Dictionary<Type, object>();
        }

        public T GetService<IT, T>() where T : class, IT where IT : class
        {
            ServiceHelper.ThrowIfNoInterface<IT>();
            return GetService<T>();
        }

        public T GetService<T>() where T : class
        {
            var typeT = typeof(T);
            ServiceHelper.ThrowIfInterface<T>();

            object instance = TryGetInstance(typeT);

            return (T)instance;
        }

        public void RegisterSingleton<T>() where T : class
        {
            RegisterService<T>(Lifetime.Singleton);
        }

        public void RegisterSingleton<T>(T service) where T : class
        {
            RegisterService<T>(service, Lifetime.Singleton);
        }

        public void RegisterTransient<T>() where T : class
        {
            RegisterService<T>(Lifetime.Transient);
        }

        /*public void RegisterTransient<T>(T service) where T : class
        {
            RegisterService<T>(service, Lifetime.Transient);
        }*/

        private void RegisterService<T>(T service, Lifetime lifeTime) where T : class
        {
            RegisterService<T>(lifeTime);
            _singletons.Add(typeof(T), service);
        }

        private void RegisterService<T>(Lifetime lifeTime) where T : class
        {
            var typeT = typeof(T);
            _services.Keys.ToList().ForEach(s => ServiceHelper.ThrowIfCanBeTreatedAsType(s, typeT));
            _services.Add(typeT, lifeTime);
        }

        private object TryGetInstance(Type typeT)
        {
            object instance;
            var returnType = _services.Keys.FirstOrDefault(s => ServiceHelper.CanBeTreatedAsType(s, typeT));
            if (returnType == null)
            {
                throw new InvalidOperationException($"No type registered for {typeT.Name}");
            }

            _services.TryGetValue(returnType, out Lifetime lifetime);
            if (lifetime == Lifetime.Singleton)
            {
                if (_singletons.Keys.Any(s => ServiceHelper.CanBeTreatedAsType(s, typeT)))
                {
                    _singletons.TryGetValue(returnType, out instance);
                }
                else
                {
                    instance = CreateInstanceOf(returnType);
                    _singletons.Add(typeT, instance);
                }
            }
            else
            {
                instance = CreateInstanceOf(returnType);
            }

            if (instance == null)
            {
                throw new TypeAccessException($"Could not create instance of type {typeT.Name}");
            }
            return instance;
        }

        private object CreateInstanceOf(Type returnType)
        {
            ConstructorInfo ctor = GetCtorWithFewestArguments(returnType); //shit I'm lazy
            if (ctor != null)
            {
                IList<object> parameters = new object[] { };

                if (!ctor.GetParameters().All(p => p.IsOptional))
                {
                    ParameterInfo[] requiredParams = ctor.GetParameters().Where(p => !p.IsOptional).ToArray();
                    
                    foreach (ParameterInfo requiredParam in requiredParams)
                    {
                        parameters.Add(TryGetInstance(requiredParam.ParameterType));
                    }
                }

                ctor.Invoke(parameters.ToArray());
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
