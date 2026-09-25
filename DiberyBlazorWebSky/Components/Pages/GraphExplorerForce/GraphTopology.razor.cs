using DiberyBlazorWebSky.Models.Graph;
using DiberyBlazorWebSky.Services;
using DiberyBlazorWebSky.Components.Pages.GraphExplorerForce.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DiberyBlazorWebSky.Components.Pages.GraphExplorerForce;

public partial class GraphTopology : ComponentBase, IDisposable
{
    [Inject] private IGraphPageContext Context { get; set; } = default!;
    [Inject] private GraphAdminApiClient GraphApi { get; set; } = default!;
    [Inject] private ILogger<GraphTopology> Logger { get; set; } = default!;

    private bool _isBusy;
    private string? _errorMessage;

    // 拓扑图
    private List<NodeDto> _topologyNodes = new();
    private List<EdgeDto> _topologyEdges = new();
    private NodeDto? _selectedTopologyNode;
    private readonly Dictionary<Guid, (double X, double Y)> _nodePositions = new();

    private string _layoutMode = "force";
    private int _layoutSeed = 42;

    // SVG 画布尺寸
    private const double CanvasWidth = 800;
    private const double CanvasHeight = 600;

    protected override async Task OnInitializedAsync()
    {
        Context.StateChanged += OnContextStateChanged;
        if (Context.Graphs.Count == 0)
        {
            await Context.LoadGraphsAsync();
        }
    }

    private Task OnContextStateChanged()
    {
        _topologyNodes.Clear();
        _topologyEdges.Clear();
        _nodePositions.Clear();
        _selectedTopologyNode = null;
        StateHasChanged();
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════
    // 拓扑图
    // ══════════════════════════════════════════════════════

    private async Task LoadTopologyAsync()
    {
        if (string.IsNullOrEmpty(Context.SelectedGraphGuid)) return;

        _isBusy = true;
        _errorMessage = null;
        _selectedTopologyNode = null;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(Context.SelectedGraphGuid);

            _topologyNodes = await GraphApi.ListAllNodesAsync(graphGuid);
            _topologyEdges = await GraphApi.ListAllEdgesAsync(graphGuid);

            RecomputeLayout();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "加载拓扑图失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private void RecomputeLayout()
    {
        try
        {
            if (_layoutMode == "circular") ComputeCircularLayout();
            else ComputeForceDirectedLayout();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "布局计算失败，回退到圆形布局");
            ComputeCircularLayout();
        }
    }

    private void SwitchLayout(string mode)
    {
        if (_layoutMode == mode) return;
        _layoutMode = mode;
        RecomputeLayout();
        StateHasChanged();
    }

    private void ShuffleLayout()
    {
        _layoutSeed = Random.Shared.Next(1, 1_000_000);
        if (_layoutMode == "force") ComputeForceDirectedLayout();
        StateHasChanged();
    }

    private void ComputeCircularLayout()
    {
        _nodePositions.Clear();
        int n = _topologyNodes.Count;
        if (n == 0) return;

        const double cx = CanvasWidth / 2;
        const double cy = CanvasHeight / 2;
        double radius = Math.Min(220, Math.Max(100, n * 8));

        for (int i = 0; i < n; i++)
        {
            double angle = 2 * Math.PI * i / n - Math.PI / 2;
            _nodePositions[_topologyNodes[i].Guid] =
                (cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle));
        }
    }

    private void ComputeForceDirectedLayout()
    {
        _nodePositions.Clear();
        int n = _topologyNodes.Count;

        if (n == 0) return;
        if (n == 1)
        {
            _nodePositions[_topologyNodes[0].Guid] = (CanvasWidth / 2, CanvasHeight / 2);
            return;
        }

        const double cx = CanvasWidth / 2;
        const double cy = CanvasHeight / 2;
        const double desiredEdgeLength = 180;
        const double minSeparation = 130;
        const double edgeStiffness = 0.10;
        const double centerCoef = 0.04;
        const double centerCoefIsolated = 0.10;

        var positions = new Dictionary<Guid, (double X, double Y)>();
        var rng = new Random(_layoutSeed);
        double initRadius = Math.Min(80, 30 + n * 4);

        for (int i = 0; i < n; i++)
        {
            double angle = 2 * Math.PI * i / n;
            double jitter = (rng.NextDouble() - 0.5) * 20;
            positions[_topologyNodes[i].Guid] =
                (cx + (initRadius + jitter) * Math.Cos(angle),
                 cy + (initRadius + jitter) * Math.Sin(angle));
        }

        var adjacency = _topologyNodes.ToDictionary(node => node.Guid, _ => new HashSet<Guid>());
        foreach (var e in _topologyEdges)
        {
            if (adjacency.ContainsKey(e.From) && adjacency.ContainsKey(e.To))
            {
                adjacency[e.From].Add(e.To);
                adjacency[e.To].Add(e.From);
            }
        }

        int iterations = Math.Clamp(n * 40, 150, 500);
        double temperature = 80.0;
        double cooling = temperature / (iterations + 1);

        for (int iter = 0; iter < iterations; iter++)
        {
            var disp = _topologyNodes.ToDictionary(node => node.Guid, _ => (Dx: 0.0, Dy: 0.0));

            // 斥力
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    var a = _topologyNodes[i].Guid;
                    var b = _topologyNodes[j].Guid;

                    double dx = positions[a].X - positions[b].X;
                    double dy = positions[a].Y - positions[b].Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);

                    if (dist < 0.1)
                    {
                        dx = rng.NextDouble() - 0.5;
                        dy = rng.NextDouble() - 0.5;
                        dist = Math.Sqrt(dx * dx + dy * dy);
                        if (dist < 0.01) { dx = 1; dy = 0; dist = 1; }
                    }

                    if (dist >= minSeparation) continue;

                    double overlap = (minSeparation - dist) / minSeparation;
                    double force = overlap * overlap * 60;

                    double fx = (dx / dist) * force;
                    double fy = (dy / dist) * force;

                    disp[a] = (disp[a].Dx + fx, disp[a].Dy + fy);
                    disp[b] = (disp[b].Dx - fx, disp[b].Dy - fy);
                }
            }

            // 边引力
            foreach (var edge in _topologyEdges)
            {
                if (edge.From == edge.To) continue;
                if (!positions.ContainsKey(edge.From) || !positions.ContainsKey(edge.To)) continue;

                double dx = positions[edge.From].X - positions[edge.To].X;
                double dy = positions[edge.From].Y - positions[edge.To].Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < 0.1) continue;

                double deviation = dist - desiredEdgeLength;
                double force = deviation * edgeStiffness;

                double fx = (dx / dist) * force;
                double fy = (dy / dist) * force;

                disp[edge.From] = (disp[edge.From].Dx - fx, disp[edge.From].Dy - fy);
                disp[edge.To] = (disp[edge.To].Dx + fx, disp[edge.To].Dy + fy);
            }

            // 中心引力
            foreach (var node in _topologyNodes)
            {
                double dx = cx - positions[node.Guid].X;
                double dy = cy - positions[node.Guid].Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < 1) continue;

                bool isolated = adjacency[node.Guid].Count == 0;
                double coef = isolated ? centerCoefIsolated : centerCoef;
                double force = coef * dist;

                double fx = (dx / dist) * force;
                double fy = (dy / dist) * force;

                disp[node.Guid] = (disp[node.Guid].Dx + fx, disp[node.Guid].Dy + fy);
            }

            // 应用位移
            foreach (var node in _topologyNodes)
            {
                var d = disp[node.Guid];
                double len = Math.Sqrt(d.Dx * d.Dx + d.Dy * d.Dy);
                if (len < 0.001) continue;

                double limited = Math.Min(len, temperature);
                double newX = positions[node.Guid].X + (d.Dx / len) * limited;
                double newY = positions[node.Guid].Y + (d.Dy / len) * limited;

                newX = Math.Clamp(newX, 60, CanvasWidth - 60);
                newY = Math.Clamp(newY, 60, CanvasHeight - 60);

                positions[node.Guid] = (newX, newY);
            }

            temperature = Math.Max(temperature - cooling, 0.5);
        }

        CenterWithoutScaling(positions);

        foreach (var kv in positions)
            _nodePositions[kv.Key] = kv.Value;
    }

    private static void CenterWithoutScaling(
        Dictionary<Guid, (double X, double Y)> positions)
    {
        if (positions.Count == 0) return;

        double minX = positions.Values.Min(p => p.X);
        double maxX = positions.Values.Max(p => p.X);
        double minY = positions.Values.Min(p => p.Y);
        double maxY = positions.Values.Max(p => p.Y);

        double currentCenterX = (minX + maxX) / 2;
        double currentCenterY = (minY + maxY) / 2;

        double offsetX = CanvasWidth / 2 - currentCenterX;
        double offsetY = CanvasHeight / 2 - currentCenterY;

        foreach (var key in positions.Keys.ToList())
        {
            var p = positions[key];
            positions[key] = (p.X + offsetX, p.Y + offsetY);
        }
    }

    private (double X, double Y)? GetPosition(Guid nodeGuid)
        => _nodePositions.TryGetValue(nodeGuid, out var p) ? p : null;

    private void SelectTopologyNode(NodeDto node)
    {
        _selectedTopologyNode = _selectedTopologyNode?.Guid == node.Guid ? null : node;
        StateHasChanged();
    }

    public void Dispose()
    {
        Context.StateChanged -= OnContextStateChanged;
    }
}
