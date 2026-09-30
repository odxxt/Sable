# Web sign-in options — research, not implemented

Goal: choose Run on an entry, open its website and fill/submit the login without simulated keyboard input or clipboard transfer. Findings reviewed September 29, 2026.

## Recommended: a small Chrome / Edge extension

SableLatch could pass just the selected credential to its paired browser extension through a native messaging companion. A content script sets the form fields through the DOM, dispatches the events required by the page and optionally submits the form. This is the same general integration pattern used by KeePassXC; it does not need character-by-character typing.

- [Chrome native messaging](https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging) supplies the application/extension communication mechanism. Chrome starts the registered host; an ordinary desktop process cannot directly fill an arbitrary browser tab.
- [Chrome content scripts](https://developer.chrome.com/docs/extensions/develop/concepts/content-scripts) can read and modify the page DOM.
- [KeePassXC browser integration](https://github.com/keepassxreboot/keepassxc/blob/develop/docs/topics/BrowserIntegration.adoc) is an existing password-manager example.

Proposed flow: Run creates a short-lived request for one entry and an exact HTTPS origin; the paired extension opens or selects the intended tab, confirms that origin/frame/document, requests that one credential, fills it and optionally submits once. Redirects must be revalidated. MFA and CAPTCHA remain the user's steps. Never put a credential in a URL, command line, temporary HTML file, log or clipboard.

This requires first-party host support. Existing declarative ZIP modules must not receive secret fields, arbitrary page scripts or a general vault-reading API. Site modules could describe supported fields/actions while the reviewed host and browser extension own secret delivery. Pair the extension, restrict native-host extension IDs, authorize site access, and bind delivery to the selected entry and current tab/document. Do not auto-submit continuously after logout.

Native messaging registration is machine-specific on Windows. The portable app would need an explicit browser-integration setup/repair step when moved. One-click launching also needs an extension-initiated connection/handshake; opening a website alone does not provide it. These are design requirements, not features already present.

## Extension-free alternatives

| Method | Where it opens | Tradeoff |
| --- | --- | --- |
| Playwright / browser automation | A browser controlled by SableLatch, normally with a dedicated profile | Can fill DOM fields without typing, but needs automation infrastructure and profile management. It cannot simply take over any ordinary browser window. |
| WebView2 | A browser window owned by SableLatch | The host can execute page code directly, but the session is outside the user's normal browser profile and adds browser runtime requirements. |
| Supported SSO / one-time sign-in handoff | The normal browser | Potentially avoids password filling, but only where the target application explicitly supports it and its server/identity setup is configured. It is not a generic website feature. |

[Playwright fill](https://playwright.dev/docs/input) sets fields and triggers input events, independently of its sequential typing API. [Chrome's remote-debugging policy](https://developer.chrome.com/blog/remote-debugging-port) requires a non-default data directory for remote debugging from Chrome 136; exposing a daily browser profile through a debugging port is not the recommended integration. [WebView2's script API](https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/javascript) allows a native host to interact with its hosted pages.

## ESXi and the password boundary

For the ESXi Host Client, begin with an explicit site adapter for the actual host-client version and login form. Verify the exact HTTPS host, its certificate and any redirect before releasing the credential. Do not silently disable certificate verification. Host Client and vCenter sign-in are distinct targets; support for one does not establish the other.

Broadcom documents [session clone tickets](https://developer.broadcom.com/xapis/virtual-infrastructure-json-api/latest/virtual-infrastructure/session-manager/) for cloning API sessions. That documentation does not establish a generic ESXi Host Client browser-login URL. A separate API login is not proof that opening a browser will inherit that authenticated session. No supported universal password-free browser handoff was established by this research.

DOM autofill avoids simulated typing, but the selected password still reaches the browser and target page. Page scripts share access to form values; developer tools or malware controlling the browser can potentially read them. An extension's isolated script world does not make the shared DOM secret. Password authentication cannot both fill the page and guarantee that the local user/browser cannot recover the value. SSO can change that boundary when supported by the server.

Before implementation, settle the initial browser, ESXi/Host Client versions, optional automatic submission, per-site field mapping and certificate trust setup. Browser sign-in remains a proposal; this release only repairs folder dragging.
