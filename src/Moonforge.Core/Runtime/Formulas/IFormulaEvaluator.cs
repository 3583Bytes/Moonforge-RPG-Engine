using System.Collections.Generic;

namespace Moonforge.Core.Runtime.Formulas;

public interface IFormulaEvaluator
{
    double Evaluate(string expression, IReadOnlyDictionary<string, double> variables);
}
