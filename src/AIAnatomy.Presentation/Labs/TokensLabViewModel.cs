using AIAnatomy.Models.Tokenization;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIAnatomy.Presentation.Labs;

/// <summary>One token as a chip: its text (spaces shown as ␣), its id, and an optional line under it.</summary>
public sealed record TokenChip(string Text, int Id, bool IsUnknown, string? Detail = null);

/// <summary>
/// The same text cut up two ways: one token per character, and byte-pair-encoding pieces learned from the fables with
/// a chosen number of merges. The BPE is learned off the UI thread, once per merges value (results are kept).
/// </summary>
public sealed partial class TokensLabViewModel : LabViewModel
{
    public const string Id = "TokensLab";
    public const int DefaultMerges = 200;
    public const int MaxMerges = 500;
    public const string DefaultText = "The fox and the crow";
    public const string DefaultWord = "strawberry";
    public const string DefaultLetter = "r";
    private const int MaxChips = 400;
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(250);

    private readonly string _corpus;
    private readonly CharTokenizer _chars;
    private readonly TimeSpan _debounce;
    private readonly Dictionary<int, BpeTokenizer> _cache = new();
    private readonly object _cacheLock = new();
    private BpeTokenizer? _bpe;
    private int _bpeMerges = -1;
    private CancellationTokenSource? _training;
    private Task _ready = Task.CompletedTask;

    public TokensLabViewModel(Func<string> fablesText, TimeSpan? debounce = null)
    {
        ArgumentNullException.ThrowIfNull(fablesText);
        _corpus = fablesText();
        _chars = CharTokenizer.FromText(_corpus);
        _debounce = debounce ?? DefaultDebounce;
        ScheduleBpe();
    }

    public override string LabId => Id;

    [ObservableProperty] private string text = DefaultText;
    [ObservableProperty] private int merges = DefaultMerges;
    [ObservableProperty] private string word = DefaultWord;
    [ObservableProperty] private string letter = DefaultLetter;

    [ObservableProperty] private IReadOnlyList<TokenChip> characterChips = [];
    [ObservableProperty] private IReadOnlyList<TokenChip> bpeChips = [];
    [ObservableProperty] private IReadOnlyList<TokenChip> wordChips = [];
    [ObservableProperty] private string characterCountText = "";
    [ObservableProperty] private string bpeCountText = "";
    [ObservableProperty] private string wordSummary = "";
    [ObservableProperty] private string? message;
    [ObservableProperty] private string? bpeStatus;
    [ObservableProperty] private bool isBpeBusy;

    /// <summary>Completes when the BPE for the current merges value is ready and shown.</summary>
    public Task BpeReady => _ready;

    public override Task WorkCompleted => _ready;

    public int CharacterTokenCount => CharacterChips.Count;
    public int BpeTokenCount => BpeChips.Count;

    partial void OnTextChanged(string value) => Refresh();
    partial void OnWordChanged(string value) => Refresh();
    partial void OnLetterChanged(string value) => Refresh();

    partial void OnMergesChanged(int value)
    {
        if (value < 0 || value > MaxMerges) { Merges = Math.Clamp(value, 0, MaxMerges); return; }
        ScheduleBpe();
    }

    public override void Reset()
    {
        Text = DefaultText;
        Word = DefaultWord;
        Letter = DefaultLetter;
        Merges = DefaultMerges;
    }

    public override void Deactivate() => _training?.Cancel();

    private void ScheduleBpe()
    {
        _training?.Cancel();
        int merges = Merges;
        BpeTokenizer? ready;
        bool cached;
        lock (_cacheLock) cached = _cache.TryGetValue(merges, out ready);
        if (cached)
        {
            _ready = Task.CompletedTask;
            UseBpe(merges, ready!);
            return;
        }
        var cts = _training = new CancellationTokenSource();
        IsBpeBusy = true;
        BpeStatus = $"Learning {merges} merges from the fables…";
        Refresh();
        _ready = TrainAsync(merges, cts);
    }

    private async Task TrainAsync(int merges, CancellationTokenSource cts)
    {
        var token = cts.Token;
        try
        {
            if (_debounce > TimeSpan.Zero) await Task.Delay(_debounce, token);   // the slider may still be moving
            var trained = await Task.Run(() => BpeTokenizer.Train(_corpus, merges), CancellationToken.None);
            lock (_cacheLock) _cache[merges] = trained;
            if (!token.IsCancellationRequested) UseBpe(merges, trained);
        }
        catch (OperationCanceledException) { /* a newer merges value replaced this one */ }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Nobody awaits this task in the app, so the failure is logged here and shown as a plain line (final review M14).
            ErrorLog.ReportBackground(e);
            if (!token.IsCancellationRequested) { IsBpeBusy = false; BpeStatus = "The pieces could not be learned: " + e.Message; }
        }
    }

    private void UseBpe(int merges, BpeTokenizer tokenizer)
    {
        _bpe = tokenizer;
        _bpeMerges = merges;
        IsBpeBusy = false;
        BpeStatus = null;
        Refresh();
    }

    private void Refresh()
    {
        CharacterChips = Chips(_chars, Text, null);
        BpeChips = _bpe == null ? [] : Chips(_bpe, Text, null);
        Message = string.IsNullOrEmpty(Text) ? LabText.EmptyPrompt : null;
        // Counted as the tokenizer reads the text: Enter types CR LF, which is one new line (D10, final review I11).
        int chars = string.IsNullOrEmpty(Text) ? 0 : _chars.Encode(Text).Count;
        CharacterCountText = chars == 0 ? "" : $"{Plural(chars, "token")} (one per character)";
        string ratio = NumberText.ShortWithDecimals((double)chars / Math.Max(1, BpeChips.Count), 1);
        BpeCountText = _bpe == null || chars == 0 ? ""
            : $"{Plural(BpeChips.Count, "token")} with {Plural(_bpeMerges, "merge")}, about "
              + $"{ratio} {(ratio == "1" ? "character" : "characters")} per token";
        RefreshWord();
        // The first report of each screen is its baseline. The pieces wait for the first BPE, so its arrival is not a light-up.
        ReportScreen("characters", new object[] { CharacterChips.ToArray(), CharacterCountText });
        if (_bpe != null) ReportScreen("pieces", new object[] { BpeChips.ToArray(), BpeCountText });
        if (_bpe != null) ReportScreen("word", new object[] { WordChips.ToArray(), WordSummary });
    }

    private void RefreshWord()
    {
        string w = Word ?? "";
        if (w.Length == 0 || _bpe == null)
        {
            WordChips = [];
            WordSummary = w.Length == 0 ? "Type a word to see the pieces the BPE sees." : "";
            return;
        }
        string? letter = string.IsNullOrEmpty(Letter) ? null : Letter[..1];
        WordChips = Chips(_bpe, w, letter);
        int characters = LabText.CountScalars(w);
        int letters = w.EnumerateRunes().Count(System.Text.Rune.IsLetter);
        int pieces = WordChips.Count;
        // Only letters are called letters; spaces, digits and marks are characters (final review M9).
        string has = letters == characters
            ? Plural(letters, "letter")
            : $"{Plural(characters, "character")}, {NumberText.Short(letters)} of them {(letters == 1 ? "a letter" : "letters")}";
        var summary = $"“{w}” has {has}. With {Plural(_bpeMerges, "merge")} the BPE sees {Plural(pieces, "piece")}.";
        if (letter != null)
            summary += $" It contains the letter “{letter}” {Plural(CountLetter(w, letter), "time")}.";
        summary += pieces == characters
            ? letters == characters ? " Every piece is one letter, so the pieces are the letters." : " Every piece is one character."
            : " The model receives the pieces, not the letters: what is inside each piece has to be learned from the training text.";
        WordSummary = summary;
    }

    private static IReadOnlyList<TokenChip> Chips(ITokenizer tokenizer, string text, string? letter)
    {
        if (string.IsNullOrEmpty(text)) return [];
        return tokenizer.Encode(text).Take(MaxChips).Select(id =>
        {
            string piece = tokenizer.TokenText(id);
            string? detail = letter == null ? null : $"{CountLetter(piece, letter)} × {letter}";
            return new TokenChip(LabText.Visible(piece), id, id == tokenizer.UnknownId, detail);
        }).ToList();
    }

    private static int CountLetter(string text, string letter) =>
        text.EnumerateRunes().Count(r => string.Equals(r.ToString(), letter, StringComparison.OrdinalIgnoreCase));

    private static string Plural(int n, string noun) => $"{NumberText.Short(n)} {noun}{(n == 1 ? "" : "s")}";
}
