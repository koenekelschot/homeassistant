using System;
using System.Collections.Generic;

namespace HomeAssistant.Hub.DependencyInjection
{
    public class ServiceProvider : IServiceProvider
    {
        private readonly IDictionary<Type, Scope> _services;

        internal ServiceProvider()
        {
            _services = new Dictionary<Type, Scope>();
        }

        public T GetService<IT, T>() where T : class, IT where IT : class
        {
            ServiceHelper.ThrowIfNoInterface<IT>();
            return GetService<T>();
        }

        public T GetService<T>() where T : class
        {
            throw new NotImplementedException();
        }

        public void RegisterSingleton<T>() where T : class
        {
            RegisterService<T>(Scope.Singleton);
        }

        public void RegisterSingleton<T>(T service) where T : class
        {
            RegisterService<T>(service, Scope.Singleton);
        }

        public void RegisterTransient<T>() where T : class
        {
            RegisterService<T>(Scope.Transient);
        }

        public void RegisterTransient<T>(T service) where T : class
        {
            RegisterService<T>(service, Scope.Transient);
        }

        private void RegisterService<T>(Scope lifeTime) where T : class
        {
            throw new NotSupportedException();
        }

        private void RegisterService<T>(T service, Scope lifeTime) where T : class
        {
            throw new NotSupportedException();
        }

        private enum Scope
        {
            Singleton,
            Transient
        }
    }
}
