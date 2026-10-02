using System;
using Microsoft.EntityFrameworkCore;

namespace QuranTracker.Models
{
    public class QuranPage
    {
        public int PageNumber { get; set; }
        public int ReadCount { get; set; }
        public bool IsJuz30 { get; set; }
    }

    public class QuranSura
    {
        public int SuraNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ReadCount { get; set; }
    }

    public class GlobalCounter
    {
        public string Key { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    public class HistoryLog
    {
        public int Id { get; set; }
        public string ActionDescription { get; set; } = string.Empty;
        public string PersianDate { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public class KhatmRecord
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class AppDbContext : DbContext
    {
        public DbSet<QuranPage> Pages { get; set; }
        public DbSet<QuranSura> Suras { get; set; }
        public DbSet<GlobalCounter> Counters { get; set; }
        public DbSet<HistoryLog> HistoryLogs { get; set; }
        public DbSet<KhatmRecord> KhatmRecords { get; set; }

        private readonly string _dbPath;

        public AppDbContext(string dbPath = "quran_tracker.db")
        {
            _dbPath = dbPath;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<QuranPage>().HasKey(p => p.PageNumber);
            modelBuilder.Entity<QuranSura>().HasKey(s => s.SuraNumber);
            modelBuilder.Entity<GlobalCounter>().HasKey(c => c.Key);
            modelBuilder.Entity<HistoryLog>().HasKey(h => h.Id);
            modelBuilder.Entity<KhatmRecord>().HasKey(k => k.Id);
        }
    }
}