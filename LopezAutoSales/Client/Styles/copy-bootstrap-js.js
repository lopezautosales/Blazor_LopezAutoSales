// Copies Bootstrap's prebuilt JS bundle out of node_modules into wwwroot, so the
// shipped JS stays pinned to the same `bootstrap` version as the SCSS-built CSS
// (see package.json devDependencies). Run via `npm run js-build`.
const fs = require("fs");
const path = require("path");

const src = path.join(__dirname, "node_modules", "bootstrap", "dist", "js");
const dest = path.join(__dirname, "..", "wwwroot", "dist", "js");
const files = [
  "bootstrap.bundle.min.js",
  "bootstrap.bundle.min.js.map",
  "bootstrap.bundle.js",
  "bootstrap.bundle.js.map",
];

fs.mkdirSync(dest, { recursive: true });
for (const f of files) {
  fs.copyFileSync(path.join(src, f), path.join(dest, f));
}
