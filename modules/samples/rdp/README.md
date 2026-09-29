# Remote Desktop module

The declarative package is compatible with host 0.8.0+. Host 0.8.1 changes row clicks to copy the password and launches Windows mstsc.exe via a temporary DPAPI-encrypted .rdp file. Choose Launch / Launch & copy in the editor; both are also available in the right-click menu and details.

The ZIP receives no passwords or executable capability. Secrets stay in the encrypted vault; trusted host code performs copying and launch. This is not protection from software running as the Windows user, which can capture and decrypt a temporary file. A user reported successful login with host 0.8.2; compatibility with other Windows configurations remains unverified; see [RDP design](../../../docs/RDP-DESIGN.md). The old host 0.8.0 opens its own ActiveX session window instead.

Host 0.8.3 removes fixed desktop dimensions and forced stretching. It starts in native full screen by default, with a per-entry option to start windowed. Existing entries and this module ZIP remain compatible.

Host 0.8.5 enables remote clipboard sharing for existing and new entries and removes extra resource/performance restrictions. Windows/client defaults and server policy apply. The module package and encrypted entry schema remain compatible.
