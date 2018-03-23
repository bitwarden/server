/// <binding BeforeBuild='build' Clean='clean' ProjectOpened='build' />

const gulp = require('gulp'),
    rimraf = require('rimraf'),
    merge = require('merge-stream'),
    runSequence = require('run-sequence'),
    concat = require('gulp-concat'),
    cssmin = require('gulp-cssmin'),
    uglify = require('gulp-uglify'),
    sass = require('gulp-sass');

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

gulp.task('clean:js', (cb) => {
    rimraf(paths.minJs, cb);
});

gulp.task('clean:css', (cb) => {
    rimraf(paths.cssDir, cb);
});

gulp.task('clean:lib', (cb) => {
    rimraf(paths.libDir, cb);
});

gulp.task('clean', ['clean:js', 'clean:css', 'clean:lib']);

gulp.task('lib', ['clean:lib'], () => {
    const libs = [
        {
            src: paths.npmDir + 'bootstrap/dist/js/*',
            dest: paths.libDir + 'bootstrap/js'
        },
        {
            src: paths.npmDir + 'popper.js/dist/*',
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
});

gulp.task('sass', () => {
    return gulp.src(paths.sass)
        .pipe(sass({ outputStyle: 'compressed' }).on('error', sass.logError))
        .pipe(gulp.dest(paths.cssDir));
});

gulp.task('sass:watch', () => {
    gulp.watch(paths.sass, ['sass']);
});

gulp.task('build', function (cb) {
    return runSequence('clean', ['lib', 'sass'], cb);
});
