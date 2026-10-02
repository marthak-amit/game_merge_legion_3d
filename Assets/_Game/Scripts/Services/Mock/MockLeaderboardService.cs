using System;
using System.Collections.Generic;

namespace MergeLegion.Services.Mock
{
    /// <summary>In-memory boards seeded with fake rivals so the UI has something to show.</summary>
    public sealed class MockLeaderboardService : ILeaderboardService
    {
        private readonly IAuthService _auth;
        private readonly Dictionary<string, List<LeaderboardEntry>> _boards = new Dictionary<string, List<LeaderboardEntry>>();

        public MockLeaderboardService(IAuthService auth) { _auth = auth; }

        public void SubmitScore(string boardId, long score, Action<bool> onComplete)
        {
            var board = GetBoard(boardId);
            var me = board.Find(e => e.PlayerId == _auth.PlayerId);
            if (me == null)
            {
                me = new LeaderboardEntry { PlayerId = _auth.PlayerId, DisplayName = _auth.DisplayName };
                board.Add(me);
            }
            if (score > me.Score) me.Score = score;
            Rerank(board);
            onComplete?.Invoke(true);
        }

        public void GetTop(string boardId, int count, Action<IReadOnlyList<LeaderboardEntry>> onComplete)
        {
            var board = GetBoard(boardId);
            int n = Math.Min(count, board.Count);
            onComplete?.Invoke(board.GetRange(0, n));
        }

        public void GetPlayerEntry(string boardId, Action<LeaderboardEntry> onComplete)
        {
            onComplete?.Invoke(GetBoard(boardId).Find(e => e.PlayerId == _auth.PlayerId));
        }

        private List<LeaderboardEntry> GetBoard(string id)
        {
            if (_boards.TryGetValue(id, out var b)) return b;
            b = new List<LeaderboardEntry>();
            for (int i = 1; i <= 20; i++)
                b.Add(new LeaderboardEntry { PlayerId = "bot" + i, DisplayName = "Rival " + i, Score = 300 - i * 12 });
            Rerank(b);
            _boards[id] = b;
            return b;
        }

        private static void Rerank(List<LeaderboardEntry> board)
        {
            board.Sort((a, b) => b.Score.CompareTo(a.Score));
            for (int i = 0; i < board.Count; i++) board[i].Rank = i + 1;
        }
    }
}
