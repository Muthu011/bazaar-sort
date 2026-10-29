using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Solver
{
    /// <summary>
    /// A winning line from the root, published by the shortest-solution search (on another thread) when it finds one.
    /// </summary>
    internal sealed class WinningLine
    {
        private volatile IReadOnlyList<SolverMove> _moves;

        public WinningLine(IReadOnlyList<SolverMove> moves = null)
        {
            _moves = moves;
        }

        /// <summary>The line, or null while none is known.</summary>
        public IReadOnlyList<SolverMove> Moves => _moves;

        public void Publish(IReadOnlyList<SolverMove> moves) => _moves = moves;
    }

    /// <summary>Outcome of <see cref="PlaySimulator.Run"/>.</summary>
    public sealed class PlayStats
    {
        internal PlayStats(int plays, int wins, int lossesStuck, int lossesCircling, int lossesTooLong,
            int aliveStates, int deadStates, int unknownStates, int playsRun = -1, string stoppedReason = null)
        {
            Plays = plays;
            PlaysRun = playsRun < 0 ? plays : playsRun;
            StoppedReason = stoppedReason;
            Wins = wins;
            LossesStuck = lossesStuck;
            LossesCircling = lossesCircling;
            LossesTooLong = lossesTooLong;
            AliveStates = aliveStates;
            DeadStates = deadStates;
            UnknownStates = unknownStates;
        }

        /// <summary>Plays asked for.</summary>
        public int Plays { get; }

        /// <summary>Plays actually finished; fewer than <see cref="Plays"/> only when the level budget ran out.</summary>
        public int PlaysRun { get; }

        /// <summary>Why the plays stopped early (the level budget ran out); null when every play ran.</summary>
        public string StoppedReason { get; }

        /// <summary>True when every play ran.</summary>
        public bool AllPlaysRun => PlaysRun == Plays && StoppedReason == null;

        public int Wins { get; }

        /// <summary>Plays that ended with items left and no legal move.</summary>
        public int LossesStuck { get; }

        /// <summary>Plays where every move led back to a board the player had already seen in that play.</summary>
        public int LossesCircling { get; }

        /// <summary>Plays that hit <see cref="SolverOptions.MaxPlayMoves"/>.</summary>
        public int LossesTooLong { get; }

        /// <summary>Distinct non-won states visited by all plays, by whether a win was still reachable from them.</summary>
        public int AliveStates { get; }
        public int DeadStates { get; }
        public int UnknownStates { get; }

        /// <summary>Wins / plays run (equal to plays asked for unless the level budget ran out).</summary>
        public double WinRate => PlaysRun == 0 ? 0 : (double)Wins / PlaysRun;

        /// <summary>Dead / (alive + dead) over the distinct non-won states the plays visited.</summary>
        public double DeadEndRate => AliveStates + DeadStates == 0 ? 0 : (double)DeadStates / (AliveStates + DeadStates);

        /// <summary>
        /// True when every play ran and every visited state was classified (no liveness search ran out of budget).
        /// </summary>
        public bool DeadEndComplete => UnknownStates == 0 && AllPlaysRun;
    }

    /// <summary>
    /// Simulates N seeded first-try plays by a "random but sensible" player and measures how often they win and how
    /// many of the boards they pass through are already lost.
    ///
    /// Policy, each turn (all moves applied with the Core <see cref="Board"/>):
    /// <list type="number">
    /// <item>The choices are every legal (shelf, item type, target shelf): identical items in one row count once, but
    /// every target shelf is its own choice, as on screen.</item>
    /// <item>If any choice clears a triple (seen as the board's item count dropping), pick uniformly among those.</item>
    /// <item>Otherwise pick uniformly among the choices whose resulting board has not been seen earlier in this play.
    /// This rules out undoing the last move and walking in circles.</item>
    /// <item>The play is lost when no move is legal (stuck), when every choice leads back to a seen board (the player
    /// is circling and would restart), or after <see cref="SolverOptions.MaxPlayMoves"/> moves.</item>
    /// </list>
    /// Play i uses its own generator seeded from (Seed, i), so results do not depend on run order. "Seen" uses
    /// <see cref="SearchContext.PlayKey"/>: normal shelves in any order count as the same board, as they always have.
    ///
    /// Dead-end rate: over the distinct non-won states visited by all plays, the fraction from which no win is
    /// reachable any more. Won plays pass only through live states. "Alive" is monotone along a play (an earlier state
    /// reaches every later one), so for each lost play a binary search with <see cref="LivenessOracle"/> finds the last
    /// live state, and every state after it is dead.
    /// </summary>
    public static class PlaySimulator
    {
        private enum Outcome
        {
            Won,
            Stuck,
            Circling,
            TooLong,
        }

        public static PlayStats Run(Board root, IReadOnlyList<bool> pinnedShelves = null, SolverOptions options = null)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            options = options ?? SolverOptions.Default;
            return Run(new SearchContext(root, pinnedShelves), options, new WinningLine(), SearchBudget.For(options));
        }

        /// <summary>
        /// Runs the plays on <see cref="SolverOptions.Threads"/> threads. Each thread has its own search context and
        /// dead-end oracle, and the oracles share what they prove (<see cref="LivenessFacts"/>); the plays are independent (own seed each) and every alive/dead answer is a fact about the
        /// board, so the merged numbers are the same for any thread count. <paramref name="winningLine"/> may be
        /// published while the plays run (the shortest solution is searched for at the same time); once it is, each
        /// thread marks its states alive before its next play. That only saves dead-end searches, it changes no answer.
        /// </summary>
        internal static PlayStats Run(SearchContext context, SolverOptions options, WinningLine winningLine,
            SearchBudget budget)
        {
            int threads = ThreadCount(options);

            var workers = new Worker[threads];
            var facts = new LivenessFacts();
            for (int w = 0; w < threads; w++)
            {
                workers[w] = new Worker(w == 0 ? context : context.CreateSibling(), options, winningLine, budget, facts);
            }

            int nextPlay = -1;
            Exception failure = null;
            void Work(Worker worker)
            {
                try
                {
                    while (!budget.Exhausted)
                    {
                        int p = System.Threading.Interlocked.Increment(ref nextPlay);
                        if (p >= options.Plays) break;
                        worker.Play(p);
                    }
                }
                catch (Exception e)
                {
                    System.Threading.Interlocked.CompareExchange(ref failure, e, null);
                    budget.Stop("a simulated play failed: " + e.Message);
                }
            }

            if (threads == 1)
            {
                Work(workers[0]);
            }
            else
            {
                var started = new System.Threading.Thread[threads - 1];
                for (int w = 1; w < threads; w++)
                {
                    var worker = workers[w];
                    started[w - 1] = new System.Threading.Thread(() => Work(worker), 16 * 1024 * 1024)
                    {
                        IsBackground = true,
                        Name = "Bazaar Sort solver plays " + w,
                    };
                    started[w - 1].Start();
                }
                Work(workers[0]);
                foreach (var thread in started) thread.Join();
            }
            if (failure != null) throw new InvalidOperationException("Simulated plays failed.", failure);

            // Merge: a known answer is never overwritten by unknown.
            var status = workers[0].Status;
            int wins = 0, stuck = 0, circling = 0, tooLong = 0, playsRun = 0;
            foreach (var worker in workers)
            {
                wins += worker.Wins;
                stuck += worker.Stuck;
                circling += worker.Circling;
                tooLong += worker.TooLong;
                playsRun += worker.PlaysRun;
                if (worker.Status == status) continue;
                foreach (var pair in worker.Status)
                {
                    if (pair.Value < 0 && status.ContainsKey(pair.Key)) continue;
                    status[pair.Key] = pair.Value;
                }
            }

            int alive = 0, dead = 0, unknown = 0;
            foreach (var value in status.Values)
            {
                if (value == 1) alive++;
                else if (value == 0) dead++;
                else unknown++;
            }

            return new PlayStats(options.Plays, wins, stuck, circling, tooLong, alive, dead, unknown, playsRun,
                budget.Reason);
        }

        /// <summary>Threads for the plays: <see cref="SolverOptions.Threads"/>, or for 0 one per processor up to
        /// <see cref="MaxAutoThreads"/>.</summary>
        internal static int ThreadCount(SolverOptions options)
        {
            int threads = options.Threads > 0 ? options.Threads : Math.Min(Environment.ProcessorCount, MaxAutoThreads);
            return Math.Max(1, Math.Min(threads, options.Plays));
        }

        internal const int MaxAutoThreads = 4;

        /// <summary>One thread's share of the plays: its own context, oracle, scratch board and results.</summary>
        private sealed class Worker
        {
            private readonly SearchContext _context;
            private readonly SolverOptions _options;
            private readonly SearchBudget _budget;
            private readonly LivenessOracle _oracle;
            private readonly List<SolverMove> _moves = new List<SolverMove>();
            private readonly List<SolverMove> _clearing = new List<SolverMove>();
            private readonly List<int> _mayClear = new List<int>();
            // Boards of the current play, by position in it (the root is not one of them): reused by every play, so a
            // play builds no boards of its own. Nothing keeps a board of a play after the play ends.
            private readonly Board[] _pool;
            private readonly HashSet<StateKey> _seen = new HashSet<StateKey>();
            private readonly List<Board> _pathBoards = new List<Board>();
            private readonly List<StateKey> _pathKeys = new List<StateKey>();
            // Candidate moves are tried on one scratch board (Board.CopyStateFrom + Board.TryMove); only the move the
            // player makes is kept as a board of its own.
            private readonly Board _scratch;

            private readonly WinningLine _winningLine;
            private bool _lineMarked;

            public Worker(SearchContext context, SolverOptions options, WinningLine winningLine, SearchBudget budget,
                LivenessFacts facts)
            {
                _winningLine = winningLine;
                _context = context;
                _options = options;
                _budget = budget;
                _oracle = new LivenessOracle(context, options.LivenessNodeBudget, budget, facts);
                _scratch = context.Root.Clone();
                _pool = new Board[options.MaxPlayMoves + 2];
            }

            // Every state on the winning line is alive (once the line is known).
            private void MarkWinningLine()
            {
                var line = _winningLine?.Moves;
                if (_lineMarked || line == null) return;
                _lineMarked = true;
                var replay = _context.Root;
                _oracle.MarkAlive(replay);
                foreach (var move in line)
                {
                    replay = SearchContext.Apply(replay, move);
                    _oracle.MarkAlive(replay);
                }
            }

            private Board PoolBoard(int position)
            {
                return _pool[position] ?? (_pool[position] = _context.Root.Clone());
            }

            /// <summary>1 = alive, 0 = dead, -1 = unknown, by <see cref="SearchContext.PlayKey"/>.</summary>
            public Dictionary<StateKey, sbyte> Status { get; } = new Dictionary<StateKey, sbyte>();

            public int Wins { get; private set; }
            public int Stuck { get; private set; }
            public int Circling { get; private set; }
            public int TooLong { get; private set; }
            public int PlaysRun { get; private set; }

            public void Play(int p)
            {
                var context = _context;
                var budget = _budget;
                var moves = _moves;
                var clearing = _clearing;
                var seen = _seen;
                var pathBoards = _pathBoards;
                var pathKeys = _pathKeys;
                var scratch = _scratch;

                MarkWinningLine();
                var rng = new SplitMix64(Hashing.Mix(_options.Seed) ^ Hashing.Mix((ulong)p + 1));
                seen.Clear();
                pathBoards.Clear();
                pathKeys.Clear();

                var board = context.Root;
                var key = context.PlayKey(board);
                Outcome outcome;
                while (true)
                {
                    if (board.IsWon)
                    {
                        outcome = Outcome.Won;
                        break;
                    }

                    seen.Add(key);
                    pathBoards.Add(board);
                    pathKeys.Add(key);

                    if (pathBoards.Count > _options.MaxPlayMoves)
                    {
                        outcome = Outcome.TooLong;
                        break;
                    }

                    context.GeneratePlayMoves(board, moves, _mayClear);
                    if (moves.Count == 0)
                    {
                        outcome = Outcome.Stuck;
                        break;
                    }

                    clearing.Clear();
                    for (int c = 0; c < _mayClear.Count; c++)
                    {
                        int m = _mayClear[c];
                        budget.Charge();
                        TryOn(scratch, board, moves[m]);
                        if (scratch.ItemCount < board.ItemCount) clearing.Add(moves[m]);
                    }

                    Board next = null;
                    StateKey nextKey = default;
                    if (clearing.Count > 0)
                    {
                        next = PoolBoard(pathBoards.Count);
                        TryOn(next, board, clearing[rng.NextInt(clearing.Count)]);
                        nextKey = next.IsWon ? default : context.PlayKey(next);
                    }
                    else
                    {
                        // Draw without replacement until a move leads to a board not seen in this play.
                        int remaining = moves.Count;
                        while (remaining > 0)
                        {
                            int pick = rng.NextInt(remaining);
                            var move = moves[pick];
                            moves[pick] = moves[remaining - 1];
                            moves[remaining - 1] = move;
                            remaining--;

                            budget.Charge();
                            TryOn(scratch, board, move);
                            var childKey = context.PlayKey(scratch);
                            if (seen.Contains(childKey)) continue;
                            next = PoolBoard(pathBoards.Count);
                            next.CopyStateFrom(scratch);
                            nextKey = childKey;
                            break;
                        }
                    }

                    if (next == null)
                    {
                        outcome = Outcome.Circling;
                        break;
                    }

                    board = next;
                    key = nextKey;
                }

                if (budget.Exhausted) return; // this play was cut short by the level budget: not counted

                PlaysRun++;
                switch (outcome)
                {
                    case Outcome.Won:
                        Wins++;
                        for (int i = 0; i < pathKeys.Count; i++)
                        {
                            Status[pathKeys[i]] = 1;
                            _oracle.MarkAlive(pathBoards[i]);
                        }
                        break;
                    default:
                        if (outcome == Outcome.Stuck) Stuck++;
                        else if (outcome == Outcome.Circling) Circling++;
                        else TooLong++;
                        ClassifyLostPlay(pathBoards, pathKeys, _oracle, Status);
                        break;
                }
            }
        }

        private static void TryOn(Board scratch, Board board, SolverMove move)
        {
            scratch.CopyStateFrom(board);
            if (!scratch.TryMove(move.FromShelf, move.FromSlot, move.ToShelf, out var result))
            {
                throw new InvalidOperationException($"Simulated player tried an illegal move {move}: {result}.");
            }
        }

        // Binary search for the last live state of a lost play; states up to it are alive, after it dead.
        private static void ClassifyLostPlay(List<Board> boards, List<StateKey> keys, LivenessOracle oracle,
            Dictionary<StateKey, sbyte> status)
        {
            int lo = -1;              // last index known alive (-1: none yet)
            int hi = boards.Count;    // first index known dead (Count: none yet)

            // A play cut off by the move cap often ends on a live board: then the whole play was alive.
            int last = boards.Count - 1;
            bool? lastAlive = Lookup(status, keys[last]) ?? oracle.IsAlive(boards[last]);
            if (lastAlive == true) lo = last;
            else if (lastAlive == false) hi = last;

            while (lastAlive != null && hi - lo > 1)
            {
                int mid = lo + (hi - lo) / 2;
                bool? alive = Lookup(status, keys[mid]) ?? oracle.IsAlive(boards[mid]);
                if (alive == null)
                {
                    // Budget ran out: record what is known and leave the rest unknown.
                    break;
                }
                if (alive.Value) lo = mid;
                else hi = mid;
            }

            for (int i = 0; i <= lo; i++) status[keys[i]] = 1;
            for (int i = hi; i < boards.Count; i++) status[keys[i]] = 0;
            for (int i = lo + 1; i < hi; i++)
            {
                if (!status.ContainsKey(keys[i])) status[keys[i]] = -1;
            }
        }

        private static bool? Lookup(Dictionary<StateKey, sbyte> status, StateKey key)
        {
            if (!status.TryGetValue(key, out sbyte value) || value < 0) return null;
            return value == 1;
        }
    }
}
