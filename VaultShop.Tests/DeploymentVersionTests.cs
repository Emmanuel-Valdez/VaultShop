using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using VaultShop.Utility;
using VaultShop.Web.Areas.Admin.Controllers;
using VaultShop.Web.Services.System;

namespace VaultShop.Web.Tests;

public class DeploymentVersionTests
{
    [Theory]
    [InlineData("abc1234", "abc1234def5678", true)]
    [InlineData("abc1234", "abc1234", true)]
    [InlineData("abc1234", "xyz9999", false)]
    [InlineData("unknown", "abc1234", false)]
    [InlineData("", "abc1234", false)]
    [InlineData("abc1234", "", false)]
    [InlineData("abc1234", null, false)]
    public void IsUpToDate_PrefixMatch(string deployed, string? latest, bool expected)
    {
        Assert.Equal(expected, DeploymentVersionService.IsUpToDate(deployed, latest));
    }

    [Fact]
    public void IsUpToDate_CaseInsensitive()
    {
        Assert.True(DeploymentVersionService.IsUpToDate("ABC1234", "abc1234def"));
    }

    [Fact]
    public void SystemController_RequiresAdminRole()
    {
        var attr = typeof(SystemController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Contains(SD.Role_Admin, attr!.Roles!);
        Assert.Equal("Admin", typeof(SystemController).GetCustomAttribute<Microsoft.AspNetCore.Mvc.AreaAttribute>()!.RouteValue);
    }
}
