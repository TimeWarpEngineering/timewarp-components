#!/usr/bin/env -S dotnet --
#:package TimeWarp.Jaribu
#:project $(SourceDirectory)timewarp-components/timewarp-components.csproj

#region Purpose
// Unit tests for CssBuilder: conditional class composition and consumer-class merge.
#endregion
#region Design
// SUT: CssBuilder. Naming: SUT_Action_Given_Should_Result. Dual-mode, block-scoped.
#endregion

#if !JARIBU_MULTI
return await RunAllTests();
#endif

namespace CssBuilder_
{
  using TimeWarp.Components;

  [TestTag("CssBuilder")]
  public class Build_Given_
  {
    [ModuleInitializer]
    internal static void Register() => RegisterTests<Build_Given_>();

    public static async Task Parts_Should_JoinWithSpaces()
    {
      new CssBuilder("a").AddClass("b").Build().ShouldBe("a b");
      await Task.CompletedTask;
    }

    public static async Task FalseCondition_Should_SkipTheClass()
    {
      new CssBuilder("a").AddClass("b", false).Build().ShouldBe("a");
      await Task.CompletedTask;
    }

    public static async Task NullOrWhitespace_Should_BeIgnored()
    {
      new CssBuilder(null).AddClass("   ").AddClass("x").Build().ShouldBe("x");
      await Task.CompletedTask;
    }

    public static async Task ConsumerClass_Should_MergeFromAttributes()
    {
      IReadOnlyDictionary<string, object> attributes = new Dictionary<string, object> { ["class"] = "consumer" };
      new CssBuilder("base").AddClassFromAttributes(attributes).Build().ShouldBe("base consumer");
      await Task.CompletedTask;
    }

    public static async Task NoClassKey_Should_LeaveBaseUnchanged()
    {
      IReadOnlyDictionary<string, object> attributes = new Dictionary<string, object> { ["id"] = "x" };
      new CssBuilder("base").AddClassFromAttributes(attributes).Build().ShouldBe("base");
      await Task.CompletedTask;
    }
  }
}
