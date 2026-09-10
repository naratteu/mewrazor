using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Naratteu.MewUI.Razor;

/// <summary>
/// Applies Blazor render batches to a MewUI control tree. Only component and text frames are
/// supported: there is no HTML element in a MewRazor tree, every tag resolves to a
/// <see cref="MewComponentBase"/> and therefore to a typed control.
/// </summary>
public sealed class MewRazorRenderer : Renderer
{
    private readonly Dictionary<int, MewNode> _nodesByComponentId = [];
    private readonly MewDispatcher _dispatcher;
    private readonly RootChildCollection _rootChildren = new();
    private MewNode? _rootNode;

    public MewRazorRenderer(IServiceProvider services, ILoggerFactory loggerFactory)
        : base(services, loggerFactory)
        => _dispatcher = new MewDispatcher(static () =>
            Application.IsRunning ? Application.Current.Dispatcher : null);

    public MewRazorRenderer()
        : this(new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance)
    {
    }

    /// <remarks>
    /// The main-window factory runs before the application is marked running, so the MewUI
    /// dispatcher is resolved on each access rather than captured in the constructor.
    /// </remarks>
    public override Dispatcher Dispatcher => _dispatcher;

    /// <summary>
    /// Renders <typeparamref name="TRoot"/> and returns the single element it produced.
    /// Must be called on the MewUI UI thread.
    /// </summary>
    public TElement Mount<TRoot, TElement>()
        where TRoot : IComponent
        where TElement : Element
    {
        var component = InstantiateComponent(typeof(TRoot));
        var componentId = AssignRootComponentId(component);
        _rootNode = new MewNode { RootChildren = _rootChildren, ComponentId = componentId };
        _nodesByComponentId[componentId] = _rootNode;

        var renderTask = RenderRootComponentAsync(componentId);
        if (renderTask.IsFaulted) renderTask.GetAwaiter().GetResult();

        return _rootChildren.Root as TElement
            ?? throw new InvalidOperationException(
                $"{typeof(TRoot).Name} must render a single <{typeof(TElement).Name}> at its root, "
                + $"but produced {_rootChildren.Root?.GetType().Name ?? "nothing"}.");
    }

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
    {
        var frames = renderBatch.ReferenceFrames;
        var updated = renderBatch.UpdatedComponents;

        for (var i = 0; i < updated.Count; i++)
        {
            ref var diff = ref updated.Array[i];
            ApplyEdits(diff.ComponentId, diff.Edits, frames);
        }

        var disposed = renderBatch.DisposedComponentIDs;
        for (var i = 0; i < disposed.Count; i++) _nodesByComponentId.Remove(disposed.Array[i]);

        return Task.CompletedTask;
    }

    protected override void HandleException(Exception exception)
        => throw new InvalidOperationException("Unhandled exception in a MewRazor component.", exception);

    private void ApplyEdits(int componentId, ArrayBuilderSegment<RenderTreeEdit> edits, ArrayRange<RenderTreeFrame> frames)
    {
        if (!_nodesByComponentId.TryGetValue(componentId, out var node)) return;

        List<(int From, int To)>? permutation = null;

        for (var i = 0; i < edits.Count; i++)
        {
            ref var edit = ref edits.Array[edits.Offset + i];
            switch (edit.Type)
            {
                case RenderTreeEditType.PrependFrame:
                    InsertFrame(node, edit.SiblingIndex, frames, edit.ReferenceFrameIndex);
                    break;

                case RenderTreeEditType.RemoveFrame:
                    RemoveChild(node, edit.SiblingIndex);
                    break;

                case RenderTreeEditType.UpdateText:
                    UpdateText(node, edit.SiblingIndex, frames.Array[edit.ReferenceFrameIndex].TextContent);
                    break;

                case RenderTreeEditType.PermutationListEntry:
                    permutation ??= [];
                    permutation.Add((edit.SiblingIndex, edit.MoveToSiblingIndex));
                    break;

                case RenderTreeEditType.PermutationListEnd:
                    Permute(node, permutation ?? []);
                    permutation = null;
                    break;

                default:
                    throw new NotSupportedException(
                        $"{edit.Type} applies to HTML elements, which a MewRazor tree does not contain.");
            }
        }
    }

    /// <summary>Inserts one frame as a child of <paramref name="parent"/>; returns the sibling count consumed.</summary>
    private int InsertFrame(MewNode parent, int siblingIndex, ArrayRange<RenderTreeFrame> frames, int frameIndex)
    {
        ref var frame = ref frames.Array[frameIndex];
        switch (frame.FrameType)
        {
            case RenderTreeFrameType.Component:
            {
                var element = (frame.Component as MewComponentBase)?.NativeElement;
                var child = new MewNode
                {
                    Parent = parent,
                    ComponentId = frame.ComponentId,
                    Element = element,
                    OwnChildren = element is null ? null : MewChildCollection.For(element),
                };

                _nodesByComponentId[frame.ComponentId] = child;
                AttachChild(parent, siblingIndex, child);
                return 1;
            }

            case RenderTreeFrameType.Text:
            {
                // Layout whitespace still occupies a sibling slot, but renders nothing.
                var element = string.IsNullOrWhiteSpace(frame.TextContent)
                    ? null
                    : new TextBlock { Text = frame.TextContent };
                AttachChild(parent, siblingIndex, new MewNode { Parent = parent, Element = element });
                return 1;
            }

            case RenderTreeFrameType.Markup:
            {
                if (!string.IsNullOrWhiteSpace(frame.MarkupContent))
                {
                    throw new NotSupportedException(
                        "Raw markup has no meaning in a MewUI tree. Use a MewUI component instead of "
                        + $"literal content: \"{frame.MarkupContent.Trim()}\".");
                }

                AttachChild(parent, siblingIndex, new MewNode { Parent = parent });
                return 1;
            }

            case RenderTreeFrameType.Region:
            {
                var end = frameIndex + frame.RegionSubtreeLength;
                var inserted = 0;
                for (var i = frameIndex + 1; i < end; i += SubtreeLength(frames.Array[i]))
                {
                    inserted += InsertFrame(parent, siblingIndex + inserted, frames, i);
                }

                return inserted;
            }

            // Attributes were already applied as component parameters by the diff; captures are inert here.
            case RenderTreeFrameType.Attribute:
            case RenderTreeFrameType.ComponentReferenceCapture:
            case RenderTreeFrameType.ComponentRenderMode:
            case RenderTreeFrameType.NamedEvent:
                return 0;

            case RenderTreeFrameType.Element:
                throw new NotSupportedException(
                    $"<{frame.ElementName}> is an HTML element. A MewRazor view may only use MewUI components.");

            default:
                throw new NotSupportedException($"{frame.FrameType} frames are not supported.");
        }
    }

    private static void AttachChild(MewNode parent, int siblingIndex, MewNode child)
    {
        var index = parent.PhysicalIndex(siblingIndex);
        parent.Children.Insert(siblingIndex, child);
        if (child.Element is not null) parent.TargetForChildren().Insert(index, child.Element);
    }

    /// <summary>
    /// Reorders keyed children. The moves describe one simultaneous permutation, so the whole
    /// run is lifted out of the target collection and reinserted in the new order; the controls
    /// themselves are moved, never rebuilt.
    /// </summary>
    private static void Permute(MewNode parent, List<(int From, int To)> moves)
    {
        if (moves.Count == 0) return;

        var current = parent.Children.ToArray();
        var reordered = new MewNode?[current.Length];
        var movedFrom = new HashSet<int>();

        foreach (var (from, to) in moves)
        {
            reordered[to] = current[from];
            movedFrom.Add(from);
        }

        // Children the diff did not list keep their relative order in the gaps that are left.
        var untouched = new Queue<MewNode>();
        for (var i = 0; i < current.Length; i++)
        {
            if (!movedFrom.Contains(i)) untouched.Enqueue(current[i]);
        }

        for (var i = 0; i < reordered.Length; i++) reordered[i] ??= untouched.Dequeue();

        var target = parent.TargetForChildren();
        var start = parent.PhysicalIndex(0);
        var count = 0;
        foreach (var child in current) count += child.ElementCount;

        for (var i = 0; i < count; i++) target.RemoveAt(start);

        parent.Children.Clear();
        parent.Children.AddRange(reordered!);

        var index = start;
        foreach (var child in reordered) Reinsert(child!, target, ref index);
    }

    private static void Reinsert(MewNode node, MewChildCollection target, ref int index)
    {
        if (node.Element is not null)
        {
            target.Insert(index++, node.Element);
            return;
        }

        foreach (var child in node.Children) Reinsert(child, target, ref index);
    }

    private void RemoveChild(MewNode parent, int siblingIndex)
    {
        var child = parent.Children[siblingIndex];
        var index = parent.PhysicalIndex(siblingIndex);
        var count = child.ElementCount;

        parent.Children.RemoveAt(siblingIndex);

        var target = parent.TargetForChildren();
        for (var i = 0; i < count; i++) target.RemoveAt(index);

        Discard(child);
    }

    /// <summary>
    /// Drops a removed subtree. Every element is disposed individually: MewUI severs a control's
    /// bindings only on that control's own <see cref="IDisposable.Dispose"/>, and neither removing
    /// it from its parent nor disposing the parent reaches it.
    /// </summary>
    private void Discard(MewNode node)
    {
        if (node.ComponentId >= 0) _nodesByComponentId.Remove(node.ComponentId);
        foreach (var child in node.Children) Discard(child);
        (node.Element as IDisposable)?.Dispose();
    }

    private static void UpdateText(MewNode parent, int siblingIndex, string text)
    {
        if (parent.Children[siblingIndex].Element is TextBlock textBlock) textBlock.Text = text;
    }

    private static int SubtreeLength(in RenderTreeFrame frame) => frame.FrameType switch
    {
        RenderTreeFrameType.Component => frame.ComponentSubtreeLength,
        RenderTreeFrameType.Element => frame.ElementSubtreeLength,
        RenderTreeFrameType.Region => frame.RegionSubtreeLength,
        _ => 1,
    };
}
