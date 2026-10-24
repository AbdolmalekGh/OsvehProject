using System;
using System.Collections.Generic;
using System.IO;
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

    public class Person
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName => $"{FirstName} {LastName}".Trim();
        public string Gender { get; set; } = "مرد";
        public string IdentityNumber { get; set; } = string.Empty;
        public string NationalCode { get; set; } = string.Empty;
        public string ProfileImagePath { get; set; } = string.Empty;
        public string BirthDateSolar { get; set; } = string.Empty;
        public string BirthDateLunar { get; set; } = string.Empty;
        public string BirthCounty { get; set; } = string.Empty;
        public string BirthDistrict { get; set; } = string.Empty;
        public string BirthRural { get; set; } = string.Empty;
        public string FatherName { get; set; } = string.Empty;
        public string MotherName { get; set; } = string.Empty;
        public string IdSerial { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        public List<PersonPhone> PhoneNumbers { get; set; } = new();
        public List<PersonAttachment> Attachments { get; set; } = new();
    }

    public class PersonPhone
    {
        public int Id { get; set; }
        public int PersonId { get; set; }
        public string Title { get; set; } = "همراه";
        public string PhoneNumber { get; set; } = string.Empty;

        public Person? Person { get; set; }
    }

    public class PersonAttachment
    {
        public int Id { get; set; }
        public int PersonId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;

        public Person? Person { get; set; }
    }

    public class AppDbContext : DbContext
    {
        public DbSet<QuranPage> Pages { get; set; }
        public DbSet<QuranSura> Suras { get; set; }
        public DbSet<GlobalCounter> Counters { get; set; }
        public DbSet<HistoryLog> HistoryLogs { get; set; }
        public DbSet<KhatmRecord> KhatmRecords { get; set; }
        public DbSet<Person> Persons { get; set; }
        public DbSet<PersonPhone> PersonPhones { get; set; }
        public DbSet<PersonAttachment> PersonAttachments { get; set; }

        private readonly string _dbPath;

        public AppDbContext()
        {
            string baseFolder = AppDomain.CurrentDomain.BaseDirectory;
            string dataFolder = Path.Combine(baseFolder, "Data");
            Directory.CreateDirectory(dataFolder);

            _dbPath = Path.Combine(dataFolder, "quran_tracker.db");

            string oldDbPath = Path.Combine(baseFolder, "quran_tracker.db");
            if (File.Exists(oldDbPath) && !File.Exists(_dbPath))
            {
                File.Move(oldDbPath, _dbPath);
            }

            Database.EnsureCreated();
            EnsurePersonTablesCreated();
        }

        private void EnsurePersonTablesCreated()
        {
            try
            {
                Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS Persons (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        FirstName TEXT NOT NULL DEFAULT '',
                        LastName TEXT NOT NULL DEFAULT '',
                        Gender TEXT NOT NULL DEFAULT 'مرد',
                        IdentityNumber TEXT NOT NULL DEFAULT '',
                        NationalCode TEXT NOT NULL DEFAULT '',
                        ProfileImagePath TEXT NOT NULL DEFAULT '',
                        BirthDateSolar TEXT NOT NULL DEFAULT '',
                        BirthDateLunar TEXT NOT NULL DEFAULT '',
                        BirthCounty TEXT NOT NULL DEFAULT '',
                        BirthDistrict TEXT NOT NULL DEFAULT '',
                        BirthRural TEXT NOT NULL DEFAULT '',
                        FatherName TEXT NOT NULL DEFAULT '',
                        MotherName TEXT NOT NULL DEFAULT '',
                        IdSerial TEXT NOT NULL DEFAULT '',
                        Notes TEXT NOT NULL DEFAULT ''
                    );

                    CREATE TABLE IF NOT EXISTS PersonPhones (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        PersonId INTEGER NOT NULL,
                        Title TEXT NOT NULL DEFAULT 'همراه',
                        PhoneNumber TEXT NOT NULL DEFAULT '',
                        FOREIGN KEY(PersonId) REFERENCES Persons(Id) ON DELETE CASCADE
                    );

                    CREATE TABLE IF NOT EXISTS PersonAttachments (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        PersonId INTEGER NOT NULL,
                        FileName TEXT NOT NULL DEFAULT '',
                        FilePath TEXT NOT NULL DEFAULT '',
                        FileExtension TEXT NOT NULL DEFAULT '',
                        FOREIGN KEY(PersonId) REFERENCES Persons(Id) ON DELETE CASCADE
                    );
                ");
            }
            catch { }
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

            modelBuilder.Entity<Person>().HasKey(p => p.Id);
            modelBuilder.Entity<PersonPhone>().HasKey(p => p.Id);
            modelBuilder.Entity<PersonAttachment>().HasKey(p => p.Id);

            modelBuilder.Entity<Person>()
                .HasMany(p => p.PhoneNumbers)
                .WithOne(ph => ph.Person)
                .HasForeignKey(ph => ph.PersonId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Person>()
                .HasMany(p => p.Attachments)
                .WithOne(a => a.Person)
                .HasForeignKey(a => a.PersonId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}