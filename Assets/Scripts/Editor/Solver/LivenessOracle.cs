using System.Collections.Generic;
using Game.Core;

namespace Game.Solver
{
    /// <summary>
    /// Answers "can this board still be won?" by greedy best-first search over canonical states: the open state with
    /// the lowest <see cref="SearchContext.Heuristic"/> is expanded next (deepest first on ties, then oldest), so a
    /// search that wanders into a large lost region can still leave it for a better one. Answers are remembered across
    /// calls for one level:
    /// <list type="bullet">
    /// <item>a win found: every state on the line from the start to the win is alive;</item>
    /// <item>the search exhausted without a win: every state it visited is dead (everything reachable from them was
    /// visited or already known dead);</item>
    /// <item>the per-call node budget or the level's shared <see cref="SearchBudget"/> ran out: the answer is null
    /// (unknown) and nothing is recorded.</item>
    /// </list>
    /// The answers are facts about the board, so the search order only changes how fast they come, never what they are.
    /// States are identified by the canonical <see cref="SearchContext.Key"/>, so answers carry over between boards
    /// that differ only in shelf order or type names. Every move is applied by the Core <see cref="Board"/>.
    /// </summary>
    internal sealed class LivenessOracle
    {
        private readonly SearchContext _context;
        private readonly int _budget;
        private readonly SearchBudget _levelBudget;
        private readonly Board _scratch;
        private readonly HashSet<StateKey> _alive = new HashSet<StateKey>();
        private readonly HashSet<StateKey> _dead = new HashSet<StateKey>();
        private readonly List<SolverMove> _moves = new List<SolverMove>();
        private readonly List<SolverMove> _replay = new List<SolverMove>();
        private readonly List<StateKey> _keys = new List<StateKey>();

        /// <param name="nodeBudget">States one search may add before answering unknown.</param>
        /// <param name="levelBudget">The level's shared budget; null for none.</param>
        public LivenessOracle(SearchContext context, int nodeBudget, SearchBudget levelBudget)
        {
            _context = context;
            _budget = nodeBudget;
            _levelBudget = levelBudget ?? new SearchBudget(long.MaxValue, double.MaxValue);
            _scratch = context.Root.Clone();
        }

        public void MarkAlive(Board board)
        {
            if (!board.IsWon) _alive.Add(_context.Key(board));
        }

        /// <summary>
        /// True if a win is still reachable, false if not, null if a budget ran out first. The board must descend from
        /// the context's root.
        /// </summary>
        public bool? IsAlive(Board board)
        {
            if (board.IsWon) return true;
            var key = _context.Key(board);
            if (_alive.Contains(key)) return true;
            if (_dead.Contains(key)) return false;
            if (_levelBudget.Exhausted) return null;
            if (!board.HasLegalMove())
            {
                _dead.Add(key);
                return false;
            }

            // Children are filtered by the cheap exact key when they are generated and looked up by the canonical
            // key (memo and dead marking) when they are expanded, so the relabelling runs once per expanded state.
            var seen = new HashSet<StateKey> { _context.CheapKey(board) };
            int rootH = _context.HeuristicOfLastRead();
            var expanded = new HashSet<StateKey>();
            var tree = new SearchTree();
            var cache = new BoardCache(256);
            var open = new MinHeap();
            _keys.Clear();
            tree.Add(-1, default, 0);
            _keys.Add(key);
            open.Push(rootH, 0, 0);

            while (open.Count > 0)
            {
                int index = open.Pop();
                var current = cache.Get(board, tree, index, _replay);
                if (index > 0)
                {
                    var currentKey = _context.Key(current);
                    if (_alive.Contains(currentKey))
                    {
                        MarkLineAlive(tree, tree.Parent[index]);
                        return true;
                    }
                    if (_dead.Contains(currentKey)) continue;
                    _keys[index] = currentKey;
                }
                if (!expanded.Add(_keys[index])) continue; // an equivalent state was expanded already

                int depth = tree.Depth[index];
                _context.GenerateMoves(current, _moves, true);
                for (int m = 0; m < _moves.Count; m++)
                {
                    if (!_levelBudget.Charge()) return null;
                    var move = _moves[m];
                    _scratch.CopyStateFrom(current);
                    if (!_scratch.TryMove(move.FromShelf, move.FromSlot, move.ToShelf, out var result))
                    {
                        throw new System.InvalidOperationException($"Solver tried an illegal move {move}: {result}.");
                    }
                    if (_scratch.IsWon)
                    {
                        MarkLineAlive(tree, index);
                        return true;
                    }
                    if (!seen.Add(_context.CheapKey(_scratch))) continue;
                    if (!_scratch.HasLegalMove()) continue; // stuck: dead, nothing to explore
                    if (tree.Count >= _budget) return null;
                    int h = _context.HeuristicOfLastRead();
                    int child = tree.Add(index, move, depth + 1);
                    _keys.Add(default);
                    open.Push(h, depth + 1, child);
                }
            }

            // Everything reachable from the start was explored without a win.
            foreach (var dead in expanded) _dead.Add(dead);
            return false;
        }

        private void MarkLineAlive(SearchTree tree, int index)
        {
            for (int i = index; i >= 0; i = tree.Parent[i]) _alive.Add(_keys[i]);
        }
    }
}
