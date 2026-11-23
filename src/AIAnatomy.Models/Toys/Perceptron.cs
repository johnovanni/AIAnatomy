namespace AIAnatomy.Models.Toys;

/// <summary>One training example: two inputs and the right answer (0 or 1).</summary>
public sealed record LabeledPoint(double X1, double X2, int Label);

/// <summary>The weights after one pass over the data, and how many points that pass got wrong.</summary>
public sealed record PerceptronEpoch(int Epoch, double W1, double W2, double Bias, int Errors);

/// <summary>A single neuron with a step activation, trained with the classic perceptron rule.</summary>
public sealed class Perceptron
{
    public double W1 { get; private set; }
    public double W2 { get; private set; }
    public double Bias { get; private set; }

    public static IReadOnlyList<LabeledPoint> And { get; } = Table(0, 0, 0, 1);
    public static IReadOnlyList<LabeledPoint> Or { get; } = Table(0, 1, 1, 1);
    public static IReadOnlyList<LabeledPoint> Xor { get; } = Table(0, 1, 1, 0);

    /// <summary>1 if W1·x1 + W2·x2 + Bias is above zero, otherwise 0.</summary>
    public int Predict(double x1, double x2) => W1 * x1 + W2 * x2 + Bias > 0 ? 1 : 0;

    /// <summary>
    /// Starts from zero weights and bias; for each point nudges the weights by rate·(label − prediction)·input.
    /// Records one entry per epoch and stops after the first epoch with no errors.
    /// </summary>
    public IReadOnlyList<PerceptronEpoch> Train(IReadOnlyList<LabeledPoint> data, double rate, int maxEpochs)
    {
        ArgumentNullException.ThrowIfNull(data);
        W1 = W2 = Bias = 0;
        var epochs = new List<PerceptronEpoch>();
        for (int epoch = 1; epoch <= maxEpochs; epoch++)
        {
            int errors = 0;
            foreach (var p in data)
            {
                int miss = p.Label - Predict(p.X1, p.X2);
                if (miss == 0) continue;
                errors++;
                W1 += rate * miss * p.X1;
                W2 += rate * miss * p.X2;
                Bias += rate * miss;
            }
            epochs.Add(new PerceptronEpoch(epoch, W1, W2, Bias, errors));
            if (errors == 0) break;
        }
        return epochs;
    }

    // Labels for the points (0,0), (0,1), (1,0), (1,1).
    private static IReadOnlyList<LabeledPoint> Table(int l00, int l01, int l10, int l11) => new[]
    {
        new LabeledPoint(0, 0, l00), new LabeledPoint(0, 1, l01),
        new LabeledPoint(1, 0, l10), new LabeledPoint(1, 1, l11),
    };
}
