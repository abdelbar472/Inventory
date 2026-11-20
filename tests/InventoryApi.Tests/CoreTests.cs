using System.ComponentModel.DataAnnotations;
using System.Reflection;
using InventoryApi.Models.Dtos;
using InventoryApi.Services;

namespace InventoryApi.Tests;

public class PasswordHelperTests
{
    [Fact]
    public void Hash_And_Verify_RoundTrips()
    {
        var hash = PasswordHelper.Hash("Strong123");

        Assert.NotEqual("Strong123", hash);
        Assert.True(PasswordHelper.Verify("Strong123", hash));
        Assert.False(PasswordHelper.Verify("wrong-password", hash));
    }

    [Fact]
    public void Same_Password_Gets_Different_Salt_Each_Time()
    {
        var a = PasswordHelper.Hash("Strong123");
        var b = PasswordHelper.Hash("Strong123");

        Assert.NotEqual(a, b);
        Assert.True(PasswordHelper.Verify("Strong123", a));
        Assert.True(PasswordHelper.Verify("Strong123", b));
    }

    [Fact]
    public void Malformed_Hash_Returns_False()
    {
        Assert.False(PasswordHelper.Verify("anything", "not-a-valid-hash"));
    }
}

public class DtoValidationTests
{
    // Positional records put validation attributes on CONSTRUCTOR PARAMETERS,
    // which Validator.TryValidateObject ignores. ASP.NET reads them correctly;
    // this helper inspects the same parameter metadata for unit tests.
    private static bool IsValid(object dto)
    {
        var type = dto.GetType();
        var constructor = type.GetConstructors().Single();

        foreach (var parameter in constructor.GetParameters())
        {
            var property = type.GetProperty(parameter.Name!,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            var value = property?.GetValue(dto);

            foreach (var attribute in parameter.GetCustomAttributes<ValidationAttribute>(true))
                if (!attribute.IsValid(value)) return false;
        }
        return true;
    }

    [Fact]
    public void Register_Rejects_Weak_Password()
    {
        Assert.False(IsValid(new RegisterRequest("a@b.com", "abc", "A")));
        Assert.True(IsValid(new RegisterRequest("a@b.com", "Strong123", "A")));
    }

    [Fact]
    public void CreateProduct_Rejects_Negative_Price()
    {
        Assert.False(IsValid(new CreateProductRequest("Bad", "BAD", -1m, 1, 1)));
        Assert.True(IsValid(new CreateProductRequest("Good", "GOOD", 5m, 1, 1)));
    }

    [Fact]
    public void CreateOrder_Requires_At_Least_One_Item()
    {
        Assert.False(IsValid(new CreateOrderRequest([])));
    }
}
