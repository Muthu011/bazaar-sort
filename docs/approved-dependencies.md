# Approved dependencies

Agents may only add what is listed here. To propose something new, open an issue labelled `proposed` with the reason, licence and size.

| Dependency | Use | Notes |
| --- | --- | --- |
| Unity 6 LTS, 2D, URP | Engine | |
| Unity Test Framework | Tests | |
| Unity Input System | Input | |
| Newtonsoft Json (`com.unity.nuget.newtonsoft-json`, Unity's official package) | Level JSON parsing in `Game.Levels` | Approved by Muthu 2026-10-28. Not used in `Game.Core` |
| TextMeshPro | Text | |
| uGUI (`com.unity.ugui`) | UI panels, HUD, buttons | built in. Approved by Muthu 2026-10-28 |
| DOTween (free) or Unity's own tweening code | Animation | pick one in week 1 |
| MCP for Unity (CoplayDev, MIT) | Editor bridge for agents | editor-only, excluded from builds |
| Unity IAP | Purchases | |
| Google Mobile Ads (AdMob) Unity plugin | Ads | LevelPlay mediation later, needs approval |
| Firebase Analytics, Crashlytics, Remote Config (Unity SDK) | Analytics, crashes, config | |
| Google Play Games / In-App Review / In-App Update plugins | Store features | optional |
| Fonts: Noto Sans, Noto Sans Tamil, Noto Sans Devanagari | UI text | OFL licence |
| Art: images generated in an image tool with commercial-use rights, then edited by hand | Objects, scenes | log every file in docs/art-provenance.csv |
