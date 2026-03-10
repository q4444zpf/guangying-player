# 光影播放中控系统

基于 WPF 的全屏媒体播放器，支持视频、图片+音频播放，通过 WebAPI 远程控制播放与电源管理。

## 功能特性

- **全屏播放**：支持多种视频格式及「图片 + 音频」组合播放
- **WebAPI 控制**：通过 HTTP 接口远程控制播放、暂停、停止、重新播放
- **播放列表**：支持多视频连续播放，可设置播放顺序与是否启用
- **循环播放**：支持播放列表循环
- **电源管理**：支持待机、关机、重启、关屏/亮屏
- **测试界面**：内置 Web 测试页面，便于调试与远程操作

## 技术栈

- **运行时**：C# + .NET 8 (net8.0-windows)
- **UI**：WPF，全屏无边框窗口
- **媒体播放**：LibVLC + LibVLCSharp.WPF
- **WebAPI**：ASP.NET Core Minimal API (Kestrel)
- **数据库**：SQLite + Entity Framework Core

## 快速开始

### 环境要求

- Windows 10/11
- .NET 8 SDK

### 编译与运行

```bash
# 克隆项目
git clone https://github.com/q4444zpf/guangying-player.git
cd guangying-player

# 编译（编译前会自动停止运行中的实例）
dotnet build MediaControlPlayer.App/MediaControlPlayer.App.csproj

# 运行
dotnet run --project MediaControlPlayer.App/MediaControlPlayer.App.csproj
```

### 配置

编辑 `MediaControlPlayer.App/Config/AppSettings.json`：

```json
{
  "WebApi": {
    "Url": "http://0.0.0.0:5000"
  },
  "Media": {
    "RootDirectory": ""
  },
  "Database": {
    "DbPath": ""
  }
}
```

- **WebApi.Url**：API 监听地址，默认 `http://0.0.0.0:5000`
- **Media.RootDirectory**：媒体文件根目录，为空时使用程序目录下的 `Media` 文件夹
- **Database.DbPath**：数据库路径，为空时使用 `Data/media.db`

## API 接口

### 播放控制

| 方法 | 路径 | 说明 |
|-----|------|------|
| POST | `/api/player/play` | 按路径播放（body: type, videoPath/imagePath/audioPath） |
| POST | `/api/player/play/{id}` | 按内容 ID 播放 |
| POST | `/api/player/play/playlist` | 播放播放列表（按 IsEnabled、PlayOrder 排序） |
| POST | `/api/player/pause` | 暂停 |
| POST | `/api/player/resume` | 继续 |
| POST | `/api/player/restart` | 重新播放 |
| POST | `/api/player/stop` | 停止 |
| GET | `/api/player/loop` | 获取循环播放状态 |
| POST | `/api/player/loop` | 设置循环播放（body: `{ "isLooping": true }`） |

### 内容管理

| 方法 | 路径 | 说明 |
|-----|------|------|
| GET | `/api/contents` | 获取播放列表（按 PlayOrder 排序） |
| GET | `/api/contents/{id}` | 获取单条内容 |
| POST | `/api/contents` | 创建内容 |
| PUT | `/api/contents/{id}` | 更新内容 |
| PATCH | `/api/contents/{id}/playlist` | 更新播放设置（isEnabled, playOrder） |
| DELETE | `/api/contents/{id}` | 删除内容 |

### 电源控制

| 方法 | 路径 | 说明 |
|-----|------|------|
| POST | `/api/system/display-off` | 关屏 |
| POST | `/api/system/display-on` | 亮屏 |
| POST | `/api/system/sleep` | 睡眠 |
| POST | `/api/system/reboot` | 重启 |
| POST | `/api/system/shutdown` | 关机 |

### 其他

| 方法 | 路径 | 说明 |
|-----|------|------|
| GET | `/api/health` | 健康检查 |
| POST | `/api/files/upload/video` | 上传视频 |
| POST | `/api/files/upload/audio` | 上传音频 |
| POST | `/api/files/upload/image` | 上传图片 |

## 测试界面

启动应用后，访问 `http://localhost:5000` 或 `http://你的IP:5000` 可打开内置测试页面，支持：

- 上传媒体文件
- 管理播放列表（新建、编辑、删除、设置播放顺序与是否启用）
- 播放控制（按路径、按 ID、播放列表）
- 循环播放开关
- 电源控制

## 项目结构

```
MediaControlPlayer.App/
├── App.xaml.cs          # 应用入口，启动时自动加载播放列表
├── MainWindow.xaml      # 全屏播放窗口
├── Config/
│   └── AppSettings.json
├── Data/
│   └── MediaDbContext.cs
├── Models/
│   ├── MediaContent.cs  # 媒体内容（含 IsEnabled、PlayOrder）
│   ├── MediaType.cs
│   ├── PlayRequest.cs
│   ├── PlaylistUpdateRequest.cs
│   └── LoopRequest.cs
├── Services/
│   ├── PlayerService.cs   # 播放逻辑
│   ├── PowerService.cs    # 电源操作
│   └── WebApiHost.cs      # WebAPI 宿主
└── wwwroot/
    └── index.html         # 测试界面
```

## 注意事项

- 关机/重启/睡眠后进程停止，无法通过 HTTP 自行唤醒，需配合 Wake-on-LAN 等方案
- 首次运行需确保 `libvlc` 目录存在（win-x64 架构）

## License

MIT
