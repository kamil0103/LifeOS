using System.Text.Json;
using LifeOS.Application.DTOs.Experience;
namespace LifeOS.Application.Services;
public static class AccomplishmentRules
{
    public static List<AccomplishmentDto> Read(string json) => JsonSerializer.Deserialize<List<AccomplishmentDto>>(json) ?? [];
    public static List<AccomplishmentDto> Validate(List<AccomplishmentDto> rows)
    {
        if(rows.Count>30)throw new InvalidOperationException("Use at most 30 accomplishments per record.");
        var result=rows.Select(row=>{
            if(row==null||string.IsNullOrWhiteSpace(row.Action)||row.Action.Length>1000||(row.Outcome?.Length??0)>1000||(row.Metric?.Length??0)>300)throw new InvalidOperationException("Each accomplishment needs an action (up to 1000 characters), an optional outcome (1000) and metric (300).");
            return new AccomplishmentDto(row.Action.Trim(),ProfessionalRecordRules.Text(row.Outcome),ProfessionalRecordRules.Text(row.Metric));
        }).ToList();
        if(Text(result).Length>20000)throw new InvalidOperationException("Keep the combined accomplishments within 20,000 characters.");
        return result;
    }
    public static string Text(IEnumerable<AccomplishmentDto> rows)=>string.Join("\n",rows.Select(row=>string.Join(" — ",new[]{row.Action,row.Outcome,row.Metric}.Where(x=>!string.IsNullOrWhiteSpace(x)))));
}
