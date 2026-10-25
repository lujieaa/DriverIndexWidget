---
name: ddrive-widget-rebuild
description: 重新编译并重启 D-Drive 索引桌面悬浮挂件 D-DriveIndexWallpaper。当用户修改 DDriveIndexWidget 下的 C# 源码后要求编译、重新生成、重启或看效果时使用。不用于初次搭建项目，也不用于其他项目。
---

# D-Drive 挂件编译重启

修改 `c:\Users\13608684731\Scripts\DDriveIndexWidget\` 下任意 `.cs` 文件后，用本流程一键完成「结束旧进程 → 递归编译全部源码 → 启动新进程 → 确认存活」。源码共 16+ 个分散在子目录（Native/Infrastructure/Indexing/Controls/Dialogs/UI），必须递归收集全部 `*.cs`，漏文件会导致编译错误。

## 步骤

在 PowerShell 5 中执行（用 `;` 分隔，**不要用** `&&`/`||`；直接内联执行，不要落成 `.ps1` 文件，本机执行策略会拦截）：

```powershell
Get-Process -Name D-DriveIndexWallpaper -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep -Seconds 1
$files = Get-ChildItem -Path 'c:\Users\13608684731\Scripts\DDriveIndexWidget' -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
& "$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:anycpu /win32manifest:"c:\Users\13608684731\Scripts\D-DriveIndexWallpaper.manifest" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:"$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\Microsoft.CSharp.dll" /out:"c:\Users\13608684731\Scripts\D-DriveIndexWallpaper.exe" $files
Write-Host ("csc=" + $LASTEXITCODE)
if ($LASTEXITCODE -eq 0) { Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine='"c:\Users\13608684731\Scripts\D-DriveIndexWallpaper.exe"'} | Out-Null; Start-Sleep -Seconds 4; Get-Process -Name D-DriveIndexWallpaper -ErrorAction SilentlyContinue | Select-Object Id | Format-Table -AutoSize }
```

要点：

- 输出里的 `CLIXML` / `PSSecurityException`（powershell-profile-snapshot 无法加载）是环境噪音，**不影响结果**；只看 `csc=0` 和最后是否列出进程 Id。
- `csc` 非 0 时不要启动；把编译错误反馈给用户并修复。
- 用 WMI `Win32_Process.Create` 启动，避免进程依附终端会话。
- `Microsoft.CSharp.dll` 引用必须保留（Program.cs 用 `dynamic` 做 WScript.Shell COM 调用）；manifest 在 Scripts 根目录而非源码目录。

## 编码约束（csc 是 C# 5 时代编译器）

- 禁止 `?.`、`$"..."` 内插字符串；字符串拼接用 `+` 或 `string.Format`。
- 所有界面可见字符串保持英文 ASCII（README/开发记录可用中文）。

## 视觉验收

挂件是品红色键（color-key）透明窗口，PrintWindow/截图会得到黑色块，无法用截图验收。编译启动成功后，用 AskUserQuestion 请用户按 Win+D 看桌面实际效果并确认；不要声称"看起来正常"。

## 不要做的事

- 不要删除或重置 `%LOCALAPPDATA%\DDriveIndexWallpaper\settings.txt`（含用户保存的面板位置、尺寸、字号），除非用户明确要求。
- 不要在未要求时 `git commit`。
- 文档（README.md、开发记录.md）只在改动实际发生后按既有风格追加版本记录。
