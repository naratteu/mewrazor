using Aprillz.MewUI.Controls;

namespace Naratteu.MewUI.Razor;

/// <summary>
/// Index-addressable child slot of a native MewUI element, so the render-tree diff can be
/// applied as ordered insert/remove operations.
/// </summary>
public abstract class MewChildCollection
{
    public abstract void Insert(int index, Element child);

    public abstract void RemoveAt(int index);

    /// <summary>Resolves the child slot of <paramref name="element"/>, or null when it is a leaf.</summary>
    public static MewChildCollection? For(Element element) => element switch
    {
        Panel panel => new PanelChildren(panel),
        ContentControl content => new SingleChild(content),
        _ => null,
    };

    private sealed class PanelChildren(Panel panel) : MewChildCollection
    {
        public override void Insert(int index, Element child) => panel.Insert(index, child);

        public override void RemoveAt(int index) => panel.RemoveAt(index);
    }

    private sealed class SingleChild(ContentControl control) : MewChildCollection
    {
        public override void Insert(int index, Element child)
        {
            if (index != 0 || control.Content is not null)
            {
                throw new InvalidOperationException(
                    $"{control.GetType().Name} accepts a single child, but the component tried to add another one.");
            }

            control.Content = child;
        }

        public override void RemoveAt(int index) => control.Content = null!;
    }
}

/// <summary>Holds the single root element produced by the mounted root component.</summary>
internal sealed class RootChildCollection : MewChildCollection
{
    public Element? Root { get; private set; }

    public override void Insert(int index, Element child)
    {
        if (index != 0 || Root is not null)
            throw new InvalidOperationException("The root component must render exactly one element.");

        Root = child;
    }

    public override void RemoveAt(int index) => Root = null;
}
