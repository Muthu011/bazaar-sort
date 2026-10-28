# Bazaar Sort — agent starter kit

Copy everything in this folder into the root of the game's GitHub repo (the same folder that holds the Unity project's `Assets/`, `Packages/` and `ProjectSettings/`). Then follow "Setup, step by step" in the *Sort Game Agent Workflow* doc.

| Path | What it is |
| --- | --- |
| `CLAUDE.md` | Rules every agent reads first: the game, the repo layout, the definition of done, the hard limits |
| `.mcp.json` | Connects Claude Code to the MCP for Unity bridge (http://localhost:8080/mcp) |
| `.claude/settings.json` | Permissions for headless runs + the STOP-file hook |
| `.claude/agents/*.md` | The eight agents |
| `.claude/skills/*/SKILL.md` | `/work-next`, `/daily-digest`, `/plan-week`, `/release-candidate` |
| `docs/` | Game design summary, level schema, style guide, approved dependencies |
| `Assets/Levels/World1/w1_l001.json` | An example level in the schema |
| `scripts/factory-loop.ps1` / `.sh` | The always-on loop |
| `scripts/setup-github.sh` | Creates the labels the workflow uses |
| `.github/workflows/` | CI tests + build, nightly internal-track upload, AI code review |
| `.github/ISSUE_TEMPLATE/task.yml` | The task format agents read |

If MCP for Unity's "Configure" button already added the Unity server to Claude Code, keep only one entry (either that or `.mcp.json`) so the server isn't listed twice. The server name must be `unityMCP`, because the permissions and agents refer to `mcp__unityMCP__*` tools.
