"""Build a per-user Windows Installer package using Windows/Python 3.12 only."""
import os
import uuid
from pathlib import Path
import msilib
from msilib import schema, sequence, add_data, Dialog, Binary, CAB

ROOT = Path(__file__).resolve().parent.parent
VERSION = "3.0.0"
UPGRADE = "{C784D197-9100-4C13-9CE2-746ED97AA676}"
PRODUCT = "{" + str(uuid.uuid5(uuid.UUID(UPGRADE.strip("{}")), VERSION)).upper() + "}"
OUTPUT = ROOT / "dist" / ("VPNKRUT-SUB-" + VERSION + "-Setup.msi")

def dialog(db, name, title, text, button="Finish", event="Return"):
    d = Dialog(db, name, 50, 50, 370, 230, 3, "VPNKRUT-SUB Setup", "Proceed", "Proceed", "Cancel")
    d.text("Title", 20, 18, 330, 35, 3, "{\\Heading}" + title)
    d.text("Description", 20, 62, 330, 105, 3, text)
    d.line("Separator", 0, 187, 370, 1)
    d.pushbutton("Proceed", 205, 201, 75, 20, 3, button, "Cancel").event("EndDialog", event)
    d.pushbutton("Cancel", 287, 201, 65, 20, 3, "Cancel", "Proceed").event("EndDialog", "Exit")
    return d

def main():
    msilib.AMD64 = True
    db = msilib.init_database(str(OUTPUT), schema, "VPNKRUT-SUB", PRODUCT, VERSION, "VPNKRUT-SUB")
    add_data(db, "Property", [
        ("UpgradeCode", UPGRADE), ("MSIINSTALLPERUSER", "1"),
        ("ARPCOMMENTS", "ChatGPT VPN helper. Does not disconnect VPN."),
        ("ARPURLINFOABOUT", "https://github.com/vanivanistolin-lab/-VPN-ChatGPT"),
        ("ARPPRODUCTICON", "BrandIcon"), ("ARPNOMODIFY", "1"),
        ("SecureCustomProperties", "OLDPRODUCTS;NEWERFOUND"),
        ("DefaultUIFont", "Body"), ("ErrorDialog", "ErrorDlg"),
    ])
    add_data(db, "LaunchCondition", [("VersionNT64", "Windows x64 is required."),
        ("NOT NEWERFOUND", "A newer VPNKRUT-SUB version is already installed.")])
    add_data(db, "Directory", [
        ("TARGETDIR", None, "SourceDir"), ("LocalAppDataFolder", "TARGETDIR", "."),
        ("ProgramsRoot", "LocalAppDataFolder", "Programs"),
        ("INSTALLDIR", "ProgramsRoot", "VPNKRUT|VPNKRUT-SUB"),
        ("EngineDir", "INSTALLDIR", "engine"),
        ("ProgramMenuFolder", "TARGETDIR", "."), ("AppMenu", "ProgramMenuFolder", "VPNKRUT|VPNKRUT-SUB"),
        ("StartupFolder", "TARGETDIR", "."), ("DesktopFolder", "TARGETDIR", "."), ("SystemFolder", "TARGETDIR", "."),
    ])
    component = "{6A6A6468-CA01-4E1E-991A-881E72724A86}"
    add_data(db, "Component", [("MainComponent", component, "INSTALLDIR", 260, None, "InstallMarker")])
    add_data(db, "Component", [("CoreComponent", "{0877B4B6-9380-47A1-97F9-4C641624EECE}", "EngineDir", 260, None, "CoreMarker")])
    add_data(db, "Registry", [("InstallMarker", 1, r"Software\VPNKRUT-SUB", "InstallDir", "[INSTALLDIR]", "MainComponent")])
    add_data(db, "Registry", [("CoreMarker", 1, r"Software\VPNKRUT-SUB", "CoreVersion", "1.14.4", "CoreComponent")])
    add_data(db, "Feature", [("MainFeature", None, "VPNKRUT-SUB", "Main program and shortcuts", 1, 1, "INSTALLDIR", 0)])
    add_data(db, "FeatureComponents", [("MainFeature", "MainComponent"), ("MainFeature", "CoreComponent")])
    cabinet = CAB("Payload.cab")
    for key, filename, source, target_component, file_version in [
        ("MAINEXE", "VPNKRUT.EXE|ChatGPTRefresh.exe", ROOT / "dist" / "ChatGPTRefresh.exe", "MainComponent", VERSION + ".0"),
        ("ENGINEHOST", "ENGINE.EXE|VPNKRUTEngine.exe", ROOT / "dist" / "VPNKRUTEngine.exe", "MainComponent", VERSION + ".0"),
        ("README", "README.TXT|README.txt", ROOT / "README.md", "MainComponent", None),
        ("AGREEMENT", "TERMS.TXT|USER_AGREEMENT.txt", ROOT / "USER_AGREEMENT.md", "MainComponent", None),
        ("NOTICE", "NOTICE.MD|THIRD-PARTY.md", ROOT / "THIRD-PARTY.md", "MainComponent", None),
        ("CATALOG", "CATALOG.JSN|catalog.json", ROOT / "catalog" / "servers.json", "MainComponent", None),
        ("SINGBOX", "SINGBOX.EXE|sing-box.exe", ROOT / "engine" / "sing-box.exe", "CoreComponent", None),
        ("WINTUN", "wintun.dll", ROOT / "engine" / "wintun.dll", "CoreComponent", None),
        ("CORELICENSE", "LICENSE1.TXT|LICENSE-sing-box.txt", ROOT / "engine" / "LICENSE-sing-box.txt", "CoreComponent", None),
        ("DRIVERLICENSE", "LICENSE2.TXT|LICENSE-wintun.txt", ROOT / "engine" / "LICENSE-wintun.txt", "CoreComponent", None),
        ("GPL", "GPL3.TXT|GPL-3.0.txt", ROOT / "engine" / "GPL-3.0.txt", "CoreComponent", None),
    ]:
        order, _ = cabinet.append(str(source), source.name, key)
        add_data(db, "File", [(key, target_component, filename, source.stat().st_size,
            file_version, None, 512, order)])
    add_data(db, "Icon", [("BrandIcon", Binary(str(ROOT / "assets" / "vpnkrut-sub.ico")))])
    add_data(db, "Shortcut", [
        ("MenuLink", "AppMenu", "VPNKRUT|VPNKRUT-SUB", "MainComponent", "[#MAINEXE]", "--managed --settings", "VPNKRUT-SUB", None, "BrandIcon", 0, 1, "INSTALLDIR"),
        ("StartupLink", "StartupFolder", "VPNKRUT|VPNKRUT-SUB", "MainComponent", "[#MAINEXE]", "--managed", "VPNKRUT-SUB background helper", None, "BrandIcon", 0, 7, "INSTALLDIR"),
        ("DesktopLink", "DesktopFolder", "VPNKRUT|VPNKRUT-SUB", "MainComponent", "[#MAINEXE]", "--managed --settings", "VPNKRUT-SUB", None, "BrandIcon", 0, 1, "INSTALLDIR"),
    ])
    add_data(db, "RemoveFile", [("RemoveMenuFolder", "MainComponent", None, "AppMenu", 2),
        ("RemoveInstallFolder", "MainComponent", None, "INSTALLDIR", 2)])
    add_data(db, "Upgrade", [(UPGRADE, "0.0.0", VERSION, None, 257, None, "OLDPRODUCTS"),
        (UPGRADE, VERSION, None, None, 2, None, "NEWERFOUND")])
    add_data(db, "TextStyle", [("Body", "Segoe UI", 10, 0, 0), ("Heading", "Segoe UI", 17, 0, 1)])
    dialog(db, "WelcomeDlg", "Install VPNKRUT-SUB", "Version 3.0.0\n\nInstalls a standalone VPN client for your Windows account, including the VPN engine. Adds desktop and Start menu shortcuts.\n\nConnections start only when requested in the app. Existing VPN clients will not be stopped.\n\nClick Install to continue.", "Install")
    d = dialog(db, "MaintenanceDlg", "Remove VPNKRUT-SUB", "Click Remove to uninstall this version.\n\nYour VPN clients and saved helper settings will remain unchanged.", "Remove")
    # Set REMOVE before returning from the maintenance dialog.
    view = db.OpenView("DELETE FROM `ControlEvent` WHERE `Dialog_`='MaintenanceDlg' AND `Control_`='Proceed'")
    view.Execute(None)
    add_data(db, "ControlEvent", [("MaintenanceDlg", "Proceed", "[REMOVE]", "ALL", "1", 1),
        ("MaintenanceDlg", "Proceed", "EndDialog", "Return", "1", 2)])
    dialog(db, "ExitDialog", "Setup completed", "VPNKRUT-SUB setup has completed.\n\nOpen VPNKRUT-SUB from the Start menu. It also starts automatically when you sign in.\n\nVPN connections were not switched off.")
    dialog(db, "FatalError", "Setup could not finish", "Windows Installer could not complete this operation.\n\nYour VPN connection was not intentionally changed.")
    dialog(db, "UserExit", "Setup cancelled", "No further installation actions will be performed.")
    err = Dialog(db, "ErrorDlg", 50, 50, 370, 140, 65539, "VPNKRUT-SUB Setup", "OK", "OK", "OK")
    err.text("ErrorText", 15, 15, 340, 70, 3, "[ErrorText]")
    err.pushbutton("OK", 280, 110, 65, 20, 3, "OK", None).event("EndDialog", "Error")
    add_data(db, "InstallExecuteSequence", sequence.InstallExecuteSequence)
    add_data(db, "CustomAction", [("LaunchProgram", 82, "MAINEXE", "--managed --settings"),
        ("ShowReadme", 98, "SystemFolder", 'notepad.exe "[INSTALLDIR]README.txt"')])
    add_data(db, "InstallExecuteSequence", [
        ("LaunchProgram", 'NOT Installed AND NOT REMOVE AND UILevel >= 3', 6800),
        ("ShowReadme", 'NOT Installed AND NOT REMOVE AND UILevel >= 3', 6810),
    ])
    add_data(db, "InstallUISequence", [row for row in sequence.InstallUISequence if row[0] not in ("ExitDialog", "FatalError", "UserExit")] + [
        ("WelcomeDlg", "NOT Installed", 1230), ("MaintenanceDlg", "Installed AND NOT REMOVE", 1231),
        ("ExitDialog", None, -1), ("FatalError", None, -3), ("UserExit", None, -2),
    ])
    cabinet.commit(db)
    db.Commit()
    print(OUTPUT)
    print("ProductCode=" + PRODUCT)

if __name__ == "__main__":
    main()



