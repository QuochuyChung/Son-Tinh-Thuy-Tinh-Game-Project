Crimson Pro (Regular, Bold, Italic) - the game's font everywhere (TMP Settings default, every scene, HUD, dialogue, menus).
CrimsonPro/: SIL Open Font License (OFL.txt), from github.com/google/fonts/ofl/crimsonpro. The variable sources are in
ArtSource/Fonts/CrimsonPro; the static weights (400 / 700 / 400 italic) were cut from them with fontTools.
The SDF assets are static atlases (2048) of every character the game's texts use plus the full Vietnamese alphabet; real bold
and italic faces are wired in their weight tables. Roboto stays behind them as the fallback for the few symbols Crimson Pro lacks
(arrows, stars, ticks). New characters in new texts: regenerate the atlas (Font Asset Creator, or add them and set Static).

Roboto (Regular, Bold, Italic) - Google, Apache License 2.0 / SIL Open Font License.
Free to ship inside the game. Has full Vietnamese diacritics.
Copied from the com.unity.searcher package cache, so nothing was downloaded.
To switch fonts, create a new TMP Font Asset (Dynamic) and assign it in TMP Settings > Default Font Asset.
