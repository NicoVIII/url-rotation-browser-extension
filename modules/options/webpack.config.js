var path = require("path");

module.exports = {
    entry: path.resolve(__dirname, './dist/App.fs.js'),
    output: {
        filename: "options.js",
        path: path.join(__dirname, "../../dist"),
    }
}
