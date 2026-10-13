import { cp, mkdir, readdir, writeFile } from "node:fs/promises";
import { execSync } from "node:child_process";
import { join } from "node:path";
import pug from "pug";

const OUT_DIR = "../ContactBook.WebApi/wwwroot";
const TEMPLATES_DIR = "src/templates";
const STYLES_SRC = "src/styles/styles.css";
const STYLES_DST = join(OUT_DIR, "styles.css");

async function buildTemplates() {
    const files = await readdir(TEMPLATES_DIR);
    for (const file of files) {
        if (!file.endsWith(".pug")) continue;
        const src = join(TEMPLATES_DIR, file);
        const dst = join(OUT_DIR, file.replace(/\.pug$/, ".html"));
        const html = pug.renderFile(src, { pretty: true });
        await writeFile(dst, html, "utf-8");
        console.log(`  pug   ${file} -> ${dst}`);
    }
}

function buildTypeScript() {
    execSync("npx tsc", { stdio: "inherit" });
}

async function buildStyles() {
    await cp(STYLES_SRC, STYLES_DST);
    console.log(`  css   ${STYLES_SRC} -> ${STYLES_DST}`);
}

async function main() {
    console.log("Building frontend...");
    await mkdir(OUT_DIR, { recursive: true });
    await buildTemplates();
    buildTypeScript();
    await buildStyles();
    console.log("Done.");
}

main().catch(err => {
    console.error(err);
    process.exit(1);
});