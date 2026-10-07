using Microsoft.EntityFrameworkCore;
using QuranTracker.Helpers;
using QuranTracker.Models;
using QuranTracker.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace QuranTracker.Services
{
    public class QuranService
    {
        private readonly AppDbContext _db;

        public QuranService(AppDbContext db)
        {
            _db = db;
            _db.Database.EnsureCreated();
            SeedInitialData();
        }

        private void SeedInitialData()
        {
            if (!_db.Pages.Any())
            {
                var pages = Enumerable.Range(1, 604).Select(i => new QuranPage
                {
                    PageNumber = i,
                    ReadCount = 0,
                    IsJuz30 = i >= 582
                }).ToList();
                _db.Pages.AddRange(pages);
            }

            if (!_db.Suras.Any())
            {
                string[] suraNames = {
                    "النبأ", "النازعات", "عبس", "التکویر", "الإنفطار", "المطففین", "الإنشقاق", "البروج", "الطارق", "الأعلى",
                    "الغاشیة", "الفجر", "البلد", "الشمس", "اللیل", "الضحى", "الشرح", "التین", "العلق", "القدر",
                    "البینة", "الزلزلة", "العادیات", "القارعة", "التکاثر", "العصر", "الهمزة", "الفیل", "قریش", "الماعون",
                    "الکوثر", "الکافرون", "النصر", "المسد", "الإخلاص", "الفلق", "الناس"
                };

                var suras = suraNames.Select((name, index) => new QuranSura
                {
                    SuraNumber = index + 78,
                    Name = name,
                    ReadCount = 0
                }).ToList();
                _db.Suras.AddRange(suras);
            }

            if (!_db.KhatmRecords.Any())
            {
                _db.KhatmRecords.AddRange(new List<KhatmRecord>
                {
                    new KhatmRecord { Id = 1, Name = "ختم کامل قرآن (اثرگذار روی کل صفحات)", Count = 0 },
                    new KhatmRecord { Id = 2, Name = "ختم سوره اخلاص", Count = 0 },
                    new KhatmRecord { Id = 3, Name = "ختم تهلیل", Count = 0 },
                    new KhatmRecord { Id = 4, Name = "ختم تنجینه", Count = 0 },
                    new KhatmRecord { Id = 5, Name = "ختم دلائل الخیرات", Count = 0 },
                    new KhatmRecord { Id = 6, Name = "سوره یاسین", Count = 0 },
                    new KhatmRecord { Id = 7, Name = "سوره ملک", Count = 0 }
                });
            }
            _db.SaveChanges();
        }

        public bool UpdatePageRange(int start, int end, int delta, string reason)
        {
            var pages = _db.Pages.Where(p => p.PageNumber >= start && p.PageNumber <= end).ToList();
            if (delta < 0 && pages.Any(p => p.ReadCount + delta < 0)) return false;

            foreach (var page in pages) page.ReadCount += delta;
            LogHistory($"{reason} (صفحات {start} تا {end}: {delta:+#;-#;0})");
            _db.SaveChanges();
            return true;
        }

        public bool UpdateJuzRange(int start, int end, int delta, string reason)
        {
            var (startPage, _) = JuzPageMapper.GetPageRangeForJuz(start);
            var (_, endPage) = JuzPageMapper.GetPageRangeForJuz(end);
            return UpdatePageRange(startPage, endPage, delta, reason);
        }

        public bool UpdateSuraRange(int start, int end, int delta, string reason)
        {
            var suras = _db.Suras.Where(s => s.SuraNumber >= start && s.SuraNumber <= end).ToList();
            if (delta < 0 && suras.Any(s => s.ReadCount + delta < 0)) return false;

            foreach (var sura in suras) sura.ReadCount += delta;
            LogHistory($"{reason} (سوره‌های {start} تا {end}: {delta:+#;-#;0})");
            _db.SaveChanges();
            CheckAndPromoteSuraToPages();
            return true;
        }

        private void CheckAndPromoteSuraToPages()
        {
            var suras = _db.Suras.ToList();
            int minSuraRead = suras.Min(s => s.ReadCount);
            if (minSuraRead > 0)
            {
                foreach (var sura in suras) sura.ReadCount -= minSuraRead;
                var juz30Pages = _db.Pages.Where(p => p.IsJuz30).ToList();
                foreach (var page in juz30Pages) page.ReadCount += minSuraRead;
                LogHistory($"انتقال {minSuraRead} تکرار کامل از سوره‌ها به صفحات جزء ۳۰");
                _db.SaveChanges();
            }
        }

        public bool UpdateKhatm(int id, int delta)
        {
            var record = _db.KhatmRecords.FirstOrDefault(k => k.Id == id);
            if (record == null || (delta < 0 && record.Count + delta < 0)) return false;

            // فقط در صورتی که آیدی 1 (ختم کل قرآن) باشد روی صفحات تأثیر می‌گذارد
            if (id == 1)
            {
                bool pageUpdateSuccess = UpdatePageRange(1, 604, delta, "اعمال ختم کامل قرآن");
                if (!pageUpdateSuccess) return false;
            }

            record.Count += delta;
            LogHistory($"ویرایش {record.Name} ({delta:+#;-#;0})");
            _db.SaveChanges();
            return true;
        }

        public (int Min, int Max) GetJuzReadStats(int juzNumber)
        {
            var (start, end) = JuzPageMapper.GetPageRangeForJuz(juzNumber);
            var pages = _db.Pages.Where(p => p.PageNumber >= start && p.PageNumber <= end).ToList();
            return (pages.Min(p => p.ReadCount), pages.Max(p => p.ReadCount));
        }

        public int GetItemCurrentCount(AppTab tab, int id)
        {
            return tab switch
            {
                AppTab.Pages => _db.Pages.FirstOrDefault(p => p.PageNumber == id)?.ReadCount ?? 0,
                AppTab.Juz30 => _db.Suras.FirstOrDefault(s => s.SuraNumber == id)?.ReadCount ?? 0,
                AppTab.Khatms => _db.KhatmRecords.FirstOrDefault(k => k.Id == id)?.Count ?? 0,
                _ => 0
            };
        }

        // فیچر وارد کردن اطلاعات قبلی
        public string ImportOldDataFromJson(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("Pages", out var pagesEl))
                {
                    foreach (var p in pagesEl.EnumerateArray())
                    {
                        if (p.TryGetProperty("PageNumber", out var numEl) && p.TryGetProperty("ReadCount", out var countEl))
                        {
                            var page = _db.Pages.Find(numEl.GetInt32());
                            if (page != null) page.ReadCount = countEl.GetInt32();
                        }
                    }
                }
                if (root.TryGetProperty("Suras", out var surasEl))
                {
                    foreach (var s in surasEl.EnumerateArray())
                    {
                        if (s.TryGetProperty("SuraNumber", out var numEl) && s.TryGetProperty("ReadCount", out var countEl))
                        {
                            var sura = _db.Suras.Find(numEl.GetInt32());
                            if (sura != null) sura.ReadCount = countEl.GetInt32();
                        }
                    }
                }
                if (root.TryGetProperty("Khatms", out var khatmsEl))
                {
                    foreach (var k in khatmsEl.EnumerateArray())
                    {
                        if (k.TryGetProperty("Id", out var idEl) && k.TryGetProperty("Count", out var countEl))
                        {
                            var khatm = _db.KhatmRecords.Find(idEl.GetInt32());
                            if (khatm != null) khatm.Count = countEl.GetInt32();
                        }
                    }
                }
                _db.SaveChanges();
                return "اطلاعات با موفقیت وارد و جایگزین سیستم شد.";
            }
            catch (Exception ex)
            {
                return $"خطا در خواندن فایل: {ex.Message}";
            }
        }

        private void LogHistory(string description)
        {
            _db.HistoryLogs.Add(new HistoryLog
            {
                ActionDescription = description,
                PersianDate = PersianDateHelper.GetCurrentPersianDateTime(),
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        public List<QuranPage> GetPages() => _db.Pages.OrderBy(p => p.PageNumber).ToList();
        public List<QuranPage> GetJuz30Pages() => _db.Pages.Where(p => p.IsJuz30).OrderBy(p => p.PageNumber).ToList();
        public List<QuranSura> GetSuras() => _db.Suras.OrderBy(s => s.SuraNumber).ToList();
        public List<KhatmRecord> GetKhatmRecords() => _db.KhatmRecords.OrderBy(k => k.Id).ToList();
        public List<HistoryLog> GetHistory() => _db.HistoryLogs.OrderByDescending(h => h.Id).ToList();
    }
}