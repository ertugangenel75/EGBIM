using System;
using System.Collections.Generic;
using System.Text.Json;

namespace EGBIMOTO.Core.Ops
{
    /// <summary>
    /// Her op'a verilen bağlam nesnesi.
    /// Revit-bağımlı kısımlar RevitOpContext'te genişletilir.
    ///
    /// Op imzası:
    ///   public static object? MyOp(OpContext ctx)
    ///
    /// Params erişimi (tip güvenli):
    ///   var category  = ctx.GetString("category", "Walls");
    ///   var limit     = ctx.GetInt("limit", 100);
    ///
    /// Input erişimi — iki yöntem:
    ///   // Geriye uyumlu (sessiz default):
    ///   var elements = ctx.Input as List<Element> ?? new();
    ///
    ///   // Tip güvenli (açık hata mesajı — önerilir):
    ///   var elements = ctx.InputAs<List<Element>>();
    /// </summary>
    public class OpContext
    {
        // ── Step kimliği (ManifestRunner tarafından set edilir) ───────────────
        /// <summary>Şu an çalışan step'in id'si — hata mesajları için.</summary>
        public string CurrentStepId { get; set; } = "";

        // ── IO ────────────────────────────────────────────────────────────────
        /// <summary>Önceki step'in çıktısı ("from" referansı)</summary>
        public object? Input { get; set; }

        /// <summary>Manifest step'indeki params bloğu (key → unwrapped değer)</summary>
        public IReadOnlyDictionary<string, object?> Params { get; set; }
            = new Dictionary<string, object?>();

        /// <summary>Tüm step sonuçları — ops arası veri paylaşımı için (read-only)</summary>
        public IReadOnlyDictionary<string, object?> Vars { get; set; }
            = new Dictionary<string, object?>();

        /// <summary>Log fonksiyonu — ManifestRunner.Log listesine ekler</summary>
        public Action<string> Log { get; set; } = _ => { };

        // ── Tip güvenli input erişimi ─────────────────────────────────────────

        /// <summary>
        /// Input'u T tipine dönüştürür. Başarısız olursa açık hata mesajıyla exception fırlatır.
        /// "from" referansı yanlış step'e bağlandığında sessiz default yerine net hata verir.
        ///
        /// Örnek:
        ///   var elements = ctx.InputAs&lt;List&lt;Element&gt;&gt;();
        ///   // Başarısız → EgInputTypeMismatchException: Beklenen List<Element>, gelen RowList.
        /// </summary>
        public T InputAs<T>()
        {
            if (Input is T typed) return typed;

            var expectedType = typeof(T).Name;
            var actualType   = Input is null ? "null" : Input.GetType().Name;
            throw new EgInputTypeMismatchException(expectedType, actualType, CurrentStepId);
        }

        /// <summary>
        /// Input'u T tipine dönüştürür. Başarısız olursa defaultValue döner (sessiz).
        /// Geriye uyumluluk veya optional input için kullan.
        /// </summary>
        public T InputAsOrDefault<T>(T defaultValue = default!)
        {
            if (Input is T typed) return typed;

            // inputs.elements / inputs.rows / inputs.input — manifest üzerinden gelen referanslar
            foreach (var key in new[] { "elements", "rows", "input", "items", "list" })
            {
                if (Params.TryGetValue(key, out var pv) && pv is T tv)
                    return tv;
            }
            return defaultValue;
        }

        /// <summary>
        /// Input null veya boş mu? (List ve Dictionary için)
        /// </summary>
        public bool HasInput => Input is not null;

        // ── Tip-güvenli param erişimi ──────────────────────────────────────────

        public T GetParam<T>(string key, T defaultValue = default!)
        {
            if (!Params.TryGetValue(key, out var raw)) return defaultValue;
            if (raw is T typed) return typed;

            // JsonElement unwrap (JSON deserializasyonundan geliyorsa)
            if (raw is JsonElement je)
            {
                try
                {
                    var result = JsonSerializer.Deserialize<T>(je.GetRawText());
                    return result ?? defaultValue;
                }
                catch { return defaultValue; }
            }

            try { return (T)Convert.ChangeType(raw, typeof(T))!; }
            catch { return defaultValue; }
        }

        public string  GetString(string key, string  def = "")     => GetParam(key, def);
        public int     GetInt   (string key, int     def = 0)      => GetParam(key, def);
        public double  GetDouble(string key, double  def = 0.0)    => GetParam(key, def);
        public bool    GetBool  (string key, bool    def = false)   => GetParam(key, def);

        /// <summary>Params'tan liste okur. Kullanım: ctx.GetList&lt;string&gt;("fields")</summary>
        public List<T> GetList<T>(string key)
            => GetParam<List<T>>(key, new List<T>());

        /// <summary>Params'tan string listesi okur. Kısayol: ctx.GetStringList("keep")</summary>
        public List<string> GetStringList(string key)
            => GetList<string>(key);

        /// <summary>Params'ta zorunlu key kontrolü. Yoksa manifest'i bilgilendiren hata fırlatır.</summary>
        public string RequireString(string key)
        {
            var val = GetString(key);
            if (string.IsNullOrWhiteSpace(val))
                throw new InvalidOperationException(
                    $"[OpContext] Zorunlu param eksik: '{key}'. " +
                    $"Step '{CurrentStepId}' için manifest'e ekleyin.");
            return val;
        }

        // ── v13: Require* ailesi — 'required' semantiğinin tek çalışma zamanı kaynağı ──
        // Kontrat üretici (deploy/generate_op_contracts.py) presence="required"
        // işaretini YALNIZCA Require* çağrılarından türetir. GetX(key) → recommended,
        // GetX(key, default) → optional. Yeni op yazarken zorunlu paramlar için
        // daima Require* kullanın.

        /// <summary>Zorunlu int param. Yoksa/parse edilemezse hata fırlatır.</summary>
        public int RequireInt(string key)
        {
            if (!Params.ContainsKey(key))
                throw new InvalidOperationException(
                    $"[OpContext] Zorunlu param eksik: '{key}'. " +
                    $"Step '{CurrentStepId}' için manifest'e ekleyin.");
            return GetInt(key);
        }

        /// <summary>Zorunlu double param. Yoksa/parse edilemezse hata fırlatır.</summary>
        public double RequireDouble(string key)
        {
            if (!Params.ContainsKey(key))
                throw new InvalidOperationException(
                    $"[OpContext] Zorunlu param eksik: '{key}'. " +
                    $"Step '{CurrentStepId}' için manifest'e ekleyin.");
            return GetDouble(key);
        }

        /// <summary>Zorunlu liste param. Yoksa veya boşsa hata fırlatır.</summary>
        public List<T> RequireList<T>(string key)
        {
            var val = GetList<T>(key);
            if (val.Count == 0)
                throw new InvalidOperationException(
                    $"[OpContext] Zorunlu liste param eksik veya boş: '{key}'. " +
                    $"Step '{CurrentStepId}' için manifest'e ekleyin.");
            return val;
        }

        /// <summary>Zorunlu string listesi. Kısayol: RequireList&lt;string&gt;.</summary>
        public List<string> RequireStringList(string key)
            => RequireList<string>(key);
    }
}
