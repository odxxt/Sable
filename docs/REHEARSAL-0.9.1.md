# Historical: initial Sable 0.9.1 rehearsal

This records the first 0.9.1 iteration. The final welcome now loops while visible, entry creation uses described starter rows, and the pin has three modes. See [current interface notes](UI-REFINEMENTS-0.9.1.md) and [the verification record](../VERIFICATION.md).

Direction: a compact, quiet tray utility with useful feedback. Existing Graphite surfaces, Segoe UI and Windows glyphs remain. No browser runtime or animation dependency was added.

Applied [Benji Taylor's Stageshow](https://github.com/benjitaylor/skills/tree/main/stageshow) principles from craft, motion, review and the payout-flow example: overlapping arrivals, one meaningful result, stable text geometry, then rest. The React kit and browser filming gate do not run against this native WPF application; this is a native adaptation, not a claim of passing its browser gate.

The onboarding scene presents two compact rows for secrets and shortcuts, then settles its checkmarks. It plays once on arrival. Windows' reduced-motion preference produces the settled frame. Buttons share quick press/release feedback; there is no permanent pulse, video playback or animation timer. Folder drag previews follow the actual pointer with a fixed grab offset, while neighboring placement uses the existing transition.

Reviewed WPF renders at the real 342 × 411 window size: welcome, recovery hidden/revealed and copied, ready at early/middle/settled frames, the template picker, command editor, in-menu delete confirmation, and folder grid. The early editor render showed the script-fields link pushing useful action controls below the fold; it was moved after the action controls. Footer controls stay fixed.

The native preview harness exercises real controls and vault persistence with fictional data. Physical mouse gestures, elevated UAC acceptance, and remote SSH/RDP connections still require manual testing; those are not inferred from screenshots.
