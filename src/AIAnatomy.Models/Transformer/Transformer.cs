using AIAnatomy.Core;

namespace AIAnatomy.Models.Transformers;

/// <summary>A GPT-style decoder-only transformer (decision D3: pre-layer-norm blocks, separate output matrix).</summary>
public sealed class Transformer : Module
{
    private readonly TokenEmbedding _tokens;
    private readonly IPositionEncoding _positions;
    private readonly List<TransformerBlock> _blocks = new();

    public Transformer(TransformerConfig config, SeededRandom rng)
    {
        if (config.Heads <= 0 || config.ModelWidth % config.Heads != 0)
            throw new ArgumentException($"Model width {config.ModelWidth} must divide evenly into {config.Heads} heads.", nameof(config));
        if (config.VocabularySize <= 0 || config.ContextLength <= 0 || config.Layers < 0)
            throw new ArgumentException("Vocabulary size and context length must be positive and layers cannot be negative.", nameof(config));
        Config = config;

        _tokens = new TokenEmbedding("token_embedding", config.VocabularySize, config.ModelWidth, rng);
        _positions = config.Positions == PositionKind.Learned
            ? new LearnedPositions("position_embedding", config.ContextLength, config.ModelWidth, rng)
            : new SinusoidalPositions(config.ModelWidth);
        for (int i = 0; i < config.Layers; i++) _blocks.Add(new TransformerBlock($"blocks.{i}", config, rng));
        FinalNorm = new LayerNormModule("final_norm", config.ModelWidth);
        OutputWeights = Weights("output.weight", rng, config.ModelWidth, config.VocabularySize);
        OutputBias = Constant("output.bias", config.VocabularySize, 0);
    }

    public TransformerConfig Config { get; }
    public LayerNormModule FinalNorm { get; }
    public Parameter OutputWeights { get; }
    public Parameter OutputBias { get; }
    public IReadOnlyList<TransformerBlock> Blocks => _blocks;

    public ForwardResult Forward(IReadOnlyList<int> tokenIds, ForwardOptions? options = null)
    {
        options ??= ForwardOptions.None;
        int t = tokenIds.Count;
        if (t == 0) throw new ArgumentException("Give at least one token.", nameof(tokenIds));
        if (t > Config.ContextLength)
            throw new ArgumentException($"{t} tokens do not fit in the context length of {Config.ContextLength}.", nameof(tokenIds));

        foreach (var (layer, head) in options.DisabledHeads)
            if (layer < 0 || layer >= Config.Layers || head < 0 || head >= Config.Heads)
                throw new ArgumentOutOfRangeException(nameof(options), $"Head {head} of layer {layer} does not exist: the model has {Config.Layers} layers of {Config.Heads} heads.");

        var tokenEmbeddings = _tokens.Forward(tokenIds);
        Tensor positionEmbeddings, input;
        if (options.RemovePositions)
        {
            positionEmbeddings = Tensor.Zeros(t, Config.ModelWidth);
            input = tokenEmbeddings;
        }
        else
        {
            positionEmbeddings = _positions.Forward(t);
            input = Ops.Add(tokenEmbeddings, positionEmbeddings);
        }

        var blockTraces = new List<BlockTrace>(_blocks.Count);
        var x = input;
        for (int layer = 0; layer < _blocks.Count; layer++)
        {
            HashSet<int>? disabled = null;
            foreach (var (l, h) in options.DisabledHeads)
                if (l == layer) (disabled ??= new HashSet<int>()).Add(h);
            var trace = _blocks[layer].Forward(x, disabled);
            blockTraces.Add(trace);
            x = trace.ResidualOut;
        }

        var finalNorm = FinalNorm.Forward(x);
        var logits = Ops.Add(Ops.MatMul(finalNorm, OutputWeights.Value), OutputBias.Value);
        var full = new ForwardTrace
        {
            TokenIds = tokenIds.ToArray(), TokenEmbeddings = tokenEmbeddings, PositionEmbeddings = positionEmbeddings,
            Input = input, Blocks = blockTraces, FinalNorm = finalNorm, Logits = logits,
        };
        return new ForwardResult(logits, full);
    }

    /// <summary>Parameter counts grouped as the lessons describe them.</summary>
    public IReadOnlyDictionary<string, long> ParameterCountsByPart()
    {
        static long Total(IEnumerable<Parameter> ps) => ps.Sum(p => (long)p.Value.Size);
        return new Dictionary<string, long>
        {
            ["Token embeddings"] = Total(_tokens.Parameters()),
            ["Positions"] = Total(_positions is Module m ? m.Parameters() : Enumerable.Empty<Parameter>()),
            ["Attention"] = Total(_blocks.SelectMany(b => b.AttentionParameters())),
            ["Feed-forward"] = Total(_blocks.SelectMany(b => b.FeedForwardParameters())),
            ["Layer norms"] = Total(_blocks.SelectMany(b => b.NormParameters()).Concat(FinalNorm.Parameters())),
            ["Output layer"] = Total(new[] { OutputWeights, OutputBias }),
        };
    }

    /// <summary>A deep copy with the same weights, sharing nothing with this model.</summary>
    public Transformer Clone()
    {
        var copy = new Transformer(Config, new SeededRandom(0));
        foreach (var (source, target) in Parameters().Zip(copy.Parameters()))
            Array.Copy(source.Value.Data, target.Value.Data, source.Value.Size);
        return copy;
    }

    public override IEnumerable<Parameter> Parameters()
    {
        foreach (var p in _tokens.Parameters()) yield return p;
        if (_positions is Module positions)
            foreach (var p in positions.Parameters()) yield return p;
        foreach (var block in _blocks)
            foreach (var p in block.Parameters()) yield return p;
        foreach (var p in FinalNorm.Parameters()) yield return p;
        yield return OutputWeights;
        yield return OutputBias;
    }
}
