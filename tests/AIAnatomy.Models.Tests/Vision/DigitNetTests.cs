using AIAnatomy.Core;
using AIAnatomy.Models.Files;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Vision;

namespace AIAnatomy.Models.Tests.Vision;

/// <summary>The bundled digits subsets (UCI 8x8), loaded once.</summary>
internal static class BundledDigits
{
    private static readonly Lazy<DigitSet> LazyTrain = new(() => DigitSet.Load(ContentPath("digits-train.bin.gz")));
    private static readonly Lazy<DigitSet> LazyTest = new(() => DigitSet.Load(ContentPath("digits-test.bin.gz")));

    public static DigitSet Train => LazyTrain.Value;
    public static DigitSet Test => LazyTest.Value;

    private static string ContentPath(string file) => Path.Combine(AppContext.BaseDirectory, "content", "data", "digits", file);

    /// <summary>
    /// The first <paramref name="perDigit"/> images of each digit of the training subset, saved and loaded as a set of their
    /// own (the file is grouped by digit, so this is balanced).
    /// </summary>
    public static DigitSet TrainSubset(int perDigit)
    {
        var train = Train;
        var images = new List<(byte Label, byte[] Pixels)>();
        for (int d = 0; d < 10; d++)
            foreach (int i in Enumerable.Range(0, train.Count).Where(i => train.Label(i) == d).Take(perDigit))
                images.Add(((byte)d, train.Pixels(i).Select(v => (byte)Math.Round(v * 255)).ToArray()));
        string path = Path.Combine(Path.GetTempPath(), "digitnet-" + Guid.NewGuid().ToString("N") + ".bin.gz");
        try
        {
            DigitSet.Save(path, train.ImageSize, images);
            return DigitSet.Load(path);
        }
        finally
        {
            try { File.Delete(path); } catch (IOException) { }
        }
    }
}

public class DigitNetTests
{
    private static Tensor Images(int n, int size, int seed)
    {
        var rng = new SeededRandom(seed);
        var data = new double[n * size * size];
        for (int i = 0; i < data.Length; i++) data[i] = rng.NextDouble();
        return Tensor.FromArray(data, n, 1, size, size);
    }

    /// <summary>Replaces the starting weights with others so every part visibly matters (biases too).</summary>
    private static void Randomise(DigitNet net, int seed)
    {
        var rng = new SeededRandom(seed);
        foreach (var p in net.Parameters())
            for (int i = 0; i < p.Value.Size; i++) p.Value.Data[i] = rng.NextGaussian() * 0.5;
    }

    [Theory]
    [InlineData(28)]
    [InlineData(8)]
    public void Forward_ShapesOfEveryLayer(int size)
    {
        var net = new DigitNet(new DigitNetConfig(ImageSize: size), new SeededRandom(1));
        var trace = net.Forward(Images(2, size, 3));
        Assert.Equal(new[] { 2, 8, size, size }, trace.Conv1.Shape);
        Assert.Equal(new[] { 2, 8, size / 2, size / 2 }, trace.Pool1.Shape);
        Assert.Equal(new[] { 2, 16, size / 2, size / 2 }, trace.Conv2.Shape);
        Assert.Equal(new[] { 2, 16, size / 4, size / 4 }, trace.Pool2.Shape);
        Assert.Equal(new[] { 2, 64 }, trace.Hidden.Shape);
        Assert.Equal(new[] { 2, 10 }, trace.Logits.Shape);
        // Conv1 and Conv2 are after ReLU, and so is the hidden layer.
        Assert.All(trace.Conv1.Data, v => Assert.True(v >= 0));
        Assert.All(trace.Conv2.Data, v => Assert.True(v >= 0));
        Assert.All(trace.Hidden.Data, v => Assert.True(v >= 0));
    }

    [Fact]
    public void Parameters_HaveTheAgreedNamesShapesAndCount()
    {
        // 28x28: conv1 8*9 + 8 = 80; conv2 16*72 + 16 = 1,168; fc1 (16*7*7 = 784) * 64 + 64 = 50,240; fc2 64*10 + 10 = 650.
        Assert.Equal(52_138, new DigitNetConfig(ImageSize: 28).CountParameters());
        // 8x8: the same convolutions and fc2; fc1 is (16*2*2 = 64) * 64 + 64 = 4,160. 80 + 1,168 + 4,160 + 650 = 6,058.
        Assert.Equal(6_058, new DigitNetConfig(ImageSize: 8).CountParameters());

        foreach (int size in new[] { 8, 28 })
        {
            var net = new DigitNet(new DigitNetConfig(ImageSize: size), new SeededRandom(1));
            var parameters = net.Parameters().ToList();
            Assert.Equal(new[] { "conv1.weight", "conv1.bias", "conv2.weight", "conv2.bias", "fc1.weight", "fc1.bias", "fc2.weight", "fc2.bias" },
                parameters.Select(p => p.Name));
            int flat = 16 * (size / 4) * (size / 4);
            Assert.Equal(new[] { 8, 9 }, parameters[0].Value.Shape);
            Assert.Equal(new[] { 8 }, parameters[1].Value.Shape);
            Assert.Equal(new[] { 16, 72 }, parameters[2].Value.Shape);
            Assert.Equal(new[] { 16 }, parameters[3].Value.Shape);
            Assert.Equal(new[] { flat, 64 }, parameters[4].Value.Shape);
            Assert.Equal(new[] { 64 }, parameters[5].Value.Shape);
            Assert.Equal(new[] { 64, 10 }, parameters[6].Value.Shape);
            Assert.Equal(new[] { 10 }, parameters[7].Value.Shape);
            Assert.Equal(net.Config.CountParameters(), parameters.Sum(p => (long)p.Value.Size));
        }
    }

    [Fact]
    public void Constructor_HeNormalWeightsAndZeroBiases()
    {
        // Larger layers so the sample standard deviation is close to sqrt(2 / fan-in).
        var net = new DigitNet(new DigitNetConfig(ImageSize: 28, Filters1: 32, Filters2: 32, Hidden: 64), new SeededRandom(4));
        var p = net.Parameters().ToDictionary(x => x.Name, x => x.Value);
        void CheckStd(string name, int fanIn)
        {
            var data = p[name].Data;
            double mean = data.Average();
            double std = Math.Sqrt(data.Select(v => (v - mean) * (v - mean)).Average());
            double expected = Math.Sqrt(2.0 / fanIn);
            Assert.InRange(std, expected * 0.9, expected * 1.1);
        }
        CheckStd("conv1.weight", 9);            // 288 values
        CheckStd("conv2.weight", 32 * 9);       // 9,216 values
        CheckStd("fc1.weight", 32 * 7 * 7);     // 100,352 values
        CheckStd("fc2.weight", 64);             // 640 values
        foreach (var bias in new[] { "conv1.bias", "conv2.bias", "fc1.bias", "fc2.bias" })
            Assert.All(p[bias].Data, v => Assert.Equal(0, v));
    }

    [Fact]
    public void Forward_FollowsTheLayersByHand()
    {
        // Rebuild the forward pass from the Core operations and compare every stage.
        var net = new DigitNet(new DigitNetConfig(ImageSize: 8, Filters1: 3, Filters2: 4, Hidden: 5), new SeededRandom(2));
        Randomise(net, 9);
        var w = net.Parameters().Select(p => p.Value).ToArray();
        var images = Images(3, 8, 5);
        var trace = net.Forward(images);

        var conv1 = Ops.Relu(Ops.Conv2d(images, w[0], w[1], 3, 1));
        var pool1 = Ops.MaxPool2d(conv1, 2);
        var conv2 = Ops.Relu(Ops.Conv2d(pool1, w[2], w[3], 3, 1));
        var pool2 = Ops.MaxPool2d(conv2, 2);
        var hidden = Ops.Relu(Ops.Add(Ops.MatMul(Ops.Reshape(pool2, 3, 16), w[4]), w[5]));
        var logits = Ops.Add(Ops.MatMul(hidden, w[6]), w[7]);

        Assert.Equal(conv1.Data, trace.Conv1.Data);
        Assert.Equal(pool1.Data, trace.Pool1.Data);
        Assert.Equal(conv2.Data, trace.Conv2.Data);
        Assert.Equal(pool2.Data, trace.Pool2.Data);
        Assert.Equal(hidden.Data, trace.Hidden.Data);
        Assert.Equal(logits.Data, trace.Logits.Data);
    }

    [Fact]
    public void Probabilities_SumToOneAndFollowTheLogits()
    {
        var net = new DigitNet(new DigitNetConfig(ImageSize: 8), new SeededRandom(1));
        Randomise(net, 3);
        var trace = net.Forward(Images(4, 8, 7));
        for (int image = 0; image < 4; image++)
        {
            var probabilities = trace.Probabilities(image);
            Assert.Equal(10, probabilities.Length);
            Assert.Equal(1.0, probabilities.Sum(), 12);
            Assert.All(probabilities, v => Assert.InRange(v, 0, 1));
            var logits = trace.Logits.Row(image);
            Assert.Equal(Array.IndexOf(logits, logits.Max()), Array.IndexOf(probabilities, probabilities.Max()));
            // softmax: p_i / p_j = exp(l_i - l_j)
            Assert.Equal(Math.Exp(logits[3] - logits[0]), probabilities[3] / probabilities[0], 9);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => trace.Probabilities(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => trace.Probabilities(-1));
    }

    [Fact]
    public void GradCheck_LossOfATinyNetwork()
    {
        var config = new DigitNetConfig(ImageSize: 4, Filters1: 2, Filters2: 3, Hidden: 4);
        var net = new DigitNet(config, new SeededRandom(1));
        Randomise(net, 11);
        var images = Images(2, 4, 12);
        var targets = new[] { 3, 7 };

        double error = GradCheck.MaxRelativeError(
            weights => Losses.CrossEntropy(DigitNet.FromWeights(config, weights).Forward(images).Logits, targets),
            net.Parameters().Select(p => p.Value).ToArray());

        Assert.True(error < 1e-6, $"worst relative error {error}");
    }

    [Fact]
    public void RefusesBadConfigsAndInputs()
    {
        Assert.Throws<ArgumentException>(() => new DigitNet(new DigitNetConfig(ImageSize: 10), new SeededRandom(1)));
        Assert.Throws<ArgumentException>(() => new DigitNet(new DigitNetConfig(ImageSize: 0), new SeededRandom(1)));
        Assert.Throws<ArgumentException>(() => new DigitNet(new DigitNetConfig(Filters1: 0), new SeededRandom(1)));
        Assert.Throws<ArgumentException>(() => new DigitNet(new DigitNetConfig(Filters2: 0), new SeededRandom(1)));
        Assert.Throws<ArgumentException>(() => new DigitNet(new DigitNetConfig(Hidden: 0), new SeededRandom(1)));
        var net = new DigitNet(new DigitNetConfig(ImageSize: 8), new SeededRandom(1));
        Assert.Throws<ArgumentException>(() => net.Forward(Images(1, 4, 1)));
        Assert.Throws<ArgumentException>(() => net.Forward(Tensor.FromArray(new double[2 * 64], 1, 2, 8, 8)));
        Assert.Throws<ArgumentException>(() => net.Forward(Tensor.FromArray(new double[64], 8, 8)));
        var good = net.Parameters().Select(p => p.Value).ToArray();
        Assert.Throws<ArgumentException>(() => DigitNet.FromWeights(net.Config, good.Take(7).ToArray()));
        Assert.Throws<ArgumentException>(() => DigitNet.FromWeights(new DigitNetConfig(ImageSize: 28), good));
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var net = new DigitNet(new DigitNetConfig(ImageSize: 8, Filters1: 4, Filters2: 6, Hidden: 12), new SeededRandom(2));
        Randomise(net, 5);
        var info = new ModelInfo("Image network", "UCI handwritten digits (8x8)", 300, null, 1, true,
            new Dictionary<string, double> { ["testAccuracy"] = 0.93 });
        using var ms = new MemoryStream();
        net.Save(ms, info);
        ms.Position = 0;

        var (loaded, loadedInfo) = DigitNet.Load(ms);

        Assert.Equal(net.Config, loaded.Config);
        Assert.Equal(info, loadedInfo);
        foreach (var (a, b) in net.Parameters().Zip(loaded.Parameters()))
        {
            Assert.Equal(a.Name, b.Name);
            Assert.Equal(a.Value.Shape, b.Value.Shape);
            Assert.Equal(a.Value.Data, b.Value.Data);
        }
        var images = Images(2, 8, 4);
        Assert.Equal(net.Forward(images).Logits.Data, loaded.Forward(images).Logits.Data);
    }

    [Fact]
    public void Load_RefusesAnotherKindOrMismatchedParameters()
    {
        var net = new DigitNet(new DigitNetConfig(ImageSize: 8, Filters1: 2, Filters2: 3, Hidden: 4), new SeededRandom(3));
        var info = new ModelInfo("Image network", "Digits", 1, null, 1, false);
        var parameters = net.Parameters().ToList();

        using (var other = new MemoryStream())
        {
            FamilyModelFile.Save(other, FamilyKinds.Memory, net.Config, "abc", info, parameters);
            other.Position = 0;
            var e = Assert.Throws<InvalidDataException>(() => DigitNet.Load(other));
            Assert.Equal("This file holds a memory network, not an image network.", e.Message);
        }
        using (var wrongHidden = new MemoryStream())
        {
            FamilyModelFile.Save(wrongHidden, FamilyKinds.Digits, net.Config with { Hidden = 5 }, null, info, parameters);
            wrongHidden.Position = 0;
            Assert.Throws<InvalidDataException>(() => DigitNet.Load(wrongHidden));
        }
        using (var badSize = new MemoryStream())
        {
            FamilyModelFile.Save(badSize, FamilyKinds.Digits, net.Config with { ImageSize = 6 }, null, info, parameters);
            badSize.Position = 0;
            var e = Assert.Throws<InvalidDataException>(() => DigitNet.Load(badSize));
            Assert.Contains("impossible shape", e.Message);
        }
        using (var swapped = new MemoryStream())
        {
            var reordered = parameters.ToList();
            (reordered[1], reordered[3]) = (reordered[3], reordered[1]);
            FamilyModelFile.Save(swapped, FamilyKinds.Digits, net.Config, null, info, reordered);
            swapped.Position = 0;
            Assert.Throws<InvalidDataException>(() => DigitNet.Load(swapped));
        }
        using (var renamed = new MemoryStream())
        {
            var named = parameters.Select((p, i) => i == 5 ? new Parameter("fc1.offset", p.Value) : p).ToList();
            FamilyModelFile.Save(renamed, FamilyKinds.Digits, net.Config, null, info, named);
            renamed.Position = 0;
            Assert.Throws<InvalidDataException>(() => DigitNet.Load(renamed));
        }
    }

    [Fact]
    public void Accuracy_CountsTheImagesWhoseLargestProbabilityIsTheLabel()
    {
        var set = BundledDigits.TrainSubset(7);   // 70 images, more than one batch of the accuracy pass
        var net = new DigitNet(new DigitNetConfig(ImageSize: 8), new SeededRandom(6));
        Randomise(net, 2);
        var logits = net.Forward(set.Batch(Enumerable.Range(0, set.Count).ToArray())).Logits;
        int right = 0;
        for (int i = 0; i < set.Count; i++)
        {
            var row = logits.Row(i);
            if (Array.IndexOf(row, row.Max()) == set.Label(i)) right++;
        }
        Assert.Equal((double)right / set.Count, DigitTrainer.Accuracy(net, set), 12);
        Assert.Throws<ArgumentException>(() => DigitTrainer.Accuracy(new DigitNet(new DigitNetConfig(ImageSize: 28), new SeededRandom(1)), set));
    }

    [Fact]
    public void DigitTraining_SettingsFromTheSpeedCheck()
    {
        // The same network for "train it again" and the shipped file, on the bundled 8x8 digits; the shipped one trains longer.
        Assert.Equal(new DigitNetConfig(ImageSize: 8, Filters1: 16, Filters2: 32, Hidden: 128), DigitTraining.Retrain);
        Assert.Equal(new DigitNetConfig(ImageSize: 8, Filters1: 16, Filters2: 32, Hidden: 128), DigitTraining.Shipped);
        Assert.Equal(BundledDigits.Train.ImageSize, DigitTraining.Retrain.ImageSize);
        Assert.Equal(2_000, DigitTraining.RetrainSteps);
        Assert.Equal(4_000, DigitTraining.ShippedSteps);
        // 16*9 + 16 + 32*144 + 32 + (32*2*2 = 128) * 128 + 128 + 128*10 + 10 = 160 + 4,640 + 16,512 + 1,290.
        Assert.Equal(22_602, DigitTraining.Shipped.CountParameters());
    }

    [Fact]
    public void Trainer_RefusesBadSettings()
    {
        var set = BundledDigits.TrainSubset(2);
        var net = new DigitNet(new DigitNetConfig(ImageSize: 8), new SeededRandom(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DigitTrainer(net, set, 0, seed: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DigitTrainer(net, set, 10, seed: 1, batchSize: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DigitTrainer(net, set, 10, seed: 1, learningRate: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DigitTrainer(net, set, 10, seed: 1, learningRate: double.NaN));
        Assert.Throws<ArgumentException>(() => new DigitTrainer(new DigitNet(new DigitNetConfig(ImageSize: 28), new SeededRandom(1)), set, 10, seed: 1));
        var trainer = new DigitTrainer(net, set, 1, seed: 1);
        Assert.True(double.IsFinite(trainer.Step()));
        Assert.Equal(1, trainer.StepsTaken);
        Assert.Throws<InvalidOperationException>(() => trainer.Step());
    }
}

[Collection(TrainingCollection.Name)]
public class DigitNetTrainingTests
{
    private static double MeanLoss(DigitNet net, DigitSet set)
    {
        var all = Enumerable.Range(0, set.Count).ToArray();
        return Losses.CrossEntropy(net.Forward(set.Batch(all)).Logits, set.Labels.ToArray()).Item();
    }

    [Fact]
    public void Trainer_150StepsOn200Images_LowersTheMeanTrainingLossBelow80Percent()
    {
        var set = BundledDigits.TrainSubset(20);
        Assert.Equal(200, set.Count);
        var net = new DigitNet(new DigitNetConfig(ImageSize: 8), new SeededRandom(1));
        double first = MeanLoss(net, set);
        var trainer = new DigitTrainer(net, set, totalSteps: 150, seed: 1);

        while (trainer.StepsTaken < trainer.TotalSteps) Assert.True(double.IsFinite(trainer.Step()));

        double last = MeanLoss(net, set);
        Assert.True(last < 0.8 * first, $"mean training loss {first:F3} before, {last:F3} after");
    }

    [Fact]
    public void Trainer_SameSeedGivesTheSameWeightsAfter20Steps_OnTheParallelConvolutionPath()
    {
        // Batch 64 so both convolutions take Conv2d's parallel path (above 200,000 multiply-adds a call, Ops.Matrix.cs
        // ParallelThreshold): conv1 64 * 8 * 9 * 64 = 294,912 and conv2 64 * 16 * 72 * 16 = 1,179,648.
        const int Batch = 64;
        Assert.True((long)Batch * 8 * 9 * 64 > 200_000);
        Assert.True((long)Batch * 16 * 72 * 16 > 200_000);

        double[][] Run(int seed)
        {
            var net = new DigitNet(new DigitNetConfig(ImageSize: 8), new SeededRandom(seed));
            var trainer = new DigitTrainer(net, BundledDigits.Train, totalSteps: 20, seed: seed, batchSize: Batch);
            while (trainer.StepsTaken < trainer.TotalSteps) trainer.Step();
            return net.Parameters().Select(p => (double[])p.Value.Data.Clone()).ToArray();
        }

        var a = Run(5);
        var b = Run(5);
        var c = Run(6);
        Assert.Equal(a.Length, b.Length);
        for (int i = 0; i < a.Length; i++) Assert.Equal(a[i], b[i]);
        Assert.NotEqual(a[0], c[0]);
    }
}
