Simple VRChat log parser leveraging the XSNotifications library at https://github.com/nnaaa-vr/XSNotifications to display toast notifications in VR. Currently supported events include world changes, player joined, player left, portal dropped, and shader keywords exceeded. Logged events are also saved with timestamps to session logs, so previous sessions can be referenced if necessary (see who was where, what world you were in, et cetera).

On first run, a config.json file will be generated at `%AppData%\..\LocalLow\XSOverlay VRChat Parser\config.json`. Please see this file for configuration options, and then restart the process after making any changes.

The process runs in the background and does not have an active window. 
The parsing is a bit messy at the moment (but functional). VRChat's log output is very inconsistent and has changed fairly frequently. The parsing function was written around two years ago and has just been patched as time went on.

There are plans to expand the feature set of this application, including additional event types for those running extended logging in VRChat. Of course, feel free to make PRs yourselves!

More detailed documentation will come soon.
