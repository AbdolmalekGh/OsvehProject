using Microsoft.EntityFrameworkCore;
using QuranTracker.Helpers;
using QuranTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuranTracker.Services
{
    public enum SortMode { QuranicOrder, LeastRead, MostRead }

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
                var pages = new List<QuranPage>();
                for (int i = 1; i <= 604; i++)
                {
                    pages.Add(new QuranPage { PageNumber = i, ReadCount = 0, IsJuz30 = i >= 582 });
                }
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

            if (!_db.Counters.Any(c => c.Key == "KhatmTahlil"))
                _db.Counters.Add(new GlobalCounter { Key = "KhatmTahlil", Value = 0 });

            if (!_db.KhatmRecords.Any())
            {
                _db.KhatmRecords.AddRange(new List<KhatmRecord>
                {
                    new KhatmRecord { Name = "ختم اخلاص", Count = 0 },
                    new KhatmRecord { Name = "ختم تنجینه", Count = 0 },
                    new KhatmRecord { Name = "ختم دلائل الخیرات", Count = 0 },
                    new KhatmRecord { Name = "سوره یاسین", Count = 0 },
                    new KhatmRecord { Name = "سوره ملک", Count = 0 }
                });
            }
            _db.SaveChanges();
        }

        public bool UpdatePageRangeReadCount(int startPage, int endPage, int delta, string reason)
        {
            var pages = _db.Pages.Where(p => p.PageNumber >= startPage && p.PageNumber <= endPage).ToList();
            if (delta < 0 && pages.Any(p => p.ReadCount + delta < 0)) return false;

            foreach (var page in pages) page.ReadCount += delta;
            LogHistory($"{reason} (صفحات {startPage} تا {endPage} به میزان {delta:+#;-#;0})");
            _db.SaveChanges();
            return true;
        }

        public bool UpdateJuzReadCount(int startJuz, int endJuz, int delta, string reason)
        {
            var (startPage, _) = JuzPageMapper.GetPageRangeForJuz(startJuz);
            var (_, endPage) = JuzPageMapper.GetPageRangeForJuz(endJuz);
            return UpdatePageRangeReadCount(startPage, endPage, delta, reason);
        }

        public bool UpdateSuraRangeReadCount(int startSura, int endSura, int delta, string reason)
        {
            var suras = _db.Suras.Where(s => s.SuraNumber >= startSura && s.SuraNumber <= endSura).ToList();
            if (delta < 0 && suras.Any(s => s.ReadCount + delta < 0)) return false;

            foreach (var sura in suras) sura.ReadCount += delta;
            LogHistory($"{reason} (سوره‌های {startSura} تا {endSura} به میزان {delta:+#;-#;0})");
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
                LogHistory($"انتقال {minSuraRead} تکرار کامل سوره‌های جزء ۳۰ به صفحات جزء ۳۰");
                _db.SaveChanges();
            }
        }

        public void AddFullKhatm(int countToAdd)
        {
            if (countToAdd == 0) return;
            var allPages = _db.Pages.ToList();
            foreach (var page in allPages) page.ReadCount = Math.Max(0, page.ReadCount + countToAdd);
            LogHistory($"ویرایش ختم کامل قرآن ({countToAdd:+#;-#;0})");
            _db.SaveChanges();
        }

        public int GetJuzReadCount(int juzNumber)
        {
            var (startPage, endPage) = JuzPageMapper.GetPageRangeForJuz(juzNumber);
            return _db.Pages.Where(p => p.PageNumber >= startPage && p.PageNumber <= endPage).Min(p => p.ReadCount);
        }

        public int CalculateFullKhatmCount() => _db.Pages.Min(p => p.ReadCount);

        public void UpdateTahlilCount(int delta)
        {
            var counter = _db.Counters.FirstOrDefault(c => c.Key == "KhatmTahlil");
            if (counter != null)
            {
                counter.Value = Math.Max(0, counter.Value + delta);
                LogHistory($"تغییر ختم تهلیل به میزان {delta:+#;-#;0}");
                _db.SaveChanges();
            }
        }

        public int GetTahlilCount() => _db.Counters.FirstOrDefault(c => c.Key == "KhatmTahlil")?.Value ?? 0;

        public List<QuranPage> GetPages(SortMode sortMode, bool juz30Only = false)
        {
            IQueryable<QuranPage> query = _db.Pages.Where(p => p.IsJuz30 == juz30Only);
            return sortMode switch
            {
                SortMode.LeastRead => query.OrderBy(p => p.ReadCount).ThenBy(p => p.PageNumber).ToList(),
                SortMode.MostRead => query.OrderByDescending(p => p.ReadCount).ThenBy(p => p.PageNumber).ToList(),
                _ => query.OrderBy(p => p.PageNumber).ToList()
            };
        }

        public List<QuranSura> GetSuras(SortMode sortMode)
        {
            IQueryable<QuranSura> query = _db.Suras;
            return sortMode switch
            {
                SortMode.LeastRead => query.OrderBy(s => s.ReadCount).ThenBy(s => s.SuraNumber).ToList(),
                SortMode.MostRead => query.OrderByDescending(s => s.ReadCount).ThenBy(s => s.SuraNumber).ToList(),
                _ => query.OrderBy(s => s.SuraNumber).ToList()
            };
        }

        public List<KhatmRecord> GetKhatmRecords() => _db.KhatmRecords.ToList();

        public void UpdateKhatmRecord(int id, int delta)
        {
            var record = _db.KhatmRecords.FirstOrDefault(k => k.Id == id);
            if (record != null)
            {
                record.Count = Math.Max(0, record.Count + delta);
                LogHistory($"تغییر در {record.Name} به میزان {delta:+#;-#;0}");
                _db.SaveChanges();
            }
        }

        public List<HistoryLog> GetRecentHistory() => _db.HistoryLogs.OrderByDescending(h => h.Id).ToList();

        private void LogHistory(string description)
        {
            _db.HistoryLogs.Add(new HistoryLog
            {
                ActionDescription = description,
                PersianDate = PersianDateHelper.GetCurrentPersianDateTime(),
                CreatedAtUtc = DateTime.UtcNow
            });
        }
    }
}