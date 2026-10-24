# References and third-party notices

The repository ships no downloaded third-party libraries, Valve binaries, Steam indexes, Steam schema files, game artwork or user recordings. Runtime references are the Windows .NET Framework assemblies already installed on the machine; their licenses are provided by Microsoft.

Format documentation used as reference:

- [SteamAppInfo](https://github.com/xPaw/SteamAppInfo), an MIT-licensed project documenting Steam's appinfo versions and binary structure. The appinfo reader here is an independent, name-only implementation; the upstream parser and ValveKeyValue library are not bundled.
- [SteamTracking/Protobufs](https://github.com/SteamTracking/Protobufs), especially `steam/webuimessages_gamerecordingfiles.proto`, describes internally tracked recording-index fields. This repository implements a small wire-format editor; it does not bundle generated upstream code or schema files. Tracking a format does not make it a stable, Valve-supported editing API.
- [Steam Game Recording support](https://help.steampowered.com/en/faqs/view/23B7-49AD-4A28-9590), for recording behavior and export concepts.

Steam and associated marks are owned by Valve. This project is not endorsed by Valve. If future changes vendor third-party source or assets, include the applicable license and attribution rather than relying on this reference-only notice.
