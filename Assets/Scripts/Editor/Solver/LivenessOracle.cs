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
    /// A state is expanded a little at a time: as soon as one of its children looks better (lower heuristic), the search
    /// moves there and comes back for the remaining children only if it has to. Boards known to be alive are also
    /// recognised by the cheap exact key the moment they are generated.
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
        // Alive by canonical key (states on lines the searches found) and by cheap exact key (those, plus boards
        // passed to MarkAlive: the simulated player's won plays and the shortest solution).
        private readonly HashSet<StateKey> _alive = new HashSet<StateKey>();
        private readonly HashSet<StateKey> _aliveCheap = new HashSet<StateKey>();
        private readonly HashSet<StateKey> _dead = new HashSet<StateKey>();
        private readonly List<SolverMove> _moves = new List<SolverMove>();
        private readonly List<SolverMove> _replay = new List<SolverMove>();
        // Per node of the current search: canonical key (set when popped), cheap key, heuristic, next move to try.
        private readonly List<StateKey> _keys = new List<StateKey>();
        private readonly List<StateKey> _cheapKeys = new List<StateKey>();
        private readonly List<int> _heuristic = new List<int>();
        private readonly List<int> _nextMove = new List<int>();

        /// <param name="nodeBudget">States one search may add before answering unknown.</param>
        /// <param name="levelBudget">The level's shared budget; null for none.</param>
        public LivenessOracle(SearchContext context, int nodeBudget, SearchBudget levelBudget)
        {
            _context = context;
            _budget = nodeBudget;
            _levelBudget = levelBudget ?? new SearchBudget(long.MaxValue, double.MaxValue);
            _scratch = context.Root.Clone();
        }

        /// <summary>Records that <paramref name="board"/> is alive (a win was reached from it).</summary>
        public void MarkAlive(Board board)
        {
            if (!board.IsWon) _aliveCheap.Add(_context.CheapKey(board));
        }

        /// <summary>
        /// True if a win is still reachable, false if not, null if a budget ran out first. The board must descend from
        /// the context's root.
        /// </summary>
        public bool? IsAlive(Board board)
        {
            if (board.IsWon) return true;
            var rootCheap = _context.CheapKey(board);
            if (_aliveCheap.Contains(rootCheap)) return true;
            var key = _context.Key(board);
            if (_alive.Contains(key)) return true;
            if (_dead.Contains(key)) return false;
            if (_levelBudget.Exhausted) return null;
            if (!board.HasLegalMove())
            {
                _dead.Add(key);
                return false;
            }
            int rootH = _context.HeuristicOfLastRead();

            // Children are filtered by the cheap exact key when they are generated and looked up by the canonical
            // key (memo and dead marking) when they are first popped, so the relabelling runs once per expanded state.
            var seen = new HashSet<StateKey> { rootCheap };
            var expanded = new HashSet<StateKey>();
            var tree = new SearchTree();
            var cache = new BoardCache(256);
            var open = new MinHeap();
            _keys.Clear();
            _cheapKeys.Clear();
            _heuristic.Clear();
            _nextMove.Clear();
            AddNode(tree, -1, default, 0, key, rootCheap, rootH);
            open.Push(rootH, 0, 0);

            while (open.Count > 0)
            {
                int index = open.Pop();
                var current = cache.Get(board, tree, index, _replay);
                int first = _nextMove[index];
                if (first == 0)
                {
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
                }

                int depth = tree.Depth[index];
                int h = _heuristic[index];
                _context.GenerateMoves(current, _moves, true);
                int m = first;
                while (m < _moves.Count)
                {
                    if (!_levelBudget.Charge()) return null;
                    var move = _moves[m++];
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
                    var cheap = _context.CheapKey(_scratch);
                    if (_aliveCheap.Contains(cheap))
                    {
                        MarkLineAlive(tree, index);
                        return true;
                    }
                    if (!seen.Add(cheap)) continue;
                    if (!_scratch.HasLegalMove()) continue; // stuck: dead, nothing to explore
                    if (tree.Count >= _budget) return null;
                    int childH = _context.HeuristicOfLastRead();
                    int child = AddNode(tree, index, move, depth + 1, default, cheap, childH);
                    open.Push(childH, depth + 1, child);
                    if (childH < h) break; // a better state: go there first
                }
                if (m < _moves.Count)
                {
                    // Come back for the remaining children if the search returns here.
                    _nextMove[index] = m;
                    open.Push(h, depth, index);
                }
            }

            // Everything reachable from the start was explored (every expanded state to its last child) without a win.
            foreach (var dead in expanded) _dead.Add(dead);
            return false;
        }

        private int AddNode(SearchTree tree, int parent, SolverMove move, int depth, StateKey key, StateKey cheap, int h)
        {
            int index = tree.Add(parent, move, depth);
            _keys.Add(key);
            _cheapKeys.Add(cheap);
            _heuristic.Add(h);
            _nextMove.Add(0);
            return index;
        }

        // The node at `index` and its ancestors are alive; all of them were popped, so their canonical keys are set.
        private void MarkLineAlive(SearchTree tree, int index)
        {
            for (int i = index; i >= 0; i = tree.Parent[i])
            {
                _alive.Add(_keys[i]);
                _aliveCheap.Add(_cheapKeys[i]);
            }
        }
    }
}
