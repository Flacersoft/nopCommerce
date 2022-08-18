using System;
using FluentAssertions;
using Nop.Core.Caching;
using Nop.Services.Caching;
using NUnit.Framework;

namespace Nop.Tests.Nop.Core.Tests.Caching
{
    [TestFixture]
    public class LockerTests : BaseNopTest
    {
        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void CanPerformLock(bool isDistributed)
        {
            var staticCacheManager = isDistributed ? GetService<MemoryDistributedCacheManager>() : GetService<IStaticCacheManager>();
            staticCacheManager.Should().NotBeNull();
            var locker = staticCacheManager as ILocker;
            locker.Should().NotBeNull();

            var key = new CacheKey("Nop.Task");
            var expiration = TimeSpan.FromMinutes(2);

            var actionCount = 0;
            var action = new Action(() =>
            {
                var isSet = staticCacheManager.GetAsync<object>(key, () => null);
                isSet.Should().NotBeNull();

                locker.PerformActionWithLock(key.Key, expiration,
                    () => Assert.Fail("Action in progress"))
                    .Should().BeFalse();

                if (++actionCount % 2 == 0)
                    throw new ApplicationException("Alternating actions fail");
            });

            locker.PerformActionWithLock(key.Key, expiration, action)
                .Should().BeTrue();
            actionCount.Should().Be(1);

            Assert.Throws<ApplicationException>(() =>
                locker.PerformActionWithLock(key.Key, expiration, action));

            actionCount.Should().Be(2);

            locker.PerformActionWithLock(key.Key, expiration, action)
                .Should().BeTrue();
            actionCount.Should().Be(3);

            var dt = DateTime.Now;

            locker.PerformActionWithLock("action_with_lock", TimeSpan.FromSeconds(1), () =>
            {
                while (!locker.PerformActionWithLock("action_with_lock", TimeSpan.FromSeconds(1), () => { }))
                {
                }
            });

            var span = DateTime.Now - dt;

            span.Seconds.Should().Be(1);
        }
    }
}
