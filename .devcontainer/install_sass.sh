#!/bin/bash
mkdir /tmp/sass \
  && curl -sSL -o /tmp/sass/sass.tar.gz https://github.com/sass/dart-sass/releases/download/1.80.6/dart-sass-1.80.6-linux-x64.tar.gz \
  && tar xzvf /tmp/sass/sass.tar.gz -C /tmp/sass \
  && sudo mv /tmp/sass/dart-sass/* /usr/local/bin \
  && rm -r /tmp/sass
