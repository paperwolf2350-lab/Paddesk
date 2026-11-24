# PadDesk

Use an Xbox-style controller as a mouse on Windows, with a pop-up on-screen keyboard. Browse the web, click around the desktop or type a search without reaching for a mouse and keyboard.

Made as a companion to [Spine](https://github.com/paperwolf2350-lab/spine), the console-style game library for PC, but it works on its own too.

## Get it

Download **PadDesk.exe** from the [latest release](https://github.com/paperwolf2350-lab/paddesk/releases/latest) and run it. No install needed. PadDesk sits in the notification area (bottom right, by the clock). Right-click its icon for settings.

## Controls

| Button | What it does |
|---|---|
| Left stick | Move the pointer |
| Left trigger | Precision: pull further for slower |
| A | Click (hold to drag) |
| B | Right-click (hold to drag) |
| X | Middle-click (hold to drag) |
| Right stick | Scroll up, down, left and right |
| Y | Open or close the keyboard |
| Hold View + Menu (1 second) | Turn PadDesk on or off for the app you're in. It remembers. |

**On the keyboard:** D-pad picks a key, A types, B deletes, X adds a space, Y closes. LB is Shift, RB moves the keyboard to the other edge of the screen, and pressing the right stick hides or shows the typing preview.

## Stays out of the way in games

PadDesk turns itself off in emulators, controller-driven frontends and most fullscreen games, so they never get stray clicks. It stays on in browsers and video players. You can set any app to Automatic, Always on or Always off from the tray menu.

Settings are kept in `%APPDATA%\PadDesk\settings.ini`. Other programs can add their own rules as text files in `%APPDATA%\PadDesk\lists\`, one rule per line, like `off myemulator` or `on mybrowser`. A `*` at the end matches every name starting with it (`pcsx2* = off`).

## Build it yourself

Run `build.bat`. It uses the C# compiler that comes with Windows (.NET Framework 4), so there's nothing to install. `PadDesk.exe --check` runs its self-tests.

## License

MIT
