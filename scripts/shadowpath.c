// shadowpath.c — بازنویسی مسیر /var/opt/mssql به یک مسیر قابل‌نوشتن کاربر
// بدون نیاز به root: LD_PRELOAD برای اجرای user-space اسکیما SQL Server
#define _GNU_SOURCE
#define _LARGEFILE64_SOURCE
#include <dlfcn.h>
#include <stdarg.h>
#include <stddef.h>
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <fcntl.h>
#include <unistd.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <dirent.h>

static const char *OLDP = "/var/opt/mssql";
static const char *OLDP_REL = "var/opt/mssql";
static const char *NEWP = "/home/z/mssql/mssqldata";
static char g_buf[4096];

// بازنویسی مسیر؛ خروجی: مسیر معتبر یا همان ورودی
static const char *rw(const char *p)
{
    if (!p) return p;
    size_t ol = strlen(OLDP);
    if (strncmp(p, OLDP, ol) == 0 && (p[ol] == '/' || p[ol] == '\0'))
    {
        snprintf(g_buf, sizeof g_buf, "%s%s", NEWP, p + ol);
        return g_buf;
    }
    size_t rl = strlen(OLDP_REL);
    if (strncmp(p, OLDP_REL, rl) == 0 && (p[rl] == '/' || p[rl] == '\0'))
    {
        snprintf(g_buf, sizeof g_buf, "%s/%s", NEWP, p + rl);
        return g_buf;
    }
    return p;
}

#define DECL_REAL(ret, name, ...) static ret (*real_##name)(__VA_ARGS__);
#define LOAD_REAL(name) do { if (!real_##name) real_##name = dlsym(RTLD_NEXT, #name); } while (0)

DECL_REAL(int, mkdir, const char*, mode_t)
int mkdir(const char *path, mode_t mode)
{
    LOAD_REAL(mkdir);
    return real_mkdir(rw(path), mode);
}

DECL_REAL(int, mkdirat, int, const char*, mode_t)
int mkdirat(int dirfd, const char *path, mode_t mode)
{
    LOAD_REAL(mkdirat);
    return real_mkdirat(dirfd, rw(path), mode);
}

DECL_REAL(int, stat, const char*, struct stat*)
int stat(const char *path, struct stat *st)
{
    LOAD_REAL(stat);
    return real_stat(rw(path), st);
}

DECL_REAL(int, stat64, const char*, struct stat64*)
int stat64(const char *path, struct stat64 *st)
{
    LOAD_REAL(stat64);
    return real_stat64(rw(path), st);
}

DECL_REAL(int, lstat, const char*, struct stat*)
int lstat(const char *path, struct stat *st)
{
    LOAD_REAL(lstat);
    return real_lstat(rw(path), st);
}

DECL_REAL(int, fstatat, int, const char*, struct stat*, int)
int fstatat(int dirfd, const char *path, struct stat *st, int flags)
{
    LOAD_REAL(fstatat);
    return real_fstatat(dirfd, rw(path), st, flags);
}

DECL_REAL(int, fstatat64, int, const char*, struct stat64*, int)
int fstatat64(int dirfd, const char *path, struct stat64 *st, int flags)
{
    LOAD_REAL(fstatat64);
    return real_fstatat64(dirfd, rw(path), st, flags);
}

DECL_REAL(int, access, const char*, int)
int access(const char *path, int mode)
{
    LOAD_REAL(access);
    return real_access(rw(path), mode);
}

DECL_REAL(int, faccessat, int, const char*, int, int)
int faccessat(int dirfd, const char *path, int mode, int flags)
{
    LOAD_REAL(faccessat);
    return real_faccessat(dirfd, rw(path), mode, flags);
}

DECL_REAL(int, open, const char*, int, ...)
int open(const char *path, int flags, ...)
{
    LOAD_REAL(open);
    mode_t m = 0;
    if (flags & (O_CREAT | O_TMPFILE)) { va_list a; va_start(a, flags); m = va_arg(a, mode_t); va_end(a); }
    return real_open(rw(path), flags, m);
}

DECL_REAL(int, open64, const char*, int, ...)
int open64(const char *path, int flags, ...)
{
    LOAD_REAL(open64);
    mode_t m = 0;
    if (flags & (O_CREAT | O_TMPFILE)) { va_list a; va_start(a, flags); m = va_arg(a, mode_t); va_end(a); }
    return real_open64(rw(path), flags, m);
}

DECL_REAL(int, openat, int, const char*, int, ...)
int openat(int dirfd, const char *path, int flags, ...)
{
    LOAD_REAL(openat);
    mode_t m = 0;
    if (flags & (O_CREAT | O_TMPFILE)) { va_list a; va_start(a, flags); m = va_arg(a, mode_t); va_end(a); }
    return real_openat(dirfd, rw(path), flags, m);
}

DECL_REAL(int, openat64, int, const char*, int, ...)
int openat64(int dirfd, const char *path, int flags, ...)
{
    LOAD_REAL(openat64);
    mode_t m = 0;
    if (flags & (O_CREAT | O_TMPFILE)) { va_list a; va_start(a, flags); m = va_arg(a, mode_t); va_end(a); }
    return real_openat64(dirfd, rw(path), flags, m);
}

DECL_REAL(FILE*, fopen, const char*, const char*)
FILE *fopen(const char *path, const char *m)
{
    LOAD_REAL(fopen);
    return real_fopen(rw(path), m);
}

DECL_REAL(FILE*, fopen64, const char*, const char*)
FILE *fopen64(const char *path, const char *m)
{
    LOAD_REAL(fopen64);
    return real_fopen64(rw(path), m);
}

DECL_REAL(int, unlink, const char*)
int unlink(const char *path)
{
    LOAD_REAL(unlink);
    return real_unlink(rw(path));
}

DECL_REAL(int, unlinkat, int, const char*, int)
int unlinkat(int dirfd, const char *path, int flags)
{
    LOAD_REAL(unlinkat);
    return real_unlinkat(dirfd, rw(path), flags);
}

DECL_REAL(int, rmdir, const char*)
int rmdir(const char *path)
{
    LOAD_REAL(rmdir);
    return real_rmdir(rw(path));
}

DECL_REAL(int, rename, const char*, const char*)
int rename(const char *o, const char *n)
{
    LOAD_REAL(rename);
    return real_rename(rw(o), rw(n));
}

DECL_REAL(DIR*, opendir, const char*)
DIR *opendir(const char *path)
{
    LOAD_REAL(opendir);
    return real_opendir(rw(path));
}

DECL_REAL(char*, realpath, const char*, char*)
char *realpath(const char *path, char *res)
{
    LOAD_REAL(realpath);
    return real_realpath(rw(path), res);
}
