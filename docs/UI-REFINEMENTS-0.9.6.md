# Interface refinements — 0.9.6 preview

| Before | After | Why |
| --- | --- | --- |
| Three orientations of the same pin | Static tray, upright pin and window silhouettes | Distinguish behavior without learning orientations |
| Grid tiles repeat a play button and omit entry identity | Identity on the left; secondary Run only when the main click copies | Show entry type and keep both actions accessible when they differ |
| Shortcut lightning bolt | Curved outward arrow and rounded box in starters, rows and grid | Separate shortcuts from their play action and connection symbols |
| Five folder symbols | Twenty static choices with named tooltips | Organize servers, networks, work and everyday material |
| X cramped against the card edge | Matching centered targets inside an 8px inset | Align controls and maintain comfortable hit areas |
| Repeated, stacked dismissal choices | One aligned first-use row in the same 52px card; immediate later dismissal | Explain global disable once without repeating a burden |
| Suggestions inside every folder | All items only, evaluating the latest clipboard on return | Keep folder navigation stable and avoid extra scans |
| Diagnostic console may stay behind another window | Bounded foreground handoff for the explicitly clicked diagnostic | Show the result without a permanent topmost window |

The one-time explanation state lives in encrypted vault preferences. It is marked when the first X is pressed, so navigating away does not repeat the explanation later. Settings retains the global off/on switch.

In 0.9.6 the broader icon set remained a separate review. It was approved and shipped as native entry hover motion in 0.9.7. Folder and toolbar symbols remain static; there is no browser runtime or idle icon loop.

The terminal handoff uses the launched process and a random, non-secret window-title marker to identify classic Console Host or Windows Terminal. A bounded three-second request grants the launched process foreground permission and activates its visible window. It stops reclaiming focus if user input moves to another application. It does not alter Windows focus policy or make the console topmost. Windows can decline activation, and a Terminal configuration that suppresses application titles may prevent matching the delegated window. See Microsoft's [foreground activation rules](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow) and [foreground permission API](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-allowsetforegroundwindow).
