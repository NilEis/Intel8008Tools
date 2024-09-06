// Source: https://github.com/sprache/Sprache/blob/develop/samples/LinqyCalculator/ExpressionParser.cs

using System.Linq.Expressions;
using System.Reflection;
using Sprache;

namespace AssemblerBackend;

internal static class ExpressionParser
{
    public static Parser<Expression<Func<long>>> ParseExpression(Dictionary<string, long> variables,
        Dictionary<string, long> labels)
    {
        return Lambda(variables, labels);
    }

    private static Parser<ExpressionType> Operator(string op, ExpressionType opType)
    {
        return Parse.String(op).Token().Return(opType);
    }

    private static readonly Parser<ExpressionType> Add = Operator("+", ExpressionType.AddChecked);
    private static readonly Parser<ExpressionType> Subtract = Operator("-", ExpressionType.SubtractChecked);
    private static readonly Parser<ExpressionType> Multiply = Operator("*", ExpressionType.MultiplyChecked);
    private static readonly Parser<ExpressionType> Divide = Operator("/", ExpressionType.Divide);
    private static readonly Parser<ExpressionType> Modulo = Operator("%", ExpressionType.Modulo);
    private static readonly Parser<ExpressionType> Power = Operator("^", ExpressionType.Power);

    private static Parser<Expression> Function(Dictionary<string, long> variables, Dictionary<string, long> labels)
    {
        return from name in Parse.Letter.AtLeastOnce().Text()
            from lparen in Parse.Char('(')
            from expr in Parse.Ref(() => Expr(variables, labels)).DelimitedBy(Parse.Char(',').Token())
            from rparen in Parse.Char(')')
            select CallFunction(name, expr.ToArray());
    }

    private static MethodCallExpression CallFunction(string name, Expression[] parameters)
    {
        var methodInfo = typeof(Math).GetTypeInfo().GetMethod(name, parameters.Select(e => e.Type).ToArray());
        if (methodInfo == null)
        {
            throw new ParseException(
                $"Function '{name}({string.Join(",", parameters.Select(e => e.Type.Name))})' does not exist.");
        }

        return Expression.Call(methodInfo, parameters);
    }

    private static Parser<Expression> Constant(Dictionary<string, long> variables, Dictionary<string, long> labels)
    {
        return NumberParser().Or(AddrParser(labels)).Or(VariableParser(variables)).Select(v => Expression.Constant(v))
            .Named("number");
    }

    private static Parser<Expression> Factor(Dictionary<string, long> variables, Dictionary<string, long> labels)
    {
        return
            (from lparen in Parse.Char('(')
                from expr in Parse.Ref(() => Expr(variables, labels))
                from rparen in Parse.Char(')')
                select expr).Named("expression")
            .XOr(Constant(variables, labels))
            .XOr(Function(variables, labels));
    }

    private static Parser<Expression> Operand(Dictionary<string, long> variables, Dictionary<string, long> labels)
    {
        return (from sign in Parse.Char('-')
                from factor in Factor(variables, labels)
                select Expression.Negate(factor)
            ).XOr(Factor(variables, labels)).Token();
    }

    private static Parser<Expression> InnerTerm(Dictionary<string, long> variables, Dictionary<string, long> labels)
    {
        return Parse.ChainRightOperator(Power, Operand(variables, labels), Expression.MakeBinary);
    }

    private static Parser<Expression> Term(Dictionary<string, long> variables, Dictionary<string, long> labels)
    {
        return Parse.ChainOperator(Multiply.Or(Divide).Or(Modulo), InnerTerm(variables, labels), Expression.MakeBinary);
    }

    private static Parser<Expression> Expr(Dictionary<string, long> variables, Dictionary<string, long> labels)
    {
        return Parse.ChainOperator(Add.Or(Subtract), Term(variables, labels), Expression.MakeBinary);
    }

    private static Parser<Expression<Func<long>>> Lambda(Dictionary<string, long> variables,
        Dictionary<string, long> labels)
    {
        return Expr(variables, labels).End().Select(body => Expression.Lambda<Func<long>>(body));
    }

    public static Parser<long> NumberParser()
    {
        return (
                // Hexadecimal format: 1234h
                from digits in Parse.Chars("0123456789ABCDEF").AtLeastOnce().Text()
                from suffix in Parse.Char('H')
                select Convert.ToInt64(digits, 16)
            )
            .Or
            (
                // Hexadecimal format: 0x1234
                from prefix in Parse.String("0X")
                from digits in Parse.Chars("0123456789ABCDEF").AtLeastOnce().Text()
                select Convert.ToInt64(digits, 16)
            )
            .Or
            (
                // Binary format: 1010b
                from digits in Parse.Chars("01").AtLeastOnce().Text()
                from suffix in Parse.Char('B')
                select Convert.ToInt64(digits, 2)
            )
            .Or
            (
                // Binary format: 0b1010
                from prefix in Parse.String("0B")
                from digits in Parse.Chars("01").AtLeastOnce().Text()
                select Convert.ToInt64(digits, 2)
            )
            .Or
            (
                // Decimal format: 1234
                Parse.Digit.AtLeastOnce().Text().Select(digits => Convert.ToInt64(digits, 10)
                )
            ).Named("NumberParser");
    }

    public static Parser<long> VariableParser(Dictionary<string, long> variables)
    {
        return from variable in NameParser().Where(v => variables.TryGetValue(v, out _))
            select variables[variable];
    }

    public static Parser<long> AddrParser(Dictionary<string, long> labels)
    {
        return NameParser().Where(v => labels.TryGetValue(v, out _)).Select(s =>
                labels[s]).Or(ExpressionParser.NumberParser()).Token()
            .Named("AddressParser");
    }

    public static Parser<string> NameParser()
    {
        return Parse.Identifier(Parse.Upper, Parse.Upper.Or(Parse.Numeric)).Text();
    }
}