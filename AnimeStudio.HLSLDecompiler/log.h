#pragma once

using namespace std;

#include <string>
#include <ctime>

// Wrappers to make logging cleaner.

extern FILE *LogFile;
extern bool gLogDebug;

// Note that for now I've left the definitions of LogFile and LogDebug as they
// were - either declared locally in a file, as an extern, or from another
// namespace altogether. At some point this needs to be cleaned up, but it's
// probably not worth doing so unless we were switching to use a central
// logging framework.

#define LogInfo(fmt, ...) \
	do { if (LogFile) fprintf(LogFile, fmt, __VA_ARGS__); } while (0)
#define vLogInfo(fmt, va_args) \
	do { if (LogFile) vfprintf(LogFile, fmt, va_args); } while (0)
#define LogInfoW(fmt, ...) \
	do { if (LogFile) fwprintf(LogFile, fmt, __VA_ARGS__); } while (0)
#define vLogInfoW(fmt, va_args) \
	do { if (LogFile) vfwprintf(LogFile, fmt, va_args); } while (0)

#define LogDebug(fmt, ...) \
	do { if (gLogDebug) LogInfo(fmt, __VA_ARGS__); } while (0)
#define vLogDebug(fmt, va_args) \
	do { if (gLogDebug) vLogInfo(fmt, va_args); } while (0)
#define LogDebugW(fmt, ...) \
	do { if (gLogDebug) LogInfoW(fmt, __VA_ARGS__); } while (0)

// Aliases for the above functions that we use to denote that omitting the
// newline was done intentionally. For now this is just for our reference, but
// later we might actually make the default function insert a newline:
#define LogInfoNoNL LogInfo
#define LogInfoWNoNL LogInfoW
#define LogDebugNoNL LogDebug
#define LogDebugWNoNL LogDebugW

static string LogTime()
{
	string timeStr;
	char cTime[32];
	tm timestruct;

	time_t ltime = time(0);
	localtime_s(&timestruct, &ltime);
	asctime_s(cTime, sizeof(cTime), &timestruct);

	timeStr = cTime;
	return timeStr;
}

