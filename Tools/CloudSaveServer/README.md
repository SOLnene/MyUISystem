# 本地云存档服务器

这是独立于 Unity 的 ASP.NET Core / SQLite 服务，负责账号注册、登录、UID 分配、存档和卡池配置。当前默认仅监听本机，不收费，也不需要云服务账号；玩家在其他设备访问仍需之后部署公网服务器。

## 启动与停止

Windows 本机已经发布的版本：双击 `Start-Server.cmd`，保持窗口打开。

浏览器访问 `http://127.0.0.1:5127/health`，应显示 `{"status":"ok"}`。停止时在服务器窗口按 Ctrl+C。再次启动仍使用同一数据库。

也可在 PowerShell 运行：

```powershell
& 'E:\Unity\MyUISystem\Tools\CloudSaveServer\Start-Server.ps1'
```

数据库自动生成于 `Tools/CloudSaveServer/data/saves.db`，这是服务器存储，不是 Unity 客户端的本地存档。`data/`、编译产物及本机配置不会进入 Git。这里没有读取、删除或迁移旧游戏存档。

Unity 的 Windows 客户端入口已切换到这个服务器，默认地址在 `Assets/Script/Game/Save/CloudSaveHttpClient.cs` 的 `ServiceUrl`。游戏运行期间保持服务开启；首次进入需完成服务器读取和新账号初始化写入。当前接入不读取或迁移旧本地进度。

本机 HTTP 通过 Windows 的托管 `HttpClient` 请求，不修改 Unity 的全局 HTTP 安全设置。本次验证范围为 Windows Editor；其他平台和 WebGL 尚未验证。公网部署后需把客户端地址改为 HTTPS 服务地址。

## 接口约定

存档和卡池接口必须发送 `Authorization: Bearer <accessToken>`，使用自建账号服务返回的访问凭据。每次请求由账号会话取得有效凭据；这两个接口不发送密码或刷新凭据。

服务器校验数据库中的会话凭据哈希和有效期。账号归属只取校验后的身份，客户端不能指定 UID 或存档所有者。默认不初始化 Unity 认证，也不请求 Unity 公钥；没有放在客户端的管理员密钥。

默认绑定当前项目 `56bad169-df5f-443f-b41b-394a4c0d5f6d` 的 production 环境 `31ff77d2-4511-4d76-9b95-f94d65c87a06`。同一数据库不能改为其他项目或环境；不同环境必须使用不同数据库。

| 请求 | 结果 |
| --- | --- |
| `GET /health` | 200：服务已启动 |
| `GET /v1/save` | 200：当前账号的 `saveJson`、`revision`、`updatedAtUtc` |
| `GET /v1/save` | 404 且 `code=save_not_found`：当前账号没有存档 |
| `PUT /v1/save` | 200：写入确认 `revision`、`updatedAtUtc`、`replayed` |

PUT 示例（`saveJson` 是现有游戏存档 JSON 字符串）：

```json
{
  "expectedRevision": 0,
  "requestId": "31e67a46-39f6-4781-bfde-5e0f37c8eb2c",
  "saveJson": "{\"version\":7,\"currencies\":{\"items\":[]}}"
}
```

首次创建用版本 0，更新用 GET 返回的版本。每次新快照生成新的 UUID，网络重试必须保留相同 UUID、版本和内容。

首次创建和更新均在数据库事务中比较版本，两个设备同时操作只有一个成功。成功写入和重试回执原子提交；已成功但响应丢失的请求重试不会产生新版本。回执保留最近 256 次写入、最长 7 天，当前最新回执不因超时删除；更早请求即使回执已清理，也受版本检查保护，不会无条件覆盖。

客户端应串行上传。重试回执可能属于旧版本，不能用它覆盖稍后已读取的版本。409 后重新读取并让玩家处理冲突，不要自动以新版本强行覆盖。

| 状态 | 处理 |
| --- | --- |
| 400 | 请求字段、JSON、存档版本或大小不符合约定 |
| 401 | 账号凭据无效或过期；刷新凭据或重新登录 |
| 403 | 不安全的外部 HTTP 请求 |
| 409 `save_conflict` | 版本冲突；返回当前版本，保留云端内容 |
| 409 `request_id_reused` | 同一请求编号对应不同内容 |
| 413 / 415 | 请求体过大 / 内容类型不正确 |
| 429 | 超过每账号每分钟 120 次请求或服务器并发上限 |
| 503 | 数据库暂不可用；延迟后重试原请求 |
| 500 | 服务内部错误；不能据此判断没有存档 |

只有明确的 `404 + save_not_found` 可以初始化新账号。网络超时、认证失败、服务器异常都不能回退读取旧本地进度。响应错误包含 `code`、`message` 和 `traceId`，不会返回其他账号的存档。

## 配置与编译

需要 .NET 10 SDK。服务器发布后使用自带运行时，不依赖本机 .NET 7 或 Unity 的 .NET 版本。构建时依赖来自 NuGet，版本固定并生成 `packages.lock.json`。

在此目录执行：

```powershell
dotnet restore CloudSaveServer.csproj --locked-mode
dotnet build CloudSaveServer.csproj -c Release --no-restore
dotnet publish CloudSaveServer.csproj -c Release -r win-x64 --self-contained true -o out
```

本机本次使用的 SDK 在项目 `.codex_tmp_frame_analysis/dotnet-sdk-10/dotnet.exe`，没有更换系统 SDK。

可以创建不提交的 `appsettings.Local.json` 覆盖默认配置，也可用环境变量 `SaveServer__DatabasePath`、`SaveServer__ProjectId`、`SaveServer__EnvironmentId` 或 `Urls` 覆盖。存档 UTF-8 大小最大 1 MiB，JSON 深度最大 64，支持存档版本 1–7；Unity 接入后继续执行游戏自身的数据校验。

HTTP 只能绑定回环地址。公网部署使用 HTTPS，或让本机 HTTPS 反向代理转发到回环地址；部署时还要设置允许的域名、持久磁盘、服务自启动及外部备份。不要直接对公网暴露本机测试端口。

## 备份与恢复

运行 `Backup-Server.ps1`，将在 `data/backups/` 生成带时间戳的数据库快照，允许服务器运行期间备份。采用 SQLite 备份 API，包含已提交的 WAL 内容。

不要在服务运行时只复制 `saves.db`；SQLite 可能还有未合并的 WAL 文件。恢复时停止服务，把备份放到一个新的数据库文件路径，通过本机配置指向该文件，再启动并验证。保留原文件，以便回滚。

本机备份与数据库在同一磁盘，仅防误操作。服务器部署后需增加定时任务和异地备份；本版本没有自动购买或部署云资源。

## 验证

在此目录运行独立集成测试（测试数据放入统一临时分析目录）：

```powershell
dotnet run --project Tests/CloudSaveServer.Tests.csproj -c Release -- ../../.codex_tmp_frame_analysis/backend-validation
```

加 `--live-auth` 会创建两个临时 Unity 注册账号，验证实际注册/登录、官方公钥验证和账号隔离，并在结束时删除测试账号。凭据只保留在测试进程内，不写入文件或日志。

测试覆盖伪造签名、错误项目/环境、过期凭据、首次创建竞争、更新竞争、重复请求、输入限制、服务重启、在线备份和数据库完整性。测试专用签名公钥只注入测试进程，正式服务器没有相应开关。

此版本保存客户端提交的进度，仅验证身份、JSON 基本格式、大小及写入版本。奖励、货币、抽卡等规则仍在现有游戏客户端；本版本尚未提供服务端权威计算或完整反作弊。

## 卡池配置下发

云模式每次打开抽卡页时请求 `GET /v1/gacha/config`，接口使用与云存档相同的自建账号访问凭据校验。一次返回当前开放列表和完整定义；抽卡结果、奖励发放和保底计数仍由现有客户端逻辑处理。

本目录中的 `gacha-schedule.json` 控制开放列表、显示顺序和配置版本；每个池单独使用 `gacha-<gachaKey>.json`，保存物品列表、权重和保底次数。初始三个池与项目原有配置一致。

- `enabled`：是否开放。
- `startsAtUtc`、`endsAtUtc`：可为 `null`；填写时使用 UTC，例如 `2026-10-13T00:00:00Z`。开始时刻包含在开放区间内，结束时刻不包含。
- `version`：配置发布标记，调整配置时递增。
- `entryType`：`0` 为角色，`1` 为装备；`entryKey` 和 `gachaKey` 必须与客户端已有定义一致。

服务器每次请求重新读取文件。保存合法配置后，客户端下一次打开抽卡页或点击“重试”就获取更新，无需重新发布服务器；同一抽卡展示流程不会中途更换配置。多文件更新时先完成文件编辑再让客户端刷新。

客户端仅创建内存中的临时卡池定义，不修改本地卡池资源、图片引用或存档格式。没有开放池时显示空状态；文件无效、配置包含客户端未知 ID 或请求失败时停止该页面抽卡并提示重试。选择本地游玩时不请求这个接口，继续使用原有本地配置。

`GachaConfigRoot` 可指定配置文件所在目录，默认使用服务器工作目录。已有 Windows 启动脚本和 Unity 启动工具均使用本目录。新发布版本才包含此接口；代码更新后需停止旧服务，再重新发布并启动。

## 自建账号

| 请求 | 内容与结果 |
| --- | --- |
| `POST /v1/auth/register` | 提交 `username`、`password`；返回 `uid`、`username`，注册后回到登录页 |
| `POST /v1/auth/login` | 提交 `username`、`password`；返回 UID、用户名、访问凭据、刷新凭据及到期时间 |
| `POST /v1/auth/refresh` | 提交 `refreshToken`；重新签发访问凭据 |
| `POST /v1/auth/logout` | 提交 `refreshToken`；撤销该会话的访问和刷新凭据 |

账号为 3～20 位英文、数字或 `_.@-`，忽略英文大小写判重。注册密码为 8～64 位，不要求同时包含大写、小写、数字或符号。服务器使用 ASP.NET Core PasswordHasher 保存带独立盐的密码哈希，不保存明文密码。

UID 从 `100000001` 开始，由数据库事务统一分配并永久保存。新账号的存档所有者内部使用 `local:<uid>`，与旧 Unity PlayerId 隔离；UID 显示为数字。账号、会话和存档均位于现有 `data/saves.db`，现有备份命令会一起备份。

访问凭据有效期 30 分钟，刷新凭据有效期 30 天。刷新不延长原会话的 30 天期限；客户端在访问凭据将到期时串行刷新。服务器只保存凭据哈希。账号接口按来源 IP 限制为每分钟 20 次。

Windows 客户端选择记住登录时，刷新凭据经当前 Windows 用户的 DPAPI 保护后保存，密码不保存、不回填。其他平台暂未接入安全凭据存储，可取消记住登录后手动登录。本地游玩入口仍不依赖账号服务。

旧 Unity 账号记录和旧存档保留，新账号先使用自己的新存档。当前没有提供旧账号绑定入口；不能通过用户名认领旧记录。`EnableLegacyUnityAuthentication` 默认关闭，仅保留旧认证验证能力供后续迁移及回归验证，普通账号流程无需开启。

