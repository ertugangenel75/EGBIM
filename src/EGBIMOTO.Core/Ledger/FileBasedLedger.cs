using System;
using System.IO;
using System.Text.Json;

namespace EGBIMOTO.Core.Ledger
{
    public interface IImmutableLedger
    {
        void AppendCommit(LedgerCommit commit);
        LedgerCommit? GetLastCommit();
    }

    public sealed class LedgerCommit
    {
        public string   ManifestTitle   { get; init; } = "";
        public DateTime Timestamp       { get; init; }
        public int      ElementChanges  { get; init; }
        public string   Hash            { get; init; } = "";
        public string   PreviousHash    { get; init; } = "";
    }

    public sealed class FileBasedLedger : IImmutableLedger
    {
        private readonly string _ledgerPath;
        private readonly object _lock = new();

        public FileBasedLedger(string addinDir)
        {
            _ledgerPath = Path.Combine(addinDir, "ledger.jsonl");
        }

        public void AppendCommit(LedgerCommit commit)
        {
            var line = JsonSerializer.Serialize(commit);
            lock (_lock)
                File.AppendAllText(_ledgerPath, line + Environment.NewLine);
        }

        public LedgerCommit? GetLastCommit()
        {
            if (!File.Exists(_ledgerPath)) return null;
            var lines = File.ReadAllLines(_ledgerPath);
            if (lines.Length == 0) return null;
            try { return JsonSerializer.Deserialize<LedgerCommit>(lines[^1]); }
            catch { return null; }
        }
    }
}
