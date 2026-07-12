using System.Reflection;
using KSP.UI.Screens;

namespace PartSearchSuggest
{
    /// <summary>
    /// Reflection helpers for PartCategorizer search side effects (refreshRequested, searching, etc.).
    /// </summary>
    internal static class PartCategorizerSearchFields
    {
        private static readonly MethodInfo BaseSearchStop = typeof(BasePartCategorizer).GetMethod(
            "SearchStop",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo RefreshRequestedField = typeof(PartCategorizer).GetField(
            "refreshRequested",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo SearchingField = typeof(PartCategorizer).GetField(
            "searching",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo SearchTimerField = typeof(BasePartCategorizer).GetField(
            "searchTimer",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static int _invokeBaseSearchStopDepth;

        internal static void InvokeBaseSearchStop(PartCategorizer categorizer)
        {
            if (categorizer == null || BaseSearchStop == null)
            {
                return;
            }

            if (_invokeBaseSearchStopDepth > 0)
            {
                ResetTypingSearchFlags(categorizer);
                return;
            }

            try
            {
                _invokeBaseSearchStopDepth++;
                BaseSearchStop.Invoke(categorizer, null);
            }
            finally
            {
                _invokeBaseSearchStopDepth--;
            }
        }

        internal static void ClearRefreshRequested(PartCategorizer categorizer)
        {
            if (categorizer == null || RefreshRequestedField == null)
            {
                return;
            }

            RefreshRequestedField.SetValue(categorizer, false);
        }

        internal static void ResetTypingSearchFlags(PartCategorizer categorizer)
        {
            if (categorizer == null)
            {
                return;
            }

            if (SearchingField != null)
            {
                SearchingField.SetValue(categorizer, false);
            }

            if (SearchTimerField != null)
            {
                SearchTimerField.SetValue(categorizer, 0f);
            }

            ClearRefreshRequested(categorizer);
        }
    }
}
