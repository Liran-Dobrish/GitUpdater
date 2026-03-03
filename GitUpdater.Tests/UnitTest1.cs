using GitUpdater.DM;
using GitUpdater.GitProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GitUpdater.Tests;

#region Testable wrappers to expose protected InjectToken

public class TestableGenericGitProvider : GenericGitProvider
{
    public TestableGenericGitProvider() : base(NullLogger<GenericGitProvider>.Instance) { }
    public string TestInjectToken(string repoUrl, string token) => InjectToken(repoUrl, token);
}

public class TestableGitHubGitProvider : GitHubGitProvider
{
    public TestableGitHubGitProvider() : base(NullLogger<GitHubGitProvider>.Instance) { }
    public string TestInjectToken(string repoUrl, string token) => InjectToken(repoUrl, token);
}

public class TestableGitLabGitProvider : GitLabGitProvider
{
    public TestableGitLabGitProvider() : base(NullLogger<GitLabGitProvider>.Instance) { }
    public string TestInjectToken(string repoUrl, string token) => InjectToken(repoUrl, token);
}

public class TestableBitbucketGitProvider : BitbucketGitProvider
{
    public TestableBitbucketGitProvider() : base(NullLogger<BitbucketGitProvider>.Instance) { }
    public string TestInjectToken(string repoUrl, string token) => InjectToken(repoUrl, token);
}

public class TestableAzureReposGitProvider : AzureReposGitProvider
{
    public TestableAzureReposGitProvider() : base(NullLogger<AzureReposGitProvider>.Instance) { }
    public string TestInjectToken(string repoUrl, string token) => InjectToken(repoUrl, token);
}

#endregion

#region GenericGitProvider Tests

public class GenericGitProviderTests
{
    private readonly TestableGenericGitProvider _provider = new();

    [Fact]
    public void RepoType_ReturnsGeneric()
    {
        Assert.Equal(RepoType.Generic, ((IGitProvider)_provider).RepoType);
    }

    [Fact]
    public void InjectToken_WithToken_ReturnsAuthenticatedUrl()
    {
        var result = _provider.TestInjectToken("https://example.com/repo.git", "mytoken");
        Assert.Equal("https://oauth2:mytoken@example.com/repo.git", result);
    }

    [Fact]
    public void InjectToken_WithEmptyToken_ReturnsOriginalUrl()
    {
        var result = _provider.TestInjectToken("https://example.com/repo.git", "");
        Assert.Equal("https://example.com/repo.git", result);
    }

    [Fact]
    public void InjectToken_WithNullToken_ReturnsOriginalUrl()
    {
        var result = _provider.TestInjectToken("https://example.com/repo.git", null!);
        Assert.Equal("https://example.com/repo.git", result);
    }

    [Fact]
    public void InjectToken_WithPathAndQuery_PreservesPathAndQuery()
    {
        var result = _provider.TestInjectToken("https://example.com/org/repo.git?ref=main", "tok");
        Assert.Equal("https://oauth2:tok@example.com/org/repo.git?ref=main", result);
    }
}

#endregion

#region GitHubGitProvider Tests

public class GitHubGitProviderTests
{
    private readonly TestableGitHubGitProvider _provider = new();

    [Fact]
    public void RepoType_ReturnsGitHub()
    {
        Assert.Equal(RepoType.GitHub, ((IGitProvider)_provider).RepoType);
    }

    [Fact]
    public void InjectToken_WithToken_UsesXAccessTokenScheme()
    {
        var result = _provider.TestInjectToken("https://github.com/owner/repo.git", "ghp_token123");
        Assert.Equal("https://x-access-token:ghp_token123@github.com/owner/repo.git", result);
    }

    [Fact]
    public void InjectToken_WithEmptyToken_ReturnsOriginalUrl()
    {
        var result = _provider.TestInjectToken("https://github.com/owner/repo.git", "");
        Assert.Equal("https://github.com/owner/repo.git", result);
    }

    [Fact]
    public void InjectToken_WithNullToken_ReturnsOriginalUrl()
    {
        var result = _provider.TestInjectToken("https://github.com/owner/repo.git", null!);
        Assert.Equal("https://github.com/owner/repo.git", result);
    }
}

#endregion

#region GitLabGitProvider Tests

public class GitLabGitProviderTests
{
    private readonly TestableGitLabGitProvider _provider = new();

    [Fact]
    public void RepoType_ReturnsGitLab()
    {
        Assert.Equal(RepoType.GitLab, ((IGitProvider)_provider).RepoType);
    }

    [Fact]
    public void InjectToken_WithToken_UsesOauth2Scheme()
    {
        var result = _provider.TestInjectToken("https://gitlab.com/owner/repo.git", "glpat-abc123");
        Assert.Equal("https://oauth2:glpat-abc123@gitlab.com/owner/repo.git", result);
    }

    [Fact]
    public void InjectToken_WithEmptyToken_ReturnsOriginalUrl()
    {
        var result = _provider.TestInjectToken("https://gitlab.com/owner/repo.git", "");
        Assert.Equal("https://gitlab.com/owner/repo.git", result);
    }

    [Fact]
    public void InjectToken_WithNullToken_ReturnsOriginalUrl()
    {
        var result = _provider.TestInjectToken("https://gitlab.com/owner/repo.git", null!);
        Assert.Equal("https://gitlab.com/owner/repo.git", result);
    }
}

#endregion

#region BitbucketGitProvider Tests

public class BitbucketGitProviderTests
{
    private readonly TestableBitbucketGitProvider _provider = new();

    [Fact]
    public void RepoType_ReturnsBitbucket()
    {
        Assert.Equal(RepoType.Bitbucket, ((IGitProvider)_provider).RepoType);
    }

    [Fact]
    public void InjectToken_WithToken_UsesXTokenAuthScheme()
    {
        var result = _provider.TestInjectToken("https://bitbucket.org/owner/repo.git", "bbtoken");
        Assert.Equal("https://x-token-auth:bbtoken@bitbucket.org/owner/repo.git", result);
    }

    [Fact]
    public void InjectToken_WithEmptyToken_ReturnsOriginalUrl()
    {
        var result = _provider.TestInjectToken("https://bitbucket.org/owner/repo.git", "");
        Assert.Equal("https://bitbucket.org/owner/repo.git", result);
    }

    [Fact]
    public void InjectToken_WithNullToken_ReturnsOriginalUrl()
    {
        var result = _provider.TestInjectToken("https://bitbucket.org/owner/repo.git", null!);
        Assert.Equal("https://bitbucket.org/owner/repo.git", result);
    }
}

#endregion

#region AzureReposGitProvider Tests

public class AzureReposGitProviderTests
{
    private readonly TestableAzureReposGitProvider _provider = new();

    [Fact]
    public void RepoType_ReturnsAzureRepos()
    {
        Assert.Equal(RepoType.AzureRepos, ((IGitProvider)_provider).RepoType);
    }

    [Fact]
    public void InjectToken_WithToken_UsesPatScheme()
    {
        var result = _provider.TestInjectToken("https://dev.azure.com/org/project/_git/repo", "mypat");
        Assert.Equal("https://pat:mypat@dev.azure.com/org/project/_git/repo", result);
    }

    [Fact]
    public void InjectToken_WithEmptyToken_ReturnsOriginalUrl()
    {
        var result = _provider.TestInjectToken("https://dev.azure.com/org/project/_git/repo", "");
        Assert.Equal("https://dev.azure.com/org/project/_git/repo", result);
    }

    [Fact]
    public void InjectToken_WithNullToken_ReturnsOriginalUrl()
    {
        var result = _provider.TestInjectToken("https://dev.azure.com/org/project/_git/repo", null!);
        Assert.Equal("https://dev.azure.com/org/project/_git/repo", result);
    }
}

#endregion

#region GitProviderFactory Tests

public class GitProviderFactoryTests
{
    private static IGitProvider CreateProvider(RepoType type)
    {
        return type switch
        {
            RepoType.Generic => new GenericGitProvider(NullLogger<GenericGitProvider>.Instance),
            RepoType.GitHub => new GitHubGitProvider(NullLogger<GitHubGitProvider>.Instance),
            RepoType.GitLab => new GitLabGitProvider(NullLogger<GitLabGitProvider>.Instance),
            RepoType.Bitbucket => new BitbucketGitProvider(NullLogger<BitbucketGitProvider>.Instance),
            RepoType.AzureRepos => new AzureReposGitProvider(NullLogger<AzureReposGitProvider>.Instance),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    private static GitProviderFactory CreateFactoryWithAllProviders()
    {
        var providers = new IGitProvider[]
        {
            CreateProvider(RepoType.Generic),
            CreateProvider(RepoType.GitHub),
            CreateProvider(RepoType.GitLab),
            CreateProvider(RepoType.Bitbucket),
            CreateProvider(RepoType.AzureRepos)
        };
        return new GitProviderFactory(providers);
    }

    [Theory]
    [InlineData(RepoType.GitHub)]
    [InlineData(RepoType.GitLab)]
    [InlineData(RepoType.Bitbucket)]
    [InlineData(RepoType.AzureRepos)]
    [InlineData(RepoType.Generic)]
    public void GetProvider_KnownType_ReturnsMatchingProvider(RepoType repoType)
    {
        var factory = CreateFactoryWithAllProviders();

        var provider = factory.GetProvider(repoType);

        Assert.Equal(repoType, provider.RepoType);
    }

    [Fact]
    public void GetProvider_UnknownType_FallsBackToGeneric()
    {
        // Factory with only Generic registered
        var providers = new IGitProvider[]
        {
            CreateProvider(RepoType.Generic)
        };
        var factory = new GitProviderFactory(providers);

        var provider = factory.GetProvider(RepoType.GitHub);

        Assert.Equal(RepoType.Generic, provider.RepoType);
    }

    [Fact]
    public void GetProvider_NoGenericRegistered_UnknownTypeThrows()
    {
        // Factory with only GitHub registered, no Generic fallback
        var providers = new IGitProvider[]
        {
            CreateProvider(RepoType.GitHub)
        };
        var factory = new GitProviderFactory(providers);

        Assert.Throws<KeyNotFoundException>(() => factory.GetProvider(RepoType.GitLab));
    }

    [Fact]
    public void Constructor_WithEmptyProviders_CreatesFactory()
    {
        var factory = new GitProviderFactory(Enumerable.Empty<IGitProvider>());

        Assert.Throws<KeyNotFoundException>(() => factory.GetProvider(RepoType.Generic));
    }
}

#endregion