using MinGo.ServiceRegistry.Abstractions;

namespace MinGo.ServiceRegistry.Hosting.Store;

/// <summary>
/// A stored registration: the discoverable instance plus its lease state.
/// </summary>
/// <param name="Instance">The service instance metadata.</param>
/// <param name="Lease">The lease governing the instance's lifetime.</param>
public sealed record ServiceEntry(ServiceInstance Instance, Lease Lease);
