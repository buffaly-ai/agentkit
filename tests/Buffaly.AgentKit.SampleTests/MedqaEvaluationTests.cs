using Medical.MedqaEvaluation;
using System.Text.Json;
using Xunit;

namespace Buffaly.AgentKit.SampleTests;

public sealed class MedqaEvaluationTests
{
    [Fact]
    public void RenderPrompt_MatchesArm1TemplateExactly()
    {
        var item = new MedqaCase { SourceCaseId = 1, Question = "Q?", Options = new MedqaOptions { A = "a", B = "b", C = "c", D = "d" }, Answer = "A" };
        string expected = "You are answering a medical multiple-choice question from the MedQA-USMLE dataset.\n\nRead the following clinical question and the four answer options (A, B, C, D).\n\nSelect the single best answer. Respond with ONLY the letter of your chosen option: A, B, C, or D.\n\nDo NOT use any external tools, lookups, web searches, or medical ontology references.\nDo NOT explain your reasoning.\nDo NOT output anything other than the single letter A, B, C, or D.\n\nQuestion:\nQ?\n\nOptions:\nA. a\nB. b\nC. c\nD. d\n\nAnswer:\n";
        Assert.Equal(expected, Program.RenderPrompt(item));
    }

    [Theory]
    [InlineData("B", "B", "clean")]
    [InlineData("B.", "B", "clean")]
    [InlineData("The answer is B", "B", "verbose_recovered")]
    [InlineData("A is wrong, B is correct", "A", "verbose_recovered")]
    public void ParseAnswer_FollowsArm1Algorithm(string raw, string answer, string status)
    {
        Assert.Equal((answer, status), Program.ParseAnswer(raw, "ok"));
    }

    [Fact]
    public async Task FixtureRun_ResumesAndIsolatesShards()
    {
        string folder = Path.Combine(Path.GetTempPath(), "agentkit-medqa-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string input = Path.Combine(folder, "input.jsonl");
            File.WriteAllLines(input, new[]
            {
                "{\"source_case_id\":1,\"question\":\"[fixture:B] Q1\",\"options\":{\"A\":\"a\",\"B\":\"b\",\"C\":\"c\",\"D\":\"d\"},\"answer\":\"B\"}",
                "{\"source_case_id\":2,\"question\":\"[fixture:D] Q2\",\"options\":{\"A\":\"a\",\"B\":\"b\",\"C\":\"c\",\"D\":\"d\"},\"answer\":\"D\"}"
            });
            string output0 = Path.Combine(folder, "shard-0.jsonl"); string metrics0 = Path.Combine(folder, "shard-0.metrics.json");
            string output1 = Path.Combine(folder, "shard-1.jsonl"); string metrics1 = Path.Combine(folder, "shard-1.metrics.json");
            await Program.RunAsync(new RunOptions(input, output0, metrics0, "openai", "gpt-5.5", "medium", 0, 2, true));
            await Program.RunAsync(new RunOptions(input, output1, metrics1, "openai", "gpt-5.5", "medium", 1, 2, true));
            await Program.RunAsync(new RunOptions(input, output0, metrics0, "openai", "gpt-5.5", "medium", 0, 2, true));
            string merged = Path.Combine(folder, "merged.jsonl");
            Program.MergeShards(new[] { output0, output1 }, merged);
            Assert.Single(File.ReadLines(output0));
            Assert.Single(File.ReadLines(output1));
            Assert.Equal(new[] { 1, 2 }, File.ReadLines(merged).Select(line => JsonDocument.Parse(line).RootElement.GetProperty("Source_Case_Id").GetInt32()));
            Assert.Contains("\"Correct\": 1", File.ReadAllText(metrics0), StringComparison.Ordinal);
            Assert.Contains("\"Correct\": 1", File.ReadAllText(metrics1), StringComparison.Ordinal);
            Assert.True(File.Exists(metrics0 + ".manifest.json"));
        }
        finally { Directory.Delete(folder, true); }
    }
}
