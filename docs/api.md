# HTTP API

Registry Server 的 HTTP 接口。契约模型定义在 `MinGo.ServiceRegistry.Abstractions`，序列化走 `RegistryJsonContext`（`camelCase`，写时忽略 `null`）。SDK 侧对应文档见 `service-registry-sdk/docs/protocol.md`。

- **Base path**：`/api/registry`
- **Content-Type**：`application/json`
- **错误模型**：RFC 7807 `application/problem+json`
- 默认监听 `http://localhost:5080`（`appsettings.json` 的 `Urls`）；容器内默认 `8080`（见 `deployment.md`）。

## 端点

| 方法 | 路由 | 成功 | 失败 |
| --- | --- | --- | --- |
| `POST` | `/api/registry/services/{serviceName}/instances` | `201` + `RegistrationResult`（`Location: /api/registry/leases/{leaseId}`） | `400` `ValidationProblemDetails` |
| `PUT` | `/api/registry/leases/{leaseId}` | `200` + `RenewResult` | `404`（租约缺失或已过期） |
| `DELETE` | `/api/registry/leases/{leaseId}` | `204` | `404` |
| `GET` | `/api/registry/services/{serviceName}/instances` | `200` + `ServiceInstancesResponse`（仅 Healthy 且未过期） | — |
| `DELETE` | `/api/registry/services/{serviceName}/instances/{instanceId}` | `204`（管理用途） | `404` |
| `GET` | `/api/registry/services` | `200` + `ServiceListResponse` | — |
| `GET` | `/health` | `200`（Server 自身健康） | — |

## 示例

注册（`201`）：

```bash
curl -i -X POST http://localhost:5080/api/registry/services/user-service/instances \
  -H 'Content-Type: application/json' \
  -d '{"scheme":"http","host":"10.0.0.5","port":5101,"lease":{"ttlSeconds":15}}'
```

响应体：

```json
{ "leaseId": "9c1b...", "instanceId": "user-service-ab12cd34ef56", "expiresAt": "2024-01-01T00:00:15Z", "ttlSeconds": 15 }
```

续约（`200`；租约丢失/过期则 `404`）：

```bash
curl -i -X PUT http://localhost:5080/api/registry/leases/9c1b...
```

发现（`200`）：

```bash
curl http://localhost:5080/api/registry/services/user-service/instances
```

```json
{
  "serviceName": "user-service",
  "instances": [
    { "serviceName": "user-service", "instanceId": "user-service-ab12cd34ef56",
      "scheme": "http", "host": "10.0.0.5", "port": 5101, "health": 1 }
  ]
}
```

注销（`204`）：

```bash
curl -i -X DELETE http://localhost:5080/api/registry/leases/9c1b...
```

## 校验规则（`400`）

`POST` 注册在下列情况返回 `ValidationProblemDetails`：

- `serviceName`（路由）为空。
- 缺少请求体。
- `host` 为空。
- `port` 不在 `[0, 65535]`。
- `scheme` 为空。
- `lease.ttlSeconds`（若提供）不是正数。

## 租约语义

- 注册由服务端签发 `leaseId`；SDK 生命周期围绕 `leaseId`（续约 `PUT`、注销 `DELETE`）。
- **`404` = 租约丢失**：续约遇到 `404` 时 SDK 会重新注册；注销遇到 `404` 视为幂等成功。
- 已过期租约不可续约；过期实例会立即从发现结果中消失，随后由 reaper 物理回收。

## 配置（`ServiceRegistry` 节）

| 键 | 默认 | 说明 |
| --- | --- | --- |
| `DefaultLeaseTtlSeconds` | `15` | 客户端未请求 TTL 时采用 |
| `MinLeaseTtlSeconds` | `5` | 客户端请求 TTL 的下限 |
| `MaxLeaseTtlSeconds` | `300` | 客户端请求 TTL 的上限 |
| `ReaperIntervalSeconds` | `5` | reaper 扫描过期租约的间隔 |

配置可用环境变量覆盖（双下划线），例如 `ServiceRegistry__DefaultLeaseTtlSeconds=30`。

## 健康检查

`GET /health` 由 `AddHealthChecks()` + `MapHealthChecks("/health")` 提供，返回 `200` 表示进程存活，可用作容器/编排的存活与就绪探针。
