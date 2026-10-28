using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Levels
{
    /// <summary>
    /// Thrown by <see cref="LevelParser.Parse"/> when a level file is not valid JSON or a field has the wrong type.
    /// <see cref="Errors"/> lists every such problem found, not just the first.
    /// </summary>
    public sealed class LevelFormatException : Exception
    {
        public LevelFormatException(IReadOnlyList<LevelError> errors)
            : base(errors != null && errors.Count > 0 ? errors[0].Message : "Level file is malformed.")
        {
            Errors = errors ?? Array.Empty<LevelError>();
        }

        public IReadOnlyList<LevelError> Errors { get; }
    }

    /// <summary>
    /// Reads level JSON (docs/level-schema.md, v1) into <see cref="LevelData"/>. Checks syntax and field types only;
    /// game rules (slot counts, object ids, counts, starting state) are <see cref="LevelValidator"/>'s job.
    ///
    /// Uses Newtonsoft's LINQ to JSON (<see cref="JToken"/>) rather than reflection-based deserialisation, so nothing
    /// here depends on types surviving IL2CPP managed-code stripping, and every token carries its line and position for
    /// error messages. Strings are never turned into dates, comments are ignored, duplicate keys and trailing content
    /// are errors, and unknown fields (e.g. <c>solver</c>) are ignored so newer files still load.
    /// No file IO: callers pass the text.
    /// </summary>
    public static class LevelParser
    {
        /// <summary>Level id used in errors when the file has no usable <c>id</c> and no source name is given.</summary>
        public const string NoIdName = "(no id)";

        private static readonly JsonLoadSettings _loadSettings = new JsonLoadSettings
        {
            CommentHandling = CommentHandling.Ignore,
            LineInfoHandling = LineInfoHandling.Load,
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
        };

        /// <summary>
        /// Parses <paramref name="json"/>. <paramref name="sourceName"/> (e.g. a file name) names the level in errors
        /// when the file has no <c>id</c>.
        /// </summary>
        /// <exception cref="LevelFormatException">Malformed JSON or wrongly typed fields.</exception>
        public static LevelData Parse(string json, string sourceName = null)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));

            string fallbackName = string.IsNullOrEmpty(sourceName) ? NoIdName : sourceName;

            JToken root;
            try
            {
                root = ReadRoot(json, fallbackName);
            }
            catch (JsonReaderException e)
            {
                throw new LevelFormatException(new[]
                {
                    new LevelError(LevelErrorCode.MalformedJson, fallbackName, "malformed JSON: " + Reason(e.Message),
                        line: e.LineNumber > 0 ? e.LineNumber : (int?)null,
                        position: e.LineNumber > 0 ? e.LinePosition : (int?)null),
                });
            }

            var reader = new FieldReader(fallbackName);
            var level = reader.ReadLevel(root);
            if (reader.Errors.Count > 0) throw new LevelFormatException(reader.Errors);
            return level;
        }

        /// <summary>Reads exactly one JSON value (plus comments) from the text.</summary>
        private static JToken ReadRoot(string json, string fallbackName)
        {
            using (var text = new StringReader(json))
            using (var reader = new JsonTextReader(text))
            {
                reader.DateParseHandling = DateParseHandling.None;
                reader.FloatParseHandling = FloatParseHandling.Double;

                // Skip leading comments; JToken.ReadFrom would otherwise return the comment itself.
                bool hasToken;
                do
                {
                    hasToken = reader.Read();
                } while (hasToken && reader.TokenType == JsonToken.Comment);

                if (!hasToken)
                {
                    throw new LevelFormatException(new[]
                    {
                        new LevelError(LevelErrorCode.MalformedJson, fallbackName, "malformed JSON: the text is empty"),
                    });
                }

                var root = JToken.ReadFrom(reader, _loadSettings);

                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.Comment) continue;
                    throw new LevelFormatException(new[]
                    {
                        new LevelError(LevelErrorCode.MalformedJson, fallbackName,
                            "malformed JSON: unexpected content after the level object",
                            line: reader.LineNumber, position: reader.LinePosition),
                    });
                }
                return root;
            }
        }

        /// <summary>
        /// Newtonsoft messages end with " Path '...', line N, position M."; drop that part, since
        /// <see cref="LevelError"/> adds the line and position itself.
        /// </summary>
        private static string Reason(string message)
        {
            int cut = message.LastIndexOf(" Path '", StringComparison.Ordinal);
            return (cut >= 0 ? message.Substring(0, cut) : message).Trim();
        }

        /// <summary>Walks the JSON tree, filling <see cref="LevelData"/> and collecting type errors.</summary>
        private sealed class FieldReader
        {
            private readonly List<LevelError> _errors = new List<LevelError>();
            private string _levelId;

            public FieldReader(string fallbackName)
            {
                _levelId = fallbackName;
            }

            public IReadOnlyList<LevelError> Errors => _errors;

            public LevelData ReadLevel(JToken root)
            {
                var level = new LevelData();
                if (!(root is JObject obj))
                {
                    Error(root, $"the file must contain a JSON object, found {Describe(root)}");
                    return level;
                }

                // Read the id first so every later error can name the level.
                level.Id = ReadString(obj, "id", "field \"id\"");
                if (!string.IsNullOrEmpty(level.Id)) _levelId = level.Id;

                level.Schema = ReadInt(obj, "schema", "field \"schema\"");
                level.World = ReadInt(obj, "world", "field \"world\"");
                level.Index = ReadInt(obj, "index", "field \"index\"");
                level.Role = ReadString(obj, "role", "field \"role\"");
                level.Tutorial = ReadString(obj, "tutorial", "field \"tutorial\"");
                level.MoveLimit = ReadInt(obj, "moveLimit", "field \"moveLimit\"");
                level.TimeLimitSec = ReadInt(obj, "timeLimitSec", "field \"timeLimitSec\"");

                var shelves = ReadArray(obj, "shelves", "field \"shelves\"");
                if (shelves != null)
                {
                    level.Shelves = new List<ShelfData>(shelves.Count);
                    for (int i = 0; i < shelves.Count; i++)
                    {
                        level.Shelves.Add(ReadShelf(shelves[i], i));
                    }
                }

                var trays = ReadArray(obj, "trays", "field \"trays\"");
                if (trays != null)
                {
                    for (int i = 0; i < trays.Count; i++)
                    {
                        level.Trays.Add(ReadTray(trays[i], i));
                    }
                }

                var wrapped = ReadArray(obj, "wrapped", "field \"wrapped\"");
                if (wrapped != null)
                {
                    for (int i = 0; i < wrapped.Count; i++)
                    {
                        if (wrapped[i].Type == JTokenType.String) level.Wrapped.Add((string)wrapped[i]);
                        else Error(wrapped[i], $"wrapped[{i}] must be a string like \"s3:1:0\", found {Describe(wrapped[i])}");
                    }
                }

                return level;
            }

            private ShelfData ReadShelf(JToken value, int index)
            {
                var shelf = new ShelfData();
                if (!(value is JObject obj))
                {
                    Error(value, $"must be a JSON object, found {Describe(value)}", index);
                    return shelf;
                }

                shelf.Id = ReadString(obj, "id", "field \"id\"", index);
                shelf.Kind = ReadString(obj, "kind", "field \"kind\"", index, shelf.Id) ?? ShelfKinds.Normal;
                shelf.LockClears = ReadInt(obj, "lockClears", "field \"lockClears\"", index, shelf.Id) ?? 0;

                var layers = ReadArray(obj, "layers", "field \"layers\"", index, shelf.Id);
                if (layers == null) return shelf;

                shelf.Layers = new List<List<string>>(layers.Count);
                for (int layer = 0; layer < layers.Count; layer++)
                {
                    var slots = new List<string>();
                    shelf.Layers.Add(slots);
                    if (!(layers[layer] is JArray slotValues))
                    {
                        Error(layers[layer], $"must be an array of slots, found {Describe(layers[layer])}",
                            index, shelf.Id, layer);
                        continue;
                    }

                    for (int slot = 0; slot < slotValues.Count; slot++)
                    {
                        var item = slotValues[slot];
                        if (item.Type == JTokenType.String)
                        {
                            slots.Add((string)item);
                        }
                        else
                        {
                            slots.Add(null);
                            if (item.Type != JTokenType.Null)
                            {
                                Error(item, $"must be an object id string or null, found {Describe(item)}",
                                    index, shelf.Id, layer, slot);
                            }
                        }
                    }
                }
                return shelf;
            }

            private TrayData ReadTray(JToken value, int index)
            {
                var tray = new TrayData();
                if (!(value is JObject obj))
                {
                    Error(value, $"trays[{index}] must be a JSON object, found {Describe(value)}");
                    return tray;
                }

                tray.Id = ReadString(obj, "id", $"trays[{index}].id");
                var set = ReadArray(obj, "set", $"trays[{index}].set");
                if (set != null)
                {
                    for (int i = 0; i < set.Count; i++)
                    {
                        if (set[i].Type == JTokenType.String) tray.Set.Add((string)set[i]);
                        else Error(set[i], $"trays[{index}].set[{i}] must be a string, found {Describe(set[i])}");
                    }
                }
                return tray;
            }

            // ---------- Typed field access. A missing field or JSON null reads as null. ----------

            private static JToken Get(JObject obj, string key)
            {
                if (!obj.TryGetValue(key, out JToken value)) return null;
                return value.Type == JTokenType.Null || value.Type == JTokenType.Undefined ? null : value;
            }

            private string ReadString(JObject obj, string key, string name, int? shelf = null, string shelfId = null)
            {
                var value = Get(obj, key);
                if (value == null) return null;
                if (value.Type == JTokenType.String) return (string)value;
                Error(value, $"{name} must be a string, found {Describe(value)}", shelf, shelfId);
                return null;
            }

            private int? ReadInt(JObject obj, string key, string name, int? shelf = null, string shelfId = null)
            {
                var value = Get(obj, key);
                if (value == null) return null;
                // Whole numbers written as decimals (1.0) are accepted; very large integers arrive as BigInteger and
                // are rejected like any other out-of-range value.
                object raw = value.Type == JTokenType.Integer || value.Type == JTokenType.Float
                    ? ((JValue)value).Value
                    : null;
                if (raw is long whole && whole >= int.MinValue && whole <= int.MaxValue) return (int)whole;
                if (raw is double d && d == Math.Floor(d) && d >= int.MinValue && d <= int.MaxValue) return (int)d;
                Error(value, $"{name} must be a whole number, found {Describe(value)}", shelf, shelfId);
                return null;
            }

            private JArray ReadArray(JObject obj, string key, string name, int? shelf = null, string shelfId = null)
            {
                var value = Get(obj, key);
                if (value == null) return null;
                if (value is JArray array) return array;
                Error(value, $"{name} must be an array, found {Describe(value)}", shelf, shelfId);
                return null;
            }

            private void Error(JToken at, string detail, int? shelf = null, string shelfId = null, int? layer = null,
                int? slot = null)
            {
                int? line = null;
                int? position = null;
                if (at is IJsonLineInfo info && info.HasLineInfo())
                {
                    line = info.LineNumber;
                    position = info.LinePosition;
                }
                _errors.Add(new LevelError(LevelErrorCode.WrongType, _levelId, detail, shelf, shelfId, layer, slot,
                    line: line, position: position));
            }

            private static string Describe(JToken value)
            {
                if (value == null) return "nothing";
                switch (value.Type)
                {
                    case JTokenType.Null: return "null";
                    case JTokenType.String: return "string " + value.ToString(Formatting.None);
                    case JTokenType.Integer:
                    case JTokenType.Float: return "number " + value.ToString(Formatting.None);
                    case JTokenType.Boolean: return value.ToString(Formatting.None);
                    case JTokenType.Array: return "an array";
                    case JTokenType.Object: return "an object";
                    default: return value.Type.ToString();
                }
            }
        }
    }
}
