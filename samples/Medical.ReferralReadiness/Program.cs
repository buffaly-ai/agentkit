using Buffaly.AgentKit;
using Buffaly.AgentKit.AspNetCore;
using Buffaly.AgentKit.ProtoScript;
using Buffaly.AgentKit.SampleSupport;
using Medical.ReferralReadiness.Repositories;
using Medical.ReferralReadiness.Tools;
using Medical.ReferralReadiness.Scenarios;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);
string contentRoot = builder.Environment.ContentRootPath;
string dataRoot = Path.Combine(contentRoot, "Data");
string storageRoot = Path.Combine(contentRoot, ".agentkit");
Directory.CreateDirectory(storageRoot);

builder.Services.AddSingleton<IReferralRepository>(new JsonReferralRepository(dataRoot));
builder.Services.AddSingleton<IChatClient>(SampleChatClientFactory.Create(ReferralScenarioFactory.Create()));
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

