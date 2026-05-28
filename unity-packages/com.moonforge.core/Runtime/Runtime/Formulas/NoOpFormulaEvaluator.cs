using System.Collections.Generic;
using System.Globalization;

namespace Moonforge.Core.Runtime.Formulas
{

    /// <summary>
    /// Minimal placeholder evaluator for early-phase scaffolding.
    /// Supports numeric literals and direct variable lookup only.
    /// </summary>
    public sealed class NoOpFormulaEvaluator : IFormulaEvaluator
    {
        public double Evaluate(string expression, IReadOnlyDictionary<string, double> variables)
        {
            if (variables.TryGetValue(expression, out double value))
            {
                return value;
            }

            return double.Parse(expression, CultureInfo.InvariantCulture);
        }
    }
}
