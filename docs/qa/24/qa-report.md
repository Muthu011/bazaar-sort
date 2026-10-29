# QA report: issue #24 World 1 look page

Branch `task/24-world1-look-page` (3 commits over origin/main e38d119). Docs-only. Unity tests were not run: no file under `Assets/` changed.

## Check a: diff scope
- `git diff origin/main --name-only`: only `docs/style-guide.md` (186 insertions, 0 deletions). Met.
- No `Assets/` change, no image, `docs/art-provenance.csv` untouched (empty diff).
- All previous v0 bullets, including the touch-area rule, are intact (zero removed lines). The only edit to existing text is an added intro paragraph under the title.

## Acceptance criteria
| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Section: warm Madurai kitchen, brass and steel, soft shadows, bright not neon; palette with hex; line and shading rules | Met | "Mood", "Palette (World 1)", "Line and shading rules" present; 23 hex entries; saturation rules stated. Two palette entries break the doc's own V rule (finding 4). |
| 2 | Asset list with exact pixel sizes: 10 objects, shelf, background, UI buttons and panels, padlock, coin, star | Met with errors | All items listed with source and display px. Button width and coin size disagree with code (findings 1 and 2). |
| 3 | Readability rules: recognisable at 48 dp, distinct silhouette and main colour per object | Met | 7 rules plus a 10-row table (silhouette, main colour hex, greyscale, nearest confusable). Near-ties are named. |
| 4 | Image-tool prompt per object matching the style | Met | Shared style block, shared negative prompt, 10 per-object lines, hexes in prompts all exist in the palette. Two prompts exceed the detail budget (finding 7). |

## Check b: sizes against code
Re-derived with the formula in `BoardLayout.Compute` and `BoardLayoutSettings.Default` (side 4%, top 10%, bottom 10%, padding 0.12, hidden strip 0.35, column gap 0.25, row gap 0.35, max 3 columns, 3 slots, so shelf is 3.48 x 1.59 tiles). Safe area assumed to be the full screen.

| Case | Doc | Computed | Verdict |
| --- | --- | --- | --- |
| 4 shelves 1080x2400 | 259 px, 86 dp | 1 col, 259.1 px, 86.4 dp | OK |
| 6 shelves | 170 px, 57 dp | 1 col, 170.1 px, 56.7 dp | OK |
| 8 shelves | 138 px, 46 dp | 2 col, 137.8 px, 45.9 dp | OK |
| 14 shelves | "2 to 3 columns", "about 138 or less" | exactly 2 col, 137.8 px (3 col gives 90.8) | Imprecise (finding 8) |
| 14 shelves 720x1600 | 92 px, 46 dp | 2 col, 91.9 px, 45.9 dp | OK |
| Shelf at 4 shelves | 901 x 412 px | 901.3 x 411.8 | OK |
| Shelf at 8 shelves | 481 x 220 | 479.5 x 219.1 (479.7 x 219.4 using 137.8 -> 138 rounding gives 480 x 219) | Off by 1 px, trivial |
| Shelf source 1808 x 826 | aspect 3.48:1.59 | 2.189 vs 2.189 | OK |
| Padlock | 0.55 tile, aspect 70/64, 142 x 156 | `PadlockWidthTiles` 0.55; `PlaceholderSprites` 64 x 70; 142.5 x 155.8 | OK |
| Star | 52 dp = 156 px | `ResultPanelLayout.StarDp` 52 | OK |
| Button height | 56 dp | `ButtonHeightDp` 56 | OK (height); width wrong, see finding 1 |
| Card max width | 320 dp = 960 px | `MaxCardWidthDp` 320 | OK |
| HUD counter | 84 x 40 dp = 252 x 120 px | `CounterWidthDp` 84, `CounterHeightDp` 40 | OK |
| Coin | 32 dp = 96 px | `UiFeel.CoinDp` = 36 (108 px) | MISMATCH (finding 2) |
| `PixelsPerUnit` 100; density = dpi/160; CanvasScaler ConstantPixelSize with scaleFactor = density | as stated | `BoardView.PixelsPerUnit` 100; `ScreenDensity` dpi/160; `GameHud`, `ResultPanels` set ConstantPixelSize | OK |
| Smallest World 1 shelf count | 4 shelves is the max display | World 1 levels use 4 to 8 shelves, so 259 px is the max | OK |

## Check c: WCAG contrast (computed)
| Pair | Doc | Computed | Verdict |
| --- | --- | --- | --- |
| Roasted brown #3B2418 on Card cream #FFF4DC | 13.3 | 13.25 | OK |
| Roasted brown on Sandstone #E9DCC3 | about 11 | 10.67 | Passes AA/AAA; "about 11" is 0.33 high (outside the 0.1 tolerance, though worded "about") |
| Roasted brown on Turmeric #F2B632 | 7.9 | 7.93 | OK |
| White on Peacock blue #1F6F8B | 5.7 | 5.67 | OK |
| White on Kumkum red #B3321F | 6.2 | 6.19 | OK |
| White on Leaf dark #2E6F4E | 6.0 | 6.00 | OK (row is labelled "Banana-leaf green #2E6F4E", wrong name, finding 6) |
| White on Night slate #2A3A44 | 11.8 | 11.75 | OK |
| Not listed: Roasted brown on Leaf #4E9A5A | doc says "use Roasted brown there" | 4.20 | Fails AA normal text (4.5), passes large (3). Finding 3 |
| Not listed: white on Leaf #4E9A5A | doc says do not | 3.45 | Doc correct |
| Not listed: white on Turmeric | doc says do not | 1.82 | Doc correct |

## Check d: saturation / value rule (S <= 0.85, V <= 0.95 for non-highlight fills, no two channels at 255)
| Colour | Hex | S | V | Verdict |
| --- | --- | --- | --- | --- |
| Cream | #FFF4DC | 0.14 | 1.00 | V violation (one channel 255); wall and card fill |
| Sandstone | #E9DCC3 | 0.16 | 0.91 | ok |
| Plank light | #E3C79A | 0.32 | 0.89 | ok |
| Plank mid | #B88B5A | 0.51 | 0.72 | ok |
| Plank dark | #8A5A33 | 0.63 | 0.54 | ok |
| Lime panel | #F0DFC0 | 0.20 | 0.94 | ok |
| Brass / hi / shadow | #B8862B / #E8C46A / #7A5416 | 0.77 / 0.54 / 0.82 | 0.72 / 0.91 / 0.48 | ok |
| Steel / hi / shadow | #B9C2C6 / #F1F5F6 / #6F7B82 | 0.07 / 0.02 / 0.15 | 0.78 / 0.96 / 0.51 | ok (highlight 0.96 allowed) |
| Iron / hi | #2A2A2E / #55555C | 0.09 / 0.08 | 0.18 / 0.36 | ok |
| Turmeric | #F2B632 | 0.79 | 0.95 | ok (on the limit) |
| Kumkum red | #B3321F | 0.83 | 0.70 | ok (close to the limit) |
| Peacock blue | #1F6F8B | 0.78 | 0.55 | ok |
| Leaf | #4E9A5A | 0.49 | 0.60 | ok |
| Leaf dark | #2E6F4E | 0.59 | 0.44 | ok |
| Jamun | #6B4A8A | 0.46 | 0.54 | ok |
| Idli white | #F4EFE2 | 0.07 | 0.96 | V violation: an object fill, not a highlight |
| Roasted brown / Night slate | #3B2418 / #2A3A44 | 0.59 / 0.38 | 0.23 / 0.27 | ok |

No colour has two channels at 255. Neutral Sat < 0.20 holds for steel, iron, cream. Greyscale values quoted in the doc match computed luma (Rec. 709) within 0.03 (cooker 0.30, tiffin 0.38, kettle 0.34, scraper 0.38, rolling pin 0.50 vs 0.52 computed, kudam 0.54, masala 0.73, tumbler 0.75, tawa 0.17, idli 0.94).

## Check e: the 10 objects
All ten ids (tumbler, pressure_cooker, idli_stand, tawa, masala_dabba, steel_tiffin, coconut_scraper, brass_kudam, rolling_pin, chai_kettle) have an asset-list row (512 x 512), a readability row with a palette hex, and a prompt. Ids match `docs/game-design.md`. Prompts contain no brand, text, people, hands, religious symbol or deity image. `brass_kudam` is plain (prompt and row both say no markings, cloth, coconut, leaves, religious decoration). The negative prompt names "om, swastika, kolam, tilak" as things to exclude, which is fine as a negative list.

## Check f: pronouns
`grep -E '\b(he|him|his|himself)\b'` on `docs/style-guide.md`: no matches. Muthu is referred to by name. Met.

## Check g: coherence
- Every hex used in the readability table and prompts is in the palette. Met.
- Asset sizes agree between the table and the text (512 px objects at 1.98x of 259 px; 144 / 96 px for 48 dp). Exceptions are findings 1 and 2.
- Inconsistency: prompts ask for a 1024 x 1024 image while source is 512 x 512 (downscale 2x, intended; not an error).

## Findings
1. MEDIUM, wrong size. Button display size "256 x 168 px (about 85 x 56 dp)" does not match the code. `ResultPanelLayout.Compute` makes the button `cardWidth - 2 x PaddingDp` = 272 x 56 dp (816 x 168 px at 3x). The 9-slice centre must stretch about 1.6x horizontally, and the 96 px vertical borders (192 total) exceed the 168 px display height, so the corners are scaled down. The doc should state 272 x 56 dp and check the border against the 168 px height. The HUD pill (252 x 120 px with border 96) has the same border-larger-than-height issue.
2. MEDIUM, wrong size. Coin: doc says 96 px (32 dp); `UiFeel.CoinDp` is 36 dp (108 px). Fix the doc or the code constant.
3. MEDIUM, contrast. "Use Roasted brown on #4E9A5A" gives 4.20:1, below AA normal text (4.5). It passes only as large text (3:1), which contradicts the doc's stated aim of AA body text everywhere. Use a lighter or different surface, or restrict to 20 dp bold and say so.
4. LOW, palette rule broken. Cream #FFF4DC has V 1.00 and Idli white #F4EFE2 has V 0.96, against the stated "Val 0.95 or lower for any colour that is not a highlight". Either add them to the exceptions (neutral wall/card, idli) or change the hex.
5. LOW, wording. "Roasted brown on Sandstone: about 11:1" is 10.67. Say "10.7".
6. LOW, naming. Contrast row "White on Banana-leaf green #2E6F4E" should say "Leaf dark".
7. LOW, detail budget. The doc caps objects at 3 secondary details, but the masala_dabba prompt asks for seven bowls plus a spoon and the idli_stand prompt asks for 3 to 4 plates with scalloped edges, a post and idlis in holes. Either relax the rule for these two or simplify the prompts (for example 5 bowls, 3 plates).
8. LOW, imprecise. 14 shelves is exactly 2 columns at 137.8 px (not "2 to 3 columns, about 138 or less"). 14 shelves on 1080x2400 are beyond World 1 anyway (levels 1 to 20 use at most 8).
9. LOW, dangling reference. "see the art director workflow in `CLAUDE.md`" (line 108): `CLAUDE.md` has no such section.
10. NOTE. Greyscale of rolling_pin (0.50) and brass_kudam (0.54) are also close but not listed with the near-tie group; their silhouettes differ strongly, so no action is needed beyond mentioning it.
11. NOTE. Panel source 768 px displays up to 960 px wide (centre stretched 1.25x): fine for a 9-slice, but it is the one place the "never upscales" statement does not hold; say "except 9-slice centres".

No player-visible problems in the game itself (docs only; no screens changed, so no screenshots apply).

RESULT: FAIL

Top issues for the author: (1) correct the button display size to 272 x 56 dp and check the 9-slice border against the 168 px height; (2) coin size 36 dp, not 32; (3) fix the Roasted-brown-on-#4E9A5A guidance (4.20:1 fails AA); (4) reconcile Cream and Idli white with the V <= 0.95 rule; (5) trim the masala_dabba and idli_stand prompts to the 3-detail budget.

## Round 2

Reviewed commit 82821ba. Diff scope: `git diff origin/main --name-only` lists only `docs/style-guide.md` and `docs/qa/24/qa-report.md`; no Assets, no images, `art-provenance.csv` untouched. Still no removed v0 lines. `grep` for he/him/his/himself: no matches. All hexes in the doc are in the palette table (script check, empty difference). Palette unchanged, so the round 1 saturation table stands.

| # | Round 1 finding | Round 2 verdict | Evidence |
| --- | --- | --- | --- |
| 1 | Button size | Fixed | 816 x 168 px (272 x 56 dp) matches `ResultPanelLayout` (320 - 2 x 24 dp). Source 480 x 336, border 120: 2 x 120 = 240 < 336 and < 480, valid. Displayed at 2x downscale: border 60 px, height 168 px. Only the flat centre stretches (240 source px to 696 px, about 2.9x), consistent with the reworded rule. HUD pill 504 x 240, border 80: 160 < 240 and < 504, valid, shown at 252 x 120 px. |
| 2 | Coin | Fixed | `UiFeel.CoinDp` 36 dp = 108 px; source 216 is exactly 2x. |
| 3 | Text on #4E9A5A | Fixed | Computed 4.20 (brown) and 3.45 (white), both listed as large text and icons only; normal text uses white on Leaf dark #2E6F4E (6.00). White on Turmeric (1.82) is banned. |
| 4 | V rule | Fixed | Explicit exception for Cream (1.00), Lime panel (0.94, within anyway) and Idli white (0.96), all Sat < 0.20, never #FFFFFF. Checked: Sat 0.14, 0.20, 0.07. Note: Lime Sat is 0.20, not under 0.20 (see R2-1), but its V is 0.94 so it needs no exception. |
| 5 | Detail budget | Fixed | Repeated elements count as one; idli_stand = plates, post, idlis; masala_dabba = tin, bowl ring, spoon. Prompts unchanged and now consistent. |
| 6 | Names / Sandstone | Fixed | Row reads "White on Leaf dark #2E6F4E", Sandstone 10.7 (computed 10.67). |
| 7 | 14 shelves | Fixed | Row: 2 columns, 137.8 px, 46 dp (computed 137.8, 3 columns would be 90.8). |
| 8 | CLAUDE.md pointer | Fixed | `.claude/agents/art-director.md` line 10 describes sprite import: single, 512 px max, compressed for Android, atlas, provenance row. `docs/art-provenance.csv` exists. |
| 9 | Shelf / panel / near-ties | Fixed | Shelf 902 x 412 (259.1 x 3.48 = 901.7 and 411.9) and 480 x 219 (137.8 x 3.48 = 479.6, x 1.59 = 219.1). Panel 960 x 960, border 144: 320 dp x 3 = 960 px, so shown 1:1 at xxhdpi, 288 < 960. Near-tie list now includes rolling_pin (0.50) and brass_kudam (0.54). |

Round 1 checks re-run: a (scope) pass, c (contrast table, new rows computed) pass, d pass, e (10 ids, rows, prompts, no brand/people/religion, kudam plain) pass, f pass, g pass.

Remaining findings (all LOW, none block):
- R2-1. Palette note says light neutrals have "Sat under 0.20", but Lime panel #F0DFC0 is exactly 0.20 (rounded; 0.1999 unrounded may be below). No effect since its V is 0.94.
- R2-2. The 9-slice border sizes assume Image `pixelsPerUnitMultiplier` of 2 (button, pill) and 1 (panel) at xxhdpi. The current placeholder code (`UiParts.cs`) uses 1.5. That is an implementation matter for the later art-import task; the style guide could add one line saying so.
- R2-3. "Shown 1:1 (48 dp)" for the panel border is true only at 3 px per dp; at xhdpi (2 px per dp) a 144 px border is 72 dp unless scaled. Minor wording.

RESULT: PASS
