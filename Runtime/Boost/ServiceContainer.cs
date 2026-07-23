using System;
using System.Collections.Generic;

namespace PschLib
{
    public sealed class ServiceContainer
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public bool Register<TService>(TService service)
            where TService : class
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            var serviceType = typeof(TService);
            if (_services.ContainsKey(serviceType))
            {
                return false;
            }

            _services.Add(serviceType, service);
            return true;
        }

        public bool Unregister<TService>()
            where TService : class
        {
            return _services.Remove(typeof(TService));
        }

        public TService Get<TService>()
            where TService : class
        {
            object service;
            if (!_services.TryGetValue(typeof(TService), out service))
            {
                throw new InvalidOperationException(
                    $"Service {typeof(TService).FullName} is not registered.");
            }

            return (TService)service;
        }
    }
}
