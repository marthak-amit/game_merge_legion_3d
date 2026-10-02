using System;

namespace MergeLegion.Services.Mock
{
    public sealed class MockAuthService : IAuthService
    {
        private readonly string _playerId;

        public bool IsSignedIn { get; private set; }
        public string PlayerId => IsSignedIn ? _playerId : null;
        public string DisplayName { get; set; } = "Commander";

        /// <param name="playerId">Stable id, normally the save's playerId so mock and local data agree.</param>
        public MockAuthService(string playerId) { _playerId = playerId; }

        public void SignInAnonymously(Action<bool> onComplete)
        {
            IsSignedIn = true;
            onComplete?.Invoke(true);
        }

        public void DeleteAccount(Action<bool> onComplete)
        {
            IsSignedIn = false;
            onComplete?.Invoke(true);
        }
    }
}
