#
# This file is part of Project Hybrasyl.
#
# This program is free software; you can redistribute it and/or modify
# it under the terms of the Affero General Public License as published by
# the Free Software Foundation, version 3.
#
# This program is distributed in the hope that it will be useful, but
# without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
# or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
# for more details.
#
# You should have received a copy of the Affero General Public License along
# with this program. If not, see <http://www.gnu.org/licenses/>.
#
# (C) 2013 Hybrasyl Project <info@hybrasyl.com>.
#
# Authors:		Justin Baugh	<baughj@hybrasyl.com>
#
#

!include "MUI2.nsh"
!include "Library.nsh"

!define APPNAME "Hybrasyl Server"
!define ORGNAME "Project Hybrasyl"
!define DESCRIPTION "A DOOMVAS v1 emulator for the game Dark Ages"

!define VERSIONMAJOR 0
!define VERSIONMINOR 3
!define VERSIONBUILD 0

!define HELPURL "http://www.hybrasyl.com" # "Support Information" link
!define UPDATEURL "http://www.hybrasyl.com" # "Product Updates" link
!define ABOUTURL "http://www.hybrasyl.com" # "Publisher" link

!define INSTALLSIZE 5000


# By default this is a relative path to the Hybrasyl git repository. Whatever this path is, it needs to work.
!define LOCALPATH "..\hybrasyl\bin\Release"

; General

; Name and file
Name "${APPNAME} ${VERSIONMAJOR}.${VERSIONMINOR}.${VERSIONBUILD}"
OutFile "Setup_HybrasylServer.exe"
Icon "hybrasyl.ico"

; Default installation folder
InstallDir "$PROGRAMFILES\Hybrasyl Server"
DirText "Server binaries and startup files will be installed here. Please note: Hybrasyl's file data (maps and scripts) are stored in Documents\Hybrasyl, by default."

; Request application privileges
RequestExecutionLevel admin

; --------------------------------
; Interface Settings

  !define MUI_ABORTWARNING

; --------------------------------
; Pages

  !insertmacro MUI_PAGE_LICENSE "agplv3.txt"
  !insertmacro MUI_PAGE_DIRECTORY
  !insertmacro MUI_PAGE_INSTFILES

  !insertmacro MUI_UNPAGE_CONFIRM
  !insertmacro MUI_UNPAGE_INSTFILES

; --------------------------------
; Languages

  !insertmacro MUI_LANGUAGE "English"

; --------------------------------
; Installer Sections

Section "NSIS_Project1" SecDummy

  SetOutPath "$INSTDIR"

  File ${LOCALPATH}\hybrasyl.exe
  File ${LOCALPATH}\hybrasyl.exe.config
  File hybrasyl.ico
  File agplv3.txt

  # Make start menu entry
  createDirectory "$SMPROGRAMS\${ORGNAME}"
  createShortCut "$SMPROGRAMS\${ORGNAME}\${APPNAME}.lnk" "$INSTDIR\hybrasyl.exe" "" "$INSTDIR\hybrasyl.ico"

  # Registry information for add/remove programs
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${ORGNAME} ${APPNAME}" "DisplayName" "${ORGNAME} - ${APPNAME} - ${DESCRIPTION}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${ORGNAME} ${APPNAME}" "UninstallString" "$\"$INSTDIR\uninstall.exe$\""
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${ORGNAME} ${APPNAME}" "QuietUninstallString" "$\"$INSTDIR\uninstall.exe$\" /S"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${ORGNAME} ${APPNAME}" "InstallLocation" "$\"$INSTDIR$\""
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${ORGNAME} ${APPNAME}" "DisplayIcon" "$\"$INSTDIR\logo.ico$\""
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${ORGNAME} ${APPNAME}" "Publisher" "${ORGNAME}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${ORGNAME} ${APPNAME}" "HelpLink" "${HELPURL}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${ORGNAME} ${APPNAME}" "URLUpdateInfo" "${UPDATEURL}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${ORGNAME} ${APPNAME}" "URLInfoAbout" "${ABOUTURL}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${ORGNAME} ${APPNAME}" "DisplayVersion" "${VERSIONMAJOR}.${VERSIONMINOR}.${VERSIONBUILD}"
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${ORGNAME} ${APPNAME}" "VersionMajor" ${VERSIONMAJOR}
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${ORGNAME} ${APPNAME}" "VersionMinor" ${VERSIONMINOR}

  # There is no option for modifying or repairing the install
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${ORGNAME} ${APPNAME}" "NoModify" 1
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${ORGNAME} ${APPNAME}" "NoRepair" 1

  # Set the INSTALLSIZE constant (!defined at the top of this script) so Add/Remove Programs can accurately report the size
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${ORGNAME} ${APPNAME}" "EstimatedSize" ${INSTALLSIZE}

  # Store installation folder
  WriteRegStr HKCU "Software\${ORGNAME}\${APPNAME}" "" $INSTDIR

  ; Create uninstaller
  WriteUninstaller "$INSTDIR\Uninstall.exe"

SectionEnd

; Uninstaller Section

Section "Uninstall"

  # Remove shortcut / start menu entry
  Delete "$SMPROGRAMS\${ORGNAME}\${APPNAME}.lnk"
  rmDir "$SMPROGRAMS\${ORGNAME}"
 
  # Remove files
  Delete $INSTDIR\hybrasyl.exe
  Delete $INSTDIR\hybrasyl.exe.config
  Delete $INSTDIR\hybrasyl.ico
  Delete $INSTDIR\agplv3.txt
  Delete $INSTDIR\uninstall.exe
 
  # Try to remove the install directory - this will only happen if it is empty
  rmDir $INSTDIR

  DeleteRegKey /ifempty HKCU "Software\${ORGNAME}\${APPNAME}"
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${ORGNAME} ${APPNAME}"

SectionEnd
