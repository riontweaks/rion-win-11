Unicode true
!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"
!ifndef PACKAGE
 !error "PACKAGE must point to the verified self-contained release."
!endif
!ifndef APP_ICON
 !error "APP_ICON must be the application project's ApplicationIcon."
!endif
!define MUI_ICON "${APP_ICON}"
!define MUI_UNICON "${APP_ICON}"
!ifndef COMPRESSION_DICTIONARY
 !ifdef WEB_INSTALLER
 !define COMPRESSION_DICTIONARY 8
 !else
 !define COMPRESSION_DICTIONARY 64
 !endif
!endif
!ifdef FREE_EDITION
 !define PRODUCT_LABEL "Rion Win 11 Free"
 !define PRODUCT_KEY "RionWin11Free"
!else
 !define PRODUCT_LABEL "Rion Win 11"
 !define PRODUCT_KEY "RionWin11"
!endif
Name "${PRODUCT_LABEL}"
OutFile "${OUTPUT}"
InstallDir "$PROGRAMFILES64\${PRODUCT_LABEL}"
InstallDirRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}" "InstallLocation"
RequestExecutionLevel admin
SetCompressor /SOLID /FINAL lzma
SetCompressorDictSize ${COMPRESSION_DICTIONARY}
SetDatablockOptimize on
ShowInstDetails show
ShowUninstDetails show
VIProductVersion "1.0.0.0"
VIAddVersionKey "ProductName" "${PRODUCT_LABEL} Setup"
VIAddVersionKey "FileDescription" "Rion Win 11 installer"
VIAddVersionKey "FileVersion" "1.0.0"
VIAddVersionKey "LegalCopyright" "Rion and respective component authors"
!define MUI_ABORTWARNING
!ifdef WEB_INSTALLER
 !define MUI_WELCOMEPAGE_TEXT "Setup will download and install Rion Win 11. An internet connection is required."
!endif
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "${PACKAGE}\LICENSE"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!ifndef WEB_INSTALLER
!define MUI_UNCONFIRMPAGE_TEXT_TOP "This removes the application files and shortcut. Profiles, downloaded utilities and recovery history are preserved. System settings changed in Rion are not automatically restored; use the app's Restore actions first if needed."
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!endif
!insertmacro MUI_LANGUAGE "English"

Function .onInit
 ${IfNot} ${RunningX64}
  MessageBox MB_ICONSTOP "Rion Win 11 requires 64-bit Windows 11."
  Abort
 ${EndIf}
 SetRegView 64
 ReadRegStr $0 HKLM "SOFTWARE\Microsoft\Windows NT\CurrentVersion" "CurrentBuildNumber"
 ${If} $0 < 22000
  MessageBox MB_ICONSTOP "Rion Win 11 requires Windows 11 (build 22000 or newer)."
  Abort
 ${EndIf}
 SetShellVarContext all
FunctionEnd

Section "Rion Win 11" Core
 SetRegView 64
 SetShellVarContext all
 ; Do not overwrite an unrelated copy chosen as the installation directory.
 ${If} ${FileExists} "$INSTDIR\Rion Win 11.exe"
 ${AndIfNot} ${FileExists} "$INSTDIR\Uninstall.exe"
  MessageBox MB_ICONSTOP "This folder contains an unmanaged copy of Rion Win 11. Choose a different installation folder."
  Abort
 ${EndIf}
 !ifdef WEB_INSTALLER
 InitPluginsDir
 SetOutPath "$PLUGINSDIR"
 File /oname=Download-Payload.ps1 "${DOWNLOAD_SCRIPT}"
 File /oname=payload.json "${PAYLOAD_CONFIG}"
 DetailPrint "Downloading Rion Win 11..."
 nsExec::ExecToLog '"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\Download-Payload.ps1" -InstallDirectory "$INSTDIR"'
 Pop $0
 ${If} $0 != 0
  MessageBox MB_ICONSTOP "Setup did not complete. Check your connection, close Rion and retry. The details below show what failed."
  SetErrorLevel 1
  Abort
 ${EndIf}
 !else
 !include "${INSTALL_FILES}"
 WriteUninstaller "$INSTDIR\Uninstall.exe"
 CreateDirectory "$SMPROGRAMS\${PRODUCT_LABEL}"
 CreateShortCut "$SMPROGRAMS\${PRODUCT_LABEL}\Rion Win 11.lnk" "$INSTDIR\Rion Win 11.exe"
 WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}" "DisplayName" "${PRODUCT_LABEL}"
 WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}" "DisplayVersion" "1.0.0"
 WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}" "Publisher" "Rion"
 WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}" "InstallLocation" "$INSTDIR"
 WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}" "DisplayIcon" "$INSTDIR\Rion Win 11.exe"
 WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
 WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}" "NoModify" 1
 WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}" "NoRepair" 1
 !endif
SectionEnd

!ifndef WEB_INSTALLER
Function un.onInit
 SetRegView 64
 SetShellVarContext all
 ReadRegStr $0 HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}" "InstallLocation"
 ${If} $0 != $INSTDIR
  MessageBox MB_ICONSTOP "The registered Rion installation does not match this uninstaller. No files were removed."
  Abort
 ${EndIf}
FunctionEnd
Section "Uninstall"
 SetRegView 64
 SetShellVarContext all
 !include "${REMOVE_FILES}"
 Delete "$INSTDIR\Uninstall.exe"
 Delete "$SMPROGRAMS\${PRODUCT_LABEL}\Rion Win 11.lnk"
 RMDir "$SMPROGRAMS\${PRODUCT_LABEL}"
 RMDir "$INSTDIR"
 DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_KEY}"
 ; Local profiles, rollback history and separately downloaded utilities are preserved.
SectionEnd
!endif
