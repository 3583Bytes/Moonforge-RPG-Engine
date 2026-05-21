using Moonforge.Core.Runtime.Formulas;

namespace Moonforge.Core.Tests;

public sealed class ExpressionFormulaEvaluatorTests
{
    [Theory]
    [InlineData("1 + 2", 3)]
    [InlineData("2 * 3 + 4", 10)]
    [InlineData("2 + 3 * 4", 14)]
    [InlineData("(2 + 3) * 4", 20)]
    [InlineData("10 - 4 - 2", 4)]
    [InlineData("20 / 4", 5)]
    [InlineData("-5 + 10", 5)]
    [InlineData("+7", 7)]
    [InlineData("2.5 * 4", 10)]
    [InlineData("vit + (level - 1) * 4", 50)] // vit=34, level=5 → 34 + 16
    public void Evaluate_AppliesOperatorPrecedence(string expression, double expected)
    {
        ExpressionFormulaEvaluator evaluator = new();
        Dictionary<string, double> vars = new() { ["vit"] = 34, ["level"] = 5 };
        Assert.Equal(expected, evaluator.Evaluate(expression, vars));
    }

    [Fact]
    public void Evaluate_UnknownIdentifier_TreatsAsZero()
    {
        ExpressionFormulaEvaluator evaluator = new();
        Assert.Equal(7, evaluator.Evaluate("missing + 7", new Dictionary<string, double>()));
    }

    [Fact]
    public void Evaluate_DivisionByZero_ReturnsZeroInsteadOfThrowing()
    {
        ExpressionFormulaEvaluator evaluator = new();
        Assert.Equal(0, evaluator.Evaluate("10 / 0", new Dictionary<string, double>()));
    }

    [Fact]
    public void Evaluate_EmptyOrWhitespace_ReturnsZero()
    {
        ExpressionFormulaEvaluator evaluator = new();
        Assert.Equal(0, evaluator.Evaluate("", new Dictionary<string, double>()));
        Assert.Equal(0, evaluator.Evaluate("   ", new Dictionary<string, double>()));
    }

    [Theory]
    [InlineData("(1 + 2")] // unbalanced paren
    [InlineData("1 +")] // dangling operator
    [InlineData("1 2")] // trailing junk
    [InlineData("@")] // unexpected character
    public void Evaluate_Malformed_Throws(string expression)
    {
        ExpressionFormulaEvaluator evaluator = new();
        Assert.Throws<FormatException>(() => evaluator.Evaluate(expression, new Dictionary<string, double>()));
    }
}
