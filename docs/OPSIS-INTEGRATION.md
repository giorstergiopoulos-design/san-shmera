# Integration with Opsis (widget and News Crawler)

Full description of the contract and of who does what: `OpsisNewsCrawler\docs\PLUGIN-FEEDS-AND-WIDGETS.md`.

## What the plugin offers
* **Widget** `calendar` (replaces Opsis' built-in calendar): the Chronikologio calendar panel, with the flag for the country, the dates of both calendars, holidays and fasts.
* **Feed** `san-simera` («Σαν σήμερα») for the bars of the News Crawler: holidays and fast of the day, who celebrates (with the old calendar date), the saints, the events, the births.

## What the host must have
An Opsis built after 2026-11-02 (its `OpsisNewsCrawler.Plugin.Abstractions.dll` contains the text `PluginFeeds`). The Opsis 2.3.1 installed under `C:\Program Files\Opsis - Desktop Customization`
has the older contract: it skips the plugin and writes `Could not load type '...IPluginWidgetProvider'` to `%AppData%\Opsis\plugins.log`.
The current Opsis builds are in `MotionDeskStudio\bin\Release\net10.0-windows10.0.19041.0\win-x64`.

## Install and check
1. Close Opsis. Run `install-plugin.bat` (or the setup, component "Plugin"): it copies `dist\plugin` to `%AppData%\Opsis\plugins\Chronikologio`.
2. Start an Opsis with the new contract; approve the plugin when asked (it asks again after every new build: the approval is the SHA-256 of the DLL).
3. Desktop: the calendar widget is the Chronikologio one. News Crawler settings: a bar slot can show «Σαν σήμερα».
4. `%AppData%\Opsis\plugins.log` shows any load error.

## Tests
`test-all.bat` runs this repository's tests (`HostLoadingTests` loads the DLL the way `PluginHost` does) and the three Opsis suites.