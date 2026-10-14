# PasteProtect

A small Windows system-tray app that pastes your clipboard with find-and-replace rules applied, keeping the original capitalization.

Copy text as usual, then press **Ctrl+Shift+Alt+V** instead of Ctrl+V. PasteProtect pastes the text with your rules applied, then puts the original clipboard back. It's handy for hiding client names, project names or other sensitive words before pasting into chats, tickets or emails.

```
Rule:    acme → contoso
Copied:  Acme and ACME signed with acme.com
Pasted:  Contoso and CONTOSO signed with contoso.com
```

## Features

- **Case-preserving replacements.** The replacement follows the capitalization of the text it replaces: `acme` → `contoso`, `ACME` → `CONTOSO`, `Acme` → `Contoso`, `Acme Corp` → `Contoso Ltd`, `acmeCorp` → `contosoLtd`.
- **Per-rule options:**
  - **Whole word**: only match when the text isn't part of a longer word.
  - **Case**: *Preserve case* (default), *Ignore case* (insert the replacement exactly as typed, e.g. `[REDACTED]`), or *Match case* (only match the exact capitalization).
- **Priority order.** Where rules overlap, the one higher in the list wins. Reorder by dragging the rule numbers, with the ▲ ▼ buttons, or with Alt+Up / Alt+Down.
- **Live preview.** Type sample text in the settings window to see the result as you edit rules.
- **Original clipboard restored.** After pasting, the original clipboard (including formatting, images and files) is put back, so a normal Ctrl+V still pastes the original. The temporary replaced text is kept out of Windows clipboard history.
- **Always plain text.** A special paste is always plain text, so hidden formatting such as link addresses can't carry the original words along.
- **Configurable shortcut**, restore delay, and optional start with Windows.

## Requirements

- Windows 10 or 11
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (to run), or the .NET 10 SDK (to build)

## Usage

1. Run `PasteProtect.exe`. It appears in the system tray, and the settings window opens on first run.
2. Add rules in the table: type the text to find in **Find** and what to paste instead in **Replace with**.
3. Click **Save**.
4. Copy text anywhere and press **Ctrl+Shift+Alt+V** to paste it with the rules applied.

Left-click the tray icon to open the settings. Right-click it to pause special paste, turn on start with Windows, or exit.

Settings are stored in `%APPDATA%\PasteProtect\settings.json`.

### Notes

- The paste happens when you release the shortcut keys.
- If the clipboard can't be read or changed, PasteProtect beeps and pastes nothing rather than pasting the original text.
- Windows doesn't let a normal app send keystrokes to apps running as administrator. To paste into those, run PasteProtect as administrator too.

## Building

```
dotnet build
dotnet test
```

Run from source:

```
dotnet run --project src/PasteProtect
```

Publish a single-file exe (requires the .NET 10 Desktop Runtime on the target PC):

```
dotnet publish src/PasteProtect -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o publish
```

## License

[MIT](LICENSE)
