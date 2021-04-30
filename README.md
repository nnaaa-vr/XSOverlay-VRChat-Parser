# XSOverlay VRChat Parser
A tool for parsing the VRChat output log and leveraging [XSNotifications](https://github.com/nnaaa-vr/XSNotifications) to display notifications to the user via [XSOverlay](https://store.steampowered.com/app/1173510/XSOverlay/). The tool is useful in desktop mode as well, and will continue to output to the UI. Events currently include: Player Joined, Player Left, Portal Dropped, World Changed, and Shader Keywords Exceeded. The application is built on .NET 5.0 and both framework-dependent and standalone builds can be found in [Releases](https://github.com/nnaaa-vr/XSOverlay-VRChat-Parser/releases). Things are a little messy right now, but should run fine.

I'm currentling leveraging [Avalonia](https://github.com/AvaloniaUI/Avalonia) for the UI, but I plan to move away from it after .NET 6 and a first party framework like [MAUI](https://github.com/dotnet/maui) are stable.

Improvements and feature implementations are welcome, so feel free to submit your PRs if you do something cool!

-------------
#### Contributions welcome

For news and updates about the parser, feel free to contact me here on GitHub, in the [XSOverlay Discord](https://discord.gg/PvccFrfqTw), or even my [Twitter](https://twitter.com/nnaaa_vr).

--------------
#### Session Logs

When reporting bugs or simply to review past sessions, note that every session with the application has its output logged to the following directory:

>"%AppData%\\..\\LocalLow\\VRChat\\vrchat\\Logs

--------------
#### Advanced Configuration

For the time being, only a subset of the available configuration options are exposed to the GUI. To access additional configuration options like how frequently the parser checks for updates, what sounds it uses, what icons it uses, where it looks for output logs, please see the configuration json file at the following path:

>"%AppData%\\..\\LocalLow\\VRChat\\vrchat\\config.json

--------------
#### Interface
An example of the interface can be seen below. 

![](https://github.com/nnaaa-vr/XSOverlay-VRChat-Parser/blob/development-avalonia/SampleImages/GUISample.png)

-------------
#### Known Issues

- Some glyphs don't display correctly in the Event Log. There's a known issue with the UI framework regarding glyph fallbacks for font families. This will either be resolved in a later patch, or when we move to a new UI framework. Most names will display correctly, and I've embedded Noto for its reasonable support of many languages.

- Currently, the parser does not detect whether or not XSOverlay is running, and does not fall back to playing audio on your primary audio device in the event that it can't have XSOverlay play audio for it. This also means it does not have an option to close when XSOverlay closes at this time. This is a planned feature. 

- The window is currently not resizable. The UI isn't built in a way that is conducive to expanding and contracting due to time constraints and my relative unfamiliarity with XAML. This isn't currently a high priority item, but is on the list!

- Hyperlinks don't currently work in the Event Log. This is due to a defect in AvaloniaEdit that I came across while adding support for the generation of world links in the log. I've created an issue for it [here](https://github.com/AvaloniaUI/AvaloniaEdit/issues/133) and am waiting to hear back on it. 