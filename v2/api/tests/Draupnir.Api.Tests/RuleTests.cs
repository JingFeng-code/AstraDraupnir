using System.Text.Json;
using Draupnir.Api;

namespace Draupnir.Api.Tests;

public sealed class RuleTests
{
    [Fact]
    public void ReachabilityTraversesIndirectPathsAndStopsOnCycles()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var edges = new[]
        {
            new WorkItemDependency { WorkItemId = a, DependsOnId = b },
            new WorkItemDependency { WorkItemId = b, DependsOnId = c },
            new WorkItemDependency { WorkItemId = c, DependsOnId = b },
        };
        Assert.True(ApiRules.Reaches(a, c, edges));
        Assert.True(ApiRules.Reaches(a, a, edges));
        Assert.False(ApiRules.Reaches(c, a, edges));
    }

    [Theory]
    [InlineData("{}", 1)]
    [InlineData("{\"attempts\":0}", 1)]
    [InlineData("{\"attempts\":3}", 3)]
    [InlineData("{\"attempt\":2,\"attempts\":3}", 2)]
    public void AttemptsSupportBothSupervisorFormats(string json, int expected) =>
        Assert.Equal(expected, SupervisorImport.Attempt(JsonSerializer.Deserialize<JsonElement>(json)));

    [Fact]
    public void FingerprintsAndGeneratedKeysAreStable()
    {
        Assert.Equal(SupervisorImport.Fingerprint("log"), SupervisorImport.Fingerprint("log"));
        Assert.NotEqual(SupervisorImport.Fingerprint("log"), SupervisorImport.Fingerprint("other"));
        Assert.Equal(SupervisorImport.ProjectKey("sandbox"), SupervisorImport.ProjectKey(" SANDBOX "));
        Assert.Matches("^[A-Z]{10}$", SupervisorImport.ProjectKey("sandbox"));
    }

    [Theory]
    [InlineData("PASS", true)]
    [InlineData("CHANGES_REQUESTED", true)]
    [InlineData("reviewer assigned", false)]
    public void ReviewVerdictRequiresExplicitDecision(string input, bool expected) =>
        Assert.Equal(expected, SupervisorImport.TryVerdict(input, out _));

    [Fact]
    public void GateNeedsAnExceptionUnlessTheWorkItemEvidencePasses()
    {
        Assert.True(GateRules.NeedsException([], []));
        Assert.True(GateRules.NeedsException([EvidenceResult.Fail], []));
        Assert.True(GateRules.NeedsException([EvidenceResult.Pass], [Verdict.ChangesRequested]));
        Assert.True(GateRules.NeedsException([EvidenceResult.NotTested], [Verdict.Pass]));
        Assert.False(GateRules.NeedsException([EvidenceResult.Pass], []));
        Assert.False(GateRules.NeedsException([EvidenceResult.Pass, EvidenceResult.NotTested], [Verdict.Pass]));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("核准", false)]
    [InlineData("【人工例外決定：缺少或未通過必要證據】太短", false)]
    [InlineData("【人工例外決定：缺少或未通過必要證據】這是十八個字的理由這是十八個字的理由", false)]
    [InlineData("【人工例外決定：缺少或未通過必要證據】測試環境沒有真實證據，由管理者承擔風險後放行", true)]
    [InlineData("【人工例外決定：缺少或未通過必要證據】   測試環境沒有真實證據，由管理者承擔風險後放行   ", true)]
    [InlineData("沒有前綴但理由很長很長很長很長很長很長很長很長很長", false)]
    public void ExceptionNoteNeedsThePrefixAndTwentyCharacters(string? note, bool expected)
    {
        Assert.Equal(expected, GateRules.IsValidExceptionNote(note));
    }
}
