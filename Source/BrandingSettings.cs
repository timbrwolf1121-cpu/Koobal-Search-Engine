using System;

using System.IO;



namespace PartSearchSuggest

{

    internal enum DropdownBrandingVariant

    {

        Wordmark,

        FullTagline

    }



    internal static class BrandingSettings

    {

        private const string RelativePath = "GameData/KoobalSearchEngine/PluginData/BrandingSettings.cfg";



        private const float WordmarkFooterHeight = 48f;

        private const float FullTaglineFooterHeight = 68f;



        private static bool _loaded;

        private static DropdownBrandingVariant _dropdownBranding = DropdownBrandingVariant.Wordmark;



        public static DropdownBrandingVariant DropdownBranding

        {

            get

            {

                EnsureLoaded();

                return _dropdownBranding;

            }

        }



        public static float FooterHeight =>

            DropdownBranding == DropdownBrandingVariant.FullTagline

                ? FullTaglineFooterHeight

                : WordmarkFooterHeight;



        private static void EnsureLoaded()

        {

            if (_loaded)

            {

                return;

            }



            _loaded = true;



            string path = Path.Combine(KSPUtil.ApplicationRootPath, RelativePath);

            if (!File.Exists(path))

            {

                return;

            }



            foreach (string line in File.ReadAllLines(path))

            {

                string trimmed = line.Trim();

                if (trimmed.StartsWith("#", StringComparison.Ordinal)

                    || trimmed.StartsWith("//", StringComparison.Ordinal))

                {

                    continue;

                }



                if (!trimmed.StartsWith("dropdownBranding", StringComparison.OrdinalIgnoreCase))

                {

                    continue;

                }



                int eq = trimmed.IndexOf('=');

                if (eq < 0)

                {

                    continue;

                }



                string value = trimmed.Substring(eq + 1).Trim();

                if (value.Equals("FullTagline", StringComparison.OrdinalIgnoreCase))

                {

                    _dropdownBranding = DropdownBrandingVariant.FullTagline;

                }

                else if (value.Equals("Wordmark", StringComparison.OrdinalIgnoreCase))

                {

                    _dropdownBranding = DropdownBrandingVariant.Wordmark;

                }

                else

                {

                    EditorBootstrap.LogWarning(

                        "BrandingSettings: unknown dropdownBranding '"

                        + value

                        + "' — using Wordmark.");

                }

            }

        }

    }

}


