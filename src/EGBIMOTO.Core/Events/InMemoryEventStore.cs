using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace EGBIMOTO.Core.Events
{
    public interface IEventStore
    {
        void Append(ManifestStepEvent @event);
        IReadOnlyList<ManifestStepEvent> Replay(string manifestId);
    }

    public sealed class ManifestStepEvent
    {
        public string   ManifestId  { get; init; } = "";
        public string   StepId      { get; init; } = "";
        public string   OpName      { get; init; } = "";
        public long     DurationMs  { get; init; }
        public bool     Success     { get; init; }
        public DateTime Timestamp   { get; init; }
        public string   VarsHash    { get; init; } = "";
    }

    public sealed class InMemoryEventStore : IEventStore
    {
        private readonly ConcurrentDictionary<string, List<ManifestStepEvent>> _store = new();

        public void Append(ManifestStepEvent @event)
        {
            var list = _store.GetOrAdd(@event.ManifestId, _ => new List<ManifestStepEvent>());
            lock (list) list.Add(@event);
        }

        public IReadOnlyList<ManifestStepEvent> Replay(string manifestId)
            => _store.TryGetValue(manifestId, out var list)
                ? list.AsReadOnly()
                : Array.Empty<ManifestStepEvent>();
    }
}
