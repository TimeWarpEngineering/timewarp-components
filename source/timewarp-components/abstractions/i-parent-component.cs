namespace TimeWarp.Components;

#region Purpose
// Marker for components that render child content.
#endregion

public interface IParentComponent
{
  RenderFragment ChildContent { get; set; }
}
