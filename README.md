# Marquee Reminder

A small Windows 10/11 (x64) app that shows your reminders as scrolling **marquee bars** on the
edge of the desktop, at the time you schedule them.

## Features

- **Any number of reminders.** Each has its own text, schedule and look.
- **Schedules:** once on a date, every day, weekly on the days you pick (e.g. Mon + Fri), or
  monthly on a day of the month. For day 29 to 31, shorter months use their last day.
- **Appearance:** background colour, text colour, font, bar thickness, scroll speed, and how long
  the bar stays up (or until you dismiss it).
- **Position:** top, bottom, left or right edge of the primary screen (above the taskbar), plus an
  **offset** in pixels. A positive offset
  moves the bar inward, towards the centre of the screen. A negative offset pushes it outward past
  the edge. Several bars on the same edge stack instead of overlapping.
- **Preview** any reminder on screen before saving it.
- **Starts with Windows** automatically. This is switched on the first time you run the app and
  starts it silently in the tray when you sign in. Turn it off or on with "Start with Windows" in
  the main window or the tray menu. If you move the app, the startup entry follows it.
- **Keeps running when you close the window.** Closing (X) only hides it to the notification area
  (system tray) so reminders still fire. Press **Ctrl+Q** in the window, or choose **Quit** in the
  tray menu, to exit completely. Only one copy runs at a time.
- Marquees never take focus from whatever you are typing in. Double-click a bar (or right-click →
  Dismiss) to close it.

## Timing guarantees

Reminders use local wall-clock time, so "09:00" means 09:00 on the clock, including across
daylight-saving changes. The schedule is checked twice a second and is covered by unit tests.

| Situation | Behaviour |
|---|---|
| Normal | Fires within half a second of the scheduled minute. |
| Clocks go back (the same hour happens twice) | Fires once, not twice. |
| Clocks go forward and skip the scheduled time | Fires as soon as the clock jumps past it. |
| PC was asleep or the app was closed at the scheduled time | Fires when the app wakes up, if it was missed by 30 minutes or less. Older misses are skipped so you don't get a pile of stale bars. |
| System time or time zone changed | Picked up immediately. |
| Reminder created, edited or re-enabled after today's time has passed | Waits for the next occurrence. Never fires "in the past". |

## Requirements

- Windows 10 or 11, x64.
- .NET Framework 4.8, which ships with current Windows 10 and 11. Nothing else needs installing.

## Build

You need a .NET SDK (6 or later) to build.

```powershell
dotnet build MarqueeReminder.sln -c Release
dotnet test  MarqueeReminder.sln -c Release
```

The app is written to `src\MarqueeReminder\bin\Release\net48\`. Copy that folder anywhere and run
`MarqueeReminder.exe`.

## Where data lives

Reminders are saved to `%APPDATA%\MarqueeReminder\reminders.json`. The file is readable JSON and is
written atomically. If it is ever corrupted, the app keeps a `.corrupt-<timestamp>` copy and starts
empty instead of overwriting it. Unexpected errors are logged to `%APPDATA%\MarqueeReminder\error.log`.

## Project layout

```
src/MarqueeReminder.Core     Models, scheduling engine, JSON storage, layout math (no UI, fully tested)
src/MarqueeReminder          WinForms app: tray, main window, editor, marquee windows
tests/MarqueeReminder.Core.Tests   xUnit tests for scheduling, storage and layout
```
