using Buffaly.AgentKit;
using Buffaly.AgentKit.AspNetCore;
using Buffaly.AgentKit.ProtoScript;
using Buffaly.AgentKit.SampleSupport;
using Medical.ReferralReadiness.Repositories;
using Medical.ReferralReadiness.Tools;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);
string contentRoot = builder.Environment.ContentRootPath;
string dataRoot = Path.Combine(contentRoot, "Data");
string storageRoot = Path.Combine(contentRoot, ".agentkit");
Directory.CreateDirectory(storageRoot);

builder.Services.AddSingleton<IReferralRepository>(new JsonReferralRepository(dataRoot));
builder.Services.AddSingleton<IChatClient>(SampleChatClientFactory.Create(ReferralScenario.Create()));
builder.Services.AddSingleton<IReadOnlyList<AIFunction>>(sp =>
{
    var repo = sp.GetRequiredService<IReferralRepository>();
    ProtoScriptToolSet proto = ProtoScriptToolSet.LoadAsync(Path.Combine(contentRoot, "AgentTools", "agentkit.json")).GetAwaiter().GetResult();
    return ReferralFunctions.Create(repo).Concat(proto.Tools).ToArray();
});
builder.Services.AddBuffalyAgentKit(b => b.UseJsonlStore(storageRoot));

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapBuffalyAgentKit("/_agentkit");
app.MapGet("/referrals", async (IReferralRepository repo, CancellationToken ct) => Results.Json(await repo.ListReferralsAsync(ct)));
app.MapGet("/", () => Results.Redirect("/referrals.html"));
app.Run();

public static class ReferralScenario
{
    public static ScenarioDefinition Create() => new()
    {
        ScenarioId = "referral-ref-1003",
        Responses = new[]
        {
            ScriptedChatResponse.Call("get_referral_facts", new() { ["referral_id"] = "REF-1003" }),
            ScriptedChatResponse.Call("get_referral_requirements", new() { ["service_line"] = "Orthopedic consultation" }, expectedLastToolName: "get_referral_facts", expectedLastToolResultContains: "REF-1003"),
            ScriptedChatResponse.Call("assess_referral_readiness", new() { ["serviceLine"] = "Orthopedic consultation", ["hasSignedOrder"] = false, ["hasInsuranceAuthorization"] = true, ["hasClinicalSummary"] = true, ["hasRelevantImaging"] = false }, expectedLastToolName: "get_referral_requirements", expectedLastToolResultContains: "Orthopedic consultation"),
            ScriptedChatResponse.Final("REF-1003 is not administratively ready for scheduling. Missing administrative items: signed referral order and relevant imaging report. Draft neutral request: Please send the signed referral order and relevant imaging report for REF-1003 so the scheduling packet can be completed. This is an administrative completeness review only; it is not diagnosis, triage, treatment, or clinical advice.", expectedLastToolName: "assess_referral_readiness", expectedLastToolResultContains: "signed referral order")
        }
    };
}
