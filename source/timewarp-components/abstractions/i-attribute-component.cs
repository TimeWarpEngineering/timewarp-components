namespace TimeWarp.Components;

#region Purpose
// Marker for components that splat unmatched HTML attributes onto their root element.
#endregion

public interface IAttributeComponent
{
  IReadOnlyDictionary<string, object> Attributes { get; set; }
}
