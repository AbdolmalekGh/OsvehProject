using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuranTracker.Models;

namespace QuranTracker.Services
{
    public class PersonService
    {
        private readonly AppDbContext _db;
        private readonly string _attachmentsBaseDir;

        public PersonService(AppDbContext db)
        {
            _db = db;
            _attachmentsBaseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Attachments");
            Directory.CreateDirectory(_attachmentsBaseDir);
        }

        public List<Person> SearchPersons(string nameQuery, string nationalCodeQuery)
        {
            var q = _db.Persons
                .Include(p => p.PhoneNumbers)
                .Include(p => p.Attachments)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(nameQuery))
            {
                nameQuery = nameQuery.Trim().ToLower();
                q = q.Where(p => p.FirstName.ToLower().Contains(nameQuery) || p.LastName.ToLower().Contains(nameQuery));
            }

            if (!string.IsNullOrWhiteSpace(nationalCodeQuery))
            {
                nationalCodeQuery = nationalCodeQuery.Trim();
                q = q.Where(p => p.NationalCode.Contains(nationalCodeQuery));
            }

            return q.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ToList();
        }

        public void SavePerson(Person person)
        {
            if (person.Id == 0)
            {
                _db.Persons.Add(person);
            }
            else
            {
                _db.Persons.Update(person);
            }
            _db.SaveChanges();
        }

        public void DeletePerson(int personId)
        {
            var person = _db.Persons
                .Include(p => p.Attachments)
                .FirstOrDefault(p => p.Id == personId);

            if (person != null)
            {
                string personDir = Path.Combine(_attachmentsBaseDir, person.Id.ToString());
                if (Directory.Exists(personDir))
                {
                    try { Directory.Delete(personDir, true); } catch { }
                }

                _db.Persons.Remove(person);
                _db.SaveChanges();
            }
        }

        public string SaveProfileImage(int personId, string sourceFilePath)
        {
            if (!File.Exists(sourceFilePath)) return string.Empty;

            string personDir = Path.Combine(_attachmentsBaseDir, personId.ToString());
            Directory.CreateDirectory(personDir);

            string fileName = $"profile_{Guid.NewGuid()}{Path.GetExtension(sourceFilePath)}";
            string destPath = Path.Combine(personDir, fileName);

            File.Copy(sourceFilePath, destPath, true);
            return destPath;
        }

        public PersonAttachment AddAttachmentToPerson(int personId, string sourceFilePath)
        {
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException("فایل مورد نظر یافت نشد.");

            string personDir = Path.Combine(_attachmentsBaseDir, personId.ToString());
            Directory.CreateDirectory(personDir);

            string fileName = Path.GetFileName(sourceFilePath);
            string destPath = Path.Combine(personDir, $"{Guid.NewGuid()}_{fileName}");

            File.Copy(sourceFilePath, destPath, true);

            var attachment = new PersonAttachment
            {
                PersonId = personId,
                FileName = fileName,
                FilePath = destPath,
                FileExtension = Path.GetExtension(fileName).ToLower()
            };

            _db.PersonAttachments.Add(attachment);
            _db.SaveChanges();

            return attachment;
        }

        public void DeleteAttachment(int attachmentId)
        {
            var att = _db.PersonAttachments.Find(attachmentId);
            if (att != null)
            {
                if (File.Exists(att.FilePath))
                {
                    try { File.Delete(att.FilePath); } catch { }
                }
                _db.PersonAttachments.Remove(att);
                _db.SaveChanges();
            }
        }
    }
}