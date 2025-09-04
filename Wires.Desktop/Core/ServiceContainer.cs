using System;
using System.Collections.Generic;

namespace Wires.Core;

public class ServiceContainer : IServiceProvider
{
    private readonly Dictionary<Type, object> _services = [];

    public ServiceContainer Add<T>(T service)
        where T : notnull
    {
        _services.TryAdd(typeof(T), service);
        return this;
    }

    public object GetService(Type serviceType)
    {
        return _services[serviceType];
    }

    public T GetService<T>()
    {
        return (T)_services[typeof(T)];
    }

    public T GetUniform<T>()
    {
        return (T)_services[typeof(T)];
    }

    public ServiceContainer Remove<T>()
    {
        _services.Remove(typeof(T));
        return this;
    }
}