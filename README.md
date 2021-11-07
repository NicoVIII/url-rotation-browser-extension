# Url Rotation

[![GitHub Actions Build](https://img.shields.io/github/workflow/status/NicoVIII/url-rotation-browser-extension/Build?style=flat-square)](https://github.com/NicoVIII/url-rotation-browser-extension/actions/workflows/build.yml)
![Last commit](https://img.shields.io/github/last-commit/NicoVIII/url-rotation-browser-extension?style=flat-square)
[![Gitpod ready-to-code](https://img.shields.io/badge/Gitpod-ready--to--code-blue?style=flat-square&logo=gitpod)](https://gitpod.io/#https://github.com/NicoVIII/url-rotation-browser-extension)

This is a browser extension in development which can be used for screens which should show different
urls. It is made for Firefox but will be made compatible with Chrome, if its ready.

It is inspired by [Tab Rotate for Chrome](https://github.com/KevinSheedy/chrome-tab-rotate).

To use the extnesion, first install it (either as a temporary addon or from extension store). Then open the
preferences of the extension and configure it like you need it. After that you can click
on the icon in the extension bar and start presenting the configured urls.

If you change the tab or close one of the tabs managed by the application, the playback will stop and
needs to be started again. The current state of the extension is easily seen at the icon.
It will show a play button if it is paused and a pause button if the rotation is running.

## Development

![Visualization](images/diagram.svg)

You should use the devcontainer for VScode to develop this. For this you need VScode and Docker installed.
For VScode you should install the "Remote - Containers" extension. After that you can open the repo
in the devcontainer with the "Remote Containers: Rebuild and Reopen in Container" command.

You can then build this and enter watch mode with `dotnet run watch`.
After that you can open `about:debugging` in Firefox and add the manifest.json as a temporary extension
for testing.

If you want to just build once, run `dotnet run build`.
If you want to pack the extension ready to use (for Firefox only atm) you can run `dotnet run pack`.
