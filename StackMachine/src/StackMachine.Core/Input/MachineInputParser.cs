using System.Globalization;

namespace StackMachine.Core.Input;

public static class MachineInputParser
{
    public static IReadOnlyList<int> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<int>();
        }

        string[] tokens = text.Split(',');
        var values = new List<int>(tokens.Length);

        for (int index = 0; index < tokens.Length; index++)
        {
            string token = tokens[index].Trim();

            if (!int.TryParse(
                    token,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out int value))
            {
                throw new MachineInputException(
                    $"Input item {index}: expected a 32-bit integer, "
                    + $"got \"{token}\".");
            }

            values.Add(value);
        }

        return values.ToArray();
    }
}