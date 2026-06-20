#region Purpose
// Dev CLI command for TimeWarp.Components CI/CD pipeline
#endregion

// ═══════════════════════════════════════════════════════════════════════════════
// WORKFLOW COMMAND
// ═══════════════════════════════════════════════════════════════════════════════
// Orchestrates the full CI/CD pipeline with mode detection.
// Auto-detects mode from GITHUB_EVENT_NAME or accepts explicit --mode flag.
//
// Modes:
//   pr/merge:  clean -> build -> verify-samples -> test -> check-version
//   release:   clean -> build -> push

using System.Xml.Linq;

namespace DevCli.Commands;

[NuruRoute("workflow", Description = "Run full CI/CD pipeline")]
internal sealed class WorkflowCommand : ICommand<Unit>
{
  [Option("mode", "m", Description = "CI mode: pr, merge, or release (auto-detected from GITHUB_EVENT_NAME if not specified)")]
  public string? Mode { get; set; }

  [Option("api-key", Description = "NuGet API key for publishing (from OIDC Trusted Publishing)")]
  public string? ApiKey { get; set; }

  internal sealed class Handler : ICommandHandler<WorkflowCommand, Unit>
  {
    private readonly ITerminal Terminal;

    public Handler(ITerminal terminal)
    {
      Terminal = terminal;
    }

    public async ValueTask<Unit> Handle(WorkflowCommand command, CancellationToken ct)
    {
      CiMode mode = DetermineMode(command.Mode);

      Terminal.WriteLine("===============================================================================");
      Terminal.WriteLine($"  CI/CD Pipeline - Mode: {mode}");
      Terminal.WriteLine("===============================================================================");
      Terminal.WriteLine("");

      if (mode == CiMode.Release)
      {
        await RunReleaseWorkflowAsync(command.ApiKey, ct);
      }
      else
      {
        await RunPrWorkflowAsync(ct);
      }

      return Value;
    }

    private CiMode DetermineMode(string? explicitMode)
    {
      if (!string.IsNullOrEmpty(explicitMode))
      {
        return explicitMode.ToLowerInvariant() switch
        {
          "pr" => CiMode.Pr,
          "merge" => CiMode.Merge,
          "release" => CiMode.Release,
          _ => CiMode.Pr
        };
      }

      string? eventName = Environment.GetEnvironmentVariable("GITHUB_EVENT_NAME");

      CiMode mode = eventName switch
      {
        "pull_request" => CiMode.Pr,
        "push" => CiMode.Merge,
        "release" => CiMode.Release,
        "workflow_dispatch" => CiMode.Release,
        _ => CiMode.Pr
      };

      string displayEventName = eventName ?? "(not set)";
      Terminal.WriteLine($"Detected GITHUB_EVENT_NAME: {displayEventName} -> Mode: {mode}");
      return mode;
    }

    private async Task RunPrWorkflowAsync(CancellationToken ct)
    {
      Terminal.WriteLine("Pipeline: clean -> build -> verify-samples -> test -> check-version");
      Terminal.WriteLine("");

      Environment.ExitCode = 0;

      Terminal.WriteLine("===============================================================================");
      Terminal.WriteLine("  Step 1/5: Clean");
      Terminal.WriteLine("===============================================================================");
      await new CleanCommand.Handler(Terminal).Handle(new CleanCommand(), ct);

      if (StopOnFailure("Clean"))
      {
        return;
      }

      Terminal.WriteLine("");
      Terminal.WriteLine("===============================================================================");
      Terminal.WriteLine("  Step 2/5: Build");
      Terminal.WriteLine("===============================================================================");
      await new BuildCommand.Handler(Terminal).Handle(new BuildCommand(), ct);

      if (StopOnFailure("Build"))
      {
        return;
      }

      Terminal.WriteLine("");
      Terminal.WriteLine("===============================================================================");
      Terminal.WriteLine("  Step 3/5: Verify Samples");
      Terminal.WriteLine("===============================================================================");
      await new VerifySamplesCommand.Handler(Terminal).Handle(new VerifySamplesCommand(), ct);

      if (StopOnFailure("Verify Samples"))
      {
        return;
      }

      Terminal.WriteLine("");
      Terminal.WriteLine("===============================================================================");
      Terminal.WriteLine("  Step 4/5: Test");
      Terminal.WriteLine("===============================================================================");
      await new TestCommand.Handler(Terminal).Handle(new TestCommand(), ct);

      if (StopOnFailure("Test"))
      {
        return;
      }

      Terminal.WriteLine("");
      Terminal.WriteLine("===============================================================================");
      Terminal.WriteLine("  Step 5/5: Check Version");
      Terminal.WriteLine("===============================================================================");
      await new CheckVersionCommand.Handler().Handle(new CheckVersionCommand(), ct);

      if (StopOnFailure("Check Version"))
      {
        return;
      }

      Terminal.WriteLine("");
      Terminal.WriteLine("===============================================================================");
      Terminal.WriteLine("  Pipeline SUCCEEDED");
      Terminal.WriteLine("===============================================================================");
    }

    private async Task RunReleaseWorkflowAsync(string? apiKey, CancellationToken ct)
    {
      Terminal.WriteLine("Pipeline: clean -> build -> push");
      Terminal.WriteLine("");

      Environment.ExitCode = 0;

      string? repoRoot = Git.FindRoot();
      if (repoRoot is null)
      {
        Terminal.WriteErrorLine("Error: could not find repository root.");
        Environment.ExitCode = 1;
        return;
      }

      Terminal.WriteLine("===============================================================================");
      Terminal.WriteLine("  Step 1/3: Clean");
      Terminal.WriteLine("===============================================================================");
      await new CleanCommand.Handler(Terminal).Handle(new CleanCommand(), ct);

      if (StopOnFailure("Clean"))
      {
        return;
      }

      Terminal.WriteLine("");
      Terminal.WriteLine("===============================================================================");
      Terminal.WriteLine("  Step 2/3: Build");
      Terminal.WriteLine("===============================================================================");
      await new BuildCommand.Handler(Terminal).Handle(new BuildCommand(), ct);

      if (StopOnFailure("Build"))
      {
        return;
      }

      Terminal.WriteLine("");
      Terminal.WriteLine("===============================================================================");
      Terminal.WriteLine("  Step 3/3: Push to NuGet");
      Terminal.WriteLine("===============================================================================");
      await PushPackageAsync(repoRoot, apiKey, ct);

      if (StopOnFailure("Push to NuGet"))
      {
        return;
      }

      Terminal.WriteLine("");
      Terminal.WriteLine("===============================================================================");
      Terminal.WriteLine("  Pipeline SUCCEEDED - Package published to NuGet.org");
      Terminal.WriteLine("===============================================================================");
    }

    private async Task PushPackageAsync(string repoRoot, string? apiKey, CancellationToken ct)
    {
      string artifactsDir = Path.Combine(repoRoot, "artifacts", "packages");

      string propsPath = Path.Combine(repoRoot, "source", "Directory.Build.props");
      XDocument doc = XDocument.Load(propsPath);
      string? version = doc.Descendants("Version").FirstOrDefault()?.Value;

      if (string.IsNullOrEmpty(version))
      {
        throw new InvalidOperationException("Could not determine version for push");
      }

      string nupkgPath = Path.Combine(artifactsDir, $"TimeWarp.Components.{version}.nupkg");

      if (!File.Exists(nupkgPath))
      {
        throw new FileNotFoundException($"Package not found: {nupkgPath}");
      }

      Terminal.WriteLine($"Pushing TimeWarp.Components.{version}.nupkg...");

      List<string> args = ["nuget", "push", nupkgPath, "--source", "https://api.nuget.org/v3/index.json", "--no-symbols"];

      if (!string.IsNullOrEmpty(apiKey))
      {
        args.AddRange(["--api-key", apiKey]);
      }

      int exitCode = await Shell.Builder("dotnet")
        .WithArguments([.. args])
        .WithWorkingDirectory(repoRoot)
        .WithNoValidation()
        .RunAsync(ct);

      if (exitCode != 0)
      {
        Terminal.WriteErrorLine($"\nNuGet push failed with exit code {exitCode}");
        Environment.ExitCode = 1;
        return;
      }

      Terminal.WriteLine("\nPackage pushed successfully!");
    }

    private bool StopOnFailure(string stepName)
    {
      if (Environment.ExitCode == 0)
      {
        return false;
      }

      Terminal.WriteErrorLine("");
      Terminal.WriteErrorLine("===============================================================================");
      Terminal.WriteErrorLine($"  Pipeline FAILED - {stepName} failed");
      Terminal.WriteErrorLine("===============================================================================");
      return true;
    }
  }
}

internal enum CiMode
{
  Pr,
  Merge,
  Release
}