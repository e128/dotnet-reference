namespace E128.Analyzers.Tests;

/// <summary>
///     Stub sources shared by paired analyzer and code fix test files. One copy
///     per stub keeps the fake package shapes in one place.
/// </summary>
internal static class TestStubs
{
    /// <summary>Synthetic AngleSharp.Dom shape used by the text guard and its fixer tests.</summary>
    public const string AngleSharpDom =
        """
        namespace AngleSharp.Dom
        {
            public interface INode { string TextContent { get; } }
            public interface IElement : INode { bool IsVisible { get; } }
        }
        """;

    /// <summary>Synthetic System.Numerics.Tensors shape used by the span-sum tests.</summary>
    public const string TensorPrimitives =
        """
        namespace System.Numerics.Tensors
        {
            public static class TensorPrimitives
            {
                public static float Sum(ReadOnlySpan<float> values)
                {
                    return 0f;
                }

                public static double Sum(ReadOnlySpan<double> values)
                {
                    return 0d;
                }
            }
        }
        """;
}
