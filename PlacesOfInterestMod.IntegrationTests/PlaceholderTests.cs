using System;
using Xunit;
using FluentAssertions;

namespace PlacesOfInterestMod.IntegrationTests
{
    public class PlaceholderTests
    {
        [Fact]
        public void PlaceholderTest_Succeeds()
        {
            // Arrange
            int a = 1;
            int b = 2;

            // Act
            int sum = a + b;

            // Assert
            sum.Should().Be(3);
        }
    }
}
