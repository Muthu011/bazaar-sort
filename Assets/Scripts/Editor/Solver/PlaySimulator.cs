using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Solver
{
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
            var context = new SearchContext(root, pinnedShelves);
            var budget = SearchBudget.For(options);
            return Run(context, options, new LivenessOracle(context, options.LivenessNodeBudget, budget), budget);
        }

        internal static PlayStats Run(SearchContext context, SolverOptions options, LivenessOracle oracle,
            SearchBudget budget)
        {
            var root = context.Root;
            // 1 = alive, 0 = dead, -1 = unknown (a known answer is never overwritten by unknown).
            var status = new Dictionary<StateKey, sbyte>();
            int wins = 0, stuck = 0, circling = 0, tooLong = 0;

            var moves = new List<SolverMove>();
            var clearing = new List<SolverMove>();
            // Candidate moves are tried on one scratch board (Board.CopyStateFrom + Board.TryMove); only the move
            // the player makes is kept as a board of its own.
            var scratch = root.Clone();
            var seen = new HashSet<StateKey>();
            var pathBoards = new List<Board>();
            var pathKeys = new List<StateKey>();

            int playsRun = 0;
            for (int p = 0; p < options.Plays && !budget.Exhausted; p++)
            {
                var rng = new SplitMix64(Hashing.Mix(options.Seed) ^ Hashing.Mix((ulong)p + 1));
                seen.Clear();
                pathBoards.Clear();
                pathKeys.Clear();

                var board = root;
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

                    if (pathBoards.Count > options.MaxPlayMoves)
                    {
                        outcome = Outcome.TooLong;
                        break;
                    }

                    context.GenerateMoves(board, moves, false);
                    if (moves.Count == 0)
                    {
                        outcome = Outcome.Stuck;
                        break;
                    }

                    clearing.Clear();
                    for (int m = 0; m < moves.Count; m++)
                    {
                        if (!SearchContext.MayClear(board, moves[m])) continue;
                        budget.Charge();
                        TryOn(scratch, board, moves[m]);
                        if (scratch.ItemCount < board.ItemCount) clearing.Add(moves[m]);
                    }

                    Board next = null;
                    StateKey nextKey = default;
                    if (clearing.Count > 0)
                    {
                        next = SearchContext.Apply(board, clearing[rng.NextInt(clearing.Count)]);
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
                            next = scratch.Clone();
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

                if (budget.Exhausted) break; // this play was cut short by the level budget: not counted

                playsRun++;
                switch (outcome)
                {
                    case Outcome.Won:
                        wins++;
                        for (int i = 0; i < pathKeys.Count; i++)
                        {
                            status[pathKeys[i]] = 1;
                            oracle.MarkAlive(pathBoards[i]);
                        }
                        break;
                    default:
                        if (outcome == Outcome.Stuck) stuck++;
                        else if (outcome == Outcome.Circling) circling++;
                        else tooLong++;
                        ClassifyLostPlay(pathBoards, pathKeys, oracle, status);
                        break;
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
