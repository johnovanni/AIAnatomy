namespace AIAnatomy.Lessons;

/// <summary>Parses the lines under "## Check yourself". Errors are InvalidDataException naming the question number.</summary>
public static class QuestionParser
{
    public static IReadOnlyList<Question> Parse(IReadOnlyList<string> lines, IReadOnlySet<string> anchors)
    {
        var result = new List<Question>();
        List<string>? block = null;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith('?'))
            {
                if (block != null) result.Add(ParseOne(block, result.Count + 1, anchors));
                block = [line];
            }
            else if (line.Length == 0) continue;
            else if (block == null) throw new InvalidDataException($"Check yourself: text '{line}' comes before the first '? ' question.");
            else block.Add(line);
        }
        if (block != null) result.Add(ParseOne(block, result.Count + 1, anchors));
        return result;
    }

    private static Question ParseOne(List<string> block, int number, IReadOnlySet<string> anchors)
    {
        string Fail(string message) => $"question {number}: {message}";

        var prompt = block[0][1..].Trim();
        if (prompt.Length == 0) throw new InvalidDataException(Fail("the question has no text."));

        var answers = new List<string>();
        int correct = -1;
        string? why = null, see = null, checkedBy = null;
        foreach (var line in block.Skip(1))
        {
            if (line.StartsWith("why:", StringComparison.OrdinalIgnoreCase)) why = line[4..].Trim();
            else if (line.StartsWith("see:", StringComparison.OrdinalIgnoreCase)) see = line[4..].Trim();
            else if (line.StartsWith("checked-by-code:", StringComparison.OrdinalIgnoreCase)) checkedBy = line[16..].Trim();
            else if (line.StartsWith('-') || line.StartsWith('*'))
            {
                if (line[0] == '*')
                {
                    if (correct >= 0) throw new InvalidDataException(Fail("more than one answer is marked correct with '*'."));
                    correct = answers.Count;
                }
                answers.Add(line[1..].Trim());
            }
            else throw new InvalidDataException(Fail($"cannot read the line '{line}'."));
        }

        if (answers.Count < 2) throw new InvalidDataException(Fail("needs at least two answers."));
        if (answers.Any(a => a.Length == 0)) throw new InvalidDataException(Fail("an answer is empty."));
        if (correct < 0) throw new InvalidDataException(Fail("no answer is marked correct with '*'."));
        if (string.IsNullOrEmpty(why)) throw new InvalidDataException(Fail("is missing 'why:'."));
        if (string.IsNullOrEmpty(see)) throw new InvalidDataException(Fail("is missing 'see:'."));
        if (!anchors.Contains(see))
            throw new InvalidDataException(Fail($"'see: {see}' is not a known anchor (use {string.Join(", ", anchors.Order())})."));
        return new Question(prompt, answers, correct, why, see, string.IsNullOrEmpty(checkedBy) ? null : checkedBy);
    }
}
