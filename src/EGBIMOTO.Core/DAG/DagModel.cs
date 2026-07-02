using System;
using System.Collections.Generic;
using EGBIMOTO.Core.Manifest;

namespace EGBIMOTO.Core.DAG
{
    /// <summary>
    /// DAG planlayıcısının ürettiği tek adım planı.
    /// </summary>
    public sealed class PlannedStep
    {
        public EgStep          Step         { get; set; } = new();
        public int             Level        { get; set; }          // Topolojik seviye (0 = bağımsız)
        public bool            ParallelSafe { get; set; } = true;  // Transaction gerektirmiyor
        public List<string>    Reads        { get; set; } = new(); // Okuduğu değişkenler
        public List<string>    Writes       { get; set; } = new(); // Yazdığı değişkenler
        public string?         CacheKey     { get; set; }
    }

    /// <summary>
    /// Tüm manifest için üretilen yürütme planı.
    /// </summary>
    public sealed class DagPlan
    {
        public List<PlannedStep>       Steps       { get; set; } = new();
        public List<List<PlannedStep>> Levels      { get; set; } = new();
        public List<string>            Diagnostics { get; set; } = new();
        public bool                    Success     { get; set; }
        public string?                 Error       { get; set; }
    }

    /// <summary>
    /// DAG çalıştırma sonucu.
    /// v2.0: Telemetry alanları eklendi (TotalDuration, TotalSteps, CachedSteps, SkippedSteps, ManifestHash).
    /// </summary>
    public sealed class DagRunResult
    {
        public bool                        Success       { get; init; }
        public string?                     ErrorMessage  { get; init; }
        public string?                     ErrorStep     { get; init; }
        public Dictionary<string, object?> Vars          { get; init; } = new();
        public List<DagTraceRow>           Trace         { get; init; } = new();
        public List<string>                Log           { get; init; } = new();

        // v2.0 — Telemetry
        public string?   ManifestHash  { get; init; }
        public TimeSpan  TotalDuration { get; init; }
        public int       TotalSteps    { get; init; }
        public int       CachedSteps   { get; init; }
        public int       SkippedSteps  { get; init; }

        public ManifestRunResult ToManifestRunResult() => new()
        {
            Success      = Success,
            ErrorMessage = ErrorMessage,
            ErrorStep    = ErrorStep,
            Vars         = Vars,
            Log          = Log,
            TotalSteps   = TotalSteps,
            CachedSteps  = CachedSteps,
            SkippedSteps = SkippedSteps,
            DurationMs   = (long)TotalDuration.TotalMilliseconds,
        };
    }

    public sealed class DagTraceRow
    {
        public string StepId  { get; init; } = "";
        public string Op      { get; init; } = "";
        public bool   Success { get; init; }
        public string Status  { get; init; } = "";
        public long   Ms      { get; init; }
    }
}
