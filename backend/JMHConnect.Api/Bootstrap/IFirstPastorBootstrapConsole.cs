namespace JMHConnect.Api.Bootstrap;

public interface IFirstPastorBootstrapConsole
{
    bool IsInputRedirected { get; }

    void Write(string value);

    void WriteLine(string value = "");

    string? ReadLine();

    string ReadPassword(bool showPassword);
}

public sealed class SystemFirstPastorBootstrapConsole : IFirstPastorBootstrapConsole
{
    public bool IsInputRedirected => Console.IsInputRedirected;

    public void Write(string value) => Console.Write(value);

    public void WriteLine(string value = "") => Console.WriteLine(value);

    public string? ReadLine() => Console.ReadLine();

    public string ReadPassword(bool showPassword)
    {
        if (showPassword)
        {
            return Console.ReadLine() ?? string.Empty;
        }

        var value = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (value.Length > 0)
                {
                    value.Length--;
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                value.Append(key.KeyChar);
            }
        }

        return value.ToString();
    }
}
