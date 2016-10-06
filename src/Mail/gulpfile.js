/// <binding BeforeBuild='build, dist' Clean='clean' ProjectOpened='build. dist' />

var gulp = require('gulp'),
    rimraf = require('rimraf'),
    premailer = require('gulp-premailer');

var paths = {
    dist: '../../mail_dist/',
    wwwroot: './wwwroot/'
};

gulp.task('inline', ['clean'], function () {
    return gulp.src(paths.wwwroot + 'templates/*.html')
        .pipe(premailer())
        .pipe(gulp.dest(paths.dist));
});

gulp.task('clean', function (cb) {
    return rimraf(paths.dist, cb);
});
