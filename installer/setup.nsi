; Desktop Düzen Sabitleyici — kurulum (NSIS 3, Unicode). Kullanıcı başına, yönetici izni gerekmez, tek tık (oneClick).
; Kanal: win-setup (latest.json). Klasör 1.0.x'in kendini kurduğu yerle AYNI: %LOCALAPPDATA%\Programs\DesktopDuzenSabitleyici
; Veri (%APPDATA%\DesktopDuzenSabitleyici) kurulumda da kaldırmada da DOKUNULMAZ.
; Derleme: makensis /DVERSION=1.1.0 /DEXE=..\out\DesktopDuzenSabitleyici.exe /DOUTFILE=..\out\DesktopDuzenSabitleyici-Setup-1.1.0.exe installer\setup.nsi
Unicode true
SetCompressor /SOLID lzma
RequestExecutionLevel user

!ifndef VERSION
  !define VERSION "0.0.0"
!endif
!ifndef EXE
  !define EXE "..\out\DesktopDuzenSabitleyici.exe"
!endif
!ifndef OUTFILE
  !define OUTFILE "DesktopDuzenSabitleyici-Setup-${VERSION}.exe"
!endif
!define APPNAME "Desktop Düzen Sabitleyici"
!define EXENAME "DesktopDuzenSabitleyici.exe"
!define UNKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopDuzenSabitleyici"
!define LNK "Desktop Düzen Sabitleyici.lnk"

Name "${APPNAME}"
OutFile "${OUTFILE}"
InstallDir "$LOCALAPPDATA\Programs\DesktopDuzenSabitleyici"
Icon "..\src\app.ico"
UninstallIcon "..\src\app.ico"
BrandingText "${APPNAME} ${VERSION}"
ShowInstDetails nevershow
ShowUninstDetails nevershow
AutoCloseWindow true
VIProductVersion "${VERSION}.0"
VIAddVersionKey /LANG=0 "ProductName" "${APPNAME}"
VIAddVersionKey /LANG=0 "FileDescription" "${APPNAME} Setup"
VIAddVersionKey /LANG=0 "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=0 "ProductVersion" "${VERSION}"
VIAddVersionKey /LANG=0 "CompanyName" "departures-mil"
VIAddVersionKey /LANG=0 "LegalCopyright" "departures-mil"

!include "MUI2.nsh"
!define MUI_ICON "..\src\app.ico"
!define MUI_UNICON "..\src\app.ico"
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_UNPAGE_INSTFILES
; Sistem diline göre kendiliğinden seçilir (tr / en / ar)
!insertmacro MUI_LANGUAGE "Turkish"
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "Arabic"

LangString Closing ${LANG_TURKISH} "Çalışan program kapatılıyor…"
LangString Closing ${LANG_ENGLISH} "Closing the running program…"
LangString Closing ${LANG_ARABIC} "جارٍ إغلاق البرنامج قيد التشغيل…"
LangString Locked ${LANG_TURKISH} "Program dosyası kullanımda. Programı sistem tepsisinden kapatıp Yeniden Dene'ye basın."
LangString Locked ${LANG_ENGLISH} "The program file is in use. Close the program from the system tray and press Retry."
LangString Locked ${LANG_ARABIC} "ملف البرنامج قيد الاستخدام. أغلق البرنامج من علبة النظام ثم اضغط إعادة المحاولة."

; Çalışan kopyayı kapat: önce kibarca (1.1.0+ "Quit" olayını dinler), sonra zorla (1.0.x). Kayıtlar atomik yazıldığı için veri kaybı olmaz.
!macro CloseApp
  DetailPrint "$(Closing)"
  System::Call 'kernel32::OpenEventW(i 0x0002, i 0, w "DesktopDuzenSabitleyici_Quit_v1") p .r0'
  ${If} $0 P<> 0
    System::Call 'kernel32::SetEvent(p r0)'
    System::Call 'kernel32::CloseHandle(p r0)'
    Sleep 2500
  ${EndIf}
  nsExec::Exec 'taskkill /F /IM ${EXENAME}'
  Pop $0
  Sleep 500
!macroend

Section "Install"
  SetOutPath "$INSTDIR"
  !insertmacro CloseApp
  ClearErrors
  retry:
  File "/oname=${EXENAME}" "${EXE}"
  ${If} ${Errors}
    MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION "$(Locked)" IDRETRY retry
    Abort
  ${EndIf}
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  WriteRegStr HKCU "${UNKEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKCU "${UNKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNKEY}" "Publisher" "departures-mil"
  WriteRegStr HKCU "${UNKEY}" "DisplayIcon" "$INSTDIR\${EXENAME}"
  WriteRegStr HKCU "${UNKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "${UNKEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegStr HKCU "${UNKEY}" "URLInfoAbout" "https://github.com/departures-mil/DesktopDuzenSabitleyici"
  WriteRegDWORD HKCU "${UNKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNKEY}" "NoRepair" 1
  WriteRegDWORD HKCU "${UNKEY}" "EstimatedSize" 70000

  CreateShortcut "$SMPROGRAMS\${LNK}" "$INSTDIR\${EXENAME}" "" "$INSTDIR\${EXENAME}" 0
  CreateShortcut "$DESKTOP\${LNK}" "$INSTDIR\${EXENAME}" "" "$INSTDIR\${EXENAME}" 0

  ; tek tık: kurulumdan sonra program açılır
  Exec '"$INSTDIR\${EXENAME}"'
SectionEnd

Section "Uninstall"
  !insertmacro CloseApp
  Delete "$INSTDIR\${EXENAME}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  Delete "$SMPROGRAMS\${LNK}"
  Delete "$DESKTOP\${LNK}"
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "DesktopDuzenSabitleyici"
  DeleteRegKey HKCU "${UNKEY}"
  ; %APPDATA%\DesktopDuzenSabitleyici (kayıtlarınız) bilerek silinmez.
SectionEnd
