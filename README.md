# Gramatik

Gramatik is a Windows tray app that corrects selected text through OpenRouter. It can either preserve the detected source language or correct and translate the selection into English.

## Quick Install

Requirements:

- Windows
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git

Clone, compile, test, and run:

```powershell
git clone https://github.com/exen675-collab/Gramatik.git
cd Gramatik
dotnet restore .\Gramatik.sln
dotnet build .\Gramatik.sln -c Release
dotnet test .\Gramatik.sln -c Release --no-build
dotnet run --project .\Gramatik.App\Gramatik.App.csproj -c Release
```

Optional publish command for a standalone Windows x64 build:

```powershell
dotnet publish .\Gramatik.App\Gramatik.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish\win-x64
.\publish\win-x64\Gramatik.App.exe
```

## Run

```powershell
dotnet run --project .\Gramatik.App\Gramatik.App.csproj
```

The app starts in the system tray. Open settings from the tray icon, enter an OpenRouter API key, refresh models, choose a model, and save.

## Defaults

- Correct selected text: `Ctrl+Alt+G`
- Correct and translate to English: `Ctrl+Alt+E`

Both bindings can be changed in settings. Keyboard shortcuts and mouse `Middle`, `XButton1`, and `XButton2` are supported.

## Privacy

The OpenRouter API key is stored under `%AppData%\Gramatik\settings.json` encrypted with Windows DPAPI for the current user. Selected text is not logged or saved by the app.

## Logs

Runtime logs are written to `%AppData%\Gramatik\logs\gramatik.log`. The tray menu has `Open logs folder`, and the settings window includes a log viewer with reload, open-folder, and clear actions.

Logs include app events, hotkey matches, clipboard operation steps, OpenRouter HTTP statuses, selected/replacement text lengths, and exception messages. They do not include selected text, model responses, or the API key.
