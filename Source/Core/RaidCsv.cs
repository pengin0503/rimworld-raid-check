using System.Globalization;
using System.Text;
namespace RaidCheck.Core;
public sealed record CsvFaction(PreviewChoice Choice, string Name, PreviewSession? Session);
public static class RaidCsv
{
    private static readonly string[] Header = ["row_type","faction_id","faction_name","map","game_ticks","strategy","arrival","base_points","requested","attempted","status","minimum","median","maximum","successful_sample_index","adjusted_points","kind_id","kind_label","count","combat_power","error"];
    public static void Write(TextWriter writer, IReadOnlyList<CsvFaction> factions)
    {
        Line(writer, Header);
        foreach (var faction in factions)
        {
            var s=faction.Session;var snap=s?.Snapshot;var choice=faction.Choice;
            string[] row = ["faction", choice.FactionId, faction.Name, snap?.MapLabel ?? "", N(snap?.GameTicks), choice.StrategyId, choice.ArrivalId, N(snap?.BasePoints), N(choice.SampleCount), N(s?.Attempted), s?.State.ToString() ?? "NotStarted", N(s?.Summary?.Minimum), N(s?.Summary?.Median), N(s?.Summary?.Maximum), "", "", "", "", "", "", string.Join(";",s?.Failures.Select(f=>f.Code).Distinct() ?? [])];
            Line(writer,row);
            if (s?.Summary is { } summary)
                foreach(var kind in summary.Kinds){var item=(string[])row.Clone();item[0]="kind";item[16]=kind.DefName;item[17]=kind.Label;item[18]=N(kind.MeanCount);item[20]="";Line(writer,item);}
            if(s is null)continue;
            for(int i=0;i<s.Results.Count;i++)
            {
                var sample=s.Results[i];
                foreach(var group in SampleGroups.ByDefName(sample.Pawns))
                {
                    var item=(string[])row.Clone();item[0]="sample_kind";item[14]=N(i+1);item[15]=N(sample.AdjustedPoints);item[16]=group.Key;item[17]=group.Value.First.Label;item[18]=N(group.Value.Count);item[19]=N(group.Value.First.CombatPower);item[20]="";Line(writer,item);
                }
            }
        }
    }
    public static string WriteFile(string folder, IReadOnlyList<CsvFaction> factions)
    {
        Directory.CreateDirectory(folder);
        string stem="RaidCheck-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N");
        string final=Path.Combine(folder,stem+".csv"), temp=Path.Combine(folder,stem+".tmp");
        try
        {
            using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            using(var writer=new StreamWriter(file,new UTF8Encoding(true))) Write(writer,factions);
            File.Move(temp,final,false);return final;
        }
        finally { if(File.Exists(temp))File.Delete(temp); }
    }
    private static string N<T>(T? value) where T:struct,IFormattable => value.HasValue ? value.Value.ToString(null,CultureInfo.InvariantCulture) : "";
    private static string N<T>(T value) where T:struct,IFormattable => value.ToString(null,CultureInfo.InvariantCulture);
    private static void Line(TextWriter writer,IEnumerable<string> cells) => writer.WriteLine(string.Join(",",cells.Select(Escape)));
    private static string Escape(string value)
    {
        var trimmed=value.TrimStart();
        if(trimmed.Length>0 && "=+-@\t\r".Contains(trimmed[0])) value="'"+value;
        return value.IndexOfAny([',','"','\r','\n'])>=0 ? "\""+value.Replace("\"","\"\"")+"\"" : value;
    }
}
