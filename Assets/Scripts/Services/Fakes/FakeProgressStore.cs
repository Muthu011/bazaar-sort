namespace Game.Services
{
    /// <summary>
    /// In-memory <see cref="IProgressStore"/> for tests and editor tools: holds the save as JSON text (so it goes
    /// through the real <see cref="ProgressSerializer"/>), can pretend the save is corrupt or the disk fails, and
    /// counts saves. Never touches the file system.
    /// </summary>
    public sealed class FakeProgressStore : IProgressStore
    {
        /// <summary>The saved JSON; null means no save yet.</summary>
        public string Json { get; set; }

        /// <summary>When true, <see cref="Save"/> fails like a full disk.</summary>
        public bool FailSaves { get; set; }

        public int SaveCount { get; private set; }
        public int LoadCount { get; private set; }

        public ProgressLoadStatus Load(out ProgressData data, out string problem)
        {
            LoadCount++;
            problem = null;
            if (Json == null)
            {
                data = new ProgressData();
                return ProgressLoadStatus.Missing;
            }
            if (ProgressSerializer.TryRead(Json, out data, out problem)) return ProgressLoadStatus.Loaded;
            data = new ProgressData();
            return ProgressLoadStatus.Unreadable;
        }

        public bool Save(ProgressData data, out string problem)
        {
            if (FailSaves)
            {
                problem = "fake disk full";
                return false;
            }
            Json = ProgressSerializer.Write(data);
            SaveCount++;
            problem = null;
            return true;
        }
    }
}
