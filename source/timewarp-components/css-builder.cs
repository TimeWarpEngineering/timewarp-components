namespace TimeWarp.Components;

#region Purpose
// Fluent builder for composing a CSS class string from conditional parts, including
// merging a consumer-supplied `class` pulled out of a splatted attribute dictionary.
#endregion
#region Design
// Dependency-free replacement for the unmaintained BlazorComponentUtilities.CssBuilder.
// AddClassFromAttributes is the merge the canonical element pattern relies on (so a
// consumer-passed `class` composes with the component's own classes instead of clobbering).
#endregion

public sealed class CssBuilder
{
  private readonly List<string> Classes = [];

  public CssBuilder(string? value = null) => AddClass(value);

  public CssBuilder AddClass(string? value)
  {
    if (!string.IsNullOrWhiteSpace(value))
      Classes.Add(value);

    return this;
  }

  public CssBuilder AddClass(string? value, bool when) => when ? AddClass(value) : this;

  public CssBuilder AddClass(string? value, Func<bool> when) => AddClass(value, when());

  public CssBuilder AddClassFromAttributes(IReadOnlyDictionary<string, object>? attributes)
  {
    if (attributes is not null && attributes.TryGetValue("class", out object? value))
      AddClass(value?.ToString());

    return this;
  }

  public string Build() => string.Join(' ', Classes);

  public override string ToString() => Build();
}
