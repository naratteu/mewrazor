using Aprillz.MewUI.Controls;

namespace Naratteu.MewUI.Razor;

/// <summary>
/// One position in the render tree. A node owns a native element when it maps to a MewUI
/// control; component nodes that render no control of their own are logical and let their
/// children flow into the nearest ancestor that does.
/// </summary>
internal sealed class MewNode
{
    public MewNode? Parent { get; init; }

    public int ComponentId { get; init; } = -1;

    public List<MewNode> Children { get; } = [];

    public Element? Element { get; init; }

    /// <summary>Child slot of <see cref="Element"/>, null when the element is a leaf.</summary>
    public MewChildCollection? OwnChildren { get; init; }

    /// <summary>Set on the root node only.</summary>
    public MewChildCollection? RootChildren { get; init; }

    /// <summary>Number of native elements this node contributes to its target collection.</summary>
    public int ElementCount
    {
        get
        {
            if (Element is not null) return 1;

            var count = 0;
            foreach (var child in Children) count += child.ElementCount;
            return count;
        }
    }

    /// <summary>The collection this node's children are inserted into.</summary>
    public MewChildCollection TargetForChildren()
    {
        if (Element is not null)
        {
            return OwnChildren ?? throw new InvalidOperationException(
                $"{Element.GetType().Name} is a leaf control and cannot contain child content.");
        }

        return RootChildren ?? Parent?.TargetForChildren()
            ?? throw new InvalidOperationException("Node is detached from the render tree.");
    }

    /// <summary>Index in <see cref="TargetForChildren"/> where this node's children begin.</summary>
    private int BaseIndex()
    {
        if (Element is not null || Parent is null) return 0;

        var index = Parent.BaseIndex();
        foreach (var sibling in Parent.Children)
        {
            if (ReferenceEquals(sibling, this)) break;
            index += sibling.ElementCount;
        }

        return index;
    }

    /// <summary>Index in the target collection for the child at logical position <paramref name="siblingIndex"/>.</summary>
    public int PhysicalIndex(int siblingIndex)
    {
        var index = BaseIndex();
        for (var i = 0; i < siblingIndex && i < Children.Count; i++) index += Children[i].ElementCount;
        return index;
    }
}
