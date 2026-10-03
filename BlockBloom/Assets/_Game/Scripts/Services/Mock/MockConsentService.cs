using System;

namespace BlockBloom.Services.Mock
{
    public sealed class MockConsentService : IConsentService
    {
        public ConsentStatus Status { get; private set; } = ConsentStatus.Unknown;

        public void RequestConsent(Action<ConsentStatus> onComplete)
        {
            Status = ConsentStatus.Granted;
            onComplete?.Invoke(Status);
        }

        public void RequestTracking(Action<bool> onComplete) => onComplete?.Invoke(true);
    }
}
