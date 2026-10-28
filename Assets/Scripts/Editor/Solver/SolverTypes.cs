using System;

namespace Game.Solver
{
    /// <summary>One move as <see cref="Game.Core.Board.TryMove(int,int,int)"/> takes it: (from shelf, from slot, to shelf).</summary>
    public readonly struct SolverMove : IEquatable<SolverMove>
    {
        public SolverMove(int fromShelf, int fromSlot, int toShelf)
        {
            FromShelf = fromShelf;
            FromSlot = fromSlot;
            ToShelf = toShelf;
        }

        public int FromShelf { get; }
        public int FromSlot { get; }
        public int ToShelf { get; }

        public bool Equals(SolverMove other)
        {
            return FromShelf == other.FromShelf && FromSlot == other.FromSlot && ToShelf == other.ToShelf;
        }

        public override bool Equals(object obj) => obj is SolverMove other && Equals(other);

        public override int GetHashCode() => (FromShelf * 397 + FromSlot) * 397 + ToShelf;

        public override string ToString() => $"({FromShelf},{FromSlot},{ToShelf})";
    }

    /// <summary>Whether a level can be won. Unknown means a search budget ran out before the answer was proven.</summary>
    public enum Solvability
    {
        Unknown = 0,
        Solvable,
        Unsolvable,
    }

    /// <summary>
    /// Budgets and fixed parameters for one analysis. The defaults are the values the pack tests and the editor menu
    /// use; the stored <c>solver</c> blocks in the level files are only valid for these defaults.
    /// </summary>
    public sealed class SolverOptions
    {
        /// <summary>Fixed seed for the simulated plays (the launch date). Changing it changes every estWinRate.</summary>
        public const ulong DefaultSeed = 20270112UL;

        /// <summary>Number of simulated first-try plays per level.</summary>
        public const int DefaultPlays = 1000;

        /// <summary>Shortest-solution search: board states expanded before giving up with Unknown.</summary>
        public int MaxExpandedNodes { get; set; } = 2_000_000;

        /// <summary>Shortest-solution search: wall-clock limit in seconds before giving up with Unknown.</summary>
        public double TimeLimitSeconds { get; set; } = 60;

        /// <summary>
        /// Threads for the simulated plays; 0 picks one per processor, at most 4 (more measured no faster, as the
        /// threads share Mono's garbage collector). The numbers do not depend on it: each play has its own seed and
        /// every dead-end answer is a fact about the board. Only the count of boards built (and so when the level node
        /// budget runs out) varies, since threads may each search the same dead end.
        /// </summary>
        public int Threads { get; set; }

        /// <summary>Simulated plays for estWinRate and deadEndRate.</summary>
        public int Plays { get; set; } = DefaultPlays;

        public ulong Seed { get; set; } = DefaultSeed;

        /// <summary>A simulated play that has not won after this many moves counts as a loss (the player gives up).</summary>
        public int MaxPlayMoves { get; set; } = 300;

        /// <summary>Dead-end check: states one "can this still be won?" search may visit before answering Unknown.</summary>
        public int LivenessNodeBudget { get; set; } = 500_000;

        /// <summary>
        /// Whole-level wall-clock limit in seconds, shared by every phase of one analysis (shortest-solution search,
        /// dead-end searches and simulated plays). When it runs out the analysis is Inconclusive (issue #31).
        /// </summary>
        public double LevelTimeLimitSeconds { get; set; } = 30;

        /// <summary>
        /// Whole-level node limit, shared by every phase of one analysis: each board the solver builds with the Core
        /// rules (a search child, a dead-end search child or a simulated move) counts one. When it runs out the
        /// analysis is Inconclusive (issue #31).
        /// </summary>
        public long LevelNodeBudget { get; set; } = 40_000_000;

        /// <summary>
        /// Cross-check mode for tests: plain breadth-first search with no heuristic, no merging of equivalent shelves
        /// and shelf order kept in the state key. Much slower; gives the same minMoves by a simpler route.
        /// </summary>
        public bool ReferenceMode { get; set; }

        public static SolverOptions Default => new SolverOptions();
    }

    /// <summary>
    /// One budget for a whole level analysis (issue #31): a node limit and a wall-clock limit that every phase charges.
    /// Once either runs out, <see cref="Exhausted"/> stays true, <see cref="Reason"/> says which, and every phase stops
    /// and reports Unknown / incomplete instead of continuing.
    /// </summary>
    /// Safe to charge from several threads (the simulated plays run in parallel).
    public sealed class SearchBudget
    {
        private readonly System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();
        private readonly object _lock = new object();
        private long _used;
        private int _sinceClockCheck;
        private volatile string _reason;

        public SearchBudget(long maxNodes, double timeLimitSeconds)
        {
            MaxNodes = maxNodes;
            TimeLimitSeconds = timeLimitSeconds;
        }

        /// <summary>The level budget from <paramref name="options"/>, starting now.</summary>
        public static SearchBudget For(SolverOptions options)
        {
            options = options ?? SolverOptions.Default;
            return new SearchBudget(options.LevelNodeBudget, options.LevelTimeLimitSeconds);
        }

        public long MaxNodes { get; }
        public double TimeLimitSeconds { get; }

        /// <summary>Nodes charged so far, over all phases.</summary>
        public long Used => System.Threading.Interlocked.Read(ref _used);

        public double ElapsedSeconds => _stopwatch.Elapsed.TotalSeconds; // reading a running Stopwatch is thread-safe

        /// <summary>Why the budget ran out; null while it lasts.</summary>
        public string Reason => _reason;

        public bool Exhausted => _reason != null;

        /// <summary>Counts <paramref name="nodes"/> more nodes. Returns false once the budget has run out.</summary>
        public bool Charge(long nodes = 1)
        {
            if (_reason != null) return false;
            if (System.Threading.Interlocked.Add(ref _used, nodes) > MaxNodes)
            {
                RunOut($"level node budget of {MaxNodes} boards ran out");
                return false;
            }
            if (System.Threading.Interlocked.Add(ref _sinceClockCheck, (int)Math.Min(nodes, 1024)) >= 256)
            {
                System.Threading.Interlocked.Exchange(ref _sinceClockCheck, 0);
                return CheckClock();
            }
            return true;
        }

        /// <summary>Checks the wall clock now. Returns false once the budget has run out.</summary>
        public bool CheckClock()
        {
            if (_reason != null) return false;
            if (ElapsedSeconds > TimeLimitSeconds)
            {
                RunOut($"level time limit of {TimeLimitSeconds:0.#} s ran out");
                return false;
            }
            return true;
        }

        /// <summary>Stops every phase now, with <paramref name="reason"/> (unless the budget already ran out).</summary>
        public void Stop(string reason) => RunOut(reason);

        // The first reason wins.
        private void RunOut(string reason)
        {
            lock (_lock)
            {
                if (_reason == null) _reason = reason;
            }
        }
    }

    /// <summary>Deterministic SplitMix64 generator, so seeded results are identical on every machine and runtime.</summary>
    internal sealed class SplitMix64
    {
        private ulong _state;

        public SplitMix64(ulong seed)
        {
            _state = seed;
        }

        public ulong Next()
        {
            _state += 0x9E3779B97F4A7C15UL;
            return Hashing.Mix(_state);
        }

        /// <summary>Uniform integer in [0, n).</summary>
        public int NextInt(int n)
        {
            if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n));
            return (int)(((Next() >> 32) * (ulong)n) >> 32);
        }
    }

    internal static class Hashing
    {
        /// <summary>SplitMix64 finaliser: a strong 64-bit mixer.</summary>
        public static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Order-dependent combine of a running hash with one more value.</summary>
        public static ulong Step(ulong h, ulong x)
        {
            return Mix(h ^ Mix(x + 0x9E3779B97F4A7C15UL));
        }
    }

    /// <summary>128-bit fingerprint of a canonical board state (collisions are negligible at search sizes).</summary>
    internal readonly struct StateKey : IEquatable<StateKey>
    {
        public StateKey(ulong a, ulong b)
        {
            A = a;
            B = b;
        }

        public ulong A { get; }
        public ulong B { get; }

        public bool Equals(StateKey other) => A == other.A && B == other.B;

        public override bool Equals(object obj) => obj is StateKey other && Equals(other);

        public override int GetHashCode() => (int)(A ^ (A >> 32));
    }
}
