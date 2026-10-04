namespace E128.Analyzers.Testing;

/// <summary>
///     The low-value test conditions detected by <see cref="LowValueTestAnalyzer" />.
/// </summary>
internal enum ConditionKind
{
    BareSize = 0,
    NameMirror = 1,
    NoAssertion = 2,
    SingleRowTheory = 3,
    LiteralEcho = 4,
    ExceptionMessageLock = 5,
    MockVerifyOnly = 6,
    LogAssert = 7,
    ConstructorPassthrough = 8,
    InternalsReachIn = 9,
    SelfFulfillingExpected = 10,
    MagicConstantEcho = 11,
    OrderLock = 12,
    DuplicateCoverage = 13
}
