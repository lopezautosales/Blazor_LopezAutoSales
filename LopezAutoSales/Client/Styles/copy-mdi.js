// Copies the Material Design Icons webfont (CSS + font files) out of node_modules
// into wwwroot, pinning the shipped icons to the @mdi/font version in package.json.
// Run via `npm run mdi-build`.
const fs = require("fs");
const path = require("path");

const pkg = path.join(__dirname, "node_modules", "@mdi", "font");
const destRoot = path.join(__dirname, "..", "wwwroot", "dist");

const sets = [
  {
    src: path.join(pkg, "css"),
    dest: path.join(destRoot, "css"),
    files: [
      "materialdesignicons.css",
      "materialdesignicons.css.map",
      "materialdesignicons.min.css",
      "materialdesignicons.min.css.map",
    ],
  },
  {
    src: path.join(pkg, "fonts"),
    dest: path.join(destRoot, "fonts"),
    files: [
      "materialdesignicons-webfont.eot",
      "materialdesignicons-webfont.ttf",
      "materialdesignicons-webfont.woff",
      "materialdesignicons-webfont.woff2",
    ],
  },
];

for (const { src, dest, files } of sets) {
  fs.mkdirSync(dest, { recursive: true });
  for (const f of files) {
    fs.copyFileSync(path.join(src, f), path.join(dest, f));
  }
}
