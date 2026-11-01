using System;
using System.Globalization;

var balance = new PaymentBalance("Aigerim", 1000m);
var inputs = new[] { "250", "abc", "-5", "99999" };

Console.WriteLine("=== Task 1: Original exception-based workflow ===");
foreach (var input in inputs)
    Console.WriteLine($"{input,-6} -> {LegacyWorkflow.Process(balance, input)}");

Console.WriteLine();
Console.WriteLine("=== Tasks 2-4: Functional Result<T> workflow (Bind + Map, no try/catch) ===");
foreach (var input in inputs)
    Console.WriteLine($"{input,-6} -> {FunctionalWorkflow.Process(balance, input)}");


public record PaymentBalance(string Owner, decimal Amount);

// Task 2: Result<T> as an abstract record with two sealed derived records
public abstract record Result<T>;
public sealed record Ok<T>(T Value) : Result<T>;
public sealed record Fail<T>(string Error) : Result<T>;

public static class ResultExtensions
{
    public static Result<U> Bind<T, U>(this Result<T> result, Func<T, Result<U>> next)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(next);

        return result switch
        {
            Ok<T>(var value) => next(value),
            Fail<T>(var error) => new Fail<U>(error),
            _ => throw new InvalidOperationException($"Unexpected Result case: {result.GetType().Name}")
        };
    }

    public static Result<U> Map<T, U>(this Result<T> result, Func<T, U> mapper)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(mapper);

        return result switch
        {
            Ok<T>(var value) => new Ok<U>(mapper(value)),
            Fail<T>(var error) => new Fail<U>(error),
            _ => throw new InvalidOperationException($"Unexpected Result case: {result.GetType().Name}")
        };
    }
}

public static class LegacyWorkflow
{
    static decimal ParseAmount(string text) => decimal.Parse(text, CultureInfo.InvariantCulture);

    static void EnsurePositive(decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive.");
    }

    static PaymentBalance Withdraw(PaymentBalance balance, decimal amount)
    {
        if (amount > balance.Amount) throw new InvalidOperationException("Insufficient funds.");
        return balance with { Amount = balance.Amount - amount };
    }

    public static string Process(PaymentBalance balance, string input)
    {
        try
        {
            var amount = ParseAmount(input);
            EnsurePositive(amount);
            var after = Withdraw(balance, amount);
            return $"OK: balance is now {after.Amount.ToString("F2", CultureInfo.InvariantCulture)}";
        }
        catch (Exception ex)
        {
            return $"ERROR ({ex.GetType().Name}): {ex.Message}";
        }
    }
}

public static class FunctionalWorkflow
{
    // Task 3: Rewritten functions returning Result<...> without throwing
    public static Result<decimal> ParseAmount(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? new Ok<decimal>(amount)
            : new Fail<decimal>($"The input string '{text}' was not in a correct format.");
    }

    public static Result<decimal> EnsurePositive(decimal amount) =>
        amount > 0
            ? new Ok<decimal>(amount)
            : new Fail<decimal>("Amount must be positive.");

    public static Result<PaymentBalance> Withdraw(PaymentBalance balance, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(balance);

        return amount <= balance.Amount
            ? new Ok<PaymentBalance>(balance with { Amount = balance.Amount - amount })
            : new Fail<PaymentBalance>("Insufficient funds.");
    }

    // Tasks 3 & 4: Chained with Bind + Map, handled with switch expression, no try/catch
    public static string Process(PaymentBalance balance, string input)
    {
        ArgumentNullException.ThrowIfNull(balance);
        ArgumentNullException.ThrowIfNull(input);

        var result = ParseAmount(input)
            .Bind(EnsurePositive)
            .Bind(amount => Withdraw(balance, amount))
            .Map(after => after.Amount); // Task 4: infallible step extracting the new amount

        // Note: With only Ok<decimal> and Fail<decimal>, the compiler emits CS8509:
        // "The switch expression does not handle all possible values of its input type (it is not exhaustive).
        // For example, the pattern '_' is not covered."
        return result switch
        {
            Ok<decimal>(var newAmount) => $"OK: balance is now {newAmount.ToString("F2", CultureInfo.InvariantCulture)}",
            Fail<decimal>(var error) => $"ERROR: {error}",
            _ => throw new InvalidOperationException($"Unexpected Result case: {result.GetType().Name}")
        };
    }
}
