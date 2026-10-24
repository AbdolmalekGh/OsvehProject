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
            SyncKhatm1();
        }

        private void SyncKhatm1()
        {
            var khatm1 = _db.KhatmRecords.Find(1);
            if (khatm1 != null && _db.Pages.Any())
            {
                khatm1.Count = _db.Pages.Min(p => p.ReadCount);
            }
        }

        public bool UpdatePageRange(int start, int end, int delta, string reason)
        {
            var pages = _db.Pages.Where(p => p.PageNumber >= start && p.PageNumber <= end).ToList();
            if (!pages.Any()) return false;
            if (delta < 0 && pages.Any(p => p.ReadCount + delta < 0)) return false;

            foreach (var page in pages) page.ReadCount += delta;
            SyncKhatm1();

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
            if (!suras.Any()) return false;
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
            if (!suras.Any()) return;
            int minSuraRead = suras.Min(s => s.ReadCount);
            if (minSuraRead > 0)
            {
                foreach (var sura in suras) sura.ReadCount -= minSuraRead;
                var juz30Pages = _db.Pages.Where(p => p.IsJuz30).ToList();
                foreach (var page in juz30Pages) page.ReadCount += minSuraRead;

                SyncKhatm1();
                LogHistory($"انتقال {minSuraRead} تکرار کامل از سوره‌ها به صفحات جزء ۳۰");
                _db.SaveChanges();
            }
        }

        public bool UpdateKhatmRange(int start, int end, int delta, string reason)
        {
            var records = _db.KhatmRecords.Where(k => k.Id >= start && k.Id <= end).ToList();
            if (!records.Any()) return false;
            if (delta < 0 && records.Any(k => k.Count + delta < 0)) return false;

            foreach (var record in records)
            {
                if (record.Id == 1)
                {
                    bool pageUpdateSuccess = UpdatePageRange(1, 604, delta, "اعمال ختم کامل قرآن");
                    if (!pageUpdateSuccess) return false;
                    continue; // جلوگیری از اضافه شدن مضاعف دلتا روی ختم کل
                }
                record.Count += delta;
            }

            LogHistory($"{reason} (شناسه‌های {start} تا {end}: {delta:+#;-#;0})");
            _db.SaveChanges();
            return true;
        }

        public (int Min, int Max) GetJuzReadStats(int juzNumber)
        {
            var (start, end) = JuzPageMapper.GetPageRangeForJuz(juzNumber);
            var pages = _db.Pages.Where(p => p.PageNumber >= start && p.PageNumber <= end).ToList();
            if (!pages.Any()) return (0, 0);
            return (pages.Min(p => p.ReadCount), pages.Max(p => p.ReadCount));
        }

        public int GetItemCurrentCount(AppTab tab, Juz30Mode juz30Mode, int id)
        {
            return tab switch
            {
                AppTab.Pages => _db.Pages.FirstOrDefault(p => p.PageNumber == id)?.ReadCount ?? 0,
                AppTab.Juzs => GetJuzReadStats(id).Min,
                AppTab.Juz30 => juz30Mode == Juz30Mode.Suras
                    ? _db.Suras.FirstOrDefault(s => s.SuraNumber == id)?.ReadCount ?? 0
                    : _db.Pages.FirstOrDefault(p => p.PageNumber == id)?.ReadCount ?? 0,
                AppTab.Khatms => _db.KhatmRecords.FirstOrDefault(k => k.Id == id)?.Count ?? 0,
                _ => 0
            };
        }

        private static JsonElement? GetPropertyCaseInsensitive(JsonElement element, params string[] names)
        {
            if (element.ValueKind != JsonValueKind.Object) return null;
            foreach (var prop in element.EnumerateObject())
            {
                foreach (var name in names)
                {
                    if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return prop.Value;
                    }
                }
            }
            return null;
        }

        public string ImportOldDataFromJson(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                int updatedPages = 0;
                int updatedSuras = 0;
                int updatedKhatms = 0;
                int importedHistory = 0;

                // ۱. پردازش صفحات قرآن (Case-Insensitive)
                var pagesProp = GetPropertyCaseInsensitive(root, "pageReadCounts", "Pages", "pages", "pageList", "quranPages");
                if (pagesProp.HasValue && pagesProp.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in pagesProp.Value.EnumerateArray())
                    {
                        var numEl = GetPropertyCaseInsensitive(p, "PageNumber", "pageNumber", "number", "id", "page");
                        var countEl = GetPropertyCaseInsensitive(p, "PageReadCounts", "pageReadCounts", "ReadCount", "readCount", "count", "value");

                        if (numEl.HasValue && numEl.Value.ValueKind == JsonValueKind.Number &&
                            countEl.HasValue && countEl.Value.ValueKind == JsonValueKind.Number)
                        {
                            var page = _db.Pages.Find(numEl.Value.GetInt32());
                            if (page != null) { page.ReadCount = countEl.Value.GetInt32(); updatedPages++; }
                        }
                    }
                }

                // ۲. پردازش صفحات جزء ۳۰
                var juz30Prop = GetPropertyCaseInsensitive(root, "chapter30Pages", "Chapter30Pages", "juz30Pages", "juz30");
                if (juz30Prop.HasValue && juz30Prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in juz30Prop.Value.EnumerateArray())
                    {
                        var numEl = GetPropertyCaseInsensitive(p, "PageNumber", "pageNumber", "number", "id");
                        var countEl = GetPropertyCaseInsensitive(p, "PageReadCounts", "pageReadCounts", "ReadCount", "readCount", "count");

                        if (numEl.HasValue && numEl.Value.ValueKind == JsonValueKind.Number &&
                            countEl.HasValue && countEl.Value.ValueKind == JsonValueKind.Number)
                        {
                            var page = _db.Pages.Find(numEl.Value.GetInt32());
                            if (page != null) { page.ReadCount = countEl.Value.GetInt32(); updatedPages++; }
                        }
                    }
                }

                // ۳. پردازش سوره‌های جزء ۳۰
                var suraProp = GetPropertyCaseInsensitive(root, "sura", "Suras", "suras", "Sura", "suraList", "suraReadCounts");
                if (suraProp.HasValue && suraProp.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in suraProp.Value.EnumerateArray())
                    {
                        var numEl = GetPropertyCaseInsensitive(s, "SuraNumber", "suraNumber", "number", "id");
                        var countEl = GetPropertyCaseInsensitive(s, "ReadCount", "readCount", "SuraReadCounts", "suraReadCounts", "count");

                        if (numEl.HasValue && numEl.Value.ValueKind == JsonValueKind.Number &&
                            countEl.HasValue && countEl.Value.ValueKind == JsonValueKind.Number)
                        {
                            int rawNum = numEl.Value.GetInt32();
                            int dbSuraNum = (rawNum >= 1 && rawNum <= 37) ? rawNum + 77 : rawNum;
                            var sura = _db.Suras.Find(dbSuraNum);
                            if (sura != null) { sura.ReadCount = countEl.Value.GetInt32(); updatedSuras++; }
                        }
                    }
                }

                // ۴. پردازش اذکار و ختم‌ها
                var tahlilProp = GetPropertyCaseInsensitive(root, "KhatmTahlil", "khatmTahlil");
                if (tahlilProp.HasValue && tahlilProp.Value.ValueKind == JsonValueKind.Number)
                {
                    var khatm3 = _db.KhatmRecords.Find(3);
                    if (khatm3 != null) { khatm3.Count = tahlilProp.Value.GetInt32(); updatedKhatms++; }
                }

                var khatmsProp = GetPropertyCaseInsensitive(root, "Khatms", "khatms", "khatmRecords", "khatm", "khatmList");
                if (khatmsProp.HasValue && khatmsProp.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var k in khatmsProp.Value.EnumerateArray())
                    {
                        var idEl = GetPropertyCaseInsensitive(k, "Id", "id", "khatmId");
                        var countEl = GetPropertyCaseInsensitive(k, "Count", "count", "value");

                        if (idEl.HasValue && idEl.Value.ValueKind == JsonValueKind.Number &&
                            countEl.HasValue && countEl.Value.ValueKind == JsonValueKind.Number)
                        {
                            var khatm = _db.KhatmRecords.Find(idEl.Value.GetInt32());
                            if (khatm != null) { khatm.Count = countEl.Value.GetInt32(); updatedKhatms++; }
                        }
                    }
                }

                // ۵. پردازش تاریخچه
                var historyProp = GetPropertyCaseInsensitive(root, "historyList", "HistoryList", "history", "Histories", "historyLogs");
                if (historyProp.HasValue && historyProp.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var h in historyProp.Value.EnumerateArray())
                    {
                        var infoEl = GetPropertyCaseInsensitive(h, "Information", "information", "desc", "description", "actionDescription");
                        var dateEl = GetPropertyCaseInsensitive(h, "date", "Date", "persianDate", "PersianDate");

                        string desc = infoEl.HasValue && infoEl.Value.ValueKind == JsonValueKind.String ? infoEl.Value.GetString() : "";
                        string dateStr = dateEl.HasValue && dateEl.Value.ValueKind == JsonValueKind.String ? dateEl.Value.GetString() : "";

                        if (!string.IsNullOrEmpty(desc))
                        {
                            _db.HistoryLogs.Add(new HistoryLog
                            {
                                ActionDescription = desc,
                                PersianDate = dateStr,
                                CreatedAtUtc = DateTime.UtcNow
                            });
                            importedHistory++;
                        }
                    }
                }

                CheckAndPromoteSuraToPages();
                SyncKhatm1();
                _db.SaveChanges();

                if (updatedPages == 0 && updatedSuras == 0 && updatedKhatms == 0 && importedHistory == 0)
                {
                    return "فایل انتخاب‌شده حاوی ساختار اطلاعاتی شناخته‌شده‌ای نبود.";
                }

                return $"اطلاعات با موفقیت وارد گردید:\n\n• صفحات بروز شده: {updatedPages}\n• سوره‌ها: {updatedSuras}\n• ختم‌ها: {updatedKhatms}\n• رکوردهای تاریخچه: {importedHistory}";
            }
            catch (Exception ex)
            {
                return $"خطا در فراخوانی فایل: {ex.Message}";
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