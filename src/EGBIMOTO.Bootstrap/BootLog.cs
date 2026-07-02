// Copyright 2026 Ertuğan Genel
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.IO;
using System.Text;

namespace EGBIMOTO.Bootstrap
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  BootLog  —  Bootstrap aşaması logger'ı
    //
    //  Engine yüklenmeden ÖNCE çalışır, bu yüzden Serilog veya engine'in
    //  log altyapısına bağımlı olamaz. Saf BCL ile %AppData%\EGBIMOTO\logs\'a yazar.
    //
    //  Engine yükleme başarısız olursa, sorunun ne olduğunu bu log dosyasından
    //  görebilirsiniz: %AppData%\EGBIMOTO\logs\bootstrap_YYYYMMDD.log
    // ═══════════════════════════════════════════════════════════════════════════

    internal static class BootLog
    {
        private static readonly string LogDir =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EGBIMOTO", "logs");

        private static string LogFile =>
            Path.Combine(LogDir, $"bootstrap_{DateTime.Now:yyyyMMdd}.log");

        public static void Info(string message) => Write("INFO ", message);

        public static void Error(string message, Exception? ex = null)
        {
            Write("ERROR", message);
            if (ex != null) Write("ERROR", ex.ToString());
        }

        private static void Write(string level, string message)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                var line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(LogFile, line, Encoding.UTF8);
            }
            catch { /* log asla throw etmez */ }
        }
    }
}
