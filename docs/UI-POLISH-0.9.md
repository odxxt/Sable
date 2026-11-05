# Sable 0.9 interface pass

This is a polish pass within the existing compact WPF tray window. It keeps the dark theme, Segoe UI typography, fixed window size and established actions.

## Audit and direction

The folder wrap consumed entry space, the initial 100-pixel grid slots were too tall, the view toggle used a text glyph that did not match nearby icons, and suggested actions flattened the command and target into one caption. The direction is a quiet, precise Windows utility: calm navigation, readable saved content and short feedback for copy/run actions.

## Changes by pass

- **Spacing:** folders have one 36-pixel strip with four equal slots; tiles use 68-pixel slots and consistent padding. Folder content never resizes during a successful drop.
- **Color:** keep the existing graphite surfaces and neutral accent. Suggestions use a raised surface, with no new decorative colors.
- **Icons:** use small line geometry for the view switch and suggested run action. Folder names align with their existing folder/custom icon.
- **Copy:** name the saved action, show its target separately, and use tooltips to explain browsing versus moving folders.
- **Type:** retain the Windows font; use 11-pixel secondary action text and a 13-pixel target with tabular figures. Grid labels stay readable as tiles shrink.
- **Layout:** retain header, navigation, suggestions and entries in that order. The four-slot rail prevents folders from consuming multiple rows. Grid remains two entries across. Tiles are low-profile controls, with a name, short type/purpose and right-side action; their old icon footer is removed. Rows and tiles have no pencil; Edit remains in the right-click menu.
- **Details:** visible hover/focus surfaces, ellipsis for long names, full folder name in the tooltip, and whole suggestion buttons with accessible names/help text.
- **Visuals:** use actual product content and fictional preview fixtures; no generated illustration or new image dependency.
- **Motion:** sideways panning follows the pointer, wheel/snap movement settles in 160 ms, and existing folder reorder/drop feedback remains. Entry buttons press to 98.5% using a transform, then return in 120 ms. Copying shows a muted check in the same fixed action slot with no layout shift. Windows reduced-motion preference is respected; no new idle timer.

Workflow reference: [unslop by Damir Sokolovsky (@heydamir)](https://github.com/heydamir/unslop/tree/main/skills/unslop), CC BY 4.0. The installed skill and reference files retain their original attribution. See [verification](../VERIFICATION.md) for test coverage and manual-check limits.
