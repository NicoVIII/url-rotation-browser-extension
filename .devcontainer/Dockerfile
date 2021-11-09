FROM mcr.microsoft.com/vscode/devcontainers/dotnet:0-6.0

# [Choice] Node.js version: none, lts/*, 16, 14, 12, 10
ARG NODE_VERSION="lts/*"
RUN if [ "${NODE_VERSION}" != "none" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi

# [Optional] Uncomment this section to install additional OS packages.
# RUN apt-get update && export DEBIAN_FRONTEND=noninteractive \
#     && apt-get -y install --no-install-recommends <your-package-list-here>

# [Optional] Uncomment this line to install global node packages.
RUN su vscode -c "source /usr/local/share/nvm/nvm.sh && npm install -g pnpm" 2>&1

# Install SASS
RUN mkdir /tmp/sass \
    && curl -sSL -o /tmp/sass/sass.tar.gz https://github.com/sass/dart-sass/releases/download/1.43.4/dart-sass-1.43.4-linux-x64.tar.gz \
    && tar xzvf /tmp/sass/sass.tar.gz -C /tmp/sass \
    && mv /tmp/sass/dart-sass/sass /usr/local/bin \
    && rm -r /tmp/sass
