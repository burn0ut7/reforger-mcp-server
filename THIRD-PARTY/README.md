# Dependency notices

ModelContextProtocol / ModelContextProtocol.Core 2.2.0: Copyright Model Context Protocol a Series of LF Projects, LLC. The unmodified upstream license from commit `6fa3825973949a9c4f0cd8af344e15a8db09dc35` is in `MCP-SDK-LICENSE.txt`.

The self-contained package also includes Microsoft .NET runtime and Microsoft.Extensions components. Their license is in `DOTNET-LICENSE.txt`; runtime third-party notices are copied from the exact runtime distribution during packaging to `DOTNET-NOTICES.txt`. Exact application dependencies are recorded in `ReforgerMcp.deps.json` and the repository's NuGet lock files.

Native Workbench commands use the .NET Windows Desktop UIAutomationClient/UIAutomationTypes assemblies. The self-contained package includes the matching Windows Desktop runtime; its package license is copied to `WINDOWSDESKTOP-LICENSE.txt`. UI Automation itself is provided by Windows.

Windows Graphics Capture uses the Microsoft.Windows.SDK.NET.Ref 10.0.19041.57 targeting pack, pinned by `WindowsSdkPackageVersion`. Its Windows SDK projection and WinRT runtime assemblies are included in the application output. The package identifies the [Windows SDK license](https://aka.ms/WinSDKLicenseURL); `CSWINRT-LICENSE.txt` preserves the C#/WinRT MIT license from Microsoft's repository. The Windows capture APIs themselves are provided by the operating system.

Ghidra and the JDK are separately installed prerequisites, not redistributed here. No game executables or decompiled game code are included.

The PAC1 format was researched against rvost/PakInspector's `formats/pak.ksy`, commit `4d005ffee8f848176cabdd107c247f6944e8c1ab`, Copyright (C) 2025 rvost, Apache-2.0. `PAKINSPECTOR-LICENSE.txt` preserves the upstream license. The server's seek-based C# reader is a new implementation; the upstream generated parser and CLI are not runtime dependencies or shipped backends. No proprietary archive contents or exported script mirrors are included.
