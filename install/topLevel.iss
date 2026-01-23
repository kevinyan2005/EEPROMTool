#define partPublisher "Monteris Medical, Inc"
#define partURL "http://www.monteris.com/"
#define partNumber "ST-00239"
#define partName "HCB Test Tool"
#define partVersion "1.0.0"

#define rootDir ""
#define partComponent ""
#define defaultDir "C:\mmi_devtools\OneWire EEPROM Test Tool"
#define installDir "{app}"

[Setup]
AppId={{04E353BD-1448-461E-87C1-D6CB74EB6DDA}
AppName={#partName}
AppVersion={#partVersion}
DefaultDirName={#defaultDir}
DefaultGroupName={#partName}
AllowNoIcons=yes
OutputBaseFilename={#partNumber}-{#partVersion} Rev A - Setup
OutputDir=.
WizardImageFile="..\Build\Installer\images\installImage.bmp"
DisableProgramGroupPage=yes
SetupLogging=yes
Uninstallable=no

#include "./partLevel.iss"