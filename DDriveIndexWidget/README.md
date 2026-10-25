# D-DriveIndexWallpaper 功能说明

一个悬浮在 Windows 桌面壁纸上的 **D 盘实时文件索引**小面板。无边框、无标题、背景全透明，白字带黑色描边，像"浮"在桌面上；不抢焦点、不显示在任务栏，并自动沉底（位于桌面图标层之上、普通窗口之下）。

## 功能一览

| 功能 | 说明 |
| --- | --- |
| 默认显示 | 面板**不显示 "D:" 盘符行**，根级直接列出 D 盘的**二级文件夹**（系统/垃圾目录自动过滤），树可懒加载到任意层级，文件夹内同时列出文件；树区域始终全透明，无任何常驻底色条 |
| 滚轮滚动 | 不显示任何系统滚动条（会产生不透明白色轨道）；鼠标悬停面板时直接**滚动滚轮上下浏览**（每格行数跟随系统设置） |
| 字号缩放 | 鼠标悬停面板时按住 **Ctrl + 滚轮**放大/缩小目录树字体（7–28pt，行高随动），字号自动记忆；过长名称自动省略并悬停显示全名 |
| 控件外观 | Add/☰ 为**透明底白色描边文字**（悬停变浅蓝），右下角为淡蓝实心小方块（14px，按下变深蓝）；除此之外无任何不透明边框、底色或滚动条 |
| 单击打开 | 单击文件夹/文件名，以**系统默认程序**打开；单击条目左侧 `+/-` 展开/折叠（双击不会折叠）；左键和右键均不产生选中底色条 |
| 任意拖动 | 在面板**任何位置**（文字、缩进、空白）按住并移动即可拖动面板；快速点击仍按"打开"处理 |
| 缩放 | 右下角常驻 **14px 淡蓝色实心方块**（悬停加深、按住变深蓝）可双向缩放；鼠标靠近其余三边/三角时出现**浅蓝色条**，按住即可从该边/角缩放 |
| 尺寸限制 | 最小 200×140；最大高 = 整屏高，最大宽 = 屏宽 3/4；默认宽 = 屏宽 1/5、高 = 工作区 2/3，停靠右下角 |
| 隐藏文件夹 | 右键任意文件夹 → `Hide from panel`，立即移除并永久记住 |
| 添加文件夹 | 右上角 `Add`：勾选恢复被隐藏的文件夹，或 `Browse folder...` 挂接任意更深层的 D 盘文件夹（仅允许 D 盘） |
| 菜单 | 右上角 `☰`（或树内空白处右键）：`Add folder...` / `Rescan now` / `Collapse tree` / `Exit`；右键文件另有 `Open containing folder`（资源管理器定位） |
| 实时更新 | 监视整个 D 盘的新建/删除/重命名，700ms 防抖后只刷新已展开的分支；变化行**蓝色高亮**并自动滚动定位，15 秒后高亮消退 |
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
  - `H|路径` 被隐藏的文件夹；`A|路径` 手动挂接的深层文件夹；`R|x|y|w|h` 面板位置尺寸；`F|字号` 目录树字体大小
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
│  ├─ Settings.cs              设置持久化（隐藏/挂接/矩形）
│  └─ AppLog.cs                轻量文件日志
├─ Indexing/
│  ├─ NodeItem.cs              树条目数据模型
│  └─ FolderScanner.cs         目录枚举、噪音过滤、路径规整
├─ Controls/
│  ├─ IndexTreeView.cs         自定义 TreeView（吞掉按下消息、屏蔽双击折叠、TVS_NOSCROLL 隐藏滚动条）
│  ├─ GlyphButton.cs           透明色键底、白字黑描边的自绘文字按钮（悬停浅蓝）
│  └─ MouseWheelHook.cs        WH_MOUSE_LL 钩子：悬停面板时普通滚轮滚动、Ctrl+滚轮缩放
├─ Dialogs/
│  └─ AddForm.cs               Add 按钮的无边框选择/浏览对话框
└─ UI/
   ├─ MainForm.cs              主窗体：控件搭建、窗口样式与点击穿透
   ├─ MainForm.Layout.cs       几何、拖动/缩放、悬停热区
   ├─ MainForm.Tree.cs         树交互、自绘、懒加载、重建
   ├─ MainForm.Menu.cs         右键菜单与隐藏/添加筛选
   └─ MainForm.Watcher.cs      FileSystemWatcher 实时刷新与差异高亮
```

所有文件同属命名空间 `DDriveIndexWallpaper`，`MainForm` 用 partial class 按功能拆在 5 个文件中。
