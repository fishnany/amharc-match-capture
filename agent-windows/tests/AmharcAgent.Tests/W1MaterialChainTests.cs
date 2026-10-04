using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using AmharcAgent.Core.Contracts;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Data.Repositories;
using AmharcAgent.Infrastructure.Clock;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.X509;
using Xunit;

namespace AmharcAgent.Tests;

public sealed class W1MaterialChainTests
{
    private sealed class Time : TimeProvider
    { public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-10-04T10:00:00Z"); }
    private sealed class SnapshotJournal(List<W1JournalActivity> records, W1JournalState checkpoint) : IW1ClockJournal
    {
        public W1JournalState? Read(string subject) => checkpoint;
        public W1JournalActivity? ReadActivity(string id) => records.SingleOrDefault(a => a.Id == id);
        public W1JournalActivity? FindOperation(string subject, string key) => records.SingleOrDefault(a => a.OperationKey == key);
        public IReadOnlyList<W1JournalActivity> ReadActivities(string subject) => records;
        public void Commit(long revision, W1JournalState state, W1JournalActivity activity) => throw new NotSupportedException();
    }
    [Theory]
    [InlineData("valid")]
    [InlineData("missing-parent")]
    [InlineData("tampered-parent")]
    [InlineData("cross-subject-parent")]
    [InlineData("cycle")]
    [InlineData("competing-heads")]
    [InlineData("stale-checkpoint")]
    [InlineData("missing-historical-dependency")]
    [InlineData("legacy-boundary")]
    public void ActualMaterialRecords_BoundedHistoricalFaultMatrix(string scenario)
    {
        var path = Path.Combine(Path.GetTempPath(), "w1-chain-" + Guid.NewGuid() + ".sqlite");
        var seed = RandomNumberGenerator.GetBytes(32);
        try
        {
            var vector = JsonNode.Parse(File.ReadAllText(Path.Combine(W1ControlledVectorTests.DependenciesDirectory(),
                "W1_CONFORMANCE_VECTORS.json")))!["cases"]![0]!["input"]!["snapshot"]!;
            var identity = (JsonObject)vector["subject"]!.DeepClone();
            var context = (JsonObject)vector["context"]!.DeepClone();
            var subject = identity["occurrenceId"]!.GetValue<string>();
            var publicDer = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(
                new Ed25519PrivateKeyParameters(seed, 0).GeneratePublicKey()).GetEncoded();
            var pem = "-----BEGIN PUBLIC KEY-----\n" + Convert.ToBase64String(publicDer) + "\n-----END PUBLIC KEY-----\n";
            var verifier = new W1DevelopmentGrantVerifier(new(pem, new HashSet<string> { subject }, "official-match-accumulated"));
            var original = new W1SqliteClockJournal(path);
            var runtime = new W1SubjectBoundRuntime(identity, context, original, "synthetic-chain", "targeted-build",
                "p1", W1ControlledVectorTests.LoadDependencies(), new Time());
            var bundle = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(
                W1ControlledVectorTests.DependenciesDirectory(), "fixtures/trustBundle.json")))!;
            bundle["grant"]!["incarnationId"] = runtime.IncarnationId; bundle["principalPublicKey"] = pem;
            var d = W1CanonicalJson.Digest(bundle.ToJsonString(), "governanceProof");
            bundle["governanceProof"] = new JsonObject { ["payloadSha256"] = d, ["signatureBase64"] = W1CanonicalJson.Sign(d, seed) };
            runtime.AdoptVerifiedDevelopmentGrant(subject, verifier.Verify(bundle, subject, runtime.IncarnationId), "activation");
            runtime.Transition(subject, "correct", "correction", 120, basis: "synthetic");
            var records = original.ReadActivities(subject).ToList();
            var checkpoint = original.Read(subject)!;
            var root = records.Single(a => JsonNode.Parse(a.Json)!["inputActivity"] is null);
            var child = records.Single(a => a.Id != root.Id);
            W1JournalActivity Rewrite(W1JournalActivity a, Action<JsonObject> modify)
            {
                var node = (JsonObject)JsonNode.Parse(a.Json)!; modify(node);
                return a with { Json = node.ToJsonString() };
            }
            switch (scenario)
            {
                case "missing-parent": records.Remove(root); break;
                case "tampered-parent":
                    records[records.IndexOf(root)] = Rewrite(root, a => a["sourceBasis"] = "tampered"); break;
                case "cross-subject-parent":
                    records[records.IndexOf(root)] = Rewrite(root, a => a["subject"] = Guid.NewGuid().ToString("D")); break;
                case "cycle":
                    records[records.IndexOf(root)] = Rewrite(root, a => a["inputActivity"] = child.Id); break;
                case "competing-heads":
                    var competingId = Guid.NewGuid().ToString("D");
                    var competing = Rewrite(root, a => {
                        a["activityId"] = competingId; a["identifier"] = "urn:amharc:w1:activity:" + competingId;
                        a["logicalOperationKey"] = "competing";
                    }) with { Id = competingId, OperationKey = "competing" };
                    records.Add(competing); break;
                case "stale-checkpoint": checkpoint = checkpoint with { ActivityId = root.Id }; break;
            }
            var dependencies = scenario == "missing-historical-dependency" ? new W1DependencyResolver() : W1ControlledVectorTests.LoadDependencies();
            var journal = new SnapshotJournal(records, checkpoint);
            string Check()
            {
                var head = W1MaterialChain.CurrentHead(journal, checkpoint, dependencies);
                return W1MaterialChain.Validate(head, subject, context, dependencies, verifier);
            }
            if (scenario == "valid") Assert.Equal("QUALIFIED", Check());
            else if (scenario == "legacy-boundary")
            {
                var boundary = Rewrite(root, a => {
                    a["baselineStanding"] = "legacy-assurance-gap";
                    a.Remove("historicalGrantEvidence");
                });
                var newRootRef = W1MaterialChain.Reference(boundary);
                var changedChild = Rewrite(child, a => a["parentRef"] = new JsonObject {
                    ["id"] = newRootRef.Id, ["sha256"] = newRootRef.Sha256 });
                dependencies.Add(newRootRef, Encoding.UTF8.GetBytes(boundary.Json));
                var head = W1MaterialChain.Reference(changedChild);
                dependencies.Add(head, Encoding.UTF8.GetBytes(changedChild.Json));
                Assert.Equal("LEGACY_ASSURANCE_GAP", W1MaterialChain.Validate(head, subject, context, dependencies, verifier));
            }
            else Assert.Throws<InvalidOperationException>(() => Check());
        }
        finally { CryptographicOperations.ZeroMemory(seed); File.Delete(path); }
    }
    [Fact]
    public void PersistedMaterialHistoryRejectsUpdateAndDelete()
    {
        var path = Path.Combine(Path.GetTempPath(), "w1-immutable-" + Guid.NewGuid() + ".sqlite");
        try
        {
            var journal = new W1SqliteClockJournal(path);
            journal.Commit(0, new("synthetic", 1, "1.0.0", "{}", "a"), new("a", "synthetic", "op", "hash", "{}"));
            using var c = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + path); c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE W1ClockActivities SET Json='changed'";
            Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => cmd.ExecuteNonQuery());
            cmd.CommandText = "DELETE FROM W1ClockActivities";
            Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => cmd.ExecuteNonQuery());
            Assert.NotNull(journal.ReadActivity("a"));
        }
        finally { File.Delete(path); }
    }
}