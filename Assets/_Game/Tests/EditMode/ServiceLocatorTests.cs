using System;
using MergeLegion.Core;
using NUnit.Framework;

namespace MergeLegion.Tests
{
    public class ServiceLocatorTests
    {
        private interface IFoo { }
        private sealed class Foo : IFoo { }

        [SetUp] public void SetUp() => ServiceLocator.Clear();
        [TearDown] public void TearDown() => ServiceLocator.Clear();

        [Test]
        public void Register_ThenGet_ReturnsSameInstance()
        {
            var foo = new Foo();
            ServiceLocator.Register<IFoo>(foo);
            Assert.AreSame(foo, ServiceLocator.Get<IFoo>());
        }

        [Test]
        public void Get_Unregistered_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => ServiceLocator.Get<IFoo>());
        }

        [Test]
        public void TryGet_Unregistered_ReturnsFalse()
        {
            Assert.IsFalse(ServiceLocator.TryGet<IFoo>(out var s));
            Assert.IsNull(s);
        }

        [Test]
        public void Register_Twice_ReplacesService()
        {
            var a = new Foo();
            var b = new Foo();
            ServiceLocator.Register<IFoo>(a);
            ServiceLocator.Register<IFoo>(b);
            Assert.AreSame(b, ServiceLocator.Get<IFoo>());
        }

        [Test]
        public void Register_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ServiceLocator.Register<IFoo>(null));
        }
    }
}
