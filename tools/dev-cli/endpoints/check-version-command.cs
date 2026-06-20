#region Purpose
// Check whether source version is safe to release
#endregion
#region Design
// Delegates version-check logic to RepoCheckVersionService and formats CLI output
// Supports two strategies: git-tag (default) and nuget-search
#endregion

namespace DevCli.Commands;

[NuruRoute("check-version", Description = "Verify version is ready to release")]
internal sealed class CheckVersionCommand : ICommand<Unit>
{
  [Option("strategy", Description = "Version check strategy: git-tag (default) or nuget-search")]
  public string? Strategy { get; set; }

  [Option("package", Description = "NuGet package ID to check (comma-separated, nuget-search only)")]
  public string? Package { get; set; }

  [Option("tag", Description = "Git tag to compare against (git-tag only)")]
  public string? Tag { get; set; }

  internal sealed class Handler : ICommandHandler<CheckVersionCommand, Unit>
  {
    private readonly ITerminal Terminal;

    public Handler(ITerminal terminal)
    {
      Terminal = terminal;
    }

    public async ValueTask<Unit> Handle(CheckVersionCommand command, CancellationToken ct)
    {
      ArgumentNullException.ThrowIfNull(command);

      string? repoRoot = Git.FindRoot();
      if (repoRoot is null)
      {
        Terminal.WriteErrorLine("Error: could not find repository root.");
        Environment.ExitCode = 1;
        return Value;
      }

      NuGetPackageService nuGetPackageService = new();
      RepoCheckVersionService repoCheckVersionService = new(nuGetPackageService);

      string strategy = command.Strategy ?? "git-tag";

      if (string.Equals(strategy, "git-tag", StringComparison.OrdinalIgnoreCase))
      {
        GitTagCheckResult result = await repoCheckVersionService.CheckGitTagVersionAsync
        (
          tag: command.Tag,
          cancellationToken: ct
        );

        WriteGitTagOutput(result);
      }
      else if (string.Equals(strategy, "nuget-search", StringComparison.OrdinalIgnoreCase))
      {
        if (string.IsNullOrWhiteSpace(command.Package))
        {
          Terminal.WriteErrorLine("Error: --package is required for nuget-search strategy");
          Environment.ExitCode = 1;
          return Value;
        }

        string[] packages = command.Package.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        NuGetCheckResult result = await repoCheckVersionService.CheckNuGetVersionAsync
        (
          packages: packages,
          cancellationToken: ct
        );

        WriteNuGetSearchOutput(result);
      }
      else
      {
        Terminal.WriteErrorLine($"Error: unknown strategy: {strategy}");
        Environment.ExitCode = 1;
        return Value;
      }

      return Value;
    }

    private void WriteGitTagOutput(GitTagCheckResult result)
    {
      if (string.IsNullOrEmpty(result.Version))
      {
        Terminal.WriteErrorLine("Error: could not determine version from repository");
        Environment.ExitCode = 1;
        return;
      }

      string latestReleaseTag = result.LatestReleaseTag ?? "(none found)";

      Terminal.WriteLine("Strategy: git-tag (GitHub releases)");
      Terminal.WriteLine("");
      Terminal.WriteLine($"Version in source: {result.Version}");
      Terminal.WriteLine($"Latest release tag on GitHub: {latestReleaseTag}");
      Terminal.WriteLine("");

      if (result.IsNewVersion)
      {
        Terminal.WriteLine("Version in source is new — safe to release.");
      }
      else
      {
        Terminal.WriteErrorLine("Version in source already matches latest release tag.");
        Environment.ExitCode = 1;
      }
    }

    private void WriteNuGetSearchOutput(NuGetCheckResult result)
    {
      if (string.IsNullOrEmpty(result.Version))
      {
        Terminal.WriteErrorLine("Error: could not determine version from repository");
        Environment.ExitCode = 1;
        return;
      }

      string checkedPackagesText = result.CheckedPackages.Count > 0
        ? string.Join(", ", result.CheckedPackages)
        : "(none)";
      string latestNuGetVersion = result.LatestNuGetVersion ?? "(none found)";

      Terminal.WriteLine("Strategy: nuget-search (NuGet packages)");
      Terminal.WriteLine("");
      Terminal.WriteLine($"Version in source: {result.Version}");
      Terminal.WriteLine($"Latest NuGet version: {latestNuGetVersion}");
      Terminal.WriteLine($"Packages checked: {checkedPackagesText}");
      Terminal.WriteLine("");

      if (result.IsNewVersion)
      {
        Terminal.WriteLine("Version in source is new — safe to release.");
      }
      else
      {
        IReadOnlyList<string> alreadyPublishedPackages = result.AlreadyPublishedPackages ?? [];
        string publishedPackagesText = alreadyPublishedPackages.Count > 0
          ? string.Join(", ", alreadyPublishedPackages)
          : "(unknown)";
        Terminal.WriteErrorLine($"Version in source already published for: {publishedPackagesText}");
        Environment.ExitCode = 1;
      }
    }
  }
}