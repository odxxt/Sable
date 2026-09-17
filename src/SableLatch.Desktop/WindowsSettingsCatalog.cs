namespace SableLatch.Desktop;

// Built-in destinations only. No resident search index or external downloads.
// References: learn.microsoft.com/windows/apps/develop/launch/launch-settings
// and learn.microsoft.com/windows/win32/shell/controlpanel-canonical-names.
internal static class WindowsSettingsCatalog
{
    public static IReadOnlyList<PickerChoice> Read()
    {
        var choices = new List<PickerChoice>();
        foreach (var line in Settings.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fields = line.Split('|');
            choices.Add(new(fields[0], "ms-settings:" + fields[1], Category: "Windows Settings", Keywords: fields.ElementAtOrDefault(2) ?? "", Glyph: "\uE713"));
        }
        foreach (var line in Panels.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fields = line.Split('|');
            choices.Add(new(fields[0], fields[1] == "control" ? "control" : "Microsoft." + fields[1], Category: "Control Panel", Keywords: fields.ElementAtOrDefault(2) ?? "", Glyph: "\uE770"));
        }
        return choices;
    }
    public static IEnumerable<PickerChoice> Search(IReadOnlyList<PickerChoice> choices, string query)
    {
        var words = query.Replace('-', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) return choices.Where(c => c.Target is "ms-settings:display" or "ms-settings:sound" or "ms-settings:network-wifi" or "ms-settings:windowsupdate" or "Microsoft.ProgramsAndFeatures" or "control");
        return choices.Where(c => words.All(w => (c.Name + " " + c.Target + " " + c.Category + " " + c.Keywords).Replace('-', ' ').Contains(w, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(c => c.Name.StartsWith(query.Trim(), StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(c => c.Name).ThenBy(c => c.Category);
    }
    private const string Settings = """
Windows Settings||home all
Display|display|screen monitor resolution brightness
Advanced display|display-advanced|refresh rate monitor
Graphics|display-advancedgraphics|gpu performance
Default graphics|display-advancedgraphics-default|gpu
Night light|nightlight|blue light
Sound|sound|audio speaker microphone
Volume mixer|apps-volume|audio volume per app
Sound devices|sound-devices|audio input output
Default microphone|sound-defaultinputproperties|audio recording input
Default speakers|sound-defaultoutputproperties|audio playback output
Notifications|notifications|alerts banners
Focus assist|quiethours|do not disturb
Focus during scheduled hours|quietmomentsscheduled|quiet notifications
Focus during games|quietmomentsgame|quiet notifications
Focus during presentations|quietmomentspresentation|quiet notifications
Power and sleep|powersleep|battery energy screen timeout
Battery saver|batterysaver|energy laptop
Battery saver options|batterysaver-settings|energy laptop
Battery usage|batterysaver-usagedetails|energy laptop
Energy recommendations|energyrecommendations|power windows 11
Storage|storagesense|disk space
Storage Sense|storagepolicies|automatic cleanup disk
Storage recommendations|storagerecommendations|cleanup disk
Disks and volumes|disksandvolumes|partitions storage
Default save locations|savelocations|storage files drive
Multitasking|multitasking|snap windows desktops
Projecting to this PC|project|wireless display
Shared experiences|crossdevice|nearby sharing
Clipboard|clipboard|copy paste history
Remote Desktop|remotedesktop|rdp
About this PC|about|system version specifications name
Device encryption|deviceencryption|bitlocker
Presence sensing|presence|windows 11 sensor
Bluetooth|bluetooth|devices pairing
Connected devices|connecteddevices|hardware
Printers and scanners|printers|printing devices
Mouse|mousetouchpad|pointer cursor
Touchpad|devices-touchpad|gestures laptop
Touch|devices-touch|touchscreen
Pen and Windows Ink|pen|stylus
Pen shortcuts|pen-button|stylus
Typing|typing|keyboard autocorrect
Text suggestions|devicestyping-hwkbtextsuggestions|keyboard typing
AutoPlay|autoplay|media removable
USB|usb|devices connection
Cameras|camera|webcam windows 11
Wheel|wheel|surface dial
Network status|network-status|internet adapters connection
Advanced network|network-advancedsettings|adapters reset
Wi-Fi|network-wifi|wireless internet
Known Wi-Fi networks|network-wifisettings|wireless saved passwords
Ethernet|network-ethernet|wired internet
VPN|network-vpn|virtual private network
Proxy|network-proxy|internet server
Airplane mode|network-airplanemode|wireless radio
Mobile hotspot|network-mobilehotspot|share internet tethering
Cellular|network-cellular|sim mobile data
Dial-up|network-dialup|modem internet
DirectAccess|network-directaccess|work network
Personalization|personalization|appearance theme
Background|personalization-background|wallpaper desktop
Colors|personalization-colors|accent dark light mode
Lock screen|lockscreen|wallpaper sign in
Themes|themes|appearance desktop
Fonts|fonts|typeface
Start menu|personalization-start|apps menu
Start menu folders|personalization-start-places|shortcuts
Taskbar|taskbar|desktop icons
Text input appearance|personalization-textinput|keyboard theme
Touch keyboard|personalization-touchkeyboard|onscreen
Dynamic Lighting|personalization-lighting|rgb windows 11
Copilot key|personalization-textinput-copilot-hardwarekey|keyboard windows 11
Installed apps|appsfeatures|uninstall programs applications
Default apps|defaultapps|file associations browser
Optional features|optionalfeatures|windows components
Apps for websites|appsforwebsites|links browser
Startup apps|startupapps|boot login
Video playback|videoplayback|streaming
Your account|yourinfo|profile picture
Email and accounts|emailandaccounts|mail profile
Sign-in options|signinoptions|password pin windows hello
Work or school account|workplace|organization domain
Other users|otherusers|accounts
Family|family-group|parental accounts
Sync your settings|sync|account preferences backup
Date and time|dateandtime|clock timezone
Region|regionformatting|country formats
Language|regionlanguage|locale keyboard
Keyboard languages|keyboard|layout
Advanced keyboard|keyboard-advanced|input language
Speech|speech|voice language
Japanese IME|regionlanguage-jpnime|keyboard language
Korean IME|regionlanguage-korime|keyboard language
Pinyin IME|regionlanguage-chsime-pinyin|chinese keyboard language
Wubi IME|regionlanguage-chsime-wubi|chinese keyboard language
Game Bar|gaming-gamebar|gaming xbox
Captures|gaming-gamedvr|gaming recording screenshots
Game Mode|gaming-gamemode|gaming performance
Accessibility display|easeofaccess-display|text size vision
Text cursor|easeofaccess-cursor|accessibility indicator
Mouse pointer|easeofaccess-mousepointer|accessibility size color
Magnifier|easeofaccess-magnifier|accessibility zoom
Color filters|easeofaccess-colorfilter|accessibility colorblind
Contrast themes|easeofaccess-highcontrast|accessibility high contrast
Narrator|easeofaccess-narrator|accessibility screen reader
Accessibility audio|easeofaccess-audio|mono hearing
Captions|easeofaccess-closedcaptioning|accessibility subtitles
Accessibility speech|easeofaccess-speechrecognition|voice access recognition
Accessibility keyboard|easeofaccess-keyboard|sticky filter toggle keys
Accessibility mouse|easeofaccess-mouse|mouse keys
Eye control|easeofaccess-eyecontrol|accessibility tracker
Visual effects|easeofaccess-visualeffects|accessibility animations transparency
Hearing devices|easeofaccess-hearingaids|accessibility windows 11
Search|search|index files
Search permissions|search-permissions|history safesearch
Search details|search-moredetails|indexing
Privacy|privacy|permissions general
Account information permission|privacy-accountinfo|privacy apps
Activity history|privacy-activityhistory|privacy
App diagnostics permission|privacy-appdiagnostics|privacy
Automatic downloads permission|privacy-automaticfiledownloads|privacy
Calendar permission|privacy-calendar|privacy apps
Call history permission|privacy-callhistory|privacy apps
Camera permission|privacy-webcam|privacy webcam
Contacts permission|privacy-contacts|privacy
Documents permission|privacy-documents|privacy files
Downloads permission|privacy-downloadsfolder|privacy files
Email permission|privacy-email|privacy
Eye tracker permission|privacy-eyetracker|privacy
Diagnostics and feedback|privacy-feedback|privacy telemetry
File system permission|privacy-broadfilesystemaccess|privacy files
Screenshot permissions|privacy-graphicscaptureprogrammatic|privacy graphics
Screenshot border permissions|privacy-graphicscapturewithoutborder|privacy graphics
Inking and typing personalization|privacy-speechtyping|privacy dictionary
Location permission|privacy-location|privacy gps
Messaging permission|privacy-messaging|privacy texts
Microphone permission|privacy-microphone|privacy recording audio
Motion permission|privacy-motion|privacy sensors
Music library permission|privacy-musiclibrary|privacy files
Notification permission|privacy-notifications|privacy apps
Other devices permission|privacy-customdevices|privacy
Phone calls permission|privacy-phonecalls|privacy
Pictures permission|privacy-pictures|privacy files photos
Radios permission|privacy-radios|privacy bluetooth
Speech privacy|privacy-speech|online recognition
Tasks permission|privacy-tasks|privacy
Videos permission|privacy-videos|privacy files
Voice activation permission|privacy-voiceactivation|privacy
Windows Update|windowsupdate|updates download install
Update history|windowsupdate-history|updates installed
Advanced update options|windowsupdate-options|updates pause
Optional updates|windowsupdate-optionalupdates|drivers updates
Active hours|windowsupdate-activehours|updates restart
Restart options|windowsupdate-restartoptions|updates reboot
Delivery Optimization|delivery-optimization|updates bandwidth
Delivery activity|delivery-optimization-activity|updates download statistics
Advanced delivery options|delivery-optimization-advanced|updates bandwidth
Windows Security|windowsdefender|antivirus firewall protection
Backup|backup|restore sync
Troubleshoot|troubleshoot|repair diagnostics
Recovery|recovery|reset startup
Activation|activation|license product key
Find my device|findmydevice|lost location
Developer settings|developers|developer mode
Windows Insider|windowsinsider|preview builds
Security key enrollment|signinoptions-launchsecuritykeyenrollment|fido account
Phone Link|mobile-devices|android iphone
Device usage|deviceusage|personalization
""";
    private const string Panels = """
Control Panel|control|home all
Security and Maintenance|ActionCenter|action center reliability
Windows Tools|AdministrativeTools|administrative tools services
AutoPlay|AutoPlay|media discs
BitLocker Drive Encryption|BitLockerDriveEncryption|disk security
Color Management|ColorManagement|display monitor profile calibration
Credential Manager|CredentialManager|password credentials
Date and Time|DateAndTime|clock timezone
Default Programs|DefaultPrograms|file associations
Device Manager|DeviceManager|drivers hardware
Devices and Printers|DevicesAndPrinters|printing hardware
Ease of Access Center|EaseOfAccessCenter|accessibility
File History|FileHistory|backup restore
File Explorer Options|FolderOptions|hidden files folders extensions
Fonts|Fonts|typeface
Indexing Options|IndexingOptions|search files
Internet Options|InternetOptions|browser proxy certificates
Keyboard|Keyboard|repeat delay
Mouse|Mouse|pointer wheel buttons
Network and Sharing Center|NetworkAndSharingCenter|adapters internet wifi
Pen and Touch|PenAndTouch|stylus touchscreen
Phone and Modem|PhoneAndModem|dialing
Power Options|PowerOptions|battery sleep energy plan
Programs and Features|ProgramsAndFeatures|uninstall programs applications
Recovery|Recovery|system restore
Region|RegionAndLanguage|language formats locale
RemoteApp and Desktop Connections|RemoteAppAndDesktopConnections|rdp work
Sound|Sound|audio playback recording microphone speakers
Speech Recognition|SpeechRecognition|voice
Storage Spaces|StorageSpaces|disks pool
Sync Center|SyncCenter|offline files
System|System|about computer
Tablet PC Settings|TabletPCSettings|touch calibrate
Taskbar and Navigation|Taskbar|desktop
Troubleshooting|Troubleshooting|repair diagnostics
User Accounts|UserAccounts|users password
Windows Firewall|WindowsFirewall|network security defender
Windows Mobility Center|MobilityCenter|laptop brightness presentation
Work Folders|WorkFolders|sync work
""";
}
