# D-DriveIndexWallpaper 功能说明

一个悬浮在 Windows 桌面壁纸上的桌面挂件，**一个进程、一个托盘、一个设置窗口**同时承载两个无边框全透明面板：

1. **D 盘实时文件索引**（目录树，默认右下角）；
2. **最近文件网格**（最近保存/访问/编辑过的文件，默认左下角，行列数可在设置中配置）。

面板无边框、无标题、背景全透明，白字带黑色描边，像"浮"在桌面上；不抢焦点、不显示在任务栏，并自动沉底（位于桌面图标层之上、普通窗口之下）。

## 功能一览

| 功能 | 说明 |
| --- | --- |
| 默认显示 | 面板**不显示 "D:" 盘符行**，根级直接列出 D 盘的**二级文件夹**（系统/垃圾目录自动过滤），树可懒加载到任意层级，文件夹内同时列出文件；树区域始终全透明，无任何常驻底色条 |
| 排序方式 | 根级文件夹按**最后编辑时间倒序**：取文件夹自身或其内部任意文件/子文件夹（递归）的最新修改时间，最近编辑过的排最前；时间相同按名称排序 |
| 滚轮滚动 | 不显示任何系统滚动条（会产生不透明白色轨道）；鼠标悬停面板时直接**滚动滚轮上下浏览**（每格行数跟随系统设置） |
| 字号缩放 | 鼠标悬停面板时按住 **Ctrl + 滚轮**放大/缩小目录树字体（7–28pt，行高随动），字号自动记忆；过长名称自动省略并悬停显示全名 |
| 控件外观 | Add/☰ 为**透明底白色描边文字**（悬停变浅蓝），右下角为淡蓝实心小方块（14px，按下变深蓝）；除此之外无任何不透明边框、底色或滚动条 |
| 单击打开 | 单击文件夹/文件名，以**系统默认程序**打开；单击条目左侧 `+/-` 展开/折叠（双击不会折叠）；左键和右键均不产生选中底色条 |
| 任意拖动 | 在面板**任何位置**（文字、缩进、空白）按住并移动即可拖动面板；快速点击仍按"打开"处理 |
| 缩放 | 右下角常驻 **14px 淡蓝色实心方块**（悬停加深、按住变深蓝）可双向缩放；鼠标靠近其余三边/三角时出现**浅蓝色条**，按住即可从该边/角缩放 |
| 尺寸限制 | 最小 200×140；最大高 = 整屏高，最大宽 = 屏宽 3/4；默认宽 = 屏宽 1/5、高 = 工作区 2/3，停靠右下角 |
| 隐藏文件夹 | 右键任意文件夹 → `Hide from panel`，立即移除并永久记住 |
| 添加文件夹 | 右上角 `Add`：勾选恢复被隐藏的文件夹，或 `Browse folder...` 挂接任意更深层的 D 盘文件夹（仅允许 D 盘） |
| 菜单 | 树面板 `☰`（或树内空白处右键）：`Add folder...` / `Rescan now` / `Collapse tree` / `Settings...` / `Hide this panel` / `Exit`；右键文件另有 `Open containing folder`（资源管理器定位） |
| 实时更新 | 监视整个 D 盘的新建/删除/重命名/**文件内容保存**，700ms 防抖后只刷新已展开的分支；结构变化行**蓝色高亮**并自动滚动定位，15 秒后高亮消退；内容保存会让对应根级文件夹**立即上浮到清单最前**（视口位置保持不变） |
| 最近文件网格 | 第二个面板（默认左下角）：读取系统"最近使用的项目"（`%APPDATA%\Microsoft\Windows\Recent` 快捷方式，打开/保存即由系统自动记录，覆盖所有盘符与应用），解析为真实存在的文件，按最近使用时间倒序；每格**左侧是系统默认程序关联的真实文件类型小图标**，右侧是文件名，单击以默认程序打开，右键可 `Open containing folder` 定位 |
| 文件名显示全 | 长文件名按字符数**均分**到 N 行（8 字/2 行 → 4+4，13 字/3 行 → 5+4+4，不足 N 字只占一行并在块内居中）：**所有图标统一大小，图标高度始终等于 N 行文本的总高**；每行最多可设字数（默认 12）、最多 N 行（默认 2，即文件名最多取前 24 字）；全网格共用一个字号，按当前最宽的文件名自动缩放，格子大则放大（上限 24pt）、列太窄则缩小保证完整显示；悬停显示完整路径 |
| 网格行列可配 | 同一设置窗口中设置列数×行数：如 **3×2 = 2 行 3 列共 6 个**，**8×4 = 4 行 8 列共 32 个**（列 1–12、行 1–8），并可设置文件名**行数（1–4）、每行字数（4–40）与格子间距（0–40px）**；Ctrl+滚轮在该面板上调整基准字号 |
| 最近文件实时更新 | 监视 Recent 目录 + 60 秒兜底轮询，700ms 防抖后台刷新；新出现的文件**蓝色高亮**15 秒；面板位置/尺寸/字号独立记忆 |
| 系统托盘 | 唯一常驻托盘图标：勾选切换两个面板的显示/隐藏、`Settings...`、`Rescan D: now`、`Exit`；双击图标打开设置。面板隐藏后只能从托盘恢复 |
| 统一设置 | 树面板 ☰ / 最近面板 ☰ / 托盘三处打开同一个设置窗口：网格列×行、文件名行数/字数、格子间距、两个面板显隐 |
| 开机自启 | `-install` 向启动目录写入快捷方式；`-uninstall` 移除 |
| 单实例 | 通过命名 Mutex 保证只运行一份 |

## 过滤规则

- 根目录噪音：`$Recycle.Bin`、`System Volume Information`、`Program Files`/`Program Files (x86)`/`ProgramData`、`Users`、`Windows`、`Recovery`、`PerfLogs`、`program*` 等；
- 所有层级：隐藏/系统属性、重解析点（junction/软链）不显示；
- 文件：`desktop.ini`、`thumbs.db`、`*.tmp`、`*.crdownload` 不显示。

## 运行与安装

- 直接运行：`D-DriveIndexWallpaper.exe`（无参数）
- 开机自启：`D-DriveIndexWallpaper.exe -install`（再次运行 `-uninstall` 取消）
- 退出：面板 `☰ → Exit`

## 数据与日志位置

- 设置：`%LOCALAPPDATA%\DDriveIndexWallpaper\settings.txt`
  - `H|路径` 被隐藏的文件夹；`A|路径` 手动挂接的深层文件夹
  - `R|x|y|w|h` 树面板位置尺寸；`F|字号` 目录树字体大小
  - `G|列|行` 最近文件网格行列；`Q|x|y|w|h` 最近文件面板位置尺寸；`Z|字号` 网格文件名基准字号（实际字号随格子/文件名自适应）
  - `N|行数|每行字数` 文件名分行设置；`S|间距` 格子间距像素（默认 8）
  - `U|0/1` 树面板是否显示；`V|0/1` 最近文件面板是否显示
  - 删除该文件即可恢复全部默认状态
- 日志：`%TEMP%\D-DriveIndexWallpaper.log`（安装快捷方式失败日志为 `D-DriveIndexWallpaper-install.log`）

## 从源码编译

纯 .NET Framework 4.x WinForms，**无需第三方依赖、无需 csproj**，用系统自带编译器一次编译全部源文件：

```powershell
$src = 'c:\Users\13608684731\Scripts\DDriveIndexWidget'
$out = 'c:\Users\13608684731\Scripts\D-DriveIndexWallpaper.exe'
$csc = "$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$files = Get-ChildItem -Path $src -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
& $csc /nologo /target:winexe /platform:anycpu `
  /win32manifest:"$src\..\D-DriveIndexWallpaper.manifest" `
  /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
  /reference:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\Microsoft.CSharp.dll" `
  /out:$out $files
```

`D-DriveIndexWallpaper.manifest` 声明 PerMonitorV2 DPI 感知，保证高分屏下物理像素与缩放假定一致。
注意：编译器为 C# 5 时代版本，源码不使用 `?.`、字符串内插 `$""` 等新语法。

## 源码结构

```
DDriveIndexWidget/
├─ Program.cs                  程序入口、单实例、-install/-uninstall 快捷方式
├─ Native/
│  └─ Win32.cs                 全部 P/Invoke、窗口/TreeView 消息常量与结构体
├─ Infrastructure/
│  ├─ WidgetColors.cs          集中配色（透明键、高亮蓝、浅蓝缩放条、深蓝握柄等）
│  ├─ Settings.cs              设置持久化（隐藏/挂接/矩形/网格行列/分行/间距）
│  ├─ FileIcons.cs             SHGetFileInfo 按扩展名取系统关联图标，缓存 32bpp 位图（GDI+ DrawImage 绘制）
│  └─ AppLog.cs                轻量文件日志
├─ Indexing/
│  ├─ NodeItem.cs              树条目数据模型
│  ├─ FolderScanner.cs         目录枚举、噪音过滤、路径规整、递归最后编辑时间统计与倒序排序
│  └─ RecentFiles.cs           系统 Recent 快捷方式枚举、COM 解析目标、去重/过滤/倒序（带缓存）
├─ Controls/
│  ├─ IndexTreeView.cs         自定义 TreeView（吞掉按下消息、屏蔽双击折叠、去 WS_xSCROLL 样式并压制系统重显滚动条）
│  ├─ RecentGrid.cs            最近文件自绘网格（色键透明、左真实图标右均分文件名、统一图标字号自适应、蓝高亮、按压/拖拽路由）
│  ├─ GlyphButton.cs           透明色键底、白字黑描边的自绘文字按钮（悬停浅蓝）
│  └─ MouseWheelHook.cs        WH_MOUSE_LL 钩子（WheelZone 多区）：悬停面板时普通滚轮滚动、Ctrl+滚轮缩放
├─ Dialogs/
│  ├─ AddForm.cs               Add 按钮的无边框选择/浏览对话框
│  └─ SettingsForm.cs          共享设置窗口：网格行列、文件名行数/字数、格子间距 + 两个面板显隐（按系统 DPI 等比缩放布局）
└─ UI/
   ├─ MainForm.cs              主窗体：两套面板控件搭建、窗口样式与点击穿透
   ├─ MainForm.Layout.cs       几何、双面板拖动/缩放、悬停热区
   ├─ MainForm.Tree.cs         树交互、自绘、懒加载、重建
   ├─ MainForm.Menu.cs         树右键菜单、隐藏/添加筛选、统一设置入口
   ├─ MainForm.Recent.cs       最近面板：交互、实时数据、高亮、缩放、菜单
   ├─ MainForm.Tray.cs         唯一系统托盘图标（面板开关/设置/重扫/退出）
   └─ MainForm.Watcher.cs      D 盘 FileSystemWatcher 实时刷新与差异高亮
```

所有文件同属命名空间 `DDriveIndexWallpaper`，`MainForm` 用 partial class 按功能拆在 7 个文件中。
