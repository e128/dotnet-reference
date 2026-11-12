namespace E128.Analyzers.Tests;

/// <summary>
///     Compiler-required runtime surface for the C# union feature. The net10.0 reference
///     assemblies predate the union runtime, so the tests inject this source and the
///     compiler binds the synthesized interface and attribute to it.
/// </summary>
internal static class UnionTestRuntime
{
    internal const string StubSource =
        """
        namespace System.Runtime.CompilerServices
        {
            [global::System.AttributeUsage(
                global::System.AttributeTargets.Struct | global::System.AttributeTargets.Class,
                AllowMultiple = false)]
            public sealed class UnionAttribute : global::System.Attribute
            {
                public UnionAttribute()
                {
                }
            }

            [global::System.AttributeUsage(
                global::System.AttributeTargets.Struct | global::System.AttributeTargets.Class,
                AllowMultiple = false)]
            public sealed class IsClosedTypeAttribute : global::System.Attribute
            {
                public IsClosedTypeAttribute()
                {
                }
            }

            public interface IUnion
            {
            }
        }
        """;
}
