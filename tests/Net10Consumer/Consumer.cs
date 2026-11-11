namespace Net10Consumer
{
    /// <summary>
    ///     Holds a deliberate E128024 violation: a plain comment above a method.
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

        // A plain comment above a method is what E128024 reports.
        public static int Other()
        {
            return 2;
        }
    }
}
