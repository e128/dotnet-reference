namespace Net10Consumer
{
    /// <summary>
    ///     Carries a method so the compiler runs the analyzer over a compilation unit.
    /// </summary>
    public sealed class Consumer
    {
        /// <summary>
        ///     Gets a value.
        /// </summary>
        /// <returns>The value.</returns>
        public static int Value()
        {
            return 1;
        }

        /// <summary>
        ///     Gets another value.
        /// </summary>
        /// <returns>The other value.</returns>
        public static int Other()
        {
            return 2;
        }
    }
}
