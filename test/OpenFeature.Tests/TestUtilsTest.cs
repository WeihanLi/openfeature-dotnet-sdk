using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace OpenFeature.Tests
{
    [SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task")]
    public class TestUtilsTest
    {
        [Fact]
        public async void Should_Fail_If_Assertion_Fails()
        {
            await Assert.ThrowsAnyAsync<Exception>(() => Utils.AssertUntilAsync(_ => Assert.True(1.Equals(2)), 100, 10));
        }

        [Fact]
        public async void Should_Pass_If_Assertion_Fails()
        {
            await Utils.AssertUntilAsync(_ => Assert.True(1.Equals(1)));
        }
    }
}
