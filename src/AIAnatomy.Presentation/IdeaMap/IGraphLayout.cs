namespace AIAnatomy.Presentation.IdeaMap;

/// <summary>Computes box positions and line routes for the idea map. Kept behind an interface so hand-placed positions
/// could replace the automatic layout without touching the data or the drawing.</summary>
public interface IGraphLayout
{
    GraphLayoutResult Layout(GraphInput input);
}
