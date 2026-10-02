using MergeLegion.Core;
using NUnit.Framework;

namespace MergeLegion.Tests
{
    public class EventBusTests
    {
        private readonly struct Ping { public readonly int Value; public Ping(int v) { Value = v; } }

        [SetUp] public void SetUp() => EventBus.ClearAll();
        [TearDown] public void TearDown() => EventBus.ClearAll();

        [Test]
        public void Publish_DeliversToSubscriber()
        {
            int got = 0;
            EventBus.Subscribe<Ping>(p => got = p.Value);
            EventBus.Publish(new Ping(7));
            Assert.AreEqual(7, got);
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            int calls = 0;
            System.Action<Ping> h = _ => calls++;
            EventBus.Subscribe(h);
            EventBus.Publish(new Ping(1));
            EventBus.Unsubscribe(h);
            EventBus.Publish(new Ping(1));
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void Subscribe_SameHandlerTwice_FiresOnce()
        {
            int calls = 0;
            System.Action<Ping> h = _ => calls++;
            EventBus.Subscribe(h);
            EventBus.Subscribe(h);
            EventBus.Publish(new Ping(1));
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void Unsubscribe_DuringPublish_IsSafe()
        {
            int a = 0, b = 0;
            System.Action<Ping> ha = null;
            ha = _ => { a++; EventBus.Unsubscribe(ha); };
            System.Action<Ping> hb = _ => b++;
            EventBus.Subscribe(ha);
            EventBus.Subscribe(hb);

            EventBus.Publish(new Ping(1));
            EventBus.Publish(new Ping(1));

            Assert.AreEqual(1, a);
            Assert.AreEqual(2, b);
        }

        [Test]
        public void Subscribe_DuringPublish_FiresFromNextPublish()
        {
            int late = 0;
            System.Action<Ping> lateHandler = _ => late++;
            EventBus.Subscribe<Ping>(_ => EventBus.Subscribe(lateHandler));

            EventBus.Publish(new Ping(1));
            Assert.AreEqual(0, late);
            EventBus.Publish(new Ping(1));
            Assert.AreEqual(1, late);
        }

        [Test]
        public void ThrowingHandler_DoesNotBlockOthers()
        {
            int calls = 0;
            EventBus.Subscribe<Ping>(_ => throw new System.InvalidOperationException("boom"));
            EventBus.Subscribe<Ping>(_ => calls++);
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Exception, new System.Text.RegularExpressions.Regex("boom"));
            EventBus.Publish(new Ping(1));
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void ClearAll_RemovesEverySubscriber()
        {
            int calls = 0;
            EventBus.Subscribe<Ping>(_ => calls++);
            EventBus.ClearAll();
            EventBus.Publish(new Ping(1));
            Assert.AreEqual(0, calls);
        }
    }
}
