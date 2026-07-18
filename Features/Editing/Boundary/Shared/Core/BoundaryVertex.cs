namespace XIAOFUTools.Features.Editing.Boundary.Shared.Core
{
    internal readonly record struct BoundaryVertex(double X, double Y);

    internal readonly record struct BoundaryLabelBounds(
        double X,
        double Y,
        double Width,
        double Height);
}
