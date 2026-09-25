const path = require("path");
const MiniCssExtractPlugin = require("mini-css-extract-plugin");

const paths = {
  assets: "./wwwroot/assets/",
  sassDir: "./Sass/",
};

/** @type {import("webpack").Configuration} */
module.exports = {
  mode: "production",
  devtool: "source-map",
  entry: {
    site: [
      path.resolve(__dirname, paths.sassDir, "site.scss"),
      "bootstrap",
      "jquery",
      "font-awesome/css/font-awesome.css",
    ],
  },
  output: {
    clean: true,
    path: path.resolve(__dirname, paths.assets),
  },
  module: {
    rules: [
      {
        test: /\.(sa|sc|c)ss$/,
        use: [MiniCssExtractPlugin.loader, "css-loader", "sass-loader"],
      },
      {
        test: /.(ttf|otf|eot|svg|woff(2)?)(\?[a-z0-9]+)?$/,
        exclude: /loading(|-white).svg/,
        generator: {
          filename: "fonts/[name].[contenthash][ext]",
        },
        type: "asset/resource",
      },

      // Expose jquery globally so they can be used directly in asp.net
      {
        test: require.resolve("jquery"),
        loader: "expose-loader",
        options: {
          exposes: ["$", "jQuery"],
        },
      },
    ],
  },
  resolve: {
    alias: {
      // jQuery 4 ships an exports map that sends bundlers to the ESM build,
      // which doesn't match the path used by expose-loader's `test`. Force
      // webpack to resolve "jquery" to the CJS build so the expose-loader
      // rule fires and window.$ / window.jQuery are set for inline scripts.
      jquery: require.resolve("jquery"),
    },
  },
  plugins: [
    new MiniCssExtractPlugin({
      filename: "[name].css",
    }),
  ],
};
