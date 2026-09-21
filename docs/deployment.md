# 部署

Registry Server 可作为普通 .NET 进程运行，也可打包为容器镜像并部署到 Kubernetes。所有构建命令均在**仓库根目录**执行，构建上下文为仓库根。

> **前置：Abstractions 包来源**
> Server 依赖 NuGet 包 `MinGo.ServiceRegistry.Abstractions`（由 `service-registry-sdk` 仓库产出）。
> - CI/生产：该包已发布到真实 feed（nuget.org 或 GitHub Packages），`nuget.config` 指向即可还原。
> - 本地容器构建：该包尚未公开发布时，先把 SDK 的 Abstractions 打包到一个本地目录（见 SDK 仓库 `local/build-local.ps1`），再通过 `EXTRA_NUGET_SOURCE` 构建参数把该目录作为额外 NuGet 源传入（见下文）。

## 直接运行 / 发布

```bash
dotnet run --project src/MinGo.ServiceRegistry.Server        # 开发运行，监听 http://localhost:5080
dotnet publish src/MinGo.ServiceRegistry.Server -c Release -o ./publish
```

监听地址与 `ServiceRegistry` 配置节可用环境变量覆盖：

```bash
ASPNETCORE_URLS="http://+:8080" \
ServiceRegistry__DefaultLeaseTtlSeconds=30 \
dotnet ./publish/MinGo.ServiceRegistry.Server.dll
```

## Docker

多阶段 Dockerfile 位于 `deploy/docker/Dockerfile`（`sdk:10.0` 构建 → `aspnet:10.0` 运行，非 root `app` 用户，容器内监听 `8080`）。

```bash
# 从真实 feed 还原 Abstractions（已发布时）
docker build -f deploy/docker/Dockerfile -t mingo/service-registry:0.1.0 .

# 或：把本地 feed 目录作为额外 NuGet 源传入
docker build -f deploy/docker/Dockerfile \
  --build-arg EXTRA_NUGET_SOURCE=/feed \
  -t mingo/service-registry:0.1.0 .

# 运行：宿主 5080 -> 容器 8080
docker run --rm -p 5080:8080 mingo/service-registry:0.1.0
```

健康检查：

```bash
curl -i http://localhost:5080/health
```

## Podman

`deploy/podman/` 提供复用同一 Dockerfile 的脚本（PowerShell / pwsh）：

```powershell
pwsh deploy/podman/build.ps1     # podman build -f ../docker/Dockerfile，上下文为仓库根
pwsh deploy/podman/run.ps1       # podman run --rm -p 5080:8080
```

## Kubernetes

清单位于 `deploy/kubernetes/`（`Deployment` + `Service` + `kustomization.yaml`）。镜像默认 `mingo/service-registry:0.1.0`，容器端口 `8080`，`/health` 作为存活与就绪探针，`Service` 将集群内 `80` 映射到 `8080`。

```bash
kubectl apply -k deploy/kubernetes
kubectl get deploy,svc -l app.kubernetes.io/name=service-registry
```

> **副本数**：MVP 为内存存储、单节点，`Deployment` 固定 `replicas: 1`。多副本不会共享注册状态；如需水平扩展，请先接入分布式 Store（`IServiceRegistryStore` 的持久化实现）。

集群内其他服务通过 `Service` 名访问，例如 SDK 的 `RegistryEndpoint = http://service-registry`（`Service` 监听 `80`）。

## 配置参考

| 配置 | 默认 | 环境变量 |
| --- | --- | --- |
| 监听地址 | `http://localhost:5080` | `ASPNETCORE_URLS`（容器内镜像已设 `http://+:8080`） |
| `DefaultLeaseTtlSeconds` | `15` | `ServiceRegistry__DefaultLeaseTtlSeconds` |
| `MinLeaseTtlSeconds` | `5` | `ServiceRegistry__MinLeaseTtlSeconds` |
| `MaxLeaseTtlSeconds` | `300` | `ServiceRegistry__MaxLeaseTtlSeconds` |
| `ReaperIntervalSeconds` | `5` | `ServiceRegistry__ReaperIntervalSeconds` |

## 安全说明

MVP **不含鉴权**。生产部署应把 Registry 置于可信网络（集群内部 / mTLS / 反向代理鉴权）之后，或等待后续版本引入认证与授权。
