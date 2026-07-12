using System.Collections.Generic;
using System.Linq;

namespace PartSearchSuggest
{
    internal static class IndexDebugDump
    {
        private static readonly string[] SampleQueries = { "engine", "intake", "lis", "harmony", "a" };

        public static void LogIfEnabled(
            SuggestionIndex partIndex,
            MetadataSuggestionIndex metadataIndex,
            CategorizerSuggestionIndex categorizerIndex)
        {
            if (!DebugSettings.DumpIndexStats)
            {
                return;
            }

            int partCount = partIndex?.PartCount ?? 0;
            int authorCount = metadataIndex?.AuthorCount ?? 0;
            int modCount = metadataIndex?.ModCount ?? 0;
            int categorizerCount = categorizerIndex?.EntryCount ?? 0;

            EditorBootstrap.Log(
                "IndexStats: parts="
                + partCount
                + " authors="
                + authorCount
                + " mods="
                + modCount
                + " categorizer="
                + categorizerCount);

            if (partIndex == null || metadataIndex == null || categorizerIndex == null)
            {
                return;
            }

            foreach (string query in SampleQueries)
            {
                int parts = partIndex.Match(query, 24).Count();
                int meta = metadataIndex.Match(query, 6).Count();
                int cat = categorizerIndex.Match(query, 6).Count();
                EditorBootstrap.Log(
                    "IndexStats query '"
                    + query
                    + "': parts="
                    + parts
                    + " meta="
                    + meta
                    + " categorizer="
                    + cat);
            }
        }
    }
}
