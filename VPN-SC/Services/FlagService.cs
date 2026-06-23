using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace VpnSc.Services;

public static class FlagService
{
    private static readonly Dictionary<string, ImageSource> ImageCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly (string Fragment, string SvgFile)[] FlagMatchers =
    {
        ("Netherlands", "nl.svg"), ("Finland", "fi.svg"), ("USA", "us.svg"), ("United States", "us.svg"),
        ("France", "fr.svg"), ("UK", "gb.svg"), ("United Kingdom", "gb.svg"), ("Russia", "ru.svg"),
        ("Germany", "de.svg"), ("Canada", "ca.svg"), ("Australia", "au.svg"), ("Japan", "jp.svg"),
        ("Singapore", "sg.svg"), ("Switzerland", "ch.svg"), ("Sweden", "se.svg"), ("Norway", "no.svg"),
        ("Denmark", "dk.svg"), ("Italy", "it.svg"), ("Spain", "es.svg"), ("Poland", "pl.svg"),
        ("Czech Republic", "cz.svg"), ("Austria", "at.svg"), ("Belgium", "be.svg"), ("Ireland", "ie.svg"),
        ("Portugal", "pt.svg"), ("Greece", "gr.svg"), ("Turkey", "tr.svg"), ("Brazil", "br.svg"),
        ("Mexico", "mx.svg"), ("Argentina", "ar.svg"), ("Chile", "cl.svg"), ("Colombia", "co.svg"),
        ("Peru", "pe.svg"), ("India", "in.svg"), ("South Korea", "kr.svg"), ("Thailand", "th.svg"),
        ("Malaysia", "my.svg"), ("Indonesia", "id.svg"), ("Philippines", "ph.svg"), ("Vietnam", "vn.svg"),
        ("Hong Kong", "hk.svg"), ("Taiwan", "tw.svg"), ("New Zealand", "nz.svg"), ("South Africa", "za.svg"),
        ("Israel", "il.svg"), ("UAE", "ae.svg"), ("Saudi Arabia", "sa.svg"), ("Egypt", "eg.svg"),
        ("Nigeria", "ng.svg"), ("Kenya", "ke.svg"), ("Morocco", "ma.svg"), ("Tunisia", "tn.svg"),
        ("Algeria", "dz.svg"), ("Ukraine", "ua.svg"), ("Belarus", "by.svg"), ("Kazakhstan", "kz.svg"),
        ("Uzbekistan", "uz.svg"), ("Kyrgyzstan", "kg.svg"), ("Tajikistan", "tj.svg"),
        ("Turkmenistan", "tm.svg"), ("Moldova", "md.svg"), ("Romania", "ro.svg"), ("Bulgaria", "bg.svg"),
        ("Croatia", "hr.svg"), ("Serbia", "rs.svg"), ("Slovenia", "si.svg"), ("Slovakia", "sk.svg"),
        ("Hungary", "hu.svg"), ("Lithuania", "lt.svg"), ("Latvia", "lv.svg"), ("Estonia", "ee.svg"),
        ("Iceland", "is.svg"), ("Luxembourg", "lu.svg"), ("Malta", "mt.svg"), ("Cyprus", "cy.svg"),
        ("Albania", "al.svg"), ("Bosnia", "ba.svg"), ("Montenegro", "me.svg"), ("Macedonia", "mk.svg"),
        ("Georgia", "ge.svg"), ("Armenia", "am.svg"), ("Azerbaijan", "az.svg")
    };

    private static readonly (string Fragment, string I18nKey)[] CountryMatchers =
    {
        ("Smart Location", "country_smart_location"), ("Netherlands", "country_netherlands"),
        ("Finland", "country_finland"), ("USA", "country_usa"), ("United States", "country_usa"),
        ("France", "country_france"), ("UK", "country_uk"), ("United Kingdom", "country_uk"),
        ("Russia", "country_russia"), ("Germany", "country_germany"), ("Canada", "country_canada"),
        ("Australia", "country_australia"), ("Japan", "country_japan"), ("Singapore", "country_singapore"),
        ("Switzerland", "country_switzerland"), ("Sweden", "country_sweden"), ("Norway", "country_norway"),
        ("Denmark", "country_denmark"), ("Italy", "country_italy"), ("Spain", "country_spain"),
        ("Poland", "country_poland"), ("Czech Republic", "country_czech_republic"),
        ("Austria", "country_austria"), ("Belgium", "country_belgium"), ("Ireland", "country_ireland"),
        ("Portugal", "country_portugal"), ("Greece", "country_greece"), ("Turkey", "country_turkey"),
        ("Brazil", "country_brazil"), ("Mexico", "country_mexico"), ("Argentina", "country_argentina"),
        ("Chile", "country_chile"), ("Colombia", "country_colombia"), ("Peru", "country_peru"),
        ("India", "country_india"), ("South Korea", "country_south_korea"), ("Thailand", "country_thailand"),
        ("Malaysia", "country_malaysia"), ("Indonesia", "country_indonesia"),
        ("Philippines", "country_philippines"), ("Vietnam", "country_vietnam"),
        ("Hong Kong", "country_hong_kong"), ("Taiwan", "country_taiwan"),
        ("New Zealand", "country_new_zealand"), ("South Africa", "country_south_africa"),
        ("Israel", "country_israel"), ("UAE", "country_uae"), ("Saudi Arabia", "country_saudi_arabia"),
        ("Egypt", "country_egypt"), ("Nigeria", "country_nigeria"), ("Kenya", "country_kenya"),
        ("Morocco", "country_morocco"), ("Tunisia", "country_tunisia"), ("Algeria", "country_algeria"),
        ("Ukraine", "country_ukraine"), ("Belarus", "country_belarus"), ("Kazakhstan", "country_kazakhstan"),
        ("Uzbekistan", "country_uzbekistan"), ("Kyrgyzstan", "country_kyrgyzstan"),
        ("Tajikistan", "country_tajikistan"), ("Turkmenistan", "country_turkmenistan"),
        ("Moldova", "country_moldova"), ("Romania", "country_romania"), ("Bulgaria", "country_bulgaria"),
        ("Croatia", "country_croatia"), ("Serbia", "country_serbia"), ("Slovenia", "country_slovenia"),
        ("Slovakia", "country_slovakia"), ("Hungary", "country_hungary"), ("Lithuania", "country_lithuania"),
        ("Latvia", "country_latvia"), ("Estonia", "country_estonia"), ("Iceland", "country_iceland"),
        ("Luxembourg", "country_luxembourg"), ("Malta", "country_malta"), ("Cyprus", "country_cyprus"),
        ("Albania", "country_albania"), ("Bosnia", "country_bosnia"), ("Montenegro", "country_montenegro"),
        ("Macedonia", "country_macedonia"), ("Georgia", "country_georgia"),
        ("Armenia", "country_armenia"), ("Azerbaijan", "country_azerbaijan")
    };

    public static bool IsSmartLocation(string serverName) =>
        serverName.IndexOf("Smart", StringComparison.OrdinalIgnoreCase) >= 0;

    public static string? GetFlagSvgFileName(string serverName)
    {
        if (IsSmartLocation(serverName))
            return null;
        foreach (var (fragment, svg) in FlagMatchers)
        {
            if (serverName.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                return svg;
        }
        return null;
    }

    public static ImageSource? GetFlagImage(string serverName)
    {
        var svg = GetFlagSvgFileName(serverName);
        if (svg == null)
            return null;

        if (ImageCache.TryGetValue(svg, out var cached))
            return cached;

        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Flags", svg);
        if (!File.Exists(path))
            return null;

        try
        {
            var settings = new WpfDrawingSettings { IncludeRuntime = true };
            var reader = new FileSvgReader(settings);
            var drawing = reader.Read(new Uri(path, UriKind.Absolute));
            if (drawing == null)
                return null;

            var image = new DrawingImage(drawing);
            image.Freeze();
            ImageCache[svg] = image;
            return image;
        }
        catch
        {
            return null;
        }
    }

    public static string GetCountryName(string serverName)
    {
        foreach (var (fragment, key) in CountryMatchers)
        {
            if (serverName.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                return Localization.I18n.T(key);
        }
        return GetFallbackCountryName(serverName);
    }

    private static string GetFallbackCountryName(string serverName)
    {
        var clean = Regex.Replace(serverName, @"\s*\([^)]*\)", "").Trim();
        if (clean.Equals("Smart Location", StringComparison.OrdinalIgnoreCase))
            return Localization.I18n.T("country_smart_location");
        return string.IsNullOrEmpty(clean)
            ? Localization.I18n.T("country_vpn_server")
            : clean;
    }
}
