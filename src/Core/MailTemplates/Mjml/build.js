const mjml2html = require("mjml");
const { registerComponent } = require("mjml-core");
const fs = require("fs");
const path = require("path");
const glob = require("glob");

// Parse command line arguments
const args = process.argv.slice(2); // Remove 'node' and script path

// Parse flags
const flags = {
  minify: args.includes("--minify") || args.includes("-m"),
  watch: args.includes("--watch") || args.includes("-w"),
  hbs: args.includes("--hbs") || args.includes("-h"),
  trace: args.includes("--trace") || args.includes("-t"),
  clean: args.includes("--clean") || args.includes("-c"),
  help: args.includes("--help"),
};

// Use __dirname to get absolute paths relative to the script location
const config = {
  inputDir: path.join(__dirname, "emails"),
  outputDir: path.join(__dirname, "out"),
  minify: flags.minify,
  validationLevel: "strict",
  hbsOutput: flags.hbs,
};

// Debug output
if (flags.trace) {
  console.log("[DEBUG] Script location:", __dirname);
  console.log("[DEBUG] Input directory:", config.inputDir);
  console.log("[DEBUG] Output directory:", config.outputDir);
}

// Ensure output directory exists
if (!fs.existsSync(config.outputDir)) {
  fs.mkdirSync(config.outputDir, { recursive: true });
  if (flags.trace) {
    console.log("[INFO] Created output directory:", config.outputDir);
  }
}

// Find all MJML files with absolute paths, excluding components directories
const mjmlFiles = glob.sync(`${config.inputDir}/**/*.mjml`, {
  ignore: ['**/components/**']
});

console.log(`\n[INFO] Found ${mjmlFiles.length} MJML file(s) to compile...`);

if (mjmlFiles.length === 0) {
  console.error("[ERROR] No MJML files found!");
  console.error("[ERROR] Looked in:", config.inputDir);
  console.error(
    "[ERROR] Does this directory exist?",
    fs.existsSync(config.inputDir),
  );
  process.exit(1);
}

// Compile each MJML file
let successCount = 0;
let errorCount = 0;

mjmlFiles.forEach((filePath) => {
  try {
    const mjmlContent = fs.readFileSync(filePath, "utf8");
    const fileName = path.basename(filePath, ".mjml");
    const relativePath = path.relative(config.inputDir, filePath);

    console.log(`\n[BUILD] Compiling: ${relativePath}`);

    // Compile MJML to HTML
    const result = mjml2html(mjmlContent, {
      minify: config.minify,
      validationLevel: config.validationLevel,
      filePath: filePath, // Important: tells MJML where the file is for resolving includes
      mjmlConfigPath: __dirname, // Point to the directory with .mjmlconfig
    });

    // Check for errors
    if (result.errors.length > 0) {
      console.error(`[ERROR] Failed to compile ${fileName}.mjml:`);
      result.errors.forEach((err) =>
        console.error(`        ${err.formattedMessage}`),
      );
      errorCount++;
      return;
    }

    // Calculate output path preserving directory structure
    const relativeDir = path.dirname(relativePath);
    const outputDir = path.join(config.outputDir, relativeDir);

    // Ensure subdirectory exists
    if (!fs.existsSync(outputDir)) {
      fs.mkdirSync(outputDir, { recursive: true });
    }

    const outputExtension = config.hbsOutput ? ".html.hbs" : ".html";
    const outputPath = path.join(outputDir, `${fileName}${outputExtension}`);
    fs.writeFileSync(outputPath, result.html);

    console.log(
      `[OK] Built: ${fileName}.mjml → ${path.relative(__dirname, outputPath)}`,
    );
    successCount++;

    // Log warnings if any
    if (result.warnings && result.warnings.length > 0) {
      console.warn(`[WARN] Warnings for ${fileName}.mjml:`);
      result.warnings.forEach((warn) =>
        console.warn(`       ${warn.formattedMessage}`),
      );
    }
  } catch (error) {
    console.error(`[ERROR] Exception processing ${path.basename(filePath)}:`);
    console.error(`        ${error.message}`);
    errorCount++;
  }
});

console.log(`\n[SUMMARY] Compilation complete!`);
console.log(`          Success: ${successCount}`);
console.log(`          Failed:  ${errorCount}`);
console.log(`          Output:  ${config.outputDir}`);

if (errorCount > 0) {
  process.exit(1);
}
