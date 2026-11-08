# Third-Party Notices

RssReader is licensed under the MIT License (see [LICENSE](LICENSE)).
This file lists the third-party components that RssReader uses and their licenses.
These components are obtained from NuGet at build time and are **not** included in
this repository's source code. They are, however, included in built application
packages (MSIX / exe), where each remains subject to its own license below.

## Summary

| Component | Version | License | Shipped with the app |
|---|---|---|---|
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | 8.4.2 | MIT | Yes |
| [System.ServiceModel.Syndication](https://github.com/dotnet/runtime) | 10.0.12 | MIT | Yes |
| [System.Numerics.Tensors](https://github.com/dotnet/runtime) (transitive) | 9.0.0 | MIT | Yes |
| [Microsoft.Web.WebView2](https://aka.ms/webview) (transitive) | 1.0.3719.77 | BSD-3-Clause style | Yes |
| [Microsoft.WindowsAppSDK](https://github.com/microsoft/windowsappsdk) (incl. WinUI, Runtime, AI, ML and other sub-packages) | 2.6.4 | Microsoft Software License Terms | Yes (redistributable portions) |
| Microsoft.Windows.AI.MachineLearning (transitive) | 2.4.89 | Microsoft Software License Terms | Yes (redistributable portions) |
| [Microsoft.Windows.SDK.BuildTools.WinApp](https://github.com/microsoft/WinAppCli) | 0.7.0 | MIT | No (build tool) |
| Microsoft.Windows.SDK.BuildTools / Microsoft.Windows.SDK.BuildTools.MSIX | 10.0.28000.2957 / 1.7.251221100 | Microsoft Windows SDK License Terms | No (build tools) |

---

## CommunityToolkit.Mvvm

Copyright © .NET Foundation and Contributors. All rights reserved.

CommunityToolkit.Mvvm incorporates third-party components; see its
`ThirdPartyNotices.txt` (included in the NuGet package and at
https://github.com/CommunityToolkit/dotnet/blob/main/ThirdPartyNotices.txt).

Licensed under the MIT License (full text below).

## System.ServiceModel.Syndication / System.Numerics.Tensors

Copyright (c) .NET Foundation and Contributors. All rights reserved.

These packages incorporate third-party components; see `THIRD-PARTY-NOTICES.TXT`
included in each NuGet package and at
https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT.

Licensed under the MIT License (full text below).

## Microsoft.Windows.SDK.BuildTools.WinApp

Copyright (c) Microsoft Corporation. All rights reserved.

Licensed under the MIT License (full text below).

### MIT License

```
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## Microsoft.Web.WebView2

```
Copyright (C) Microsoft Corporation. All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

   * Redistributions of source code must retain the above copyright
notice, this list of conditions and the following disclaimer.
   * Redistributions in binary form must reproduce the above
copyright notice, this list of conditions and the following disclaimer
in the documentation and/or other materials provided with the
distribution.
   * The name of Microsoft Corporation, or the names of its contributors
may not be used to endorse or promote products derived from this
software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

WebView2 also incorporates third-party material; see `NOTICE.txt` in the NuGet package.

---

## Microsoft Windows App SDK / Windows ML Runtime / Windows SDK Build Tools

Copyright (c) Microsoft Corporation. All rights reserved.

These components are proprietary Microsoft software and are **not** covered by
RssReader's MIT License. They are licensed under the Microsoft Software License
Terms included in each NuGet package (`license.txt` / `sdk_license.txt`):

- Microsoft Windows App SDK: https://github.com/microsoft/WindowsAppSDK
- Microsoft Windows ML Runtime (Microsoft.Windows.AI.MachineLearning)
- Microsoft Windows SDK: https://aka.ms/WinSDKLicenseURL

The files that the Windows App SDK NuGet package places alongside the application
are redistributable under Section 3 ("Distributable Code") of those terms.
These packages also incorporate third-party material; see `NOTICE.txt` /
`ThirdPartyNotices.txt` in each NuGet package.
