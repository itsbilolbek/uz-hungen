using System.Text;
using Test.Generator;
using static Test.Generator.CommandLineParser;
using static Test.Generator.DictionaryParser;

namespace Test.Generator2;

// SFX flag qatorlarini saqlash uchun
public record SFXFlagItem
{
    public string Text { get; init; } = string.Empty;
    public string Condition { get; init; } = string.Empty;
    public string Strip { get; init; } = string.Empty;
    public string MorphCode { get; init; } = string.Empty;
    public string NextFlag { get; init; } = string.Empty;
}

// SFX flagni saqlash uchun
public record SFXFlag
{
    public string TagName { get; init; } = string.Empty;
    public string SetName { get; init; } = string.Empty;
    public string FlagName { get; init; } = string.Empty;
    public string MorphCode { get; init; } = string.Empty;
    public List<SFXFlagItem> Lines { get; init; } = new();
    public string ClassName { get; init; } = string.Empty;
    public bool OnlyRoot { get; init; } = false;
}

public record AliasFlagItem
{
    public string ClassName { get; init; } = string.Empty;
    public string FlagName { get; init; } = string.Empty;
    public string SetName { get; init; } = string.Empty;
}

public record AliasEntry
{
    public string TagName { get; init; } = string.Empty;
    public List<AliasFlagItem> Flags { get; init; } = new();
}

public record TagAliasMap
{
    public string TagName { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public int AliasIndex { get; init; } = 0;
    public string Flags { get; init; } = string.Empty;
}

// Hunspell formatiga AFF/DIC fayllariga konvertatsiya qiluvchi klass
public static class HunspellGenerator
{
    private const string AffixFileName = @".\Generated\uz.aff";
    private const string DictionaryFileName = @".\Generated\uz.dic";
    private const string DefaultCondition = ".";
    private const string DefaultStrip = "0";
    private const string DefaultMorphCode = "_";

    public static void Convert(AppSettings options, SuffixGrammar grammar, List<WordElement> words)
    {
        // SFX flaglarni generatsiya qilish
        var sfxList = GenerateSfxFlags(grammar);

        // SFX falg qatorlari va Alias xaritasini tayyorlash
        var (sfxAffixContent, mapFlags) = ProcessSfxContent(sfxList);

        // Aliaslarni guruhlash va indekslash
        var tagAlias = GroupAndIndexAliases(mapFlags, options.UseAliases, options.ShowLogs);

        // AFF faylga saqlash
        WriteAffixFile(options, sfxAffixContent, tagAlias);

        // DIC faylga saqlash
        WriteDictionaryFile(options, words, tagAlias);
    }

    // Qoidalardan SFXFlag to'plamini yaratish
    private static List<SFXFlag> GenerateSfxFlags(SuffixGrammar grammar)
    {
        var sfxList = new List<SFXFlag>();
        var flag = 0;

        foreach (var tag in grammar.Tags.Values)
        {
            var tagName = tag.Name;

            foreach (var tagItem in tag.Elements)
            {
                var result = tagItem.Suffixes
                    .Select(suffix => grammar.Suffixes[suffix])
                    .Aggregate(new SuffixSet(), (current, next) => Helper.JoinSuffixSets(current, next));

                // Har Id o'zgarishida SFX guruhlarini hosil qilish kerak
                var currentSfx = new SFXFlag();
                var lastId = 0;

                foreach (var item in result.Elements)
                {
                    if (lastId != item.Id)
                    {
                        if (currentSfx.Lines.Count > 0) sfxList.Add(currentSfx);

                        flag++;
                        currentSfx = new SFXFlag() with
                        {
                            FlagName = Helper.CreateLongFlag(flag),
                            TagName = tagName,
                            SetName = item.SetName,
                            ClassName = item.Class,
                            MorphCode = item.Name,
                            OnlyRoot = item.OnlyRoot
                        };
                        lastId = item.Id;
                    }

                    currentSfx.Lines.Add(new SFXFlagItem()
                    {
                        Text = item.Suffix,
                        Condition = string.IsNullOrEmpty(item.Condition.RegexPattern) ? DefaultCondition : item.Condition.RegexPattern,
                        Strip = string.IsNullOrEmpty(item.Condition.Strip) ? DefaultStrip : item.Condition.Strip
                    });
                }

                if (currentSfx.Lines.Count > 0) sfxList.Add(currentSfx);
            }
        }

        return sfxList;
    }

    // SFXFlag ro'yxatidan AFF fayldagi SFX flaglarini va Alias xaritasini yaratish
    private static (StringBuilder SfxAffixContent, Dictionary<string, AliasEntry> MapFlags) ProcessSfxContent(List<SFXFlag> sfxList)
    {
        var sb = new StringBuilder();
        var mapFlags = new Dictionary<string, AliasEntry>();

        foreach (var sfx in sfxList)
        {
            var tagKey = sfx.TagName;

            if (mapFlags.TryGetValue(tagKey, out var aliasEntry))
            {
                aliasEntry.Flags.Add(new AliasFlagItem() { ClassName = sfx.ClassName, FlagName = sfx.FlagName, SetName = sfx.SetName });
            }
            else
            {
                mapFlags.Add(tagKey, new AliasEntry()
                {
                    TagName = tagKey,
                    Flags = new List<AliasFlagItem> { new() { ClassName = sfx.ClassName, FlagName = sfx.FlagName, SetName = sfx.SetName } }
                });
            }

            var morphCodePart = (string.IsNullOrEmpty(sfx.MorphCode) || sfx.MorphCode.Equals(DefaultMorphCode)) ? "" : $" : {sfx.MorphCode}";
            sb.AppendLine($"# {sfx.TagName}{sfx.ClassName}/{sfx.SetName}{sfx.ClassName}{morphCodePart}");

            sb.AppendLine($"SFX {sfx.FlagName} Y {sfx.Lines.Count}");

            foreach (var item in sfx.Lines)
            {
                sb.AppendLine($"SFX {sfx.FlagName} {item.Strip} {item.Text} {item.Condition}");
            }

            sb.AppendLine();
        }

        return (sb, mapFlags);
    }

    // Alias flaglarini ClassName bo'yicha guruhlash va indekslarini aniqlash
    private static Dictionary<string, TagAliasMap> GroupAndIndexAliases(Dictionary<string, AliasEntry> mapFlags, bool useAlias, bool showResults)
    {
        var tagAlias = new Dictionary<string, TagAliasMap>();
        var aliasIndex = 0;

        foreach (var entry in mapFlags.Values)
        {
            var groupedFlags = entry.Flags.GroupBy(f => f.ClassName);

            foreach (var group in groupedFlags)
            {
                var className = group.Key;

                // FlagName larni birlashtirish
                var flags = group.Select(f => f.FlagName).Aggregate(new StringBuilder(), (sb, f) => sb.Append(f)).ToString();

                if (className.Length > 0)
                {
                    var setName = group.First().SetName;
                    var otherFlags = entry.Flags
                        .Where(f => f.ClassName == className && f.SetName != setName)
                        .Select(f => f.FlagName);

                    if (otherFlags.Any())
                    {
                        flags += otherFlags.Aggregate(new StringBuilder(), (sb, f) => sb.Append(f)).ToString();
                    }
                }

                // TagAliasMap ni yaratish va indekslash
                var key = className.Length == 0 ? entry.TagName : entry.TagName + className;

                tagAlias.Add(key, new TagAliasMap()
                {
                    AliasIndex = ++aliasIndex,
                    ClassName = className,
                    Flags = flags,
                    TagName = key
                });

                if (showResults)
                {
                    Console.WriteLine($"{aliasIndex}: {key} = {flags}");
                }
            }
        }

        return tagAlias;
    }

    // AFF faylni yaratish
    private static void WriteAffixFile(AppSettings options, StringBuilder sfxAffixContent, Dictionary<string, TagAliasMap> tagAlias)
    {
        var header = new StringBuilder();

        // AFF fayl sozlamalari
        header.AppendLine("LANG uz_UZ");
        header.AppendLine("SET UTF-8");
        header.AppendLine("FLAG long");
        header.AppendLine("WORDCHARS -‘");
        header.AppendLine();

        // Aliaslarni (AF) yozish
        if (options.UseAliases)
        {
            header.AppendLine($"AF {tagAlias.Count}");

            foreach (var entry in tagAlias.Values)
            {
                header.AppendLine($"AF {entry.Flags} # {entry.AliasIndex} {entry.TagName}");
            }
            header.AppendLine();
        }

        File.WriteAllText(AffixFileName, header.ToString());
        File.AppendAllText(AffixFileName, sfxAffixContent.ToString());
    }

    // DIC faylni yaratish
    private static void WriteDictionaryFile(AppSettings options, List<WordElement> words, Dictionary<string, TagAliasMap> tagAlias)
    {
        var sb = new StringBuilder();

        foreach (var word in words)
        {
            if (word.Tag.Length > 0)
            {
                var tags = word.Tag.Split('/');
                var flags = new StringBuilder();

                foreach (var tag in tags)
                {
                    if (tagAlias.TryGetValue(tag, out var aliasMap))
                    {
                        var flagValue = options.UseAliases
                            ? aliasMap.AliasIndex.ToString()
                            : aliasMap.Flags;

                        flags.Append(flagValue);
                        flags.Append('/');
                    }
                }

                // Oxiridagi ortiqcha '/' belgisini olib tashlash
                var finalFlags = flags.Length > 0 ? flags.ToString(0, flags.Length - 1) : string.Empty;
                sb.AppendLine($"{word.Word}/{finalFlags}");
            }
            else
            {
                sb.AppendLine(word.Word);
            }
        }

        // So'zlar sonini boshiga qo'shish (DIC fayl formatiga ko'ra)
        sb.Insert(0, words.Count + "\n");

        File.WriteAllText(DictionaryFileName, sb.ToString());
    }
}