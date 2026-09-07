namespace PartSearchSuggest
{
    /// <summary>
    /// Compile-time gate for the 1.0 parts-list UI line (scroll-recycle).
    /// Enabled only when building with <c>KOOBAL_V09</c> (ReleaseV09 configuration).
    /// 0.8 Release builds keep this false — zero behaviour change.
    /// </summary>
    internal static class ExperimentalV09Gate
    {
#if KOOBAL_V09
        internal const bool Enabled = true;

        /// <summary>
        /// Max live EditorPartIcon widgets shown at once for large PartSearch results.
        /// The pool rebinds as the user scrolls so the full filtered set stays reachable.
        /// </summary>
        internal const int MaxVisibleIcons = 64;

        internal const string DisplayBand = "1.0.0";
#else
        internal const bool Enabled = false;
        internal const int MaxVisibleIcons = 0;
        internal const string DisplayBand = "0.8";
#endif
    }
}
