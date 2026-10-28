using System.Collections.Generic;
using Game.Core;

namespace Game.Solver
{
    /// <summary>
    /// Answers "can this board still be won?" by depth-first search over canonical states, trying the children with the
    /// lowest <see cref="SearchContext.Heuristic"/> first. Answers are remembered across calls for one level:
    /// <list type="bullet">
    /// <item>a win found: every state on the current search path is alive;</item>
    /// <item>the search exhausted without a win: every state it visited is dead (everything reachable from them was
    /// visited or already known dead);</item>
    /// <item>the per-call node budget ran out: the answer is null (unknown) and nothing is recorded.</item>
    /// </list>
    /// </summary>
    internal sealed class LivenessOracle
    {
        private struct Child
        {
            public SolverMove Move;
            public int H;
            public int Order;
            public StateKey Key;
        }

        private sealed class Frame
        {
            public Board Board;
            public StateKey Key;
            public List<Child> Children;
            public int Next;
        }

        private readonly SearchContext _context;
        private readonly int _budget;
        private readonly HashSet<StateKey> _alive = new HashSet<StateKey>();
        private readonly HashSet<StateKey> _dead = new HashSet<StateKey>();
        private readonly List<SolverMove> _moves = new List<SolverMove>();

        public LivenessOracle(SearchContext context, int nodeBudget)
        {
            _context = context;
            _budget = nodeBudget;
        }

        public void MarkAlive(StateKey key) => _alive.Add(key);

        public bool? IsAlive(Board board)
        {
            return IsAlive(board, _context.Key(board));
        }

        public bool? IsAlive(Board board, StateKey key)
        {
            if (board.IsWon || _alive.Contains(key)) return true;
            if (_dead.Contains(key)) return false;

            var visited = new HashSet<StateKey> { key };
            var stack = new List<Frame> { new Frame { Board = board, Key = key } };
            int nodes = 1;

            while (stack.Count > 0)
            {
                var top = stack[stack.Count - 1];
                if (top.Children == null)
                {
                    if (Expand(top, visited))
                    {
                        for (int i = 0; i < stack.Count; i++) _alive.Add(stack[i].Key);
                        return true;
                    }
                }

                if (top.Next >= top.Children.Count)
                {
                    stack.RemoveAt(stack.Count - 1);
                    continue;
                }

                var next = top.Children[top.Next++];
                if (visited.Contains(next.Key) || _dead.Contains(next.Key)) continue;
                if (++nodes > _budget) return null;
                visited.Add(next.Key);
                stack.Add(new Frame { Board = SearchContext.Apply(top.Board, next.Move), Key = next.Key });
            }

            foreach (var dead in visited) _dead.Add(dead);
            return false;
        }

        // Fills frame.Children (best first). Returns true if a child is won or already known alive.
        private bool Expand(Frame frame, HashSet<StateKey> visited)
        {
            frame.Children = new List<Child>();
            _context.GenerateMoves(frame.Board, _moves, true);
            for (int m = 0; m < _moves.Count; m++)
            {
                var child = SearchContext.Apply(frame.Board, _moves[m]);
                if (child.IsWon) return true;
                var key = _context.Key(child);
                if (_alive.Contains(key)) return true;
                if (_dead.Contains(key) || visited.Contains(key)) continue;
                if (!child.HasLegalMove())
                {
                    _dead.Add(key);
                    continue;
                }
                frame.Children.Add(new Child { Move = _moves[m], H = _context.Heuristic(child), Order = m, Key = key });
            }
            frame.Children.Sort((a, b) => a.H != b.H ? a.H.CompareTo(b.H) : a.Order.CompareTo(b.Order));
            return false;
        }
    }
}
