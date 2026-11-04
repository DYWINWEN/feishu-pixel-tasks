# Third-party notices

- **Fusion Pixel Font**, TakWolf, release `2026.10.25`: embedded Simplified Chinese 12px monospaced TTF, unmodified. Licensed under SIL Open Font License 1.1. Full notice: `src/FeishuPixelTasks/Web/fonts/LICENSE-OFL.txt` (included as `Web/fonts/LICENSE-OFL.txt` in distributions). Source: https://github.com/TakWolf/fusion-pixel-font/releases/tag/2026.10.25
- **Microsoft WebView2 SDK**, NuGet package `Microsoft.Web.WebView2` 1.0.4314.35, distributed under Microsoft's package license. Source and license: https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4314.35
- **.NET runtime / System.Security.Cryptography.ProtectedData**: Microsoft and .NET contributors, MIT; runtime distribution includes its license notices. https://github.com/dotnet/runtime
- **Playwright** is a development/test dependency, Apache-2.0. https://github.com/microsoft/playwright

The application is based on a user-supplied PowerShell/WebView2 task-widget prototype. The original private archive and runtime data are not distributed. The `lark-cli` command contract is preserved; the CLI executable itself is not included.

The two texture atlases in `Web/assets/` were produced with the built-in image generation tool from the user's approved UI concepts, with all task text removed. They are project UI assets, rendered using nine-slice Canvas decoration behind live DOM controls. The generation prompt is in `docs/skin-atlas-prompt.txt`. No Minecraft game textures or logos are bundled. This project is not affiliated with Mojang, Microsoft Minecraft, or Feishu/Lark.
