# Server 架构

Registry Server 是一个可独立部署的 ASP.NET Core（Minimal API）服务，负责服务实例的注册、租约（lease）生命周期管理与发现查询。MVP 使用**内存存储**、**单节点**、**无鉴权**。

## 项目结构

| 项目 | 类型 | 职责 |
| --- | --- | --- |
| `MinGo.ServiceRegistry.Hosting` | 类库 | Store、Managers、Reaper、Endpoints、DI 装配（`AddServiceRegistryServer` / `MapServiceRegistryEndpoints`） |
| `MinGo.ServiceRegistry.Server` | 可执行（Web） | `Program.cs` + `appsettings.json`，宿主入口 |

协议契约（领域模型、DTO、`RegistryJsonContext`）来自 NuGet 包 `MinGo.ServiceRegistry.Abstractions`（由 `service-registry-sdk` 仓库产出），确保 Client 与 Server 序列化一致。

## 分层

```
HTTP (Minimal API Endpoints)
        │
   Managers (Registration / Lease / Discovery)
        │
   IServiceRegistryStore  ── MemoryServiceRegistryStore
        ▲
   LeaseReaper (BackgroundService)
```

### Store

- `IServiceRegistryStore`：持久化抽象，方法有 `UpsertAsync` / `TryRenewAsync` / `TryRemoveByLeaseAsync` / `TryRemoveByInstanceAsync` / `GetByLeaseAsync` / `GetEntriesAsync` / `GetExpiredLeaseIdsAsync` / `GetServiceNamesAsync`。接口设计便于日后替换为 Redis / PostgreSQL / 分布式存储而不触碰核心逻辑。
- `MemoryServiceRegistryStore`：单个 `ConcurrentDictionary<string leaseId, ServiceEntry>`。续约用 `TryUpdate` 的比较交换（CAS）循环；按服务名查询通过扫描 `Values` 过滤；`Upsert` 会移除同一 `(serviceName, instanceId)` 的旧租约，避免客户端重启重复注册产生残留。
- `ServiceEntry(Instance, Lease)` 与 `Lease(LeaseId, TtlSeconds, ExpiresAt, LastRenewalAt)` 为不可变 `record`；`Lease.IsExpired(now)` / `Lease.Renew(now, expiresAt)` 承载租约时间语义。

### Managers

- `RegistrationManager`：校验后生成缺省 `instanceId`（`{service}-{guid12}`）、签发 `leaseId = Guid`、用 `TimeProvider` 计算 `ExpiresAt = now + ttl`、`Health=Healthy`，写入 Store；TTL 通过 `Math.Clamp` 收敛到 `[MinLeaseTtlSeconds, MaxLeaseTtlSeconds]`。`DeregisterByInstanceAsync` 供管理端点使用。
- `LeaseManager`：`RenewAsync` 在租约缺失**或已过期**时返回 `null`（→ HTTP 404，客户端据此重注册），否则按存储的 TTL 重算 `ExpiresAt` 并 CAS 续约；`DeregisterAsync` 幂等地按 lease 删除。
- `DiscoveryManager`：`GetInstancesAsync` 只返回 `Health==Healthy` 且**未过期**的实例（即便 reaper 尚未物理删除，过期实例也会立即从发现结果消失），按 `instanceId` 排序；`GetServicesAsync` 返回去重后的服务名。

### Reaper

`LeaseReaper : BackgroundService`，用 `new PeriodicTimer(interval, TimeProvider)` 每 `ReaperIntervalSeconds`（默认 5s）扫描 `GetExpiredLeaseIdsAsync` 并逐个删除。发现路径已过滤过期项，因此 reaper 的作用是**回收内存、保持 Store 有界**。`ReapOnceAsync` 亦暴露给测试/手动触发。

### Endpoints

Minimal API + `TypedResults`，路由映射在 `RegistrationEndpoints` / `LeaseEndpoints` / `DiscoveryEndpoints`（详见 `api.md`）。启用 `AddProblemDetails()`，未处理异常与错误状态码统一以 RFC 7807 `application/problem+json` 返回。

### DI 装配

`AddServiceRegistryServer(configuration)`（或带内联回调的重载）一次性装配：绑定 `ServiceRegistryServerOptions`（`ServiceRegistry` 配置节）、注入源生成 JSON（`RegistryJsonContext`）、`AddProblemDetails()`、`AddHealthChecks()`、`TimeProvider.System`、`MemoryServiceRegistryStore`、三个 Manager（均 Singleton）、`AddHostedService<LeaseReaper>()`。`MapServiceRegistryEndpoints()` 映射全部路由 + `/health`。

## 横切设计

- **可测时间源**：`TimeProvider` 注册为单例并注入到 Manager/Reaper；集成测试用 `FakeTimeProvider` 精确推进时钟验证过期/续约/剔除。
- **确定性过期**：过期租约不可续约（`LeaseManager` 提前返回 `null`），使得到期行为不依赖 reaper 的后台时序。
- **源生成序列化**：`camelCase`、写时忽略 `null`，无反射，AOT 友好。

## MVP 边界（后续演进）

- 仅内存 Store，**单节点**；无持久化 / HA / 集群（多副本不共享状态）。
- **无鉴权**（Registry 端点对外开放，生产需置于可信网络或加认证）。
- 无主动健康检查（依赖客户端心跳续约）；无 Metadata 富化。
- 可观测性仅 `ILogger` + `/health`；OpenTelemetry metrics/tracing 留待后续。
