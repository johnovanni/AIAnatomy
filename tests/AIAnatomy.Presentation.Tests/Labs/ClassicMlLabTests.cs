using AIAnatomy.Models.ClassicMl;
using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.Presentation.Tests.Labs;

public class ClassicMlLabTests
{
    private static IReadOnlyList<LabeledPoint> Starter(string name) => StarterSets.All.Single(s => s.Name == name).Points;

    private const string Default = "Two groups with odd points";

    /// <summary>Clears the lab and adds the four-point set: class A on the left (x = 2), class B on the right (x = 4).</summary>
    private static ClassicMlLabViewModel FourPoints()
    {
        var lab = new ClassicMlLabViewModel();
        lab.Clear();
        lab.NewPointLabel = 0;
        Assert.True(lab.AddPoint(2, 2));
        Assert.True(lab.AddPoint(2, 5));
        lab.NewPointLabel = 1;
        Assert.True(lab.AddPoint(4, 2));
        Assert.True(lab.AddPoint(4, 5));
        return lab;
    }

    [Fact]
    public void Opens_OnTheDefaultSet_WithADecisionTreeOfDepth3()
    {
        var lab = new ClassicMlLabViewModel();
        Assert.Equal(ClassicMlLabViewModel.Id, lab.LabId);
        Assert.Equal("ClassicMlLab", lab.LabId);
        Assert.Equal(StarterSets.All.Select(s => s.Name), lab.StarterNames);
        Assert.Equal(Starter(Default), lab.Points);
        Assert.Equal(Default, lab.StarterName);
        Assert.Equal(ClassicMethod.DecisionTree, lab.Method);
        Assert.Equal(3, lab.Depth);
        Assert.Equal(3, lab.K);
        Assert.Equal(0, lab.NewPointLabel);
        Assert.Null(lab.Message);
        Assert.NotEmpty(lab.Regions);
        Assert.NotEmpty(lab.SplitLines);
        Assert.NotEmpty(lab.Questions);
        Assert.StartsWith("Is ", lab.Questions[0]);
    }

    [Fact]
    public void TreeRegions_AreTheModelsLeafRectangles_AndAccuracyIsCountedFromTheModel()
    {
        var lab = new ClassicMlLabViewModel();
        var points = Starter(Default);
        var tree = DecisionTree.Grow(points, 3);
        Assert.Equal(DecisionTree.Regions(tree, 0, 10, 0, 10), lab.Regions);
        int right = points.Count(p => DecisionTree.Predict(tree, p.X, p.Y) == p.Label);
        Assert.Equal($"Right on {right} of 27 training points ({100 * right / 27}%).", lab.AccuracyText);
        Assert.True(right < 27);   // depth 3 does not carve round every odd point
    }

    [Fact]
    public void Depth10_OnTheDefaultSet_GetsEveryTrainingPointRight()
    {
        var lab = new ClassicMlLabViewModel { Depth = 10 };
        Assert.Equal("Right on 27 of 27 training points (100%).", lab.AccuracyText);
    }

    [Fact]
    public void NearestNeighbours_K1GetsEveryTrainingPointRight_K15DoesNot()
    {
        var lab = new ClassicMlLabViewModel { Method = ClassicMethod.NearestNeighbours, K = 1 };
        Assert.Equal("Right on 27 of 27 training points (100%).", lab.AccuracyText);
        lab.K = 15;
        Assert.StartsWith("Right on ", lab.AccuracyText);
        Assert.NotEqual("Right on 27 of 27 training points (100%).", lab.AccuracyText);
    }

    [Fact]
    public void NeighbourRegions_AreAGridOfSquares_EachShadedByTheVoteAtItsCentre()
    {
        var lab = new ClassicMlLabViewModel { Method = ClassicMethod.NearestNeighbours, K = 3 };
        int n = ClassicMlLabViewModel.GridCells;
        Assert.Equal(n * n, lab.Regions.Count);
        Assert.Empty(lab.SplitLines);
        double side = (ClassicMlLabViewModel.PlotMax - ClassicMlLabViewModel.PlotMin) / n;
        var points = Starter(Default);
        foreach (var r in lab.Regions)
        {
            Assert.Equal(side, r.XMax - r.XMin, 9);
            Assert.Equal(side, r.YMax - r.YMin, 9);
            Assert.Equal(NearestNeighbours.Classify(points, (r.XMin + r.XMax) / 2, (r.YMin + r.YMax) / 2, 3), r.Label);
        }
        Assert.Equal(n * n * side * side, lab.Regions.Sum(r => (r.XMax - r.XMin) * (r.YMax - r.YMin)), 6);   // the grid covers the plot
    }

    [Fact]
    public void Questions_ForTheFourPointSet_AreOneQuestionAndTwoLeaves()
    {
        var lab = FourPoints();
        Assert.Equal(["Is x < 3?", "  yes → class A", "  no → class B"], lab.Questions);
        var line = Assert.Single(lab.SplitLines);
        Assert.Equal((Axis.X, 3.0, 0.0, 10.0), line);
        Assert.Equal("Right on 4 of 4 training points (100%).", lab.AccuracyText);
    }

    [Fact]
    public void Questions_NestDeeperQuestionsTwoSpacesPerLevel()
    {
        var lab = FourPoints();
        lab.NewPointLabel = 2;
        Assert.True(lab.AddPoint(4, 8));   // class C above the class B points: the right-hand side needs a second question
        lab.Depth = 3;
        Assert.Equal(["Is x < 3?", "  yes → class A", "  no: Is y < 6.5?", "    yes → class B", "    no → class C"], lab.Questions);
        Assert.Equal(2, lab.SplitLines.Count);
        // The second split runs only across the right-hand side of the first: from x = 3 to the plot's edge.
        Assert.Equal((Axis.Y, 6.5, 3.0, 10.0), lab.SplitLines[1]);
    }

    [Fact]
    public void Questions_ShowThresholdsWithoutTrailingZeros()
    {
        var lab = new ClassicMlLabViewModel();
        lab.Clear();
        lab.AddPoint(2.6, 1);
        lab.NewPointLabel = 1;
        lab.AddPoint(3.3, 1);
        Assert.Equal("Is x < 2.95?", lab.Questions[0]);
        lab.Reset();
        Assert.Equal("Is x < 5.05?", lab.Questions[0]);
        Assert.Equal("  yes: Is x < 2.45?", lab.Questions[1]);
    }

    [Theory]
    [InlineData(3.0, "3")]
    [InlineData(6.5, "6.5")]
    [InlineData(2.9499999999999997, "2.95")]
    [InlineData(2.9500000000000002, "2.95")]
    [InlineData(1.875, "1.875")]
    [InlineData(0.05, "0.05")]
    public void ThresholdText_HasNoTrailingZeros(double value, string text) =>
        Assert.Equal(text, ClassicMlLabViewModel.ThresholdText(value));

    [Fact]
    public void InNeighboursMode_ThereAreNoQuestions_AndTheNoteSaysItComparesDistances()
    {
        var lab = new ClassicMlLabViewModel { Method = ClassicMethod.NearestNeighbours };
        Assert.Empty(lab.Questions);
        Assert.Contains("asks no questions", lab.QuestionsNote);
        Assert.Contains("distances", lab.QuestionsNote);
        lab.Method = ClassicMethod.DecisionTree;
        Assert.NotEmpty(lab.Questions);
        Assert.DoesNotContain("no questions", lab.QuestionsNote);
    }

    [Fact]
    public void SwitchingMethod_MovingASlider_AddingAndRemovingAPoint_AllRecompute()
    {
        var lab = new ClassicMlLabViewModel();
        var changed = new List<string>();
        lab.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        var tree3 = lab.Regions;
        lab.Depth = 1;
        Assert.NotEqual(tree3, lab.Regions);
        Assert.Single(lab.SplitLines);
        Assert.Contains(nameof(lab.Regions), changed);

        lab.Method = ClassicMethod.NearestNeighbours;
        Assert.Equal(ClassicMlLabViewModel.GridCells * ClassicMlLabViewModel.GridCells, lab.Regions.Count);

        var k3 = lab.Regions.Select(r => r.Label).ToArray();
        lab.K = 15;
        Assert.NotEqual(k3, lab.Regions.Select(r => r.Label).ToArray());

        string before = lab.AccuracyText;
        lab.NewPointLabel = 1;
        Assert.True(lab.AddPoint(1, 1));   // a class B point deep in class A's corner
        Assert.Equal(28, lab.Points.Count);
        Assert.StartsWith("Right on ", lab.AccuracyText);
        Assert.Contains(" of 28 training points (", lab.AccuracyText);
        Assert.NotEqual(before, lab.AccuracyText);

        Assert.True(lab.RemoveNearest(1.1, 0.9));
        Assert.Equal(27, lab.Points.Count);
        Assert.Equal(before, lab.AccuracyText);
    }

    [Fact]
    public void AddPoint_IsClampedToThePlot_AndRoundedToOneDecimal_WithTheChosenClass()
    {
        var lab = new ClassicMlLabViewModel();
        lab.Clear();
        lab.NewPointLabel = 2;
        Assert.True(lab.AddPoint(-3, 12));
        Assert.True(lab.AddPoint(2.54, 3.01));
        Assert.Equal([new LabeledPoint(0, 10, 2), new LabeledPoint(2.5, 3, 2)], lab.Points);
        Assert.Equal("Added class C at (2.5, 3).", lab.ActionText);
    }

    [Fact]
    public void NewPointLabel_IsKeptToTheThreeClasses_AndSlidersToTheirRanges()
    {
        var lab = new ClassicMlLabViewModel();
        lab.NewPointLabel = 7;
        Assert.Equal(2, lab.NewPointLabel);
        lab.NewPointLabel = -1;
        Assert.Equal(0, lab.NewPointLabel);
        lab.Depth = 50;
        Assert.Equal(ClassicMlLabViewModel.MaxDepth, lab.Depth);
        lab.Depth = -2;
        Assert.Equal(1, lab.Depth);
        lab.K = 99;
        Assert.Equal(ClassicMlLabViewModel.MaxK, lab.K);
        lab.K = 0;
        Assert.Equal(1, lab.K);
    }

    [Fact]
    public void RemoveNearest_RemovesOnlyAPointWithin0Point3()
    {
        var lab = FourPoints();
        Assert.False(lab.RemoveNearest(3, 3));   // nothing near the middle
        Assert.Equal(4, lab.Points.Count);
        Assert.False(lab.RemoveNearest(2.25, 2.25));   // 0.35 away
        Assert.True(lab.RemoveNearest(4.2, 5.1));
        Assert.Equal([new LabeledPoint(2, 2, 0), new LabeledPoint(2, 5, 0), new LabeledPoint(4, 2, 1)], lab.Points);
        Assert.Equal("Removed class B at (4, 5).", lab.ActionText);
    }

    [Fact]
    public void DeleteText_NamesThePointDeleteWouldRemove()
    {
        var lab = FourPoints();
        Assert.Equal(new LabeledPoint(4, 5, 1), ClassicMlLabViewModel.NearestWithinReach(lab.Points, 4.2, 5.1));
        Assert.Equal("Delete removes the class B point at (4, 5).", ClassicMlLabViewModel.DeleteText(lab.Points, 4.2, 5.1));
        Assert.Null(ClassicMlLabViewModel.NearestWithinReach(lab.Points, 3, 3));
        Assert.Equal("No point is within reach of Delete.", ClassicMlLabViewModel.DeleteText(lab.Points, 3, 3));
        // What Delete names is what RemoveNearest removes.
        Assert.True(lab.RemoveNearest(4.2, 5.1));
        Assert.DoesNotContain(new LabeledPoint(4, 5, 1), lab.Points);
    }

    [Fact]
    public void RemoveNearest_TakesTheNearestOfSeveral()
    {
        var lab = new ClassicMlLabViewModel();
        lab.Clear();
        lab.AddPoint(5, 5);
        lab.NewPointLabel = 1;
        lab.AddPoint(5.2, 5);
        Assert.True(lab.RemoveNearest(5.15, 5));
        Assert.Equal([new LabeledPoint(5, 5, 0)], lab.Points);
    }

    // ---- review focus 3: the edges ----

    [Fact]
    public void EmptyPlot_GivesTheMessage_AndNoRegions()
    {
        var lab = new ClassicMlLabViewModel();
        lab.Clear();
        Assert.Empty(lab.Points);
        Assert.Equal("Add points of at least one class to see regions.", lab.Message);
        Assert.Empty(lab.Regions);
        Assert.Empty(lab.SplitLines);
        Assert.Empty(lab.Questions);
        Assert.Equal("", lab.AccuracyText);
        lab.Method = ClassicMethod.NearestNeighbours;
        Assert.Empty(lab.Regions);
        Assert.Equal("Add points of at least one class to see regions.", lab.Message);
    }

    [Theory]
    [InlineData(ClassicMethod.DecisionTree)]
    [InlineData(ClassicMethod.NearestNeighbours)]
    public void OnePoint_ShadesEverythingItsClass(ClassicMethod method)
    {
        var lab = new ClassicMlLabViewModel { Method = method };
        lab.Clear();
        lab.NewPointLabel = 1;
        Assert.True(lab.AddPoint(4, 6));
        Assert.Null(lab.Message);
        Assert.NotEmpty(lab.Regions);
        Assert.All(lab.Regions, r => Assert.Equal(1, r.Label));
        Assert.Equal(100, lab.Regions.Sum(r => (r.XMax - r.XMin) * (r.YMax - r.YMin)), 6);
        Assert.Empty(lab.SplitLines);
        Assert.Equal("Right on 1 of 1 training point (100%).", lab.AccuracyText);
        if (method == ClassicMethod.DecisionTree) Assert.Equal(["No questions: everywhere → class B"], lab.Questions);
    }

    [Fact]
    public void The201stPoint_IsRefused_WithAMessage()
    {
        var lab = new ClassicMlLabViewModel();
        lab.Clear();
        for (int i = 0; i < ClassicMlLabViewModel.MaxPoints; i++)
        {
            lab.NewPointLabel = i % 3;
            Assert.True(lab.AddPoint(i % 20 * 0.5, i / 20 * 0.5));
        }
        Assert.Equal(200, lab.Points.Count);
        Assert.Null(lab.Message);
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => raised.Add(e.Screen);
        Assert.False(lab.AddPoint(9.9, 9.9));
        Assert.Equal(200, lab.Points.Count);
        Assert.Equal("The plot holds at most 200 points.", lab.Message);
        Assert.Equal(["regions"], raised);   // the refusal shows on the regions screen, so it lights
        raised.Clear();
        Assert.False(lab.AddPoint(9.9, 9.9));
        Assert.Empty(raised);   // the same refusal again changes nothing on screen
        Assert.True(lab.RemoveNearest(0, 0));   // room again: the message goes
        Assert.Null(lab.Message);
        Assert.True(lab.AddPoint(9.9, 9.9));
    }

    [Fact]
    public void LoadStarter_LoadsEachSet_AndRejectsAnUnknownName()
    {
        var lab = new ClassicMlLabViewModel();
        foreach (var (name, points) in StarterSets.All)
        {
            lab.LoadStarter(name);
            Assert.Equal(points, lab.Points);
            Assert.Equal(name, lab.StarterName);
        }
        lab.LoadStarterCommand.Execute("Corners (XOR)");
        Assert.Equal(Starter("Corners (XOR)"), lab.Points);
        Assert.Throws<ArgumentException>(() => lab.LoadStarter("Nonsense"));
    }

    [Fact]
    public void Reset_RestoresTheDefaults()
    {
        var lab = new ClassicMlLabViewModel();
        var fresh = new ClassicMlLabViewModel();
        lab.LoadStarter("Three groups");
        lab.Method = ClassicMethod.NearestNeighbours;
        lab.Depth = 8;
        lab.K = 11;
        lab.NewPointLabel = 2;
        lab.AddPoint(1, 1);
        lab.Reset();
        Assert.Equal(Starter(Default), lab.Points);
        Assert.Equal(ClassicMethod.DecisionTree, lab.Method);
        Assert.Equal(3, lab.Depth);
        Assert.Equal(3, lab.K);
        Assert.Equal(0, lab.NewPointLabel);
        Assert.Null(lab.Message);
        Assert.Equal("", lab.ActionText);
        Assert.Equal(fresh.Regions, lab.Regions);
        Assert.Equal(fresh.Questions, lab.Questions);
        Assert.Equal(fresh.AccuracyText, lab.AccuracyText);
    }

    [Fact]
    public void Screens_AreBaselines_AndLightOnceOnASliderChange()
    {
        var lab = new ClassicMlLabViewModel();
        Assert.Equal(["regions", "questions"], lab.ReportedScreens);
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => raised.Add(e.Screen);

        lab.Depth = 1;
        Assert.Equal(["regions", "questions"], raised);
        raised.Clear();
        lab.Depth = 1;
        Assert.Empty(raised);
        lab.K = 7;   // the tree does not use k
        Assert.Empty(raised);

        lab.Method = ClassicMethod.NearestNeighbours;
        Assert.Equal(["regions", "questions"], raised);
        raised.Clear();
        lab.K = 1;
        Assert.Equal(["regions"], raised);   // nearest neighbours asks no questions, whatever k is
        raised.Clear();

        lab.LoadStarter("Three groups");   // one load lights once, not once per point
        Assert.Equal(["regions"], raised);
    }

    [Fact]
    public void PointText_NamesTheClassAndPlace()
    {
        Assert.Equal("class A at (2.5, 3)", ClassicMlLabViewModel.PointText(new LabeledPoint(2.5, 3, 0)));
        Assert.Equal("class C at (0, 10)", ClassicMlLabViewModel.PointText(new LabeledPoint(0, 10, 2)));
        Assert.Equal(["A", "B", "C"], Enumerable.Range(0, 3).Select(ClassicMlLabViewModel.ClassName));
    }

    // ---- the lesson's Try this, as the lab shows it ----

    [Fact]
    public void TryThis_Depth3MissesAnOddPoint_Depth10GetsAll27()
    {
        var lab = new ClassicMlLabViewModel();
        Assert.Equal("Right on 26 of 27 training points (96%).", lab.AccuracyText);
        lab.Depth = 10;
        Assert.Equal("Right on 27 of 27 training points (100%).", lab.AccuracyText);
    }

    [Fact]
    public void TryThis_K1CarvesRoundTheOddPoints_K9DoesNot()
    {
        var lab = new ClassicMlLabViewModel { Method = ClassicMethod.NearestNeighbours, K = 1 };
        // Squares of class B in class A's corner (below and left of the middle), and of class A in class B's corner.
        int Odd() => lab.Regions.Count(r => r.XMax <= 5 && r.YMax <= 5 && r.Label == 1) + lab.Regions.Count(r => r.XMin >= 5 && r.YMin >= 5 && r.Label == 0);
        Assert.True(Odd() > 0);
        lab.K = 9;
        Assert.Equal(0, Odd());
    }

    [Fact]
    public void TryThis_CornersNeedsDepth5_ThoughTwoLevelsOfWellChosenQuestionsWouldDo()
    {
        var lab = new ClassicMlLabViewModel();
        lab.LoadStarter("Corners (XOR)");
        lab.Depth = 2;
        Assert.Equal("Right on 17 of 28 training points (60%).", lab.AccuracyText);
        lab.Depth = 5;
        Assert.Equal("Right on 28 of 28 training points (100%).", lab.AccuracyText);
        // x < 5 and then y < 5 on each side: two levels of questions classify every corner point.
        Assert.All(Starter("Corners (XOR)"), p => Assert.Equal(p.Label, (p.X < 5) == (p.Y < 5) ? 0 : 1));
    }
}
