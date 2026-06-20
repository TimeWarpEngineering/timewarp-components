namespace TimeWarp.Components;

#region Purpose
// Base for leaf display/presentational components. Provides the unmatched-attribute
// splat so consumers can forward class/style/data-*/aria onto the component's root.
#endregion
#region Design
// Plain ComponentBase — intentionally state-free (no TimeWarp.State dependency). The
// styling strategy pairs this with CSS isolation on a native root element. Attributes
// defaults to an empty dictionary so splatting is always safe.
#endregion

public abstract class DisplayComponent : ComponentBase, IAttributeComponent
{
  [Parameter(CaptureUnmatchedValues = true)]
  public IReadOnlyDictionary<string, object> Attributes { get; set; } = new Dictionary<string, object>();
}
