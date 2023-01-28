/// <binding BeforeBuild='build' Clean='clean' ProjectOpened='build' />

const gulp = require('gulp');
const merge = require('merge-stream');
const sass = require('gulp-sass')(require("sass"));
const del = require('del');

const paths = {};
paths.webroot = './wwwroot/';
paths.npmDir = './node_modules/';
paths.sassDir = './Sass/';
paths.libDir = paths.webroot + 'lib/';
paths.cssDir = paths.webroot + 'css/';
paths.jsDir = paths.webroot + 'js/';

paths.sass = paths.sassDir + '**/*.scss';
paths.minCss = paths.cssDir + '**/*.min.css';
paths.js = paths.jsDir + '**/*.js';
paths.minJs = paths.jsDir + '**/*.min.js';
paths.libJs = paths.libDir + '**/*.js';
paths.libMinJs = paths.libDir + '**/*.min.js';

function clean() {
    return del([paths.minJs, paths.cssDir, paths.libDir]);
}

function lib() {
    const libs = [
        {
            src: paths.npmDir + 'bootstrap/dist/js/*',
            dest: paths.libDir + 'bootstrap/js'
        },
        {
            src: paths.npmDir + 'popper.js/dist/umd/*',
            dest: paths.libDir + 'popper'
        },
        {
            src: paths.npmDir + 'font-awesome/css/*',
            dest: paths.libDir + 'font-awesome/css'
        },
        {
            src: paths.npmDir + 'font-awesome/fonts/*',
            dest: paths.libDir + 'font-awesome/fonts'
        },
        {
            src: paths.npmDir + 'jquery/dist/jquery.slim*',
            dest: paths.libDir + 'jquery'
        },
    ];

    const tasks = libs.map((lib) => {
        return gulp.src(lib.src).pipe(gulp.dest(lib.dest));
    });
    return merge(tasks);
}

function runSass() {
    return gulp.src(paths.sass)
        .pipe(sass({ outputStyle: 'compressed' }).on('error', sass.logError))
        .pipe(gulp.dest(paths.cssDir));
}

function sassWatch() {
    gulp.watch(paths.sass, runSass);
}

exports.build = gulp.series(clean, gulp.parallel([lib, runSass]));
exports['sass:watch'] = sassWatch;
exports.sass = runSass;
exports.lib = lib;
exports.clean = clean;
