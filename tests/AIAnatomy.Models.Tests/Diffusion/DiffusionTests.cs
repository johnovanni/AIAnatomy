using AIAnatomy.Core;
using AIAnatomy.Models.Diffusion;
using AIAnatomy.Models.Files;
using AIAnatomy.Models.Language;

namespace AIAnatomy.Models.Tests.Diffusion;

public class DiffusionTests
{
    private static IReadOnlyList<(double X, double Y)> Preset(string name) => Shapes.Presets.Single(p => p.Name == name).Points;

    private static List<(double X, double Y)> Rows(double[,] points)
    {
        var list = new List<(double X, double Y)>(points.GetLength(0));
        for (int i = 0; i < points.GetLength(0); i++) list.Add((points[i, 0], points[i, 1]));
        return list;
    }

    // ---------- Schedule ----------

    [Fact]
    public void Schedule_FullSchedule()
    {
        var s = new NoiseSchedule();
        Assert.Equal(100, s.Steps);
        Assert.Equal(Enumerable.Range(1, 100), s.Timesteps);
        Assert.Equal(1e-4, s.Beta(1), 15);
        Assert.Equal(0.1, s.Beta(100), 15);
        Assert.Equal(1e-4 + (0.1 - 1e-4) / 99, s.Beta(2), 15);
        Assert.True(s.AlphaBar(100) < 0.01, $"AlphaBar(100) = {s.AlphaBar(100)}");
        Assert.Equal(1.0, s.AlphaBar(0));
        double product = 1;
        for (int t = 1; t <= 100; t++)
        {
            product *= 1 - s.Beta(t);
            Assert.Equal(product, s.AlphaBar(t), 15);
        }
    }

    [Fact]
    public void Schedule_RespacedToTheSameLengthIsTheFullSchedule()
    {
        var full = new NoiseSchedule();
        var same = full.Respaced(100);
        Assert.Equal(100, same.Steps);
        Assert.Equal(full.Timesteps, same.Timesteps);
        for (int i = 1; i <= 100; i++)
        {
            Assert.InRange(same.Beta(i) - full.Beta(i), -1e-12, 1e-12);
            Assert.InRange(same.AlphaBar(i) - full.AlphaBar(i), -1e-12, 1e-12);
        }
    }

    [Fact]
    public void Schedule_RespacedToTenSteps()
    {
        var full = new NoiseSchedule();
        var ten = full.Respaced(10);
        Assert.Equal(10, ten.Steps);
        Assert.Equal(100, ten.OriginalSteps);
        Assert.Equal(new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 }, ten.Timesteps);
        double product = 1;
        for (int i = 1; i <= 10; i++)
        {
            Assert.Equal(full.AlphaBar(10 * i), ten.AlphaBar(i), 14);
            // beta'_i = 1 - AlphaBar(t_i) / AlphaBar(t_{i-1}), so the respaced betas multiply back to the same AlphaBar.
            Assert.Equal(1 - full.AlphaBar(10 * i) / full.AlphaBar(10 * (i - 1)), ten.Beta(i), 14);
            product *= 1 - ten.Beta(i);
            Assert.Equal(full.AlphaBar(10 * i), product, 12);
        }
    }

    [Fact]
    public void Schedule_RespacedUnevenlyKeepsDistinctStepsEndingAtTheLast()
    {
        var seven = new NoiseSchedule().Respaced(7);
        Assert.Equal(new[] { 14, 29, 43, 57, 71, 86, 100 }, seven.Timesteps);
        Assert.Equal(new[] { 100 }, new NoiseSchedule().Respaced(1).Timesteps);
    }

    [Fact]
    public void Schedule_RefusesBadArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NoiseSchedule(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NoiseSchedule(100, 0, 0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NoiseSchedule(100, 0.2, 0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NoiseSchedule(100, 1e-4, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NoiseSchedule().Respaced(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NoiseSchedule().Respaced(101));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NoiseSchedule().Beta(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NoiseSchedule().AlphaBar(101));
    }

    // ---------- Adding noise ----------

    [Fact]
    public void AddNoise_AtTheFirstStepThePointsStayNearTheShape()
    {
        var circle = Preset("Circle");
        var s = new NoiseSchedule();
        var (noisy, noise) = DiffusionSampler.AddNoise(circle, 1, s, new SeededRandom(3));
        Assert.Equal(new[] { 1000, 2 }, new[] { noisy.GetLength(0), noisy.GetLength(1) });
        Assert.Equal(new[] { 1000, 2 }, new[] { noise.GetLength(0), noise.GetLength(1) });
        double a = Math.Sqrt(s.AlphaBar(1)), b = Math.Sqrt(1 - s.AlphaBar(1));
        for (int i = 0; i < circle.Count; i++)
        {
            Assert.Equal(a * circle[i].X + b * noise[i, 0], noisy[i, 0], 12);
            Assert.Equal(a * circle[i].Y + b * noise[i, 1], noisy[i, 1], 12);
        }
        // sqrt(1 - AlphaBar(1)) = 0.01, so every point moves by well under 0.1.
        Assert.True(Shapes.MeanDistanceToShape(Rows(noisy), circle) < 0.02);
    }

    [Fact]
    public void AddNoise_AtTheLastStepThePointsAreAlmostPureNoise()
    {
        var circle = Preset("Circle");
        var (noisy, _) = DiffusionSampler.AddNoise(circle, 100, new NoiseSchedule(), new SeededRandom(4));
        // x = sqrt(abar) x0 + sqrt(1 - abar) e with abar about 0.006: E|x|^2 = abar |x0|^2 + (1 - abar) * 2, about 1.99,
        // the variance of two unit normals. With 1,000 points the sample mean has a standard error of about 0.06.
        double meanSquaredRadius = Rows(noisy).Average(p => p.X * p.X + p.Y * p.Y);
        Assert.InRange(meanSquaredRadius, 1.8, 2.2);
    }

    // ---------- Network ----------

    [Fact]
    public void Net_ShapesNamesAndTimeFeatures()
    {
        var net = new DiffusionNet(new DiffusionConfig(Hidden: 8), new SeededRandom(1));
        var parameters = net.Parameters().ToList();
        Assert.Equal(new[] { "layer1.weight", "layer1.bias", "layer2.weight", "layer2.bias", "output.weight", "output.bias" },
            parameters.Select(p => p.Name));
        Assert.Equal(new[] { 18, 8 }, parameters[0].Value.Shape);
        Assert.Equal(new[] { 8, 8 }, parameters[2].Value.Shape);
        Assert.Equal(new[] { 8, 2 }, parameters[4].Value.Shape);
        Assert.Equal(parameters.Sum(p => (long)p.Value.Size), net.Config.CountParameters());
        Assert.Equal(new DiffusionConfig(128, 16), new DiffusionConfig());

        var features = DiffusionNet.TimeFeatures(25, 100, 16);
        Assert.Equal(16, features.Length);
        for (int k = 0; k < 8; k++)
        {
            double angle = 0.25 * Math.PI * Math.Pow(2, k);
            Assert.Equal(Math.Sin(angle), features[k], 12);
            Assert.Equal(Math.Cos(angle), features[8 + k], 12);
        }

        var points = Tensor.FromArray(new[] { 0.1, 0.2, -0.3, 0.4, 0.5, -0.6 }, 3, 2);
        var output = net.Forward(points, new[] { 1, 50, 100 }, 100);
        Assert.Equal(new[] { 3, 2 }, output.Shape);
    }

    [Fact]
    public void Net_GradientsMatchFiniteDifferences()
    {
        var config = new DiffusionConfig(Hidden: 5, TimeFeatures: 4);
        var net = new DiffusionNet(config, new SeededRandom(2));
        var rng = new SeededRandom(6);
        foreach (var p in net.Parameters())
            for (int i = 0; i < p.Value.Size; i++) p.Value.Data[i] = rng.NextGaussian() * 0.5;
        var points = Tensor.FromArray(new[] { 0.3, -0.7, 0.9, 0.1 }, 2, 2);
        var target = Tensor.FromArray(new[] { 0.5, -0.2, 0.1, 0.8 }, 2, 2);

        double error = GradCheck.MaxRelativeError(
            weights => Losses.MeanSquaredError(DiffusionNet.FromWeights(config, weights).Forward(points, new[] { 3, 9 }, 10), target),
            net.Parameters().Select(p => p.Value).ToArray());

        Assert.True(error < 1e-6, $"worst relative error {error}");
    }

    [Fact]
    public void Net_RefusesBadConfigsAndInputs()
    {
        Assert.Throws<ArgumentException>(() => new DiffusionNet(new DiffusionConfig(Hidden: 0), new SeededRandom(1)));
        Assert.Throws<ArgumentException>(() => new DiffusionNet(new DiffusionConfig(TimeFeatures: 3), new SeededRandom(1)));
        var net = new DiffusionNet(new DiffusionConfig(Hidden: 4), new SeededRandom(1));
        var points = Tensor.FromArray(new[] { 0.1, 0.2 }, 1, 2);
        Assert.Throws<ArgumentException>(() => net.Forward(points, new[] { 1, 2 }, 100));
        Assert.Throws<ArgumentException>(() => net.Forward(Tensor.FromArray(new[] { 0.1, 0.2, 0.3 }, 1, 3), new[] { 1 }, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => net.Forward(points, new[] { 101 }, 100));
        var good = net.Parameters().Select(p => p.Value).ToArray();
        Assert.Throws<ArgumentException>(() => DiffusionNet.FromWeights(net.Config, good.Take(5).ToArray()));
    }

    // ---------- Sampling ----------

    [Fact]
    public void Sample_HasStepsPlusOneFramesAndIsRepeatable()
    {
        var net = new DiffusionNet(new DiffusionConfig(Hidden: 16), new SeededRandom(1));
        var s = new NoiseSchedule().Respaced(20);
        var first = DiffusionSampler.Sample(net, s, 30, new SeededRandom(7));
        var second = DiffusionSampler.Sample(net, s, 30, new SeededRandom(7));
        Assert.Equal(21, first.Count);
        Assert.All(first, f => Assert.Equal(new[] { 30, 2 }, new[] { f.GetLength(0), f.GetLength(1) }));
        for (int i = 0; i < first.Count; i++) Assert.Equal(first[i], second[i]);
        Assert.NotEqual(first[0], first[^1]);
        Assert.Equal(101, DiffusionSampler.Sample(net, new NoiseSchedule(), 3, new SeededRandom(1)).Count);
    }

    [Fact]
    public void Sample_FollowsTheAncestralUpdateWithThePosteriorVariance()
    {
        // Zero weights and output.bias = (1, -0.5): the network predicts e_hat = (1, -0.5) for every point and step. Then
        // x_{i-1} = (x_i - beta'_i / sqrt(1 - abar'_i) e_hat) / sqrt(1 - beta'_i) + sigma'_i z, with
        // sigma'_i^2 = beta'_i (1 - abar'_{i-1}) / (1 - abar'_i) from the respaced schedule's own AlphaBar, and no z at the last step.
        var net = new DiffusionNet(new DiffusionConfig(Hidden: 4), new SeededRandom(1));
        foreach (var p in net.Parameters()) Array.Clear(p.Value.Data);
        var bias = net.Parameters().Single(p => p.Name == "output.bias").Value.Data;
        bias[0] = 1;
        bias[1] = -0.5;
        var full = new NoiseSchedule();
        var s = full.Respaced(4);
        Assert.Equal(new[] { 25, 50, 75, 100 }, s.Timesteps);
        var frames = DiffusionSampler.Sample(net, s, 2, new SeededRandom(5));
        Assert.Equal(5, frames.Count);

        // Worked from the full schedule at the original t values 25, 50, 75, 100 (t_0 = 0, AlphaBar 1).
        int[] t = { 0, 25, 50, 75, 100 };
        double[] eHat = { 1, -0.5 };
        var rng = new SeededRandom(5);
        var x = new double[2, 2];
        for (int i = 0; i < 2; i++) for (int j = 0; j < 2; j++) x[i, j] = rng.NextGaussian();
        Assert.Equal(x, frames[0]);
        for (int step = 4, frame = 1; step >= 1; step--, frame++)
        {
            double abar = full.AlphaBar(t[step]), abarBefore = full.AlphaBar(t[step - 1]);
            double beta = 1 - abar / abarBefore;
            double coefficient = beta / Math.Sqrt(1 - abar);
            double sigma = Math.Sqrt(beta * (1 - abarBefore) / (1 - abar));
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                    x[i, j] = (x[i, j] - coefficient * eHat[j]) / Math.Sqrt(1 - beta) + (step > 1 ? sigma * rng.NextGaussian() : 0);
            for (int i = 0; i < 2; i++) for (int j = 0; j < 2; j++) Assert.Equal(x[i, j], frames[frame][i, j], 12);
        }

        // The first respaced step by hand: beta'_1 = 1 - abar(25), sigma'_1 = 0 (abar'_0 = 1), so with z unused the
        // last move is a pure shift by -beta'_1 / sqrt(1 - abar(25)) * e_hat / sqrt(1 - beta'_1) = -sqrt(1 - abar(25)) / sqrt(abar(25)) * e_hat.
        double a25 = full.AlphaBar(25);
        Assert.Equal(frames[3][0, 0] / Math.Sqrt(a25) - Math.Sqrt(1 - a25) / Math.Sqrt(a25), frames[4][0, 0], 12);
    }

    // ---------- Shapes ----------

    [Fact]
    public void Presets_EachHasAThousandPointsInsideTheSquare()
    {
        Assert.Equal(new[] { "Spiral", "Circle", "Two moons", "Letter S" }, Shapes.Presets.Select(p => p.Name));
        Assert.All(Shapes.Presets, preset =>
        {
            Assert.Equal(Shapes.PointCount, preset.Points.Count);
            Assert.All(preset.Points, p =>
            {
                Assert.InRange(p.X, -1, 1);
                Assert.InRange(p.Y, -1, 1);
            });
        });
        Assert.Equal(1000, Shapes.PointCount);
    }

    [Fact]
    public void FromStroke_RefusesAShortStroke()
    {
        const string message = "Draw a longer line: this one is too short to learn a shape from.";
        Assert.Equal(message, Shapes.FromStroke(Array.Empty<(double, double)>(), 400).Error);
        Assert.Equal(message, Shapes.FromStroke(new[] { (200.0, 200.0) }, 400).Error);
        var shortLine = Shapes.FromStroke(new[] { (100.0, 100.0), (199.0, 100.0) }, 400);   // 99 < 25% of 400
        Assert.Equal(message, shortLine.Error);
        Assert.Null(shortLine.Points);
    }

    [Fact]
    public void FromStroke_RefusesASmallShape()
    {
        // A zig-zag 200 long (50% of the pad) inside a 50 by 50 box (12.5% of the pad on both axes).
        var zigzag = new List<(double, double)>();
        for (int i = 0; i <= 4; i++) zigzag.Add((100 + (i % 2) * 50, 100 + i * 12.5));
        var result = Shapes.FromStroke(zigzag, 400);
        Assert.Equal("Draw bigger: this shape is too small to learn from.", result.Error);
        Assert.Null(result.Points);
        // Wide but flat is big enough on one axis.
        Assert.Null(Shapes.FromStroke(new[] { (50.0, 200.0), (350.0, 200.0) }, 400).Error);
    }

    [Fact]
    public void FromStroke_ResamplesEvenlyIntoTheSquare()
    {
        // A quarter circle of radius 150 around the pad's centre, drawn with 60 unevenly spaced points.
        var stroke = new List<(double X, double Y)>();
        for (int i = 0; i <= 60; i++)
        {
            double a = Math.PI / 2 * Math.Pow(i / 60.0, 1.5);
            stroke.Add((200 + 150 * Math.Cos(a), 200 - 150 * Math.Sin(a)));
        }
        var result = Shapes.FromStroke(stroke, 400);
        Assert.Null(result.Error);
        var points = result.Points!;
        Assert.Equal(1000, points.Count);
        Assert.Equal(0.75, points[0].X, 9);   // (350, 200) -> (0.75, 0)
        Assert.Equal(0.0, points[0].Y, 9);
        Assert.Equal(0.0, points[^1].X, 9);   // (200, 50) -> (0, 0.75): y is up
        Assert.Equal(0.75, points[^1].Y, 9);
        var gaps = points.Zip(points.Skip(1), (p, q) => Math.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Y - q.Y) * (p.Y - q.Y))).ToList();
        double mean = gaps.Average();
        Assert.All(gaps, g => Assert.InRange(g, mean * 0.99, mean * 1.01));
        Assert.All(points, p => Assert.InRange(p.X * p.X + p.Y * p.Y, 0.74 * 0.74, 0.751 * 0.751));
    }

    [Fact]
    public void FromStroke_KeepsPointsOutsideThePadOnItsEdge()
    {
        var result = Shapes.FromStroke(new[] { (-100.0, 200.0), (300.0, 200.0), (300.0, 500.0) }, 400);
        Assert.Null(result.Error);
        Assert.All(result.Points!, p => { Assert.InRange(p.X, -1, 1); Assert.InRange(p.Y, -1, 1); });
        Assert.Equal(-1, result.Points![0].X, 9);
        Assert.Equal(-1, result.Points![^1].Y, 9);
    }

    [Fact]
    public void MeanDistanceToShape_HandWorked()
    {
        var shape = new[] { (0.0, 0.0), (1.0, 0.0) };
        // Nearest shape points: (0,1) -> (0,0) at 1; (1,0) -> itself at 0; (0.5,0) -> either at 0.5; (4,4) -> (1,0) at 5.
        var samples = new[] { (0.0, 1.0), (1.0, 0.0), (0.5, 0.0), (4.0, 4.0) };
        Assert.Equal(6.5 / 4, Shapes.MeanDistanceToShape(samples, shape), 12);
        Assert.Equal(0, Shapes.MeanDistanceToShape(Preset("Spiral"), Preset("Spiral")));
        Assert.Throws<ArgumentException>(() => Shapes.MeanDistanceToShape(samples, Array.Empty<(double, double)>()));
    }

    [Fact]
    public void DiffusionTraining_SettingsFromTheSpeedCheck()
    {
        Assert.Equal(new DiffusionConfig(Hidden: 128), DiffusionTraining.Retrain);
        Assert.Equal(new DiffusionConfig(Hidden: 128), DiffusionTraining.Shipped);
        Assert.Equal(20_000, DiffusionTraining.RetrainSteps);
        Assert.Equal(60_000, DiffusionTraining.ShippedSteps);
    }

    [Fact]
    public void Coverage_HandWorked()
    {
        var shape = new[] { (0.0, 0.0), (1.0, 0.0), (2.0, 0.0), (3.0, 0.0) };
        var samples = new[] { (0.0, 0.04), (2.1, 0.0) };
        Assert.Equal(0.25, Shapes.Coverage(samples, shape, 0.05), 12);   // only (0,0) has a sample within 0.05
        Assert.Equal(0.5, Shapes.Coverage(samples, shape, 0.2), 12);     // (2,0) is 0.1 from a sample
        Assert.Equal(1.0, Shapes.Coverage(shape, shape, 0.05));
        Assert.Throws<ArgumentException>(() => Shapes.Coverage(Array.Empty<(double, double)>(), shape, 0.05));
        Assert.Throws<ArgumentOutOfRangeException>(() => Shapes.Coverage(samples, shape, 0));
    }

    [Fact]
    public void Coverage_IsLowForSamplesCollapsedToOnePoint()
    {
        // Every sample on one point of the circle: the mean distance is a perfect 0, but almost none of the circle is covered.
        var circle = Preset("Circle");
        var collapsed = Enumerable.Repeat(circle[0], 500).ToList();
        Assert.Equal(0, Shapes.MeanDistanceToShape(collapsed, circle));
        double coverage = Shapes.Coverage(collapsed, circle, 0.05);
        Assert.True(coverage < 0.03, $"coverage {coverage}");   // an arc of about 0.1 out of 5.03, so about 2%
        Assert.True(coverage > 0);
    }

    // ---------- Saving ----------

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var net = new DiffusionNet(new DiffusionConfig(Hidden: 6, TimeFeatures: 8), new SeededRandom(3));
        var schedule = new NoiseSchedule(50, 2e-4, 0.05).Respaced(10);
        var info = new ModelInfo("Diffusion", "Spiral", 400, null, 1, IsShipped: false,
            new Dictionary<string, double> { ["meanDistance"] = 0.04 });
        using var ms = new MemoryStream();
        net.Save(ms, schedule, "Spiral", info);
        ms.Position = 0;

        var (loaded, loadedSchedule, shapeName, loadedInfo) = DiffusionNet.Load(ms);

        Assert.Equal(net.Config, loaded.Config);
        Assert.Equal("Spiral", shapeName);
        Assert.Equal(info, loadedInfo);
        Assert.Equal(schedule.Steps, loadedSchedule.Steps);
        Assert.Equal(schedule.OriginalSteps, loadedSchedule.OriginalSteps);
        Assert.Equal(schedule.Timesteps, loadedSchedule.Timesteps);
        for (int i = 1; i <= schedule.Steps; i++) Assert.Equal(schedule.Beta(i), loadedSchedule.Beta(i));
        foreach (var (a, b) in net.Parameters().Zip(loaded.Parameters()))
        {
            Assert.Equal(a.Name, b.Name);
            Assert.Equal(a.Value.Shape, b.Value.Shape);
            Assert.Equal(a.Value.Data, b.Value.Data);
        }
        var points = Tensor.FromArray(new[] { 0.2, 0.3 }, 1, 2);
        Assert.Equal(net.Forward(points, new[] { 7 }, 50).Data, loaded.Forward(points, new[] { 7 }, 50).Data);
    }

    [Fact]
    public void Load_RefusesAnotherKindOrMismatchedParameters()
    {
        var net = new DiffusionNet(new DiffusionConfig(Hidden: 4), new SeededRandom(3));
        var info = new ModelInfo("Diffusion", "Circle", 1, null, 1, false);
        var parameters = net.Parameters().ToList();

        using (var other = new MemoryStream())
        {
            FamilyModelFile.Save(other, FamilyKinds.Memory, new { Hidden = 4 }, "abc", info, parameters);
            other.Position = 0;
            var e = Assert.Throws<InvalidDataException>(() => DiffusionNet.Load(other));
            Assert.Contains("memory network", e.Message);
        }
        using (var good = new MemoryStream())
        {
            net.Save(good, new NoiseSchedule(), "Circle", info);
            good.Position = 0;
            var config = System.Text.Json.Nodes.JsonNode.Parse(FamilyModelFile.Load(good).Config.GetRawText())!.AsObject();

            // The same weights under a config with another hidden size.
            var otherHidden = config.DeepClone().AsObject();
            otherHidden["hidden"] = 5;
            using var wrong = new MemoryStream();
            FamilyModelFile.Save(wrong, FamilyKinds.Diffusion, otherHidden, null, info, parameters);
            wrong.Position = 0;
            Assert.Throws<InvalidDataException>(() => DiffusionNet.Load(wrong));

            // Two parameters swapped.
            var reordered = parameters.ToList();
            (reordered[0], reordered[2]) = (reordered[2], reordered[0]);
            using var swapped = new MemoryStream();
            FamilyModelFile.Save(swapped, FamilyKinds.Diffusion, config, null, info, reordered);
            swapped.Position = 0;
            Assert.Throws<InvalidDataException>(() => DiffusionNet.Load(swapped));

            // A schedule that cannot be built.
            var badSchedule = config.DeepClone().AsObject();
            badSchedule["betaEnd"] = 1.5;
            using var bad = new MemoryStream();
            FamilyModelFile.Save(bad, FamilyKinds.Diffusion, badSchedule, null, info, parameters);
            bad.Position = 0;
            Assert.Throws<InvalidDataException>(() => DiffusionNet.Load(bad));

            // No shape name.
            var noName = config.DeepClone().AsObject();
            noName.Remove("shapeName");
            using var unnamed = new MemoryStream();
            FamilyModelFile.Save(unnamed, FamilyKinds.Diffusion, noName, null, info, parameters);
            unnamed.Position = 0;
            Assert.Throws<InvalidDataException>(() => DiffusionNet.Load(unnamed));
        }
        Assert.Throws<ArgumentException>(() => net.Save(new MemoryStream(), new NoiseSchedule(), " ", info));
    }
}

[Collection(TrainingCollection.Name)]
public class DiffusionLearningTests
{
    [Fact]
    public void Training_OnTheCircleLowersTheLossAndSamplesLandNearTheShape()
    {
        var circle = Shapes.Presets.Single(p => p.Name == "Circle").Points;
        var schedule = new NoiseSchedule();
        var net = new DiffusionNet(new DiffusionConfig(), new SeededRandom(1));
        var trainer = new DiffusionTrainer(net, schedule, circle, totalSteps: 400, seed: 1);
        var losses = new List<double>();
        while (trainer.StepsTaken < trainer.TotalSteps) losses.Add(trainer.Step());

        Assert.Equal(400, losses.Count);
        Assert.All(losses, l => Assert.True(double.IsFinite(l)));
        double first = losses.Take(20).Average(), last = losses.Skip(380).Average();
        Assert.True(last < 0.7 * first, $"first 20 steps {first:F3}, last 20 steps {last:F3}");
        Assert.Throws<InvalidOperationException>(() => trainer.Step());

        var frames = DiffusionSampler.Sample(net, schedule, 500, new SeededRandom(2));
        double noise = Shapes.MeanDistanceToShape(ToList(frames[0]), circle);
        double result = Shapes.MeanDistanceToShape(ToList(frames[^1]), circle);
        Assert.True(result < 0.5 * noise, $"pure noise {noise:F3}, samples {result:F3}");
    }

    private static List<(double X, double Y)> ToList(double[,] points)
    {
        var list = new List<(double X, double Y)>();
        for (int i = 0; i < points.GetLength(0); i++) list.Add((points[i, 0], points[i, 1]));
        return list;
    }
}
