using System.Runtime.CompilerServices;

// Game.UI: screens built from code. Thin; reads Game.View and raises button events.
[assembly: InternalsVisibleTo("Game.Tests.PlayMode")]

// Editor screenshot tools (GameSceneBuilder) use the QA hooks, e.g. ResultPanels.ShowContinueForPreview.
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
