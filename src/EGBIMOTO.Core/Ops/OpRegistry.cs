using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EGBIMOTO.Core.Ops
{
    /// <summary>
    /// Tüm [EgOp] metodlarını tutan ve çalıştıran merkezi kayıt.
    /// </summary>
    public sealed class OpRegistry
    {
        public static OpRegistry Instance { get; set; } = new();

        private readonly Dictionary<string, MethodInfo>    _methods    = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, EgOpAttribute> _attributes = new(StringComparer.OrdinalIgnoreCase);

        // ── Kayıt ─────────────────────────────────────────────────────────────

        public void ScanAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var attr = method.GetCustomAttribute<EgOpAttribute>();
                if (attr is null) continue;

                if (_methods.ContainsKey(attr.Name))
                    throw new InvalidOperationException(
                        $"[OpRegistry] Duplicate op: '{attr.Name}' zaten kayıtlı. " +
                        $"Çakışan sınıf: {type.FullName}.{method.Name}");

                _methods[attr.Name]    = method;
                _attributes[attr.Name] = attr;
            }
        }

        public void Register(string name, MethodInfo method, string description = "", string category = "Genel",
            bool requiresTransaction = false)
        {
            if (_methods.ContainsKey(name))
                throw new InvalidOperationException($"[OpRegistry] Duplicate op: '{name}' zaten kayıtlı.");
            _methods[name]    = method;
            _attributes[name] = new EgOpAttribute(name)
            {
                Description          = description,
                Category             = category,
                RequiresTransaction  = requiresTransaction
            };
        }

        // ── Çalıştırma ────────────────────────────────────────────────────────

        public object? Execute(string opName, OpContext context)
        {
            if (!_methods.TryGetValue(opName, out var method))
            {
                var registered = string.Join(", ", _methods.Keys.OrderBy(k => k));
                throw new InvalidOperationException(
                    $"Op bulunamadı: '{opName}'.\nKayıtlı op'lar: [{registered}]");
            }
            return method.Invoke(null, new object?[] { context });
        }

        public bool Has(string opName) => _methods.ContainsKey(opName);

        /// <summary>
        /// ManifestLinter için: op'un MethodInfo'sunu döner.
        /// EgOpContractAttribute tip kontrolünde kullanılır.
        /// </summary>
        public System.Reflection.MethodInfo? GetMethod(string opName)
            => _methods.TryGetValue(opName, out var m) ? m : null;

        // ── Sorgulama ─────────────────────────────────────────────────────────

        public IReadOnlyList<string> GetNames()
            => _methods.Keys.OrderBy(k => k).ToList();

        /// <summary>
        /// Tüm kayıtlı op'ların metadata'sını döner.
        /// RequiresTransaction: DagPlanner paralel safe kararı için kullanır.
        /// </summary>
        public IReadOnlyList<(string Name, string Description, string Category, bool RequiresTransaction)> GetAll()
            => _attributes.Values
                .OrderBy(a => a.Category).ThenBy(a => a.Name)
                .Select(a => (a.Name, a.Description, a.Category, a.RequiresTransaction))
                .ToList();

        /// <summary>Belirli bir op'un transaction gerektirip gerektirmediğini döner.</summary>
        public bool RequiresTransaction(string opName)
            => _attributes.TryGetValue(opName, out var a) && a.RequiresTransaction;

        public int Count => _methods.Count;
    }
}
