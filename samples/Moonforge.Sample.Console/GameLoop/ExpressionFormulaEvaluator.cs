using Moonforge.Core.Runtime.Formulas;

namespace Moonforge.Sample.ConsoleApp.GameLoop;

/// <summary>
/// Small recursive-descent evaluator for arithmetic stat formulas. Supports +, -, *, /,
/// parentheses, integer/decimal literals, and identifiers resolved against the variable
/// dictionary supplied by <see cref="IFormulaEvaluator"/>. Unknown identifiers evaluate
/// to 0 so a formula referencing a stat the actor doesn't have stored behaves sensibly.
/// </summary>
internal sealed class ExpressionFormulaEvaluator : IFormulaEvaluator
{
    public double Evaluate(string expression, IReadOnlyDictionary<string, double> variables)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return 0;
        }

        Parser parser = new(expression, variables);
        double result = parser.ParseExpression();
        parser.ExpectEnd();
        return result;
    }

    private ref struct Parser
    {
        private readonly string _source;
        private readonly IReadOnlyDictionary<string, double> _variables;
        private int _position;

        public Parser(string source, IReadOnlyDictionary<string, double> variables)
        {
            _source = source;
            _variables = variables;
            _position = 0;
        }

        public double ParseExpression()
        {
            double left = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (Match('+'))
                {
                    left += ParseTerm();
                }
                else if (Match('-'))
                {
                    left -= ParseTerm();
                }
                else
                {
                    return left;
                }
            }
        }

        private double ParseTerm()
        {
            double left = ParseFactor();
            while (true)
            {
                SkipWhitespace();
                if (Match('*'))
                {
                    left *= ParseFactor();
                }
                else if (Match('/'))
                {
                    double divisor = ParseFactor();
                    left = divisor == 0 ? 0 : left / divisor;
                }
                else
                {
                    return left;
                }
            }
        }

        private double ParseFactor()
        {
            SkipWhitespace();
            if (Match('+'))
            {
                return ParseFactor();
            }

            if (Match('-'))
            {
                return -ParseFactor();
            }

            if (Match('('))
            {
                double inner = ParseExpression();
                SkipWhitespace();
                if (!Match(')'))
                {
                    throw new FormatException($"Expected ')' at position {_position} in '{_source}'.");
                }

                return inner;
            }

            if (_position < _source.Length && (char.IsDigit(_source[_position]) || _source[_position] == '.'))
            {
                return ParseNumber();
            }

            if (_position < _source.Length && IsIdentifierStart(_source[_position]))
            {
                return ParseIdentifier();
            }

            throw new FormatException($"Unexpected character at position {_position} in '{_source}'.");
        }

        private double ParseNumber()
        {
            int start = _position;
            while (_position < _source.Length && (char.IsDigit(_source[_position]) || _source[_position] == '.'))
            {
                _position++;
            }

            return double.Parse(_source.AsSpan(start, _position - start), System.Globalization.CultureInfo.InvariantCulture);
        }

        private double ParseIdentifier()
        {
            int start = _position;
            while (_position < _source.Length && IsIdentifierPart(_source[_position]))
            {
                _position++;
            }

            string name = _source.Substring(start, _position - start);
            return _variables.TryGetValue(name, out double value) ? value : 0;
        }

        private bool Match(char c)
        {
            if (_position < _source.Length && _source[_position] == c)
            {
                _position++;
                return true;
            }

            return false;
        }

        private void SkipWhitespace()
        {
            while (_position < _source.Length && char.IsWhiteSpace(_source[_position]))
            {
                _position++;
            }
        }

        public void ExpectEnd()
        {
            SkipWhitespace();
            if (_position != _source.Length)
            {
                throw new FormatException($"Unexpected trailing characters at position {_position} in '{_source}'.");
            }
        }

        private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

        private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';
    }
}
