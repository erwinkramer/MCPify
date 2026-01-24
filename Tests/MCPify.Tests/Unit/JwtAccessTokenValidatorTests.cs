using System.Linq;
using System.Threading.Tasks;
using MCPify.Core.Auth;

namespace MCPify.Tests.Unit;

public class JwtAccessTokenValidatorTests
{
    [Fact]
    public void OAuthConfigurationStore_ReturnsCopyOfConfigurations()
    {
        var store = new OAuthConfigurationStore();
        var config = new OAuth2Configuration { AuthorizationUrl = "https://auth.example.com" };

        store.AddConfiguration(config);
        var firstSnapshot = store.GetConfigurations().ToList();
        var secondSnapshot = store.GetConfigurations().ToList();

        Assert.Single(firstSnapshot);
        Assert.Single(secondSnapshot);
        Assert.NotSame(firstSnapshot, secondSnapshot);
        firstSnapshot.Clear();
        var thirdSnapshot = store.GetConfigurations().ToList();
        Assert.Single(thirdSnapshot);
        Assert.Same(config, thirdSnapshot[0]);
    }
}
