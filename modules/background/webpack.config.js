const path = require('path');

module.exports = {
  entry: './dist/Background.fs.js',
  output: {
    filename: 'background.js',
    path: path.resolve(__dirname, '../../dist/'),
  },
};
