# MinGo Service Registry Server

可独立部署的服务注册中心（Registry Server），基于 ASP.NET Core Minimal API，目标框架 `net10.0`。负责服务实例的**注册**、**租约（lease）生命周期**与**发现查询**。MVP 使用内存存储、单节点、无鉴权。

配套客户端 SDK 在独立仓库 [`service-registry-sdk`](../service-registry-sdk)。两仓库通过 NuGet 包 `MinGo.ServiceRegistry.Abstractions` 共享协议契约。

## 快速开始

```bash
dotnet run --project src/MinGo.ServiceRegistry.Server
# 监听 http://localhost:5080
```

注册一个实例：

```bash
curl -i -X POST http://localhost:5080/api/registry/services/user-service/instances \
  -H 'Content-Type: application/json' \
  -d '{"scheme":"http","host":"10.0.0.5","port":5101,"lease":{"ttlSeconds":15}}'
```

发现该服务：

```bash
curl http://localhost:5080/api/registry/services/user-service/instances
```

## HTTP API

| 方法 | 路由 | 说明 |
| --- | --- | --- |
| `POST` | `/api/registry/services/{serviceName}/instances` | 注册实例，签发租约（`201`） |
| `PUT` | `/api/registry/leases/{leaseId}` | 续约（`200`；丢失/过期 `404`） |
| `DELETE` | `/api/registry/leases/{leaseId}` | 注销（`204`） |
| `GET` | `/api/registry/services/{serviceName}/instances` | 发现健康实例（`200`） |
| `DELETE` | `/api/registry/services/{serviceName}/instances/{instanceId}` | 管理用途删除（`204`） |
| `GET` | `/api/registry/services` | 列出所有服务名（`200`） |
| `GET` | `/health` | Server 自身健康（`200`） |

错误统一为 RFC 7807 `application/problem+json`。完整契约见 [`docs/api.md`](docs/api.md)。

## 配置（`ServiceRegistry` 节）

| 键 | 默认 | 说明 |
| --- | --- | --- |
| `DefaultLeaseTtlSeconds` | `15` | 客户端未请求 TTL 时采用 |
| `MinLeaseTtlSeconds` | `5` | TTL 下限 |
| `MaxLeaseTtlSeconds` | `300` | TTL 上限 |
| `ReaperIntervalSeconds` | `5` | 过期租约回收扫描间隔 |

监听地址默认 `http://localhost:5080`（`Urls`），可用 `ASPNETCORE_URLS` 覆盖。

## 构建与测试

```bash
dotnet build ServiceRegistry.Server.slnx
dotnet test  ServiceRegistry.Server.slnx
```

> Server 依赖 NuGet 包 `MinGo.ServiceRegistry.Abstractions`，该包已发布到 nuget.org，`Directory.Packages.props` 固定引用 `0.1.1`，默认即从 nuget.org 还原。本地开发如需使用未发布的改动，可由工作区引导脚本 `../local/build-local.ps1` 将其打包到本地 feed 并注册机器级源 `mingo-local`（该 feed 不含 `0.1.1` 时不会遮蔽线上包）。详见 [`docs/deployment.md`](docs/deployment.md)。

## 部署

- 容器：`deploy/docker/Dockerfile`（多阶段，非 root，容器内 `8080`）
- Kubernetes：`deploy/kubernetes/`（`kubectl apply -k deploy/kubernetes`）
- Podman：`deploy/podman/`

详见 [`docs/deployment.md`](docs/deployment.md)。

## 文档

- [`docs/architecture.md`](docs/architecture.md)：分层、Store/Managers/Reaper、DI 装配、MVP 边界
- [`docs/api.md`](docs/api.md)：端点、校验、租约语义、配置
- [`docs/deployment.md`](docs/deployment.md)：运行、Docker/Podman/Kubernetes、安全说明

## 目标框架

`net10.0`
