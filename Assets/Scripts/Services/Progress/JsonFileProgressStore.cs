using System;
using System.IO;
using System.Text;

namespace Game.Services
{
    /// <summary>
    /// <see cref="IProgressStore"/> as one small JSON file (on the phone, <c>Application.persistentDataPath/progress.json</c>).
    ///
    /// Atomic save: the JSON goes to <c>progress.json.tmp</c> (flushed to disk), which then replaces
    /// <c>progress.json</c> in one rename (<c>File.Replace</c>, or delete-then-move where that is not supported). A crash
    /// while writing leaves the old save untouched; a crash between delete and move leaves a complete temp file, which
    /// <see cref="Load"/> picks up when the main file is missing.
    ///
    /// A save that can't be read is moved aside to <c>progress.json.unreadable</c> (kept for a bug report, and so the next
    /// save doesn't silently overwrite the evidence) and the game starts fresh.
    /// </summary>
    public sealed class JsonFileProgressStore : IProgressStore
    {
        public const string DefaultFileName = "progress.json";
        public const string TempSuffix = ".tmp";
        public const string UnreadableSuffix = ".unreadable";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly string _path;

        public JsonFileProgressStore(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("A save file path is required.", nameof(path));
            _path = path;
        }

        public string FilePath => _path;
        public string TempPath => _path + TempSuffix;
        public string UnreadablePath => _path + UnreadableSuffix;

        public ProgressLoadStatus Load(out ProgressData data, out string problem)
        {
            data = new ProgressData();
            problem = null;
            try
            {
                if (!File.Exists(_path))
                {
                    // Crash between delete and move during the last save: the temp file is the newest complete save.
                    if (File.Exists(TempPath) && ProgressSerializer.TryRead(File.ReadAllText(TempPath, Utf8NoBom), out var recovered, out _))
                    {
                        data = recovered;
                        return ProgressLoadStatus.Loaded;
                    }
                    return ProgressLoadStatus.Missing;
                }

                if (ProgressSerializer.TryRead(File.ReadAllText(_path, Utf8NoBom), out var saved, out problem))
                {
                    data = saved;
                    return ProgressLoadStatus.Loaded;
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                problem = "could not read the save (" + e.Message + ")";
            }

            MoveAside();
            return ProgressLoadStatus.Unreadable;
        }

        public bool Save(ProgressData data, out string problem)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            try
            {
                string directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                byte[] bytes = Utf8NoBom.GetBytes(ProgressSerializer.Write(data));
                using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (!File.Exists(_path))
                {
                    File.Move(TempPath, _path);
                }
                else
                {
                    try
                    {
                        File.Replace(TempPath, _path, null);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        File.Delete(_path);
                        File.Move(TempPath, _path);
                    }
                }
                problem = null;
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                problem = "could not write the save (" + e.Message + ")";
                return false;
            }
        }

        private void MoveAside()
        {
            try
            {
                if (!File.Exists(_path)) return;
                if (File.Exists(UnreadablePath)) File.Delete(UnreadablePath);
                File.Move(_path, UnreadablePath);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Best effort: the next save replaces the bad file anyway.
            }
        }
    }
}
