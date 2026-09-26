using System.Globalization;

namespace TeleportDelivery.Cli;

/// <summary>
/// Every text the console shows or expects to be typed.
/// </summary>
/// <remarks>
/// A static class rather than resource files, for simplicity: there is one language.
/// </remarks>
internal static class Texts
{
    /// <summary>
    /// Slovak number format: a decimal comma and a space between thousands.
    /// </summary>
    /// <remarks>
    /// Not sk-SK: with InvariantGlobalization it would quietly be the invariant culture.
    /// </remarks>
    public static CultureInfo Culture { get; } = CreateCulture();

    private static CultureInfo CreateCulture()
    {
        CultureInfo culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.NumberGroupSeparator = "\u00A0";
        culture.NumberFormat.PercentDecimalSeparator = ",";
        culture.NumberFormat.PercentGroupSeparator = "\u00A0";
        return CultureInfo.ReadOnly(culture);
    }

    /// <summary>
    /// The commands, without diacritics because they are typed.
    /// </summary>
    public static class Commands
    {
        public const string Plan = "plan";
        public const string Benchmarks = "benchmarky";
        public const string Quit = "koniec";
    }

    /// <summary>
    /// The flags the commands read, and the value of <see cref="Mix"/> meaning every day.
    /// </summary>
    public static class Arguments
    {
        public const string Mix = "--mix";
        public const string Count = "--count";
        public const string Seed = "--seed";
        public const string Csv = "--csv";
        public const string Input = "--input";
        public const string AllMixes = "all";
    }

    /// <summary>
    /// The interactive menu.
    /// </summary>
    public static class Menu
    {
        public const string Banner = "Teleport";

        public static readonly string[] Introduction =
        [
            "V sklade čakajú zásielky a 120 dodávok odvezie len toľko, koľko sa do nich zmestí.",
            "Každá dodávka pojme [teal]7 m3[/] a [teal]5,5 t[/]. Plánovač rozhoduje, "
                + "ktoré zásielky pôjdu a do ktorej dodávky,",
            "aby okruh odviezol čo najväčší zisk.",
        ];

        public const string Question = "Čo chceš vidieť?";

        public const string PlanLabel = "PLÁN";
        public const string PlanNote =
            "koľko zásielok čaká, koľko sa ich zmestí do 120 dodávok a koľko zarobia";

        public const string BenchmarksLabel = "BENCHMARKY";
        public const string BenchmarksNote =
            "ako rýchlo program plánuje a koľko pamäte na to potrebuje";

        public const string QuitLabel = "KONIEC";

        public const string DayQuestion = "Pre ktorý deň objednávok?";
        public const string DayCountNote =
            "Číslo je počet zásielok, ktoré v ten deň čakajú v sklade.";
        public const string AllDaysLabel = "všetky";
        public const string AllDaysNote = "jeden typ dňa po druhom";
        public const string BackLabel = "späť";
        public const string BackNote = "na zoznam výpisov";

        public const string PressForAnotherDay = "stlač klávesu pre výber ďalšieho dňa";
        public const string PressToReturn = "stlač klávesu pre návrat do menu";

        public const string CommandLinePrefix = "dotnet run --";
        public const string AllDaysHeading = "všetky typy dní";

        public static string Parcels(int count) =>
            string.Create(Culture, $"{count:N0} zásielok");
    }

    /// <summary>
    /// The benchmark run started from the menu.
    /// </summary>
    public static class Benchmarks
    {
        public const string Heading = "benchmarky";

        public const string Explanation =
            "Plan vytvorí plán rozvozu a ukáže jeho výnos. "
            + "Benchmark opakovane meria rýchlosť a pamäťové nároky programu.";

        public const string Warning =
            "Meranie môže trvať niekoľko minút a vyžaduje .NET SDK. "
            + "Počas neho počítač radšej nezaťažuj, inak budú časy skreslené.";

        public const string CommandLine =
            "dotnet run -c Release --project benchmarks/TeleportDelivery.Benchmarks "
                + "-- --filter \"*\"";

        public const string SdkMissing =
            "Nenašlo sa .NET SDK. Benchmarky sa musia zostaviť a samotný runtime na to nestačí.";

        public static string ProjectNotFound(string project) =>
            $"Benchmarkový projekt {project} sa nenašiel. Benchmarky sa dajú spustiť "
            + "len z priečinka so zdrojovým kódom.";

        public static string Failed(int exitCode) =>
            string.Create(Culture, $"Benchmarky skončili s chybou (kód {exitCode}).");

        public const string NothingMeasured =
            "Nepodarilo sa zmerať ani jedno meranie.";

        public static string PartlyMeasured(int measured, int total) =>
            string.Create(Culture, $"Zmeraných {measured} z {total} meraní, ostatné zlyhali.");

        public const string NotMeasured = "nezmerané";

        public const string LastLines = "Posledné riadky výpisu:";

        public static string FullLog(string path) => $"Celý výpis je v súbore {path}";

        public static string Reports(string path) => $"Reporty sú v priečinku {path}";

        public const string Building = "zostavujem benchmarky";

        public static string Step(int current, int total) =>
            string.Create(Culture, $"{current}/{total}");

        public const string MeasurementColumn = "meranie";
        public const string VariantColumn = "variant";
        public const string TimeColumn = "čas";
        public const string MemoryColumn = "pamäť";
        public const string Milliseconds = "ms";

        /// <summary>
        /// Names of the benchmark classes, keyed by their name in the benchmark project.
        /// </summary>
        private static readonly Dictionary<string, string> Groups = new()
        {
            ["PlannerBenchmarks"] = "plán podľa typu dňa",
            ["ScalingBenchmarks"] = "plán podľa počtu zásielok",
            ["PlanStageBenchmarks"] = "časti plánu, bežný deň",
        };

        /// <summary>
        /// Names of the benchmark methods, keyed by class and method.
        /// </summary>
        private static readonly Dictionary<string, string> Cases = new()
        {
            ["PlannerBenchmarks.Baseline"] = Plan.Baseline,
            ["PlannerBenchmarks.Adaptive"] = Plan.Adaptive,
            ["ScalingBenchmarks.Plan"] = Plan.Adaptive,
            ["PlanStageBenchmarks.Ranking"] = "poradie bez triedenia",
            ["PlanStageBenchmarks.SortForComparison"] = "triedenie, na porovnanie",
            ["PlanStageBenchmarks.BalancedPacking"] = "nakladanie do dodávok",
        };

        /// <summary>
        /// The name of a benchmark class, or the class name when there is none.
        /// </summary>
        public static string Group(string type) => Groups.GetValueOrDefault(type, type);

        /// <summary>
        /// The name of a benchmark method, or the method name when there is none.
        /// </summary>
        public static string Case(string type, string method) =>
            Cases.GetValueOrDefault($"{type}.{method}", method);

        public static readonly (string Term, string Text)[] Legend =
        [
            ("čas", "Priemer z troch meraní jedného behu."),
            ("pamäť", "Pamäť pridelená počas jedného behu."),
            ("baseline, adaptive", "Tie isté plánovače ako v PLÁN."),
        ];
    }

    /// <summary>
    /// Slovak names and descriptions of the demand mixes.
    /// </summary>
    public static class Days
    {
        private static readonly Dictionary<string, (string Label, string Note)> ByName = new()
        {
            ["QuietDay"] = ("Pokojný deň",
                "Slabý deň s drobným tovarom. Dopyt sa zmestí do toho, čo flotila unesie, "
                + "takže niet čo vyberať a odvezie sa všetko."),
            ["OrdinaryDay"] = ("Bežný deň",
                "Bežný obchod naprieč celým sortimentom. Dopyt prerastá kapacitu, takže "
                + "časť zásielok musí ostať v sklade."),
            ["BusyDay"] = ("Rušný deň",
                "Ťažký deň na rovnakom sortimente. Dvojnásobok zásielok, ten istý tovar."),
            ["PeakSeason"] = ("Vianočná špička",
                "Vianoce. Mnohonásobne viac zásielok než ktorýkoľvek iný deň, s prevahou darov."),
            ["BulkyGoods"] = ("Objemný tovar",
                "Periny, kuchynské utierky a záhradný nábytok. Miesto dojde, kým sú dodávky "
                + "ešte ľahké."),
            ["DenseGoods"] = ("Ťažký tovar",
                "Autobatérie, činky, farby a fľaškované nápoje. Najskôr dojde nosnosť a pár "
                + "percent miesta ostane za ňou nevyužitých."),
            ["ElectronicsWeek"] = ("Týždeň elektroniky",
                "Akcia na elektroniku. Väčšina peňazí je v malých zásielkach, takže veľkosť "
                + "zle radí, čo sa oplatí voziť."),
            ["MixedExtremes"] = ("Zmes extrémov",
                "Len periny, kuchynské utierky, autobatérie a činky. Časť zásielok je objemná "
                + "a takmer bez váhy, zvyšok hustý a malý, a dodávka sa naplní na oboch "
                + "limitoch len vtedy, keď vezme z oboch."),
            ["LowMargin"] = ("Nízke marže",
                "Tenké marže na všetkom. Zásielky sa dajú ťažko odlíšiť, takže rozhodnutie "
                + "na hranici je skoro ako hod mincou."),
            ["Clearance"] = ("Výpredaj",
                "Výpredaj na konci sezóny. Veľké množstvo lacných zásielok naprieč širokým "
                + "sortimentom."),
        };

        /// <summary>
        /// The Slovak name of a day, or its ASCII name when there is none.
        /// </summary>
        public static string Label(string name) =>
            ByName.TryGetValue(name, out var day) ? day.Label : name;

        public static string Note(string name) =>
            ByName.TryGetValue(name, out var day) ? day.Note : string.Empty;
    }

    /// <summary>
    /// The usage listing, for a redirected console or an unknown command.
    /// </summary>
    public static class Usage
    {
        public const string Title = "Použitie:";

        public static readonly string[] Lines =
        [
            $"  {Commands.Plan,-9} [{Arguments.Mix} <názov|{Arguments.AllMixes}>] "
                + $"[{Arguments.Count} N] "
                + $"[{Arguments.Seed} N] [{Arguments.Csv} <cesta>]",
            $"  {Commands.Plan,-9} {Arguments.Input} <cesta.csv> naplánuje skutočný súbor zásielok",
        ];

        public const string DaysTitle = "Typy dní:";

        public static string UnknownCommand(string command) => $"Neznámy príkaz '{command}'.";

        public static string NotANumber(string argument, string value) =>
            $"Hodnota '{value}' pri {argument} nie je platné celé číslo.";

        public static string UnknownDay(string name) => $"Typ dňa '{name}' neexistuje.";
    }

    /// <summary>
    /// Lines a report prints around its table.
    /// </summary>
    public static class Report
    {
        public static string Seed(ulong seed) =>
            string.Create(Culture, $"seed {seed}");

        public static string Source(string path) => $"zdroj {path}";

        public static string WrittenTo(string path) => $"zapísané do {path}";

        public static string NoParcelsRead(string path) =>
            $"Zo súboru {path} sa nenačítali žiadne zásielky.";

        public static string Unreadable(string path, string reason) =>
            $"Súbor {path} sa nedá načítať: {reason}";

        public static string NotFound(string path) => $"Súbor {path} neexistuje.";

        public const string StageGenerating = "generujem";
        public const string StageBound = "strop";
    }

    /// <summary>
    /// The plan table: columns, planner names and legend.
    /// </summary>
    public static class Plan
    {
        public const string DayColumn = "typ dňa";
        public const string SourceColumn = "zdroj";

        public const string Waiting = "dopyt / kapacita";
        public const string Weight = "váha";
        public const string Volume = "objem";
        public const string Planner = "plánovač";
        public const string Parcels = "zásielky";
        public const string Total = "spolu";
        public const string Carried = "naložené";
        public const string Left = "ostalo";
        public const string Profit = "zisk\nmil. Kč";
        public const string OfBound = "z maxima";
        public const string FleetUsed = "využitie";
        public const string Theta = "cena váhy";
        public const string Milliseconds = "ms";

        public const string Baseline = "baseline";
        public const string Adaptive = "adaptive";

        /// <summary>
        /// Shown for the weight price when everything fitted and no price was chosen.
        /// </summary>
        public const string Fast = "fast";

        /// <summary>
        /// The legend, in column order.
        /// </summary>
        public static readonly (string Term, string Text)[] Legend =
        [
            ("dopyt / kapacita",
                "Koľkokrát viac váhy a objemu čaká v sklade, než pojme celá flotila. "
                + "1,00 ju zaplní "
                + "presne, 8,16 je osemkrát viac. Vyššie z dvoch čísel je limit, ktorý rozhoduje. "
                + "Pod 1 na oboch sa dopyt zmestí do súčtu kapacít, takže spravidla odíde všetko."),
            ("plánovač",
                "baseline účtuje za váhu a objem rovnako, nech je deň akýkoľvek; adaptive si pomer "
                + "cien odvodí zo zásielok daného dňa. Rozdiel medzi riadkami je to, čo odvodenie "
                + "prináša. baseline je meradlo, nie plánovač na použitie."),
            ("zásielky",
                "Koľko ich v sklade čakalo, koľko sa naložilo a koľko ostalo na ďalší okruh. "
                + "Nenaložené nie sú chyba: dopytu je zámerne viac, než flotila unesie."),
            ("zisk",
                "Súčet marží naložených zásielok (cena × marža), v miliónoch Kč; náklady na rozvoz "
                + "sa neodpočítavajú."),
            ("z maxima",
                "Zisk ako podiel z hornej hranice, ktorú žiadny plán nemôže prekonať. "
                + "99 % znamená, "
                + "že do najlepšieho možného plánu chýba najviac jedno percento."),
            ("využitie",
                "Ako plné dodávky skončili, zvlášť na váhu a objem. Kapacita na 100 % je tá, ktorá "
                + "zastavila nakladanie. Nevyužitá kapacita na druhom limite nie je strata, ak je "
                + "z maxima blízko 100 %."),
            ("cena váhy",
                "Akú časť ceny adaptive pripísal váhe, od 0 do 1; zvyšok pripadá na objem. "
                + "0 znamená, "
                + "že váha bola zadarmo a rozhodoval objem, 1 opak. "
                + "Odvodí si to sám zo zásielok dňa. "
                + "baseline má pevne polovicu, preto [grey]-[/]; "
                + "[grey]fast[/] znamená, že sa všetko "
                + "zmestilo a nebolo čo vyberať."),
            ("ms",
                "Čas jedného behu, nie opakované meranie, "
                + "takže kolíše podľa toho, čo počítač práve "
                + "robí. Opakovateľné časy dávajú benchmarky."),
        ];
    }
}
