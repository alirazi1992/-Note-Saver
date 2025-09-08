using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace NoteSaver
{
    class Program
    {
        // All notes live here (relative to the app working directory)
        private static readonly string NotesDir = Path.Combine(AppContext.BaseDirectory, "notes");

        static void Main()
        {
            Console.Title = "Note Saver - Day 11";
            EnsureNotesDir();

            while (true)
            {
                ShowMenu();
                switch ((Console.ReadLine() ?? "").Trim())
                {
                    case "1": CreateNote(); break;
                    case "2": ListNotes(); break;
                    case "3": ViewNote(); break;
                    case "4": SearchNotes(); break;
                    case "5": DeleteNote(); break;
                    case "0": Info("Bye 👋"); return;
                    default: Warn("Invalid choice."); break;
                }
            }
        }

        static void ShowMenu()
        {
            Console.WriteLine();
            Console.WriteLine("=== NOTE SAVER ===");
            Console.WriteLine("1) Create a note");
            Console.WriteLine("2) List notes");
            Console.WriteLine("3) View a note");
            Console.WriteLine("4) Search notes (title & body)");
            Console.WriteLine("5) Delete a note");
            Console.WriteLine("0) Exit");
            Console.Write("Choose: ");
        }

        // ----- Core actions -----

        static void CreateNote()
        {
            Console.Write("Title: ");
            string title = (Console.ReadLine() ?? "").Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                Warn("Title cannot be empty.");
                return;
            }

            Console.WriteLine("Write your note (finish with an empty line):");
            var sb = new StringBuilder();
            while (true)
            {
                var line = Console.ReadLine();
                if (string.IsNullOrEmpty(line)) break;
                sb.AppendLine(line);
            }
            string body = sb.ToString();

            // Make a safe file name: timestamp + slug
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string slug = Slugify(title, maxLen: 40);
            string fileName = $"{timestamp}_{slug}.txt";
            string path = Path.Combine(NotesDir, fileName);

            // Write: first line = title; a separator line; then body
            using (var sw = new StreamWriter(path, false, Encoding.UTF8))
            {
                sw.WriteLine(title);
                sw.WriteLine("---");
                sw.Write(body);
            }

            Notify($"Saved ✅  {fileName}");
        }

        static void ListNotes()
        {
            var files = GetNoteFiles();
            if (files.Length == 0) { Info("No notes yet."); return; }

            Console.WriteLine();
            Console.WriteLine("#  File name                              Title");
            Console.WriteLine("-----------------------------------------------------------------------");

            for (int i = 0; i < files.Length; i++)
            {
                string file = Path.GetFileName(files[i]);
                string title = TryReadTitle(files[i]) ?? "(untitled)";
                Console.WriteLine($"{i + 1,2} {Trunc(file, 36),-36}  {Trunc(title, 30)}");
            }
        }

        static void ViewNote()
        {
            var files = GetNoteFiles();
            if (!TryPickIndex(files, "View which note (number): ", out int idx)) return;

            string path = files[idx];
            var content = File.ReadAllText(path, Encoding.UTF8);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"=== {Path.GetFileName(path)} ===");
            Console.ResetColor();
            Console.WriteLine(content);
        }

        static void SearchNotes()
        {
            Console.Write("Search text: ");
            string q = (Console.ReadLine() ?? "").Trim();
            if (q.Length == 0) { Warn("Enter something to search."); return; }

            var files = GetNoteFiles();
            var hits = new List<(string path, string title, List<string> lines)>();

            foreach (var f in files)
            {
                string[] lines = File.ReadAllLines(f, Encoding.UTF8);
                string title = lines.Length > 0 ? lines[0] : "(untitled)";
                var matched = lines
                    .Select((text, i) => new { text, i })
                    .Where(x => x.text.Contains(q, StringComparison.OrdinalIgnoreCase))
                    .Select(x => $"  L{x.i + 1}: {Trunc(x.text, 80)}")
                    .ToList();

                if (title.Contains(q, StringComparison.OrdinalIgnoreCase) || matched.Count > 0)
                {
                    hits.Add((f, title, matched));
                }
            }

            if (hits.Count == 0) { Warn("No matches."); return; }

            Console.WriteLine();
            foreach (var h in hits.OrderBy(h => Path.GetFileName(h.path)))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(Path.GetFileName(h.path) + "  —  " + Trunc(h.title, 50));
                Console.ResetColor();
                foreach (var line in h.lines.Take(5)) Console.WriteLine(line);
                if (h.lines.Count > 5) Console.WriteLine("  ...");
                Console.WriteLine();
            }
        }

        static void DeleteNote()
        {
            var files = GetNoteFiles();
            if (!TryPickIndex(files, "Delete which note (number): ", out int idx)) return;

            string path = files[idx];
            Console.Write($"Are you sure you want to delete '{Path.GetFileName(path)}'? (y/n): ");
            if (!ReadYesNo()) { Info("Cancelled."); return; }

            File.Delete(path);
            Notify("Deleted 🗑️");
        }

        // ----- Helpers -----

        static void EnsureNotesDir()
        {
            if (!Directory.Exists(NotesDir))
                Directory.CreateDirectory(NotesDir);
        }

        static string[] GetNoteFiles()
        {
            EnsureNotesDir();
            return Directory.GetFiles(NotesDir, "*.txt")
                            .OrderByDescending(f => f) // newest first via timestamp prefix
                            .ToArray();
        }

        static bool TryPickIndex(string[] files, string prompt, out int idx)
        {
            idx = -1;
            if (files.Length == 0) { Warn("No notes available."); return false; }
            ListNotes();
            Console.Write(prompt);
            if (int.TryParse(Console.ReadLine(), out int n) && n >= 1 && n <= files.Length)
            {
                idx = n - 1;
                return true;
            }
            Warn("Invalid number.");
            return false;
        }

        static string? TryReadTitle(string path)
        {
            try
            {
                using var sr = new StreamReader(path, Encoding.UTF8);
                return sr.ReadLine();
            }
            catch { return null; }
        }

        static string Slugify(string raw, int maxLen = 50)
        {
            // Replace invalid filename chars with '-'
            var invalid = new string(Path.GetInvalidFileNameChars());
            string pattern = $"[{Regex.Escape(invalid)}]+";
            string s = Regex.Replace(raw, pattern, "-");
            s = Regex.Replace(s, @"\s+", "-");       // spaces -> dashes
            s = Regex.Replace(s, "-{2,}", "-");      // collapse multiple dashes
            s = s.Trim('-');
            if (s.Length > maxLen) s = s[..maxLen];
            return s.Length == 0 ? "note" : s;
        }

        static string Trunc(string s, int n) => (s ?? "—").Length <= n ? (s ?? "—") : (s ?? "—")[..(n - 1)] + "…";

        static bool ReadYesNo()
        {
            while (true)
            {
                var s = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
                if (s is "y" or "yes") return true;
                if (s is "n" or "no") return false;
                Warn("Please enter y/n.");
            }
        }

        static void Warn(string msg) { Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine(msg); Console.ResetColor(); }
        static void Notify(string msg) { Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine(msg); Console.ResetColor(); }
        static void Info(string msg) { Console.ForegroundColor = ConsoleColor.Cyan; Console.WriteLine(msg); Console.ResetColor(); }
    }
}
