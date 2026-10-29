namespace Game.Services
{
    /// <summary>Outcome of <see cref="IProgressStore.Load"/>.</summary>
    public enum ProgressLoadStatus
    {
        /// <summary>A valid save was read.</summary>
        Loaded = 0,

        /// <summary>No save yet (first launch): fresh progress, nothing to warn about.</summary>
        Missing,

        /// <summary>A save exists but can't be used (corrupt, truncated, other version): fresh progress, warn once.</summary>
        Unreadable,
    }

    /// <summary>
    /// Where progress lives between launches. <see cref="JsonFileProgressStore"/> on the device,
    /// <see cref="FakeProgressStore"/> in tests. Implementations never throw for bad or missing data.
    /// </summary>
    public interface IProgressStore
    {
        /// <summary>
        /// Reads the save. <paramref name="data"/> is always set: the save on <see cref="ProgressLoadStatus.Loaded"/>,
        /// fresh progress otherwise. <paramref name="problem"/> says why on <see cref="ProgressLoadStatus.Unreadable"/>.
        /// </summary>
        ProgressLoadStatus Load(out ProgressData data, out string problem);

        /// <summary>Writes the save so a crash part-way never leaves a broken file. False (and a reason) on an I/O failure.</summary>
        bool Save(ProgressData data, out string problem);
    }
}
