# Style guide (v0, to be refined with Muthu)

The bullets below are the base rules. The sections after them add the World 1 look (issue #24). Nothing here is art yet: no image, font or asset is in `Assets/` for World 1.

- Look: warm premium 2.5D. Slightly isometric objects, soft top-left light, real materials (brass shine, stainless steel, terracotta, banana-leaf green, glass). Not cartoon clip art.
- Palette: turmeric yellow, kumkum red, peacock blue, banana-leaf green, brass gold on warm cream. One accent colour per world.
- Objects: 512 px source, transparent background, centred, same camera angle and light for every object. Each object must read in greyscale at 64 px with a unique silhouette.
- Scenes: shelves belong to the setting (steel rack, chai kadai counter, bangle wall); backdrop softly blurred.
- Effects on clear: themed and short (coffee steam, jasmine petals, diya sparkle), under 0.6 s.
- UI: rounded cards, 48 px+ tap targets, icons and numbers over words.
- Board touch areas: the 4–14 shelf layout is fixed. Item touch areas extend past the picture into the gaps between items and shelves to reach 48 dp where there's room. 44 dp is the floor on 720p phones. Levels 1 to 20 use at most 8 shelves.
- Audio: veena, flute, tabla and mridangam loops, calm; real object sounds (tumbler clink, cooker whistle).
- Respect: no deity images as pieces; pooja items never discarded, crushed or near footwear or waste.

## World 1: Amma's Kitchen (look page, issue #24)

Status: proposal for Muthu to react to with test art. Taste calls are listed in "Questions for Muthu" at the end of this section; the defaults below are used until Muthu answers.

### Mood
- A warm Madurai home kitchen in the morning: limewashed cream wall, a plank shelf, brass and stainless steel catching a soft window light from the top left.
- Bright but not neon: colours are rich and clean, never glowing. Think painted enamel and polished metal, not plastic or gel.
- Materials: brass (warm, slightly worn), stainless steel (cool grey, soft highlights), black cast iron, enamel paint, plain wood, cloth-soft shadows.
- Lighting for every object: one soft key light from the top left, a weak cool fill from the right, a small bounce light from below. Highlights are soft strips, not hard white spots.
- Camera for every object: slightly above, about 20 to 25 degrees down, no perspective distortion. The object sits upright on an invisible floor. Objects that are long (rolling pin) are turned about 30 degrees so they fill the square.
- Not allowed: text, logos, brand marks, people, hands, steam or food scenes in the object art, deity images, religious symbols.

### Palette (World 1)
Sat = HSV saturation, Val = HSV value. Greyscale = approximate luma (0 black to 1 white), used for the greyscale test.

| Role | Name | Hex | Notes |
| --- | --- | --- | --- |
| Background wall | Cream limewash | #FFF4DC | Greyscale 0.96. Never pure white. |
| Background shade | Sandstone | #E9DCC3 | Blurred floor, window shadow, vignette |
| Shelf board top | Plank light | #E3C79A | Shelf top edge, lit side |
| Shelf front edge | Plank mid | #B88B5A | Front face of the plank |
| Shelf underside | Plank dark | #8A5A33 | Underside and the soft shadow tint |
| Shelf back panel | Lime panel | #F0DFC0 | Inset panel behind the items (items sit on this) |
| Brass | Brass | #B8862B | Mid tone. Highlight #E8C46A, shadow #7A5416 |
| Steel | Steel | #B9C2C6 | Mid tone. Highlight #F1F5F6, shadow #6F7B82 |
| Iron | Iron black | #2A2A2E | Tawa. Never pure black. Highlight #55555C |
| Accent (World 1) | Turmeric | #F2B632 | The one World 1 accent: HUD highlights, star fill, active button |
| Accent support | Kumkum red | #B3321F | Pressure cooker, warnings, fail panel |
| Accent support | Peacock blue | #1F6F8B | Steel tiffin, links, info |
| Accent support | Banana-leaf green | #4E9A5A | Rolling pin, success, win panel |
| UI success dark | Leaf dark | #2E6F4E | Win panel fill (white text). Not used on objects |
| Accent support | Jamun purple | #6B4A8A | Chai kettle |
| Coconut wood | Coconut brown | #8A5A33 | Coconut scraper body |
| Idli | Idli white | #F4EFE2 | Idlis. Slightly warm, never #FFFFFF |
| UI card | Card cream | #FFF4DC | Panels, same as wall |
| UI text | Roasted brown | #3B2418 | All text on cream and turmeric |
| UI dark | Night slate | #2A3A44 | Dark overlays, scrim (use at 60% alpha) |
| UI button | Turmeric | #F2B632 | Primary button fill, text Roasted brown |
| UI button alt | Peacock blue | #1F6F8B | Secondary button fill, text white |

Saturation and brightness limits ("bright but not neon"):
- Fill colours: Sat 0.85 or lower, and Val 0.95 or lower for any colour that is not a highlight. No fill has two RGB channels at 255.
- Exception, light neutrals: the wall cream #FFF4DC (Val 1.00), the Lime panel and the Idli white #F4EFE2 (Val 0.96) may exceed Val 0.95 because their Sat is under 0.20. They are never pure white #FFFFFF.
- Only specular highlights may reach Val 1.0 and they must be under 3% of an object's area.
- Neutral colours (steel, iron, cream) keep Sat under 0.20.
- No colour outside this table as a fill. Shading uses the highlight/shadow variants of the same material.
- A bright accent (Turmeric, Kumkum red) covers at most about 40% of one object, so a shelf of mixed objects does not look like confetti.

Contrast checks (WCAG 2.1, contrast ratio):

| Text on background | Ratio | Result |
| --- | --- | --- |
| Roasted brown #3B2418 on Card cream #FFF4DC | 13.3 : 1 | AA and AAA |
| Roasted brown on Sandstone #E9DCC3 | 10.7 : 1 | AA and AAA |
| Roasted brown on Turmeric #F2B632 (button) | 7.9 : 1 | AA and AAA |
| White on Peacock blue #1F6F8B (button) | 5.7 : 1 | AA (body text) |
| White on Kumkum red #B3321F | 6.2 : 1 | AA |
| White on Leaf dark #2E6F4E (text panels) | 6.0 : 1 | AA |
| Roasted brown on Banana-leaf green #4E9A5A | 4.20 : 1 | Fails AA for normal text; large text (24 px+ or 19 px+ bold) and icons only (3 : 1) |
| White on Banana-leaf green #4E9A5A | 3.45 : 1 | Large text and icons only |
| White on Night slate #2A3A44 | 11.8 : 1 | AA and AAA |

- Normal-size text never goes on the lighter #4E9A5A green (brown 4.20 : 1, white 3.45 : 1); use Leaf dark #2E6F4E with white there. White text never goes on Turmeric; use Roasted brown.
- Numbers (moves, coins, padlock count) are 20 dp or larger, bold, and always use the ratios above. Large text needs 3 : 1, but we aim for AA body text everywhere.
- Re-check any new text and background pair with a WCAG contrast calculator before it ships.

Colour-blind note:
- Never use colour alone for meaning. The seven main object colours are separated by silhouette and by lightness as well as hue; the red/green pair (cooker #B3321F, rolling pin #4E9A5A) differ in lightness (greyscale 0.30 against 0.50) and silhouette (squat pot against long bar).
- Win/fail panels pair colour with an icon (star row for win, retry arrow for fail), and the padlock shows a number.
- Check the final art with a simulator for red-green (deuteranopia) and blue-yellow (tritanopia) as well as greyscale.

### Line and shading rules
- Painted-realistic, not cartoon outline. No black outline. Shapes are defined by value change and a thin darker edge of the object's own colour (the shadow variant), about 2 px wide at 512 px.
- Shading: 3 to 4 value steps per material plus a soft highlight strip on the top-left side. Gradients are smooth, no banding.
- Brass: warm highlight (#E8C46A), orange-brown shadow (#7A5416), faint wear marks only. Steel: cool highlight, soft reflections of cream and shelf colour, no mirror-perfect reflections or reflected windows. Iron: matte with one faint highlight.
- Shape language: rounded forms, chunky proportions (about 15% thicker than real life) so details survive at small size. Radius of the smallest visible detail is no less than 6 px at 512 px.
- Detail budget: at most 3 secondary details per object (rivets, a handle grip, a vent). No engraving, no patterns, no text. Repeated elements count as one detail (the idli holes and idlis, the ring of spice bowls). So `idli_stand` is plates, centre post and idlis, and `masala_dabba` is the tin, the bowl ring and a spoon. Fine detail below 10 px at 512 px is removed.
- Contact shadow: a small soft ellipse at the base of the object, inside the canvas, 25% opacity of Plank dark. No long cast shadows (the game draws its own shadow for lifted and dragged items).
- Edges: anti-aliased, transparent background, no white fringe. Export as PNG with straight (non-premultiplied) alpha, sRGB.
- Every object uses the same light direction. If a generated image has light from another side, flip or redo it.

### Reference resolution and how sizes are derived
- Design resolution is 1080 x 2400, portrait, about 3 px per dp (xxhdpi, 480 dpi; 1 dp = dpi / 160 px as in `Assets/Scripts/View/ScreenDensity.cs`). 720 x 1600 (xhdpi, 2 px per dp) is the small-phone check.
- The UI is built with `CanvasScaler` in ConstantPixelSize mode (`Assets/Scripts/UI/GameHud.cs`, `ResultPanels.cs`), so UI sizes are dp times density. World units are screen pixels / 100 (`BoardView.PixelsPerUnit`). So the art is placed in screen pixels and must be sized for those.
- Board layout (`Assets/Scripts/View/BoardLayout.cs`, `BoardLayoutSettings.Default`): the item picture is one "tile". Shelf width is 3 slots + 4 x 0.12 padding = 3.48 tiles. Shelf height is 1 + 0.35 hidden strip + 2 x 0.12 = 1.59 tiles. Column gap 0.25 tile, row gap 0.35 tile, up to 3 columns, 4% side margin, 10% top and 10% bottom reserve. Every shelf holds exactly 3 slots (`docs/level-schema.md`).
- Tile size from that layout on 1080 x 2400 (computed by the same formula as `BoardLayout.Compute`):

| Shelves | Columns | Tile (px) | Tile (dp) |
| --- | --- | --- | --- |
| 4 | 1 | 259 | 86 |
| 6 | 1 | 170 | 57 |
| 8 | 2 | 138 | 46 |
| 14 | 2 | 137.8 | 46 |
| 14 on 720 x 1600 | 2 | 92 | 46 |

- So an object is shown between about 138 px (46 dp, 8 shelves, the worst case for levels 1 to 20) and 259 px (86 dp, 4 shelves). The 48 dp touch rule (`BoardLayout.TargetTouchDp`, floor `FloorTouchDp` 44) is met by the touch area growing into the gaps, but the picture itself is about 46 dp at its smallest. This is why the readability test below uses 48 dp.
- Source size is 2x the largest display size where practical, so the game downscales and mipmaps; it never upscales art. The only stretching is the flat centre of a 9-slice panel, which carries no detail. Object source: 512 px (259 px max display is 1.98x). Game import: 512 px max, compressed for Android (import settings are described in `.claude/agents/art-director.md`; provenance goes in `docs/art-provenance.csv`).
- Other sizes come from UI constants: button height 56 dp (`ResultPanelLayout.ButtonHeightDp`), card max width 320 dp (`MaxCardWidthDp`), star 52 dp (`StarDp`), HUD counter 84 x 40 dp (`HudLayout.CounterWidthDp/CounterHeightDp`), padlock width 0.55 tile with aspect 70/64 (`BoardView.PadlockWidthTiles`, `PlaceholderSprites.PadlockAspect`).

### Asset list (World 1)
All PNG, RGBA, transparent background unless marked, sRGB. "Display" is at 1080 x 2400 (xxhdpi). "Source" is what Muthu delivers. 9-slice means the art is stretched in the centre and the stated border (in source pixels) stays fixed.

| Asset | Source size (px) | Display size | Notes |
| --- | --- | --- | --- |
| `tumbler` | 512 x 512 | 138 to 259 px (46 to 86 dp) | Object rule: within a 464 x 464 safe box, centred, base on the bottom 24 px |
| `pressure_cooker` | 512 x 512 | same | same |
| `idli_stand` | 512 x 512 | same | same |
| `tawa` | 512 x 512 | same | same |
| `masala_dabba` | 512 x 512 | same | same |
| `steel_tiffin` | 512 x 512 | same | same |
| `coconut_scraper` | 512 x 512 | same | same |
| `brass_kudam` | 512 x 512 | same | same |
| `rolling_pin` | 512 x 512 | same | same |
| `chai_kettle` | 512 x 512 | same | same |
| Shelf (one, all 3-slot shelves) | 1808 x 826 | 902 x 412 px at 4 shelves, 480 x 219 px at 8 shelves | Aspect 3.48 : 1.59. Plank plus back panel, hidden-layer strip across the top 22% (0.35 / 1.59). Locked state is drawn in code. |
| Background | 1200 x 2800 | scaled to cover the screen | Opaque. Keep key detail inside the centre 1200 x 2133 so 9:16 phones crop safely. Soft blur, low contrast, nothing busy behind the board (the board sits between 10% and 90% of the height). |
| Button (primary and secondary share one 9-slice, tinted) | 480 x 336 | 816 x 168 px (272 x 56 dp: card 320 dp minus 2 x 24 dp padding, `ResultPanelLayout`) | 9-slice, border 120 on all sides (shown as 60 px, so source is 2x). Check: 2 x 120 = 240 is less than the 336 height and 480 width. Neutral light base so it can be tinted Turmeric or Peacock blue |
| Panel / card | 960 x 960 | up to 960 px wide (320 dp) | 9-slice, border 144 (shown 1:1, 48 dp). Check: 2 x 144 = 288 is less than 960. Card cream with a thin Plank mid edge and soft shadow |
| HUD pill (counter) | 504 x 240 | 252 x 120 px (84 x 40 dp) | 9-slice, border 80 (shown as 40 px, so source is 2x). Check: 2 x 80 = 160 is less than the 240 height and 504 width |
| Padlock | 256 x 280 | up to 142 x 156 px (0.55 tile at 259 px) | Brass, plain, no keyhole text; aspect 64 : 70 |
| Coin | 216 x 216 | 108 px (36 dp, `UiFeel.CoinDp`) | Brass disc with a plain embossed ring and a simple rupee-free centre (no symbols, no text) |
| Star | 256 x 256 | 156 px (52 dp) | Filled Turmeric star with warm highlight. An empty version in Sandstone with a Plank mid edge, same size |
| Icons (pause, sound, retry) | 128 x 128 each | 72 px (24 dp) | Roasted brown, simple, not in this issue's scope; listed for later |

### Readability rules
1. Each object must be recognisable at 48 dp. That is 144 px at xxhdpi, 96 px at xhdpi (720 x 1600).
2. Silhouette: each object has a unique outline when filled solid black. No two objects may share an overall shape (tall cylinder, round pot, long bar, flat disc with a handle).
3. Main colour: each object has one main colour from the table below covering at least 50% of its visible area. Details use the same material's highlight and shadow.
4. Fill the frame: the object fills at least 80% of the 464 px safe box in its longer dimension.
5. Greyscale test: convert the 512 px art to greyscale, shrink to 64 px. Objects must be told apart by outline plus lightness. Where two objects have similar greyscale (see below), the silhouette must differ clearly.
6. 48 dp thumbnail check Muthu can do: shrink each image to 144 x 144 (and 96 x 96), put all 10 on a cream #FFF4DC strip in a row, view them on a phone at normal arm's length, or at 100% zoom on a computer. Cover the names. If any object takes more than a second to name, or two are confused, fail it and note which pair.
7. A mix of 3 shelves of 3 items each must not look like the same blob: look at silhouettes only (all black) as well as the colour version.

| Object | Silhouette notes | Main colour (palette) | Greyscale | Most similar object and what separates them |
| --- | --- | --- | --- | --- |
| tumbler | Tall, slightly flared cup standing in a wide flat saucer (dabara); taller than wide, with a wide foot. No handle | Steel #B9C2C6 | 0.75 | `steel_tiffin`: tiffin is three stacked tiers with a wire clip arching over the top and is peacock blue; tumbler has the flared saucer base, one smooth tier and is silver. `brass_kudam`: kudam has a round belly and a narrow neck, tumbler has straight sides |
| pressure_cooker | Squat wide pot, two short side handles, domed lid with a small weight on top | Kumkum red #B3321F | 0.30 | `brass_kudam` (also round-ish): cooker is wider than tall with side handles and a top knob and is red; kudam is taller than wide with a narrow neck and no handles |
| idli_stand | Tall column of 3 to 4 round plates with scalloped edges and a centre post, pale idlis in the holes | Idli white #F4EFE2 (steel frame) | 0.94 | `steel_tiffin`: both stacked. Idli stand shows holes and scalloped plate edges and is the lightest object; tiffin has smooth ring tiers and a clip handle and is mid-dark blue |
| tawa | Flat disc with one long handle sticking out to the right, seen from above at the shared angle (an ellipse with a stick) | Iron black #2A2A2E | 0.17 | `coconut_scraper`: scraper is a low wooden stool with legs and a jagged round blade on one end, brown; tawa is the darkest object, round with one straight handle and no legs or teeth |
| masala_dabba | Wide round tin, seen slightly from above with the lid open: a ring of small round bowls of spice inside, a small spoon | Turmeric #F2B632 (spices) in a steel tin | 0.73 | `brass_kudam` (both warm yellow): dabba is wide and low with a bowl ring on top and is bright yellow; kudam is tall and round with a neck and is darker brass (greyscale 0.54, keep it at least 0.15 darker than the dabba) |
| steel_tiffin | Three stacked tiers with a wire clip handle arching over the top; rounder at the bottom edge | Peacock blue #1F6F8B (painted tiers) | 0.38 | `tumbler` (see above) and `idli_stand` (see above) |
| coconut_scraper | Low, long horizontal wooden stool with a round serrated blade on one end, short legs | Coconut brown #8A5A33 | 0.38 | `tawa`: described above. `rolling_pin`: pin is a smooth bar with two knobs at 30 degrees, green; scraper is low with legs and teeth, brown |
| brass_kudam | Round-bellied water pot, narrow neck, wide flared rim, slight foot, plain | Brass #B8862B | 0.54 | `tumbler` and `masala_dabba` (above). Kudam must have no markings: it is an everyday water pot |
| rolling_pin | Long bar with two end knobs, turned about 30 degrees so it fills the square | Banana-leaf green #4E9A5A (lacquered handles, pale wood roller) | 0.50 | `coconut_scraper` (above). Also keep it thicker than a real pin so it does not shrink to a line at 48 dp |
| chai_kettle | Pot-bellied body with a long curved spout on one side, a high arched handle, small knob on the lid | Jamun purple #6B4A8A (enamel) | 0.34 | `pressure_cooker`: kettle has the long spout, arched handle and is purple; cooker is squat with two short handles and is red. In greyscale these two are close (0.30 and 0.34), so the spout and handle must stay bold |

Greyscale pairs that rely on silhouette: `steel_tiffin`, `coconut_scraper`, `chai_kettle`, `pressure_cooker` (all 0.30 to 0.38), and `rolling_pin` (0.50) with `brass_kudam` (0.54). The rolling pin is a long thin bar and the kudam a round pot, so the silhouette must carry that pair. These must be flagged again when the real art arrives; if any pair is confused at 64 px, change the object's lightness rather than adding detail.

### Prompts for the 10 objects
These are tool-neutral text prompts. Use the shared style block with the object line. Generate on a plain light background and remove it by hand to get the transparent PNG, or ask the tool for a transparent background if it can.

Shared style block (paste before every object line):

> A single everyday Indian kitchen object, centred, upright, on a plain flat light grey background. Warm premium 2.5D look, painted-realistic, slightly chunky and rounded proportions. Camera slightly above at about 20 to 25 degrees, no perspective distortion. One soft key light from the top left, weak cool fill from the right, soft highlight strips, a small soft contact shadow under the object only. Smooth shading with 3 to 4 value steps, a thin darker edge of the object's own colour instead of a black outline. Clean bright colours, rich but not neon, not glowing. Simple shape, at most three small details, no engraving, no pattern. The object fills most of a square frame with a small margin all round. Square image, 1024 x 1024.

Shared negative prompt (paste after every object line, or in the tool's negative field):

> text, letters, numbers, logo, brand name, watermark, people, hands, food scene, steam, extra objects, background scenery, table, shelf, cartoon outline, thick black outline, neon colours, glow, harsh specular spots, mirror reflections, engraving, patterns, religious symbols, deity images, om, swastika, kolam, tilak, flowers, cut-off edges, blurry, low resolution, photo noise

Per-object lines (main colour from the palette; use the hex in the prompt if the tool understands it, otherwise the colour name):

1. `tumbler`: "A South Indian filter coffee tumbler and dabara set: a tall slightly flared stainless steel tumbler standing inside a wide shallow steel saucer (dabara). Cool steel #B9C2C6 with soft white highlights. Plain, no handle, no markings."
2. `pressure_cooker`: "A squat round Indian pressure cooker in kumkum red enamel #B3321F, with a domed lid, a small steel weight on top, and two short black side handles. Wider than tall. Plain, no markings."
3. `idli_stand`: "A stainless steel idli stand: a tall stack of three or four round plates with scalloped edges held on a centre post, each plate holding pale warm white idlis (#F4EFE2) in round holes. Steel frame, no food garnish, no steam, no plate or chutney."
4. `tawa`: "A flat round Indian cast iron tawa in matte iron black #2A2A2E, seen from slightly above, with one long straight handle sticking out to the right. Thick rim, a faint soft highlight only. Nothing on the tawa."
5. `masala_dabba`: "A round stainless steel masala dabba spice tin, lid off or open, seen slightly from above, showing a ring of seven small round bowls filled with bright turmeric yellow #F2B632 and warm spice colours, with a small steel spoon. Wide and low."
6. `steel_tiffin`: "A three-tier Indian tiffin carrier: three stacked round tiers painted peacock blue #1F6F8B with steel rims, held by a wire clip handle that arches over the top. Taller than wide. Plain, no markings."
7. `coconut_scraper`: "A traditional Indian coconut scraper (thengai thirugi): a low long wooden stool in coconut brown #8A5A33 with short legs and a round serrated steel blade fixed on one end. No coconut, no bowl."
8. `brass_kudam`: "A plain everyday brass water pot (kudam): a round-bellied pot with a narrow neck, a wide flared rim and a small foot, in warm brass #B8862B with soft golden highlights and faint wear. Completely plain: no pattern, no markings, no flowers, no coconut, no leaves, no cloth, no religious decoration. Taller than wide."
9. `rolling_pin`: "An Indian rolling pin (belan): a smooth pale wooden roller with two thick end handles lacquered banana-leaf green #4E9A5A, turned about 30 degrees so it fills the square frame. Chunky proportions, plain, no markings."
10. `chai_kettle`: "A pot-bellied Indian chai kettle in jamun purple enamel #6B4A8A with a long curved spout on the left, a high arched handle, a lid with a small round knob, and a steel rim. Plain, no markings, no steam."

Prompt consistency check when images come back: same light direction, same camera angle, same scale of detail, same edge treatment. Regenerate rather than paint over when the angle or light is off.

### Logging approved art
When Muthu makes or approves an image, append one row per file to `docs/art-provenance.csv` (columns: `file, source_tool, prompt_ref, edited_by, approved_by, date`). Example shape: `Assets/Art/Objects/world1/tumbler.png, <tool name and version>, docs/style-guide.md#prompts-for-the-10-objects (tumbler), <who edited by hand>, Muthu, <yyyy-mm-dd>`. Only tools listed in `docs/approved-dependencies.md` may be used. No rows are added in this issue.

### Questions for Muthu
1. Pressure cooker colour: kumkum red enamel (default, keeps hue variety) or classic plain steel (more authentic, but conflicts with tumbler and tiffin)?
2. Steel tiffin and chai kettle: peacock blue and jamun purple painted/enamel finish (default, for readability) or all plain steel?
3. Rolling pin: green lacquered handles (default) or fully wooden (authentic, but near the coconut scraper's brown)?
4. Shelf: pale limewashed wooden plank (default) or a steel rack as the older style bullet says?
5. Is turmeric yellow #F2B632 the right single World 1 accent, or would you prefer kumkum red?
6. Brass kudam: confirm a bare brass pot with no cloth, coconut or decoration is acceptable (default: bare).
7. Should the greyscale near-ties (tiffin, scraper, kettle, cooker) be fixed in the art now, or only if the 64 px test fails?
8. Sizes: are 2x-of-largest-display (512 px objects, 1808 x 826 shelf) fine for the Android build size, or do you want smaller sources?
