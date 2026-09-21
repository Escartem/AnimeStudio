//{{NO_DEPENDENCIES}}
// Microsoft Visual C++ generated include file.
// Used by DirectX9.rc
// Used by DirectX11.rc
// Used by NVAPI.rc

// Next default values for new objects
// 
#ifdef APSTUDIO_INVOKED
#ifndef APSTUDIO_READONLY_SYMBOLS
#define _APS_NEXT_RESOURCE_VALUE        101
#define _APS_NEXT_COMMAND_VALUE         40001
#define _APS_NEXT_CONTROL_VALUE         1001
#define _APS_NEXT_SYMED_VALUE           101
#endif
#endif

// Used for embedded font resource for overlay
#define SPRITEFONT					256
#define IDR_COURIERBOLD				101
#define IDR_COURIERSMALL			102
#define IDR_ARIAL				103


// Actual version, name, copyright information that is built into the dlls.
// This is what an end-user will see when they look at dll properties.
// The VERSION_MAJOR and VERSION_MINOR are hand edited when versions are to change.
// The VERSION_REVISION is automatically incremented for every Publish build.

#define VERSION_MAJOR               1
#define VERSION_MINOR               4
#define VERSION_REVISION            9
 
#define STRINGIZE2(s) #s
#define STRINGIZE(s) STRINGIZE2(s)

#define VER_FILE_VERSION            VERSION_MAJOR, VERSION_MINOR, VERSION_REVISION
#define VER_FILE_VERSION_STR        STRINGIZE(VERSION_MAJOR)        \
                                    "." STRINGIZE(VERSION_MINOR)    \
                                    "." STRINGIZE(VERSION_REVISION) \
 
#define VER_PRODUCTNAME_STR         "3Dmigoto"
#define VER_PRODUCT_VERSION         VER_FILE_VERSION
#define VER_PRODUCT_VERSION_STR     VER_FILE_VERSION_STR
#define VER_COPYRIGHT_STR           "Copyright (C) 2014-2019"
 
#ifdef _DEBUG
  #define VER_VER_DEBUG             VS_FF_DEBUG
#else
  #define VER_VER_DEBUG             0
#endif
 
#define VER_FILEOS                  VOS_NT_WINDOWS32
#define VER_FILEFLAGS               VER_VER_DEBUG
#define VER_FILETYPE                VFT_APP
