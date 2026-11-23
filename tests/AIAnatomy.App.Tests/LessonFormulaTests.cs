using System.Windows.Media;
using AIAnatomy.Lessons;
using AIAnatomy.Lessons.Markdown;
using WpfMath.Parsers;
using WpfMath.Rendering;
using XamlMath;

namespace AIAnatomy.App.Tests;

public class LessonFormulaTests
{
    private static readonly string Content = System.IO.Path.Combine(AppContext.BaseDirectory, "content");

    /// <summary>
    /// Every $$ formula in every lesson is parsed by the same parser the app uses (MarkdownView), then drawn to a
    /// bitmap, so a formula that would fall back to "shown as written" fails here instead.
    /// </summary>
    [Fact]
    public void EveryFormulaParsesInWpfMath()
    {
        var formulas = LessonCatalog.Load(Content).All
            .SelectMany(l => new[] { l.Explanation, l.Formula, l.BrainsAndModels }
                .Where(d => d != null)
                .SelectMany(d => d!.Blocks.OfType<FormulaBlock>())
                .Select(f => (l.Id, f.Latex)))
            .ToList();
        Assert.NotEmpty(formulas);

        var problems = new List<string>();
        RunOnSta(() =>
        {
            foreach (var (id, latex) in formulas)
            {
                try
                {
                    var formula = WpfTeXFormulaParser.Instance.Parse(latex);
                    var environment = WpfTeXEnvironment.Create(TexStyle.Display, 20, "Arial", Brushes.Black, Brushes.White);
                    var bitmap = formula.RenderToBitmap(environment, 1, 0, 0, 96);
                    if (bitmap.PixelWidth == 0 || bitmap.PixelHeight == 0) problems.Add($"{id}: drew nothing for {latex}");
                }
                catch (Exception e)
                {
                    problems.Add($"{id}: {e.GetType().Name} {e.Message} in {latex}");
                }
            }
        });
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    // WPF drawing objects must be created on a single-threaded-apartment thread.
    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception e) { failure = e; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw new InvalidOperationException("The STA work failed.", failure);
    }
}
