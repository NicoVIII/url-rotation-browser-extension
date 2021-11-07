# Url Rotation

[![GitHub Actions Build](https://img.shields.io/github/workflow/status/NicoVIII/url-rotation-browser-extension/Build?style=flat-square)](https://github.com/NicoVIII/url-rotation-browser-extension/actions/workflows/build.yml)
![Last commit](https://img.shields.io/github/last-commit/NicoVIII/url-rotation-browser-extension?style=flat-square)
[![Gitpod ready-to-code](https://img.shields.io/badge/Gitpod-ready--to--code-blue?style=flat-square&logo=gitpod)](https://gitpod.io/#https://github.com/NicoVIII/url-rotation-browser-extension)

This is a browser extension in development which can be used for screens which should show different
urls. It is made for Firefox but will be made compatible with Chrome, if its ready.

It is inspired by [Tab Rotate for Chrome](https://github.com/KevinSheedy/chrome-tab-rotate).

## Development

![Visualization](images/diagram.svg)

You should use the devcontainer for VScode to develop this.
You can build this and enter watch mode with `dotnet run watch`.
After that you can open `about:debugging` in Firefox and add the manifest.json as a temporary extension
for testing.
