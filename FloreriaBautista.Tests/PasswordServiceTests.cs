using FloreriaBautista.Services;
using Xunit;

namespace FloreriaBautista.Tests;

public class PasswordServiceTests
{
    private readonly PasswordService _service;

    public PasswordServiceTests()
    {
        _service = new PasswordService();
    }

    [Fact]
    public void HashPassword_ShouldReturnHashedString()
    {
        // Arrange
        var password = "TestPassword123";

        // Act
        var result = _service.HashPassword(password);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.NotEqual(password, result);
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        var password = "SecurePassword";
        var hash = _service.HashPassword(password);

        // Act
        var result = _service.VerifyPassword(password, hash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ShouldReturnFalse()
    {
        // Arrange
        var password = "SecurePassword";
        var hash = _service.HashPassword(password);
        var wrongPassword = "WrongPassword";

        // Act
        var result = _service.VerifyPassword(wrongPassword, hash);

        // Assert
        Assert.False(result);
    }
}
