using System;
using NUnit.Framework;

namespace TaskSwitcher.Core.UnitTests
{
    [TestFixture]
    public class WindowCacheIdentityTests
    {
        [Test]
        public void CacheKeyDiffersWhenProcessIdDiffers()
        {
            WindowCacheIdentity first = new(new IntPtr(42), 100, 1_000);
            WindowCacheIdentity second = new(new IntPtr(42), 101, 1_000);

            Assert.That(first.BuildCacheKey("Icon-"), Is.Not.EqualTo(second.BuildCacheKey("Icon-")));
        }

        [Test]
        public void CacheKeyDiffersWhenProcessStartTimeDiffers()
        {
            WindowCacheIdentity first = new(new IntPtr(42), 100, 1_000);
            WindowCacheIdentity second = new(new IntPtr(42), 100, 2_000);

            Assert.That(first.BuildCacheKey("Icon-"), Is.Not.EqualTo(second.BuildCacheKey("Icon-")));
        }

        [Test]
        public void IconCacheKeyIncludesWindowLifetimeAndIconSize()
        {
            WindowCacheIdentity identity = new(new IntPtr(42), 100, 1_000);

            string smallKey = IconCacheService.BuildCacheKey("Icon-", identity, WindowIconSize.Small);
            string largeKey = IconCacheService.BuildCacheKey("Icon-", identity, WindowIconSize.Large);

            Assert.Multiple(() =>
            {
                Assert.That(smallKey, Does.Contain("42-100-1000"));
                Assert.That(smallKey, Is.Not.EqualTo(largeKey));
            });
        }
    }
}
