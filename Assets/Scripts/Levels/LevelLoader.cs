using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Levels
{
    /// <summary>
    /// Outcome of <see cref="LevelLoader.Load"/>: a ready <see cref="Board"/>, or the errors that stopped it.
    /// </summary>
    public sealed class LevelLoadResult
    {
        internal LevelLoadResult(LevelData level, Board board, IReadOnlyList<LevelError> errors)
        {
            Level = level;
            Board = board;
            Errors = errors;
        }

        /// <summary>True when the level was valid and <see cref="Board"/> is set.</summary>
        public bool Success => Board != null;

        /// <summary>The board, or null when there are errors.</summary>
        public Board Board { get; }

        /// <summary>Parsed data (moveLimit, tutorial, ...), or null when the file could not be parsed.</summary>
        public LevelData Level { get; }

        /// <summary>Every parse or validation error; empty on success.</summary>
        public IReadOnlyList<LevelError> Errors { get; }
    }

    /// <summary>
    /// Turns level JSON into a <see cref="Board"/>: <see cref="LevelParser"/>, then <see cref="LevelValidator"/>, then
    /// build. The board trusts validated data, so this is the only way level files should reach it.
    /// No file IO here: the view layer reads the file (TextAsset, StreamingAssets, ...) and passes the text.
    /// Parse errors (bad JSON, wrong field types) stop before validation, since the data is then incomplete.
    /// </summary>
    public static class LevelLoader
    {
        /// <summary>
        /// Loads one level. Never throws for bad level content; check <see cref="LevelLoadResult.Success"/> and
        /// <see cref="LevelLoadResult.Errors"/>. <paramref name="sourceName"/> (e.g. the file name) names the level in
        /// errors when the file has no <c>id</c>.
        /// </summary>
        public static LevelLoadResult Load(string json, string sourceName = null)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));

            LevelData level;
            try
            {
                level = LevelParser.Parse(json, sourceName);
            }
            catch (LevelFormatException e)
            {
                return new LevelLoadResult(null, null, e.Errors);
            }

            var errors = LevelValidator.Validate(level, sourceName);
            if (errors.Count > 0) return new LevelLoadResult(level, null, errors);

            return new LevelLoadResult(level, Build(level), errors);
        }

        /// <summary>
        /// Builds the board from data that passed <see cref="LevelValidator"/>. Same result as
        /// <see cref="Board.FromLayers"/>, but keeps the shelf ids from the file.
        /// </summary>
        private static Board Build(LevelData level)
        {
            var shelves = new Shelf[level.Shelves.Count];
            for (int i = 0; i < shelves.Length; i++)
            {
                var data = level.Shelves[i];
                shelves[i] = new Shelf(data.Layers, data.Id);
            }
            return new Board(shelves);
        }
    }
}
