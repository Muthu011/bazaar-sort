using System.Collections.Concurrent;
using System.Collections.Generic;
using Game.Core;

namespace Game.Solver
{
    /// <summary>
    /// What the dead-end searches of one level have proven, shared by the oracles of all play threads (issue #38): each
    /// fact is about the board, never about the thread that found it, so one thread's answer spares the others the same
    /// search. Safe to read and add from several threads.
    /// </summary>
    internal sealed class LivenessFacts
    {
        // Alive by canonical key (states on lines the searches found) and by cheap exact key (those, plus boards passed
        // to MarkAlive: the simulated player's won plays and the shortest solution); dead by canonical key.
        private readonly ConcurrentDictionary<StateKey, byte> _alive = new ConcurrentDictionary<StateKey, byte>();
        private readonly ConcurrentDictionary<StateKey, byte> _aliveCheap = new ConcurrentDictionary<StateKey, byte>();
        private readonly ConcurrentDictionary<StateKey, byte> _dead = new ConcurrentDictionary<StateKey, byte>();

        public bool IsAlive(StateKey key) => _alive.ContainsKey(key);
        public bool IsAliveCheap(StateKey cheap) => _aliveCheap.ContainsKey(cheap);
        public bool IsDead(StateKey key) => _dead.ContainsKey(key);
        public void AddAlive(StateKey key, StateKey cheap)
        {
            _alive.TryAdd(key, 0);
            _aliveCheap.TryAdd(cheap, 0);
        }
        public void AddAliveCheap(StateKey cheap) => _aliveCheap.TryAdd(cheap, 0);
        public void AddDead(StateKey key) => _dead.TryAdd(key, 0);
    }

    /// <summary>
    /// Answers "can this board still be won?" by greedy best-first search over canonical states: the open state with
    /// the lowest <see cref="SearchContext.Heuristic"/> is expanded next (deepest first on ties, then oldest), so a
    /// search that wanders into a large lost region can still leave it for a better one. Answers are remembered across
    /// calls for one level, and shared with the oracles of the other play threads (<see cref="LivenessFacts"/>):
    /// <list type="bullet">
    /// <item>a win found: every state on the line from the start to the win is alive;</item>
    /// <item>the search exhausted without a win: every state it visited is dead (everything reachable from them was
    /// visited or already known dead);</item>
    /// <item>the per-call node budget or the level's shared <see cref="SearchBudget"/> ran out: the answer is null
    /// (unknown) and nothing is recorded.</item>
    /// </list>
    /// A state is expanded a little at a time: as soon as one of its children looks better (lower heuristic), the search
    /// moves there and comes back for the remaining children only if it has to. Boards known to be alive are also
    /// recognised by the cheap exact key the moment they are generated. A child reached by a plain move (no clear, no
    /// reveal) is worked out from its parent (<see cref="SearchContext.PredictChildHeuristic"/>) instead of built; its
    /// board is built by the Core rules only if it is expanded, and checked against the prediction then.
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
        private readonly LivenessFacts _facts;
        private readonly List<SolverMove> _moves = new List<SolverMove>();
        private readonly List<SolverMove> _replay = new List<SolverMove>();
        // Per node of the current search: canonical key (set when popped), cheap key, heuristic, next move to try.
        private readonly List<StateKey> _keys = new List<StateKey>();
        private readonly List<StateKey> _cheapKeys = new List<StateKey>();
        private readonly List<int> _heuristic = new List<int>();
        private readonly List<int> _nextMove = new List<int>();
        // Per node: worked out from its parent instead of built (see CheckPrediction).
        private readonly List<bool> _predictedNode = new List<bool>();
        // Boards of recently expanded nodes, reused by every search of this oracle (one search's tree at a time).
        private readonly BoardCache _cache = new BoardCache(2048);

        /// <param name="nodeBudget">States one search may add before answering unknown.</param>
        /// <param name="levelBudget">The level's shared budget; null for none.</param>
        /// <param name="facts">Answers shared with the other oracles of the level (all on the same puzzle, contexts made
        /// by <see cref="SearchContext.CreateSibling"/>); null for this oracle's own.</param>
        public LivenessOracle(SearchContext context, int nodeBudget, SearchBudget levelBudget, LivenessFacts facts = null)
        {
            _facts = facts ?? new LivenessFacts();
            _context = context;
            _budget = nodeBudget;
            _levelBudget = levelBudget ?? new SearchBudget(long.MaxValue, double.MaxValue);
            _scratch = context.Root.Clone();
        }

        /// <summary>Records that <paramref name="board"/> is alive (a win was reached from it).</summary>
        public void MarkAlive(Board board)
        {
            if (!board.IsWon) _facts.AddAliveCheap(_context.CheapKey(board));
        }

        /// <summary>
        /// True if a win is still reachable, false if not, null if a budget ran out first. The board must descend from
        /// the context's root.
        /// </summary>
        public bool? IsAlive(Board board)
        {
            if (board.IsWon) return true;
            var rootCheap = _context.CheapKey(board);
            if (_facts.IsAliveCheap(rootCheap)) return true;
            var key = _context.Key(board);
            if (_facts.IsAlive(key)) return true;
            if (_facts.IsDead(key)) return false;
            if (_levelBudget.Exhausted) return null;
            if (!board.HasLegalMove())
            {
                _facts.AddDead(key);
                return false;
            }
            int rootH = _context.HeuristicOfLastRead();

            // Children are filtered by the cheap exact key when they are generated and looked up by the canonical
            // key (memo and dead marking) when they are first popped, so the relabelling runs once per expanded state.
            var seen = new HashSet<StateKey> { rootCheap };
            var expanded = new HashSet<StateKey>();
            var tree = new SearchTree();
            var cache = _cache;
            cache.Clear();
            var open = new MinHeap();
            _keys.Clear();
            _cheapKeys.Clear();
            _heuristic.Clear();
            _nextMove.Clear();
            _predictedNode.Clear();
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
                        if (_predictedNode[index]) CheckPrediction(current, index);
                        var currentKey = _context.Key(current);
                        if (_facts.IsAlive(currentKey))
                        {
                            MarkLineAlive(tree, tree.Parent[index]);
                            return true;
                        }
                        if (_facts.IsDead(currentKey)) continue;
                        _keys[index] = currentKey;
                    }
                    if (!expanded.Add(_keys[index])) continue; // an equivalent state was expanded already
                }

                int depth = tree.Depth[index];
                int h = _heuristic[index];
                _context.BeginChildren(current);
                _context.GenerateMoves(current, _moves, true);
                int m = first;
                while (m < _moves.Count)
                {
                    if (!_levelBudget.Charge()) return null;
                    var move = _moves[m++];
                    // A move that only takes an item from one front row to another is worked out from the parent and
                    // not built (its board is rebuilt by the Core rules if it is ever expanded, and checked then); any
                    // other move is made on the scratch board.
                    int childH = _context.PredictChildHeuristic(move);
                    bool predicted = childH >= 0;
                    StateKey cheap;
                    if (predicted)
                    {
                        cheap = _context.ChildCheapKey();
                    }
                    else
                    {
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
                        // Scored from the parent's tallies; a clear changes more than two shelves, so then read it all.
                        childH = _context.ChildHeuristic(_scratch, move);
                        if (childH >= 0)
                        {
                            cheap = _context.ChildCheapKey();
                        }
                        else
                        {
                            cheap = _context.CheapKey(_scratch);
                            childH = _context.HeuristicOfLastRead();
                        }
                    }
                    if (_facts.IsAliveCheap(cheap))
                    {
                        MarkLineAlive(tree, index);
                        return true;
                    }
                    if (!seen.Add(cheap)) continue;
                    bool legal = predicted ? _context.PredictedChildHasLegalMove() : _scratch.HasLegalMove();
                    if (!legal) continue; // stuck: dead, nothing to explore
                    if (tree.Count >= _budget) return null;
                    int child = AddNode(tree, index, move, depth + 1, default, cheap, childH);
                    _predictedNode[child] = predicted;
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
            foreach (var dead in expanded) _facts.AddDead(dead);
            return false;
        }

        // A node whose board was worked out rather than built: its board, rebuilt by the Core rules, must match.
        private void CheckPrediction(Board board, int index)
        {
            var cheap = _context.CheapKey(board);
            int h = _context.HeuristicOfLastRead();
            if (!cheap.Equals(_cheapKeys[index]) || h != _heuristic[index] || !board.HasLegalMove())
            {
                throw new System.InvalidOperationException(
                    $"Solver predicted a board the Core rules do not make (dead-end search node {index}).");
            }
        }

        private int AddNode(SearchTree tree, int parent, SolverMove move, int depth, StateKey key, StateKey cheap, int h)
        {
            int index = tree.Add(parent, move, depth);
            _keys.Add(key);
            _cheapKeys.Add(cheap);
            _heuristic.Add(h);
            _nextMove.Add(0);
            _predictedNode.Add(false);
            return index;
        }

        // The node at `index` and its ancestors are alive; all of them were popped, so their canonical keys are set.
        private void MarkLineAlive(SearchTree tree, int index)
        {
            for (int i = index; i >= 0; i = tree.Parent[i])
            {
                _facts.AddAlive(_keys[i], _cheapKeys[i]);
            }
        }
    }
}
