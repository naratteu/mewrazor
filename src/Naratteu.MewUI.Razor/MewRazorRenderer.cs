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
                case RenderTreeEditType.PermutationListEnd:
                    throw new NotSupportedException(
                        "Reordering keyed children (@key) is not implemented yet.");

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

    private void RemoveChild(MewNode parent, int siblingIndex)
    {
        var child = parent.Children[siblingIndex];
        var index = parent.PhysicalIndex(siblingIndex);
        var count = child.ElementCount;

        parent.Children.RemoveAt(siblingIndex);

        var target = parent.TargetForChildren();
        for (var i = 0; i < count; i++) target.RemoveAt(index);

        Forget(child);
    }

    private void Forget(MewNode node)
    {
        if (node.ComponentId >= 0) _nodesByComponentId.Remove(node.ComponentId);
        foreach (var child in node.Children) Forget(child);
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
