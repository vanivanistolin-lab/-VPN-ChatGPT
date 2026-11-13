# Third-party software

VPNKRUT-SUB packages these unmodified, separate components:

- **sing-box 1.14.4**, Copyright (C) 2022 nekohasekai. GPL-3.0-or-later; additional upstream naming notice retained in `engine/LICENSE-sing-box.txt`. Full GPL text: `engine/GPL-3.0.txt`. Exact corresponding upstream source: https://github.com/SagerNet/sing-box/tree/v1.14.4 and https://github.com/SagerNet/sing-box/archive/refs/tags/v1.14.4.zip. Build information is available in the upstream source. This project is not endorsed by or affiliated with SagerNet.
- **Wintun 0.14.1**, Copyright WireGuard LLC. The official precompiled signed AMD64 DLL is redistributed under the license supplied in `engine/LICENSE-wintun.txt`. Source and documentation: https://www.wintun.net/ and https://git.zx2c4.com/wintun/.

The build downloads only official release files and verifies pinned SHA-256 values. The distributed binaries are unmodified. VPNKRUT-SUB communicates with the separately packaged sing-box executable using its command-line/configuration interface. Users receive the components and license texts in the same MSI.

The VPNKRUT-SUB test certificate is not an upstream signature and is not a publicly trusted code-signing certificate. Wintun retains its original WireGuard signature.
