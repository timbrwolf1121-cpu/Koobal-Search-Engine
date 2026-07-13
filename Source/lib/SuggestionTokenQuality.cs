using System;
using System.Collections.Generic;

namespace PartSearchSuggest
{
    /// <summary>
    /// Quality gate for free-floating suggestion tokens (especially FilterTag / FilterResource).
    /// Stock part.tags are packed with English synonym spam for the stock search box; those must
    /// not become dictionary-like dropdown rows. Meaningful domain tags (intake, aero, …) stay.
    /// </summary>
    internal static class SuggestionTokenQuality
    {
        private const int MinMeaningfulTagLength = 3;

        /// <summary>
        /// Always keep these as FilterTag suggestions even if they look like common words.
        /// </summary>
        private static readonly HashSet<string> AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "aero",
            "aircraft",
            "airplane",
            "antenna",
            "battery",
            "capsule",
            "cargo",
            "claw",
            "command",
            "control",
            "crew",
            "decouple",
            "decoupler",
            "dock",
            "docking",
            "drill",
            "engine",
            "eva",
            "experiment",
            "fairing",
            "fuel",
            "fueltank",
            "gimbal",
            "heat",
            "intake",
            "intakes",
            "isru",
            "jet",
            "ladder",
            "leg",
            "liquid",
            "liquidfuel",
            "mono",
            "monoprop",
            "monopropellant",
            "nose",
            "ore",
            "oxidizer",
            "parachute",
            "probe",
            "propellant",
            "radiat",
            "radiator",
            "rcs",
            "rocket",
            "rockomax",
            "sas",
            "science",
            "sensor",
            "solar",
            "solid",
            "solidfuel",
            "strut",
            "wheel",
            "wing"
        };

        /// <summary>
        /// Common English / synonym tokens that appear in stock tags but are not useful filters.
        /// </summary>
        private static readonly HashSet<string> JunkTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "able", "about", "above", "across", "after", "again", "against", "almost", "along",
            "already", "also", "always", "among", "another", "around", "because", "before",
            "being", "below", "between", "both", "build", "built", "can", "cannot", "come",
            "comes", "could", "does", "doing", "done", "down", "during", "each", "early",
            "either", "else", "enough", "even", "ever", "every", "extra", "first", "from",
            "full", "further", "give", "given", "good", "great", "have", "having", "here",
            "high", "into", "just", "keep", "kind", "know", "large", "last", "later", "less",
            "like", "little", "long", "look", "made", "make", "many", "more", "most", "much",
            "must", "near", "need", "never", "next", "only", "other", "over", "part", "parts",
            "place", "point", "same", "should", "since", "small", "some", "such", "than",
            "that", "their", "them", "then", "there", "these", "they", "this", "those",
            "through", "under", "until", "upon", "very", "want", "well", "were", "what",
            "when", "where", "which", "while", "with", "within", "without", "would",
            // Stock synonym spam that crowds short prefixes like "sp"
            "space", "speed", "spoil", "split", "spot", "spare", "sports", "spud",
            "special", "specific", "support", "surface", "system", "systems",
            "simple", "single", "stable", "standard", "strong", "structure", "structur",
            "power", "powered", "provide", "provides", "purpose", "payload",
            "basic", "better", "bigger", "boost", "bring", "capable", "carry", "carries",
            "cheap", "classic", "common", "compact", "complete", "complex", "connect",
            "design", "designed", "device", "direct", "easy", "effective", "efficient",
            "enable", "entire", "fast", "faster", "feature", "final", "fine", "fit",
            "flight", "fly", "free", "general", "heavy", "helpful", "hold", "holds",
            "ideal", "include", "includes", "increase", "light", "lightweight", "main",
            "major", "massive", "medium", "mini", "modern", "mount", "mounted", "move",
            "multiple", "new", "normal", "optional", "perfect", "perform", "performance",
            "primary", "quick", "ready", "real", "really", "reduce", "reliable", "require",
            "required", "safe", "safer", "secure", "short", "similar", "size", "sized",
            "slight", "slow", "smaller", "smooth", "soft", "solidly", "somewhat",
            "suitable", "super", "tiny", "total", "true", "typical", "unique", "unit",
            "useful", "usual", "various", "wide", "work", "works", "yet",
            "exploration", "explore", "explorer", "mission", "missions", "program",
            "kerbal", "kerbals", "vessel", "vessels", "craft", "ship", "ships",
            "thing", "things", "stuff", "item", "items", "piece", "pieces",
            "and", "the", "for", "you", "your", "all", "any", "are", "but", "not",
            "out", "use", "used", "using", "via", "per", "plus", "also"
        };

        public static bool IsSuggestionWorthyTag(string tag)
        {
            string cleaned = Clean(tag);
            if (cleaned.Length < MinMeaningfulTagLength)
            {
                return false;
            }

            if (cleaned[0] == '#' || cleaned.IndexOf('(') >= 0 || cleaned.IndexOf(')') >= 0)
            {
                return false;
            }

            if (AllowedTags.Contains(cleaned))
            {
                return true;
            }

            if (JunkTokens.Contains(cleaned))
            {
                return false;
            }

            // Technical / compound tokens are usually meaningful filters.
            if (HasDigit(cleaned) || cleaned.IndexOf('_') >= 0 || cleaned.IndexOf('-') >= 0)
            {
                return true;
            }

            // Keep real domain tags (including short ones not on the allowlist).
            // JunkTokens already blocks dictionary synonym spam (space/speed/spoil/…).
            return true;
        }

        public static bool IsSuggestionWorthyResourceToken(string token)
        {
            string cleaned = Clean(token);
            if (cleaned.Length < 2)
            {
                return false;
            }

            if (JunkTokens.Contains(cleaned))
            {
                return false;
            }

            // Resource keys are usually PascalCase / compound (LiquidFuel, MonoPropellant).
            if (HasDigit(cleaned) || cleaned.IndexOf('_') >= 0 || HasInternalUpper(cleaned))
            {
                return true;
            }

            if (AllowedTags.Contains(cleaned))
            {
                return true;
            }

            // Reject free-floating English words scraped from resourceInfo prose.
            if (cleaned.Length <= 8 && IsAllLetters(cleaned) && JunkTokens.Contains(cleaned))
            {
                return false;
            }

            return !JunkTokens.Contains(cleaned);
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Trim('"', '\'');
        }

        private static bool IsAllLetters(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsLetter(value[i]))
                {
                    return false;
                }
            }

            return value.Length > 0;
        }

        private static bool HasDigit(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsDigit(value[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasInternalUpper(string value)
        {
            for (int i = 1; i < value.Length; i++)
            {
                if (char.IsUpper(value[i]) && char.IsLetter(value[i - 1]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
