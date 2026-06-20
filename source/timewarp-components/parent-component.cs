namespace TimeWarp.Components;

#region Purpose
// Display component that also renders child content.
#endregion

public abstract class ParentComponent : DisplayComponent, IParentComponent
{
  [Parameter] public RenderFragment ChildContent { get; set; } = null!;
}
